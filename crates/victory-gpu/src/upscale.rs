//! A plain upscale in place of the title's FidelityFX upscaler (as a Series
//! S the title upscales its frame with FSR 4: its passes need a device
//! method we do not have, its fallback programs are shader model 5 we do not
//! translate, so the frame it writes stayed empty and only the interface
//! showed).
//!
//! The rendered part of the colour input is stretched over the output with
//! a linear filter, by a compute shader of ours, recorded into the title's
//! list where its upscale was.
//!
//! shortcut: no temporal accumulation, no sharpening, jitter ignored: the
//! picture is softer than the console's and shimmers a little.

use std::collections::HashMap;
use std::sync::Mutex;
use std::sync::atomic::{AtomicU32, Ordering};

use windows::Win32::Graphics::Direct3D::Fxc::D3DCompile;
use windows::Win32::Graphics::Direct3D::ID3DBlob;
use windows::Win32::Graphics::Direct3D12::*;
use windows::Win32::Graphics::Dxgi::Common::*;
use windows::core::s;

use crate::object::This;

const SOURCE: &str = r#"
#define RS "RootConstants(num32BitConstants = 4, b0), DescriptorTable(SRV(t0)), DescriptorTable(UAV(u0)), StaticSampler(s0, filter = FILTER_MIN_MAG_MIP_LINEAR, addressU = TEXTURE_ADDRESS_CLAMP, addressV = TEXTURE_ADDRESS_CLAMP)"
cbuffer Sizes : register(b0) {
    // The output's size, and the part of the input that was rendered as a
    // share of the input texture.
    uint2 out_size;
    float2 rendered;
};
Texture2D<float4> color : register(t0);
RWTexture2D<float4> output : register(u0);
SamplerState linear_clamp : register(s0);
[RootSignature(RS)]
[numthreads(8, 8, 1)]
void main(uint3 id : SV_DispatchThreadID) {
    if (any(id.xy >= out_size)) return;
    float2 uv = (float2(id.xy) + 0.5) / float2(out_size) * rendered;
    output[id.xy] = color.SampleLevel(linear_clamp, uv, 0);
}
"#;

const SLOTS: u32 = 256;

struct Upscaler {
    root: ID3D12RootSignature,
    pso: ID3D12PipelineState,
    heap: ID3D12DescriptorHeap,
    size: u32,
    next: AtomicU32,
}

// SAFETY: D3D12 objects are free-threaded.
unsafe impl Send for Upscaler {}
// SAFETY: as above.
unsafe impl Sync for Upscaler {}

fn make(device: &ID3D12Device) -> windows::core::Result<Upscaler> {
    let mut code: Option<ID3DBlob> = None;
    let mut errors: Option<ID3DBlob> = None;
    // SAFETY: the source and names live through the call; out pointers ours.
    let compiled = unsafe {
        D3DCompile(
            SOURCE.as_ptr().cast(),
            SOURCE.len(),
            s!("upscale"),
            None,
            None,
            s!("main"),
            s!("cs_5_1"),
            0,
            0,
            &mut code,
            Some(&mut errors),
        )
    };
    if let Err(e) = compiled {
        if let Some(errors) = errors {
            // SAFETY: the blob's own bytes.
            let text = unsafe {
                std::slice::from_raw_parts(errors.GetBufferPointer().cast::<u8>(), errors.GetBufferSize())
            };
            tracing::warn!("upscale: {}", String::from_utf8_lossy(text));
        }
        return Err(e);
    }
    let code = code.ok_or_else(windows::core::Error::empty)?;
    // SAFETY: the blob's own bytes; the root signature is inside the shader.
    unsafe {
        let bytes = std::slice::from_raw_parts(code.GetBufferPointer().cast::<u8>(), code.GetBufferSize());
        let root: ID3D12RootSignature = device.CreateRootSignature(0, bytes)?;
        let pso = device.CreateComputePipelineState(&D3D12_COMPUTE_PIPELINE_STATE_DESC {
            pRootSignature: std::mem::transmute_copy(&root),
            CS: D3D12_SHADER_BYTECODE { pShaderBytecode: bytes.as_ptr().cast(), BytecodeLength: bytes.len() },
            ..Default::default()
        })?;
        let heap: ID3D12DescriptorHeap = device.CreateDescriptorHeap(&D3D12_DESCRIPTOR_HEAP_DESC {
            Type: D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
            NumDescriptors: SLOTS * 2,
            Flags: D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE,
            NodeMask: 0,
        })?;
        let size = device.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
        Ok(Upscaler { root, pso, heap, size, next: AtomicU32::new(0) })
    }
}

fn upscaler(device: &ID3D12Device) -> Option<&'static Upscaler> {
    static U: std::sync::OnceLock<Option<Upscaler>> = std::sync::OnceLock::new();
    U.get_or_init(|| match make(device) {
        Ok(u) => Some(u),
        Err(e) => {
            tracing::warn!(error = %e, "no plain upscale: the title's upscaled frame stays empty");
            None
        }
    })
    .as_ref()
}

