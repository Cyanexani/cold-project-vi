//! The FidelityFX API's dispatch (`amd_fidelityfx_loader_xbox.dll`), taken
//! over: as a Series S the title upscales its frame with FSR 4, whose passes
//! do not run here (their pipelines need a device method we do not have,
//! and its fallback programs are shader model 5), so its output stayed
//! empty and only the interface was seen. An upscale dispatch now records a
//! plain stretch of the colour input over the output
//! (`victory_gpu::upscale`); the loader's other exports stay the title's.
//!
//! The upscale description is FidelityFX API 1.x's: a header (type, next),
//! the command list, then resources of 48 bytes (the resource, a 32-byte
//! description, the state): colour, depth, motion vectors, exposure,
//! reactive, transparency and composition, output; then the jitter, the
//! motion vector scale, the render size and the upscale size.
//!
//! shortcut: dispatches of other kinds (and upscales whose description does
//! not look like one) do nothing and report success.

use std::ffi::c_void;

use crate::registry::{HleExport, HleModule};

/// `FFX_API_DISPATCH_DESC_TYPE_UPSCALE`.
const DISPATCH_UPSCALE: u64 = 0x0001_0001;
const RESOURCE: usize = 48;
const COLOR: usize = 24;
const OUTPUT: usize = COLOR + 6 * RESOURCE;
const RENDER_SIZE: usize = OUTPUT + RESOURCE + 16;

/// `ffxDispatch(context, description)`.
extern "system" fn ffx_dispatch(_context: *mut c_void, desc: *const u8) -> u32 {
    const OK: u32 = 0;
    if desc.is_null() {
        return OK;
    }
    // SAFETY: the title passes a description that starts with its header.
    let kind = unsafe { desc.cast::<u64>().read_unaligned() };
    if kind != DISPATCH_UPSCALE {
        once_kind(kind);
        return OK;
    }
    // SAFETY: an upscale description is longer than the fields read here.
    let (list, color, output, color_size, out_size, rendered) = unsafe {
        let ptr = |at: usize| desc.add(at).cast::<*const c_void>().read_unaligned();
        let u32_at = |at: usize| desc.add(at).cast::<u32>().read_unaligned();
        (
            ptr(16),
            ptr(COLOR),
            ptr(OUTPUT),
            (u32_at(COLOR + 8 + 8), u32_at(COLOR + 8 + 12)),
            (u32_at(OUTPUT + 8 + 8), u32_at(OUTPUT + 8 + 12)),
            (u32_at(RENDER_SIZE), u32_at(RENDER_SIZE + 4)),
        )
    };
    let plausible = !list.is_null()
        && !color.is_null()
        && !output.is_null()
        && (1..=8192).contains(&rendered.0)
        && (1..=8192).contains(&rendered.1);
    static FIRST: std::sync::Once = std::sync::Once::new();
    FIRST.call_once(|| {
        tracing::info!(
            ?color_size,
            ?out_size,
            ?rendered,
            plausible,
            "FidelityFX upscale: a plain upscale stands in for it"
        );
    });
    #[cfg(windows)]
    if plausible {
        victory_gpu::upscale::upscale(list, color, output, rendered);
    }
    OK
}

fn once_kind(kind: u64) {
    static SEEN: std::sync::Mutex<Vec<u64>> = std::sync::Mutex::new(Vec::new());
    let mut seen = SEEN.lock().unwrap_or_else(|e| e.into_inner());
    if !seen.contains(&kind) {
        seen.push(kind);
        tracing::info!(kind = format_args!("{kind:#x}"), "FidelityFX dispatch of another kind: left out");
    }
}

pub(crate) fn module() -> HleModule {
    HleModule {
        name: "ffx",
        dlls: &["amd_fidelityfx_loader_xbox.dll"],
        exports: vec![HleExport {
            name: "ffxDispatch",
            address: ffx_dispatch as *const () as usize,
        }],
    }
}
