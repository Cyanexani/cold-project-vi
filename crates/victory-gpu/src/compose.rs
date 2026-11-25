//! The second present plane drawn over the first one (a Series S title
//! presents two: the game at 1920x1080, scaled up by the console, and its
//! interface at 3840x2160 over it; the intro's video is in the second).
//!
//! The first plane is copied into the back buffer as before; the second is
//! then drawn over it, scaled to the back buffer's size with a linear
//! filter, blended as premultiplied alpha (one, one minus source alpha).
//!
//! shortcut: the planes' own placement, scaling and blend words are not
//! read (each covers the whole output here); the overlay is taken as
//! premultiplied.

use std::collections::HashMap;
use std::sync::Mutex;
use std::sync::atomic::{AtomicU32, Ordering};

use windows::Win32::Graphics::Direct3D::Fxc::D3DCompile;
use windows::Win32::Graphics::Direct3D::{D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST, ID3DBlob};
use windows::Win32::Graphics::Direct3D12::*;
use windows::Win32::Graphics::Dxgi::Common::*;
use windows::core::{PCSTR, s};

const SOURCE: &str = r#"
#define RS "DescriptorTable(SRV(t0)), StaticSampler(s0, filter = FILTER_MIN_MAG_MIP_LINEAR, addressU = TEXTURE_ADDRESS_CLAMP, addressV = TEXTURE_ADDRESS_CLAMP)"
Texture2D<float4> overlay : register(t0);
SamplerState linear_clamp : register(s0);
struct Out { float4 position : SV_Position; float2 uv : TEXCOORD0; };
[RootSignature(RS)]
Out vs(uint id : SV_VertexID) {
    Out o;
    o.uv = float2((id << 1) & 2, id & 2);
    o.position = float4(o.uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}
[RootSignature(RS)]
float4 ps(Out i) : SV_Target {
    return overlay.SampleLevel(linear_clamp, i.uv, 0);
}
"#;

/// Overlay views: one per present in the air at most, reused in turn.
const SLOTS: u32 = 64;

struct Composer {
    root: ID3D12RootSignature,
    vs: ID3DBlob,
    ps: ID3DBlob,
    /// By the back buffer's format.
    pipelines: Mutex<HashMap<i32, ID3D12PipelineState>>,
    heap: ID3D12DescriptorHeap,
    size: u32,
    next: AtomicU32,
}

// SAFETY: D3D12 objects are free-threaded; the map is behind its lock.
unsafe impl Send for Composer {}
// SAFETY: as above.
unsafe impl Sync for Composer {}

fn compile(entry: PCSTR, target: PCSTR) -> windows::core::Result<ID3DBlob> {
    let mut code: Option<ID3DBlob> = None;
    let mut errors: Option<ID3DBlob> = None;
    // SAFETY: the source and names live through the call; out pointers ours.
    let compiled = unsafe {
        D3DCompile(
            SOURCE.as_ptr().cast(),
            SOURCE.len(),
            s!("compose"),
            None,
            None,
            entry,
            target,
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
            tracing::warn!("plane composition: {}", String::from_utf8_lossy(text));
        }
        return Err(e);
    }
    code.ok_or_else(windows::core::Error::empty)
}

fn bytes(blob: &ID3DBlob) -> &[u8] {
    // SAFETY: the blob's own bytes, alive as long as the blob.
    unsafe { std::slice::from_raw_parts(blob.GetBufferPointer().cast::<u8>(), blob.GetBufferSize()) }
}

fn make(device: &ID3D12Device) -> windows::core::Result<Composer> {
    let vs = compile(s!("vs"), s!("vs_5_1"))?;
    let ps = compile(s!("ps"), s!("ps_5_1"))?;
    // SAFETY: plain object creation; the root signature is inside the shader.
    unsafe {
        let root: ID3D12RootSignature = device.CreateRootSignature(0, bytes(&vs))?;
        let heap: ID3D12DescriptorHeap = device.CreateDescriptorHeap(&D3D12_DESCRIPTOR_HEAP_DESC {
            Type: D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
            NumDescriptors: SLOTS,
            Flags: D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE,
            NodeMask: 0,
        })?;
        let size = device.GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
        Ok(Composer { root, vs, ps, pipelines: Mutex::new(HashMap::new()), heap, size, next: AtomicU32::new(0) })
    }
}

fn composer(device: &ID3D12Device) -> Option<&'static Composer> {
    static C: std::sync::OnceLock<Option<Composer>> = std::sync::OnceLock::new();
    C.get_or_init(|| match make(device) {
        Ok(c) => Some(c),
        Err(e) => {
            tracing::warn!(error = %e, "no plane composition: only the first plane is shown");
            None
        }
    })
    .as_ref()
}

