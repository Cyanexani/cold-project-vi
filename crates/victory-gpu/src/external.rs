//! Host work that a host library records into a title's command list in
//! place of a console one (the host's FidelityFX upscaler, say): the host
//! device, a title texture's host copy in the state the library is told,
//! and the host list itself.

use windows::Win32::Graphics::Direct3D12::{
    ID3D12Device, ID3D12GraphicsCommandList, ID3D12Resource,
};

use crate::object::Object;

/// The host D3D12 device, created on first use; `None` without a host GPU.
pub fn host_device() -> Option<ID3D12Device> {
    crate::host::host().map(|h| h.device.clone())
}

/// A title texture's host copy, moved to the host resource `state` in the
/// title list `list`, and the state it is in: `state` without the bits the
/// host copy cannot take (see `host::legal_state`). What the list records
/// next finds it there and must leave it there. The list holds the texture
/// until it is reset. `None` for a buffer, or without a host list.
pub fn host_texture(
    list: *const Object,
    resource: *const Object,
    state: u32,
) -> Option<(ID3D12Resource, u32)> {
    let host = crate::list::host_list(list)?;
    let texture = crate::resource::host_resource(resource)?.clone();
    crate::list::ensure_state(host, resource, state);
    let state = crate::host::legal_state(&texture, state);
    Some((texture, state))
}

/// Runs `f` with the host list behind the title list `list` (open for
/// recording); `None` without one. What `f` binds (descriptor heaps, root
/// signatures, pipeline) is replaced before the list's next draw or
/// dispatch.
pub fn record<R>(
    list: *const Object,
    f: impl FnOnce(&ID3D12GraphicsCommandList) -> R,
) -> Option<R> {
    let mut out = None;
    crate::list::with_state(list, |host, state| {
        out = Some(f(&host.list));
        state.forget_bindings();
    });
    out
}

/// The FidelityFX API's surface format (`ffx_api_types.h`) for a host
/// texture of DXGI format `dxgi`, if it has one. The title describes its
/// textures with console formats; the host copies may differ (a console
/// R9G9B9E5 target is R11G11B10 here, since a PC shader cannot write the
/// former), and the PC library makes its views from the description.
pub fn ffx_format(dxgi: u32) -> Option<u32> {
    Some(match dxgi {
        2 => 3,   // R32G32B32A32_FLOAT
        10 => 4,  // R16G16B16A16_FLOAT
        16 => 6,  // R32G32_FLOAT
        24 => 17, // R10G10B10A2_UNORM
        26 => 16, // R11G11B10_FLOAT
        28 => 10, // R8G8B8A8_UNORM
        29 => 12, // R8G8B8A8_UNORM_SRGB
        34 => 18, // R16G16_FLOAT
        41 => 28, // R32_FLOAT
        40 => 28, // R32_TYPELESS: read as a float
        54 => 21, // R16_FLOAT
        56 => 23, // R16_UNORM
        61 => 25, // R8_UNORM
        49 => 26, // R8G8_UNORM
        87 => 14, // B8G8R8A8_UNORM
        _ => return None,
    })
}

const DEPTH_SOURCE: &str = r#"
#define RS "RootConstants(num32BitConstants = 2, b0), DescriptorTable(SRV(t0)), DescriptorTable(UAV(u0))"
cbuffer Size : register(b0) { uint2 size; };
Texture2D<float> depth : register(t0);
RWTexture2D<float> plain : register(u0);
[RootSignature(RS)]
[numthreads(8, 8, 1)]
void main(uint3 id : SV_DispatchThreadID) {
    if (any(id.xy >= size)) return;
    plain[id.xy] = depth.Load(int3(id.xy, 0));
}
"#;

struct DepthCopy {
    root: windows::Win32::Graphics::Direct3D12::ID3D12RootSignature,
    pso: windows::Win32::Graphics::Direct3D12::ID3D12PipelineState,
    heap: windows::Win32::Graphics::Direct3D12::ID3D12DescriptorHeap,
    size: u32,
    next: std::sync::atomic::AtomicU32,
    /// The copy (width, height, texture), and whether it was read last.
    target: std::sync::Mutex<Option<(u64, u32, ID3D12Resource)>>,
}

// SAFETY: D3D12 objects are free-threaded.
unsafe impl Send for DepthCopy {}
// SAFETY: as above.
unsafe impl Sync for DepthCopy {}

const DEPTH_SLOTS: u32 = 64;

fn depth_copier(device: &ID3D12Device) -> Option<&'static DepthCopy> {
    use windows::Win32::Graphics::Direct3D::Fxc::D3DCompile;
    use windows::Win32::Graphics::Direct3D::ID3DBlob;
    use windows::Win32::Graphics::Direct3D12::*;
    static D: std::sync::OnceLock<Option<DepthCopy>> = std::sync::OnceLock::new();
    D.get_or_init(|| {
        let mut code: Option<ID3DBlob> = None;
        // SAFETY: the source and names live through the call.
        unsafe {
            D3DCompile(
                DEPTH_SOURCE.as_ptr().cast(),
                DEPTH_SOURCE.len(),
                windows::core::s!("ffx_depth"),
                None,
                None,
                windows::core::s!("main"),
                windows::core::s!("cs_5_1"),
                0,
                0,
                &mut code,
                None,
            )
        }
        .ok()?;
        let code = code?;
        // SAFETY: the blob's bytes; the root signature is inside the shader.
        unsafe {
            let bytes = std::slice::from_raw_parts(code.GetBufferPointer().cast::<u8>(), code.GetBufferSize());
            let root: ID3D12RootSignature = device.CreateRootSignature(0, bytes).ok()?;
            let pso = device
                .CreateComputePipelineState(&D3D12_COMPUTE_PIPELINE_STATE_DESC {
                    pRootSignature: std::mem::transmute_copy(&root),
                    CS: D3D12_SHADER_BYTECODE { pShaderBytecode: bytes.as_ptr().cast(), BytecodeLength: bytes.len() },
                    ..Default::default()
                })
                .ok()?;
            let heap: ID3D12DescriptorHeap = device
                .CreateDescriptorHeap(&D3D12_DESCRIPTOR_HEAP_DESC {
                    Type: D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
                    NumDescriptors: DEPTH_SLOTS * 2,
                    Flags: D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE,
                    NodeMask: 0,
                })
                .ok()?;
            let size = device.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
            Some(DepthCopy { root, pso, heap, size, next: Default::default(), target: Default::default() })
        }
    })
    .as_ref()
}

