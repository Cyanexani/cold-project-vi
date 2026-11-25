//! The counter drawn over the top left of the picture: frame rate, where a
//! frame's time goes, memory, and what is loaded (`VICTORY_HUD=0` hides
//! it). The meter sets its text once a second ([`set`]); each present
//! draws it into the back buffer before it is shown ([`draw`]).
//!
//! The text is upper-case ASCII, [`LINES`] lines of [`COLS`] characters,
//! drawn with a 5x7 font kept in the shader, on a dark half-transparent
//! box.

use std::collections::HashMap;
use std::sync::Mutex;

use windows::Win32::Graphics::Direct3D::Fxc::D3DCompile;
use windows::Win32::Graphics::Direct3D::{D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST, ID3DBlob};
use windows::Win32::Graphics::Direct3D12::*;
use windows::Win32::Graphics::Dxgi::Common::*;
use windows::core::{PCSTR, s};

/// Lines and characters a line the counter holds (4 characters a root
/// constant: 60 of the 64 constants hold the text).
pub(crate) const LINES: usize = 6;
pub(crate) const COLS: usize = 40;

const SOURCE: &str = r#"
#define RS "RootConstants(num32BitConstants=64, b0)"
cbuffer Hud : register(b0) { uint4 head; uint4 text[15]; };
// A 5x7 font for ASCII 32..95: rows 0-3 in x, 4-6 in y, 5 bits a row,
// the left column in bit 4.
static const uint2 FONT[64] = {
    uint2(0x0, 0x0), uint2(0x0, 0x0), uint2(0x0, 0x0), uint2(0x0, 0x0),
    uint2(0x0, 0x0), uint2(0x20b38, 0xe68), uint2(0x0, 0x0), uint2(0x0, 0x0),
    uint2(0x42082, 0x888), uint2(0x10888, 0x2082), uint2(0x0, 0x0), uint2(0xf9080, 0x84),
    uint2(0x0, 0x208c), uint2(0xf8000, 0x0), uint2(0x0, 0x3180), uint2(0x20820, 0x208),
    uint2(0xace2e, 0x3a39), uint2(0x21184, 0x3884), uint2(0x1062e, 0x7d04), uint2(0x1105f, 0x3a21),
    uint2(0x928c2, 0x85f), uint2(0xfa1f, 0x3a21), uint2(0xf4106, 0x3a31), uint2(0x2083f, 0x2108),
    uint2(0x7462e, 0x3a31), uint2(0x7c62e, 0x3041), uint2(0x3180, 0x18c), uint2(0x0, 0x0),
    uint2(0x0, 0x0), uint2(0x7c00, 0x1f), uint2(0x0, 0x0), uint2(0x0, 0x0),
    uint2(0x0, 0x0), uint2(0xfc62e, 0x4631), uint2(0xf463e, 0x7a31), uint2(0x8422e, 0x3a30),
    uint2(0x8c63e, 0x7a31), uint2(0xf421f, 0x7e10), uint2(0xf421f, 0x4210), uint2(0xbc22e, 0x3e31),
    uint2(0xfc631, 0x4631), uint2(0x2108e, 0x3884), uint2(0x10847, 0x3242), uint2(0xc5251, 0x4654),
    uint2(0x84210, 0x7e10), uint2(0xad771, 0x4631), uint2(0xae631, 0x4633), uint2(0x8c62e, 0x3a31),
    uint2(0xf463e, 0x4210), uint2(0x8c62e, 0x3655), uint2(0xf463e, 0x4654), uint2(0x7420f, 0x7821),
    uint2(0x2109f, 0x1084), uint2(0x8c631, 0x3a31), uint2(0x8c631, 0x1151), uint2(0xac631, 0x2ab5),
    uint2(0x22a31, 0x462a), uint2(0x22a31, 0x1084), uint2(0x2083f, 0x7e08), uint2(0x0, 0x0),
    uint2(0x0, 0x0), uint2(0x0, 0x0), uint2(0x0, 0x0), uint2(0x0, 0x0),
};
struct Out { float4 position : SV_Position; };
[RootSignature(RS)]
Out vs(uint id : SV_VertexID) {
    Out o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.position = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}