/// A typed format for a view of a texture in `format` (its typeless
/// families get their plain member).
fn typed(format: DXGI_FORMAT) -> DXGI_FORMAT {
    match format.0 {
        1 => DXGI_FORMAT_R32G32B32A32_FLOAT,
        9 => DXGI_FORMAT_R16G16B16A16_FLOAT,
        19 => DXGI_FORMAT_R32G32_FLOAT,
        23 => DXGI_FORMAT_R10G10B10A2_UNORM,
        27 | 29 => DXGI_FORMAT_R8G8B8A8_UNORM,
        87 | 90 | 91 => DXGI_FORMAT_B8G8R8A8_UNORM,
        _ => format,
    }
}

/// Logs each kind of failure once.
fn once(what: &'static str) {
    static SEEN: Mutex<Option<HashMap<&'static str, ()>>> = Mutex::new(None);
    let mut seen = SEEN.lock().unwrap_or_else(|e| e.into_inner());
    if seen.get_or_insert_with(HashMap::new).insert(what, ()).is_none() {
        tracing::warn!("plain upscale not recorded: {what}");
    }
}

/// Records, in the title's list `list`, the part `rendered` (pixels) of
/// the title texture `color` stretched over the title texture `output`.
/// False if nothing was recorded.
pub fn upscale(list: *const std::ffi::c_void, color: *const std::ffi::c_void, output: *const std::ffi::c_void, rendered: (u32, u32)) -> bool {
    let (list, color, output) = (list.cast::<crate::object::Object>() as This, color as This, output as This);
    let mut recorded = false;
    crate::list::with_state(list, |host_list, state| {
        let Some(host) = crate::host::host() else { return };
        let Some(u) = upscaler(&host.device) else { return };
        let (Some(from), Some(to)) = (crate::resource::host_resource(color), crate::resource::host_resource(output)) else {
            once("the colour input or the output has no host texture");
            return;
        };
        // SAFETY: live resources.
        let (fd, td) = unsafe { (from.GetDesc(), to.GetDesc()) };
        if td.Flags.0 & D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS.0 == 0 {
            once("the output cannot be written by a shader");
            return;
        }
        if crate::texture::title_written(from) {
            crate::texture::upload(host_list, color, from);
        }
        const NON_PIXEL_SHADER_RESOURCE: u32 = 0x40;
        const UNORDERED_ACCESS: u32 = 0x8;
        crate::list::ensure_state(host_list, color, NON_PIXEL_SHADER_RESOURCE);
        crate::list::ensure_state(host_list, output, UNORDERED_ACCESS);
        let out = (td.Width as u32, td.Height);
        let share = [
            (rendered.0.min(fd.Width as u32).max(1) as f32 / fd.Width as f32).to_bits(),
            (rendered.1.min(fd.Height).max(1) as f32 / fd.Height as f32).to_bits(),
        ];
        let slot = u.next.fetch_add(1, Ordering::Relaxed) % SLOTS;
        // SAFETY: our heap and pipeline, live textures, an open list; a
        // slot pair is not reused before SLOTS more upscales.
        unsafe {
            let cpu = |i: u32| {
                let mut h = u.heap.GetCPUDescriptorHandleForHeapStart();
                h.ptr += (i * u.size) as usize;
                h
            };
            let gpu = |i: u32| {
                let mut h = u.heap.GetGPUDescriptorHandleForHeapStart();
                h.ptr += u64::from(i * u.size);
                h
            };
            host.device.CreateShaderResourceView(
                from,
                Some(&D3D12_SHADER_RESOURCE_VIEW_DESC {
                    Format: typed(fd.Format),
                    ViewDimension: D3D12_SRV_DIMENSION_TEXTURE2D,
                    Shader4ComponentMapping: D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                    Anonymous: D3D12_SHADER_RESOURCE_VIEW_DESC_0 {
                        Texture2D: D3D12_TEX2D_SRV { MostDetailedMip: 0, MipLevels: 1, PlaneSlice: 0, ResourceMinLODClamp: 0.0 },
                    },
                }),
                cpu(slot * 2),
            );
            host.device.CreateUnorderedAccessView(
                to,
                None,
                Some(&D3D12_UNORDERED_ACCESS_VIEW_DESC {
                    Format: typed(td.Format),
                    ViewDimension: D3D12_UAV_DIMENSION_TEXTURE2D,
                    Anonymous: D3D12_UNORDERED_ACCESS_VIEW_DESC_0 {
                        Texture2D: D3D12_TEX2D_UAV { MipSlice: 0, PlaneSlice: 0 },
                    },
                }),
                cpu(slot * 2 + 1),
            );
            let l = &host_list.list;
            l.SetComputeRootSignature(&u.root);
            l.SetDescriptorHeaps(&[Some(u.heap.clone())]);
            l.SetComputeRoot32BitConstants(0, 4, [out.0, out.1, share[0], share[1]].as_ptr().cast(), 0);
            l.SetComputeRootDescriptorTable(1, gpu(slot * 2));
            l.SetComputeRootDescriptorTable(2, gpu(slot * 2 + 1));
            l.SetPipelineState(&u.pso);
            l.Dispatch(out.0.div_ceil(8), out.1.div_ceil(8), 1);
        }
        crate::texture::host_written(output);
        // The title's next draw or dispatch binds its own pipeline, roots and heaps again.
        state.forget_bindings();
        recorded = true;
    });
    recorded
}