/// A depth texture with stencil (R32G8X24) as a plain R32_FLOAT copy, made
/// in the title list `list` and left in the host state `state` (a shader
/// read): the PC FidelityFX views depth as a single-channel float, which a
/// view of a depth-and-stencil texture cannot be. `None` for any other
/// format, or without a host list.
pub fn plain_depth(list: *const Object, resource: *const Object, state: u32) -> Option<ID3D12Resource> {
    use windows::Win32::Graphics::Direct3D12::*;
    use windows::Win32::Graphics::Dxgi::Common::*;
    let device = host_device()?;
    let source = crate::resource::host_resource(resource)?.clone();
    // SAFETY: a live texture.
    let sd = unsafe { source.GetDesc() };
    if !matches!(sd.Format.0, 19 | 20) {
        return None;
    }
    let d = depth_copier(&device)?;
    let host = crate::list::host_list(list)?;
    const NON_PIXEL_SHADER_RESOURCE: u32 = 0x40;
    crate::list::ensure_state(host, resource, NON_PIXEL_SHADER_RESOURCE);
    let mut target = d.target.lock().unwrap_or_else(|e| e.into_inner());
    let fits = target.as_ref().is_some_and(|(w, h, _)| *w == sd.Width && *h == sd.Height);
    let fresh = !fits;
    if fresh {
        let heap = D3D12_HEAP_PROPERTIES { Type: D3D12_HEAP_TYPE_DEFAULT, ..Default::default() };
        let desc = D3D12_RESOURCE_DESC {
            Dimension: D3D12_RESOURCE_DIMENSION_TEXTURE2D,
            Width: sd.Width,
            Height: sd.Height,
            DepthOrArraySize: 1,
            MipLevels: 1,
            Format: DXGI_FORMAT_R32_FLOAT,
            SampleDesc: DXGI_SAMPLE_DESC { Count: 1, Quality: 0 },
            Flags: D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS,
            ..Default::default()
        };
        let mut made: Option<ID3D12Resource> = None;
        // SAFETY: plain creation.
        unsafe {
            device
                .CreateCommittedResource(&heap, D3D12_HEAP_FLAG_NONE, &desc, D3D12_RESOURCE_STATE_UNORDERED_ACCESS, None, &mut made)
                .ok()?;
        }
        *target = Some((sd.Width, sd.Height, made?));
    }
    let (_, _, plain) = target.as_ref()?.clone();
    drop(target);
    let slot = d.next.fetch_add(1, std::sync::atomic::Ordering::Relaxed) % DEPTH_SLOTS;
    record(list, |l| {
        // SAFETY: our heap, pipeline and texture; live title texture; an
        // open list. A slot pair is not reused before DEPTH_SLOTS more.
        unsafe {
            let cpu = |i: u32| {
                let mut h = d.heap.GetCPUDescriptorHandleForHeapStart();
                h.ptr += (i * d.size) as usize;
                h
            };
            let gpu = |i: u32| {
                let mut h = d.heap.GetGPUDescriptorHandleForHeapStart();
                h.ptr += u64::from(i * d.size);
                h
            };
            device.CreateShaderResourceView(
                &source,
                Some(&D3D12_SHADER_RESOURCE_VIEW_DESC {
                    Format: DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS,
                    ViewDimension: D3D12_SRV_DIMENSION_TEXTURE2D,
                    Shader4ComponentMapping: D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
                    Anonymous: D3D12_SHADER_RESOURCE_VIEW_DESC_0 {
                        Texture2D: D3D12_TEX2D_SRV { MostDetailedMip: 0, MipLevels: 1, PlaneSlice: 0, ResourceMinLODClamp: 0.0 },
                    },
                }),
                cpu(slot * 2),
            );
            device.CreateUnorderedAccessView(&plain, None, None, cpu(slot * 2 + 1));
            if !fresh {
                l.ResourceBarrier(&[crate::host::transition(
                    &plain,
                    D3D12_RESOURCE_STATES(state as i32),
                    D3D12_RESOURCE_STATE_UNORDERED_ACCESS,
                )]);
            }
            l.SetComputeRootSignature(&d.root);
            l.SetDescriptorHeaps(&[Some(d.heap.clone())]);
            let size = [sd.Width as u32, sd.Height];
            l.SetComputeRoot32BitConstants(0, 2, size.as_ptr().cast(), 0);
            l.SetComputeRootDescriptorTable(1, gpu(slot * 2));
            l.SetComputeRootDescriptorTable(2, gpu(slot * 2 + 1));
            l.SetPipelineState(&d.pso);
            l.Dispatch(size[0].div_ceil(8), size[1].div_ceil(8), 1);
            l.ResourceBarrier(&[crate::host::transition(
                &plain,
                D3D12_RESOURCE_STATE_UNORDERED_ACCESS,
                D3D12_RESOURCE_STATES(state as i32),
            )]);
        }
    })?;
    Some(plain)
}