[RootSignature(RS)]
float4 ps(Out i) : SV_Target {
    // head: left, top, pixels a font pixel, lines shown.
    uint scale = max(head.z, 1u);
    int2 q = int2(i.position.xy) - int2(head.xy) - int2(2 * scale, 2 * scale);
    uint cw = 6u * scale, ch = 9u * scale;
    float4 box = float4(0, 0, 0, 0.6);
    if (q.x < 0 || q.y < 0) return box;
    uint col = uint(q.x) / cw, row = uint(q.y) / ch;
    if (col >= 40u || row >= head.w) return box;
    uint gx = (uint(q.x) % cw) / scale, gy = (uint(q.y) % ch) / scale;
    uint at = row * 40u + col;
    uint word = text[at / 16u][(at / 4u) % 4u];
    uint c = (word >> (8u * (at % 4u))) & 0xffu;
    if (gx < 5u && gy >= 1u && gy < 8u && c >= 32u && c < 96u) {
        uint r = gy - 1u;
        uint2 g = FONT[c - 32u];
        uint bits = r < 4u ? (g.x >> (5u * r)) : (g.y >> (5u * (r - 4u)));
        if (((bits >> (4u - gx)) & 1u) != 0u) return float4(1.0, 0.9, 0.2, 1.0);
    }
    return box;
}
"#;

/// The text to draw, as root constants (4 characters each), and its lines.
static TEXT: Mutex<Option<([u32; 60], u32)>> = Mutex::new(None);

/// Whether the counter is drawn (`VICTORY_HUD=0`: not).
pub(crate) fn on() -> bool {
    static ON: std::sync::OnceLock<bool> = std::sync::OnceLock::new();
    *ON.get_or_init(|| std::env::var_os("VICTORY_HUD").is_none_or(|v| v != "0"))
}

/// Packs `lines` as the counter's text: at most [`LINES`] lines of [`COLS`]
/// characters, lower case raised, characters outside the font as spaces.
fn pack(lines: &[String]) -> ([u32; 60], u32) {
    let mut words = [0u32; 60];
    let shown = lines.len().min(LINES);
    for l in 0..shown {
        let chars: Vec<char> = lines[l].chars().take(COLS).collect();
        for c in 0..COLS {
            let ch = chars.get(c).map_or(' ', |c| c.to_ascii_uppercase());
            let b = if (' '..='_').contains(&ch) { ch as u32 } else { 32 };
            let at = l * COLS + c;
            words[at / 4] |= b << (8 * (at % 4));
        }
    }
    (words, shown as u32)
}

/// Sets the counter's text ([`pack`]).
pub(crate) fn set(lines: &[String]) {
    *TEXT.lock().unwrap_or_else(|e| e.into_inner()) = Some(pack(lines));
}

struct Hud {
    root: ID3D12RootSignature,
    vs: ID3DBlob,
    ps: ID3DBlob,
    pipelines: Mutex<HashMap<i32, ID3D12PipelineState>>,
}

// SAFETY: D3D12 objects are free-threaded; the map is behind its lock.
unsafe impl Send for Hud {}
// SAFETY: as above.
unsafe impl Sync for Hud {}

fn compile(entry: PCSTR, target: PCSTR) -> windows::core::Result<ID3DBlob> {
    let mut code: Option<ID3DBlob> = None;
    let mut errors: Option<ID3DBlob> = None;
    // SAFETY: the source and names live through the call; out pointers ours.
    let compiled = unsafe {
        D3DCompile(SOURCE.as_ptr().cast(), SOURCE.len(), s!("hud"), None, None, entry, target, 0, 0, &mut code, Some(&mut errors))
    };
    if let Err(e) = compiled {
        if let Some(errors) = errors {
            // SAFETY: the blob's own bytes.
            let text = unsafe { std::slice::from_raw_parts(errors.GetBufferPointer().cast::<u8>(), errors.GetBufferSize()) };
            tracing::warn!("counter: {}", String::from_utf8_lossy(text));
        }
        return Err(e);
    }
    code.ok_or_else(windows::core::Error::empty)
}