impl Composer {
    fn pipeline(&self, device: &ID3D12Device, format: DXGI_FORMAT) -> Option<ID3D12PipelineState> {
        let mut map = self.pipelines.lock().unwrap_or_else(|e| e.into_inner());
        if let Some(p) = map.get(&format.0) {
            return Some(p.clone());
        }
        let mut blend = D3D12_BLEND_DESC::default();
        blend.RenderTarget[0] = D3D12_RENDER_TARGET_BLEND_DESC {
            BlendEnable: true.into(),
            LogicOpEnable: false.into(),
            SrcBlend: D3D12_BLEND_ONE,
            DestBlend: D3D12_BLEND_INV_SRC_ALPHA,
            BlendOp: D3D12_BLEND_OP_ADD,
            SrcBlendAlpha: D3D12_BLEND_ONE,
            DestBlendAlpha: D3D12_BLEND_INV_SRC_ALPHA,
            BlendOpAlpha: D3D12_BLEND_OP_ADD,
            LogicOp: D3D12_LOGIC_OP_NOOP,
            RenderTargetWriteMask: D3D12_COLOR_WRITE_ENABLE_ALL.0 as u8,
        };
        let mut formats = [DXGI_FORMAT_UNKNOWN; 8];
        formats[0] = format;
        let desc = D3D12_GRAPHICS_PIPELINE_STATE_DESC {
            // SAFETY: a borrowed interface pointer, alive through the call.
            pRootSignature: unsafe { std::mem::transmute_copy(&self.root) },
            VS: D3D12_SHADER_BYTECODE { pShaderBytecode: bytes(&self.vs).as_ptr().cast(), BytecodeLength: bytes(&self.vs).len() },
            PS: D3D12_SHADER_BYTECODE { pShaderBytecode: bytes(&self.ps).as_ptr().cast(), BytecodeLength: bytes(&self.ps).len() },
            BlendState: blend,
            SampleMask: u32::MAX,
            RasterizerState: D3D12_RASTERIZER_DESC {
                FillMode: D3D12_FILL_MODE_SOLID,
                CullMode: D3D12_CULL_MODE_NONE,
                DepthClipEnable: true.into(),
                ..Default::default()
            },
            PrimitiveTopologyType: D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE,
            NumRenderTargets: 1,
            RTVFormats: formats,
            SampleDesc: DXGI_SAMPLE_DESC { Count: 1, Quality: 0 },
            ..Default::default()
        };
        // SAFETY: a complete description whose pointers live through the call.
        match unsafe { device.CreateGraphicsPipelineState::<ID3D12PipelineState>(&desc) } {
            Ok(p) => {
                map.insert(format.0, p.clone());
                Some(p)
            }
            Err(e) => {
                tracing::warn!(error = %e, format = format.0, "no plane composition pipeline");
                None
            }
        }
    }
}

/// Records, in `list`, `overlay` (in `PIXEL_SHADER_RESOURCE`) drawn over
/// `back` (in `RENDER_TARGET`, `format`, `width` by `height`). False if
/// nothing was recorded.
pub(crate) fn overlay(
    host: &crate::host::Host,
    list: &ID3D12GraphicsCommandList,
    back: &ID3D12Resource,
    format: DXGI_FORMAT,
    (width, height): (u32, u32),
    overlay: &ID3D12Resource,
) -> bool {
    let device = &host.device;
    let Some(c) = composer(device) else { return false };
    let Some(pso) = c.pipeline(device, format) else { return false };
    // SAFETY: a live resource.
    let od = unsafe { overlay.GetDesc() };
    let view_format = match od.Format.0 {
        27 => DXGI_FORMAT_R8G8B8A8_UNORM,
        87 | 90 => DXGI_FORMAT_B8G8R8A8_UNORM,
        23 => DXGI_FORMAT_R10G10B10A2_UNORM,
        9 => DXGI_FORMAT_R16G16B16A16_FLOAT,
        _ => od.Format,
    };
    let slot = c.next.fetch_add(1, Ordering::Relaxed) % SLOTS;
    // SAFETY: our heap, a live resource, an open list; the view's slot is
    // not reused before SLOTS more presents.
    unsafe {
        let mut cpu = c.heap.GetCPUDescriptorHandleForHeapStart();
        cpu.ptr += (slot * c.size) as usize;
        let mut gpu = c.heap.GetGPUDescriptorHandleForHeapStart();
        gpu.ptr += u64::from(slot * c.size);
        let srv = D3D12_SHADER_RESOURCE_VIEW_DESC {
            Format: view_format,
            ViewDimension: D3D12_SRV_DIMENSION_TEXTURE2D,
            Shader4ComponentMapping: D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
            Anonymous: D3D12_SHADER_RESOURCE_VIEW_DESC_0 {
                Texture2D: D3D12_TEX2D_SRV { MostDetailedMip: 0, MipLevels: 1, PlaneSlice: 0, ResourceMinLODClamp: 0.0 },
            },
        };
        device.CreateShaderResourceView(overlay, Some(&srv), cpu);
        let rtv = host.rtv(Some(back), None);
        list.OMSetRenderTargets(1, Some(&rtv), false, None);
        list.RSSetViewports(&[D3D12_VIEWPORT {
            TopLeftX: 0.0,
            TopLeftY: 0.0,
            Width: width as f32,
            Height: height as f32,
            MinDepth: 0.0,
            MaxDepth: 1.0,
        }]);
        list.RSSetScissorRects(&[windows::Win32::Foundation::RECT { left: 0, top: 0, right: width as i32, bottom: height as i32 }]);
        list.SetGraphicsRootSignature(&c.root);
        list.SetDescriptorHeaps(&[Some(c.heap.clone())]);
        list.SetGraphicsRootDescriptorTable(0, gpu);
        list.SetPipelineState(&pso);
        list.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        list.DrawInstanced(3, 1, 0, 0);
    }
    true
}