fn bytes(blob: &ID3DBlob) -> &[u8] {
    // SAFETY: the blob's own bytes, alive as long as the blob.
    unsafe { std::slice::from_raw_parts(blob.GetBufferPointer().cast::<u8>(), blob.GetBufferSize()) }
}

fn hud(device: &ID3D12Device) -> Option<&'static Hud> {
    static H: std::sync::OnceLock<Option<Hud>> = std::sync::OnceLock::new();
    H.get_or_init(|| {
        let made = (|| -> windows::core::Result<Hud> {
            let vs = compile(s!("vs"), s!("vs_5_1"))?;
            let ps = compile(s!("ps"), s!("ps_5_1"))?;
            // SAFETY: plain object creation; the root signature is in the shader.
            let root: ID3D12RootSignature = unsafe { device.CreateRootSignature(0, bytes(&vs))? };
            Ok(Hud { root, vs, ps, pipelines: Mutex::new(HashMap::new()) })
        })();
        match made {
            Ok(h) => Some(h),
            Err(e) => {
                tracing::warn!(error = %e, "no counter over the picture");
                None
            }
        }
    })
    .as_ref()
}

impl Hud {
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
                tracing::warn!(error = %e, format = format.0, "no counter pipeline");
                None
            }
        }
    }
}

/// Records, in `list`, the counter drawn over `back` (in `RENDER_TARGET`,
/// `format`, `width` by `height`). False if nothing was recorded (off, no
/// text yet, or no pipeline).
pub(crate) fn draw(
    host: &crate::host::Host,
    list: &ID3D12GraphicsCommandList,
    back: &ID3D12Resource,
    format: DXGI_FORMAT,
    (width, height): (u32, u32),
) -> bool {
    if !on() {
        return false;
    }
    let Some((words, lines)) = *TEXT.lock().unwrap_or_else(|e| e.into_inner()) else { return false };
    let device = &host.device;
    let Some(h) = hud(device) else { return false };
    let Some(pso) = h.pipeline(device, format) else { return false };
    // Two pixels a font pixel at 1080 lines, at least one.
    let scale = (height / 540).max(1);
    let (left, top) = (8 * scale, 8 * scale);
    let right = (left + (COLS as u32 * 6 + 4) * scale).min(width) as i32;
    let bottom = (top + (lines * 9 + 4) * scale).min(height) as i32;
    let mut constants = [0u32; 64];
    constants[..4].copy_from_slice(&[left, top, scale, lines]);
    constants[4..].copy_from_slice(&words);
    // SAFETY: an open list, our pipeline and root signature, a back buffer
    // in the render target state.
    unsafe {
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
        list.RSSetScissorRects(&[windows::Win32::Foundation::RECT { left: left as i32, top: top as i32, right, bottom }]);
        list.SetGraphicsRootSignature(&h.root);
        list.SetGraphicsRoot32BitConstants(0, 64, constants.as_ptr().cast(), 0);
        list.SetPipelineState(&pso);
        list.IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        list.DrawInstanced(3, 1, 0, 0);
    }
    true
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn text_is_packed_four_characters_a_word_padded_with_spaces() {
        let (words, lines) = pack(&["fps 2.3".into(), "é".into()]);
        assert_eq!(lines, 2);
        assert_eq!(words[0], u32::from_le_bytes(*b"FPS "));
        assert_eq!(words[1], u32::from_le_bytes(*b"2.3 "));
        // A character outside the font is a space; the rest of the line too.
        assert_eq!(words[COLS / 4], u32::from_le_bytes(*b"    "));
    }
}
