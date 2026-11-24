//! Title memory that is a view of a section (the console's physical pages,
//! `xmem.dll`): which section and offset each view shows, so the host GPU
//! can use the section itself as a heap (`OpenExistingHeapFromFileMapping`)
//! instead of a copy of the view. One heap per section, opened on first
//! use and kept while the section lives.

use std::collections::{BTreeMap, HashMap};
use std::sync::Mutex;

use windows::Win32::Foundation::HANDLE;
use windows::Win32::Graphics::Direct3D12::*;

/// A view: its length, its section and where in it the view starts.
#[derive(Clone, Copy)]
struct View {
    len: usize,
    section: isize,
    offset: u64,
}

#[derive(Default)]
struct Sections {
    views: BTreeMap<usize, View>,
    /// Per section: its size, and its heap once opened (`None` after a
    /// failed try: not tried again).
    heaps: HashMap<isize, (u64, Option<Option<ID3D12Heap>>)>,
}

// SAFETY: D3D12 heaps are free-threaded; the handles are plain values.
unsafe impl Send for Sections {}

static SECTIONS: Mutex<Option<Sections>> = Mutex::new(None);

fn with<T>(f: impl FnOnce(&mut Sections) -> T) -> T {
    let mut s = SECTIONS.lock().unwrap_or_else(|e| e.into_inner());
    f(s.get_or_insert_with(Sections::default))
}

/// The runtime mapped `[at, at + len)` as a view of `section` (`size` bytes)
/// from byte `offset` on.
pub fn title_view_mapped(at: usize, len: usize, section: isize, offset: u64, size: u64) {
    with(|s| {
        s.views.insert(at, View { len, section, offset });
        s.heaps.entry(section).or_insert((size, None));
    });
}

/// The view at `at` is gone.
pub(crate) fn view_unmapped(at: usize, len: usize) {
    with(|s| {
        let gone: Vec<usize> = s.views.range(at..at.saturating_add(len.max(1))).map(|(k, _)| *k).collect();
        for k in gone {
            s.views.remove(&k);
        }
    });
}

/// The section was closed: its heap goes (lists holding buffers in it keep
/// it alive) and its handle value may come back for another section.
pub fn title_section_closed(section: isize) {
    with(|s| {
        s.heaps.remove(&section);
        s.views.retain(|_, v| v.section != section);
    });
}

/// For `[start, end)` inside one view: the section's heap (opened now if
/// need be) and the byte offset of `start` in it.
pub(crate) fn heap_for(device: &ID3D12Device, start: usize, end: usize) -> Option<(ID3D12Heap, u64)> {
    with(|s| {
        let (&base, &view) = s.views.range(..=start).next_back()?;
        if end > base + view.len {
            return None;
        }
        let offset = view.offset + (start - base) as u64;
        let (size, heap) = s.heaps.get_mut(&view.section)?;
        if heap.is_none() {
            let device3 = windows::core::Interface::cast::<ID3D12Device3>(device).ok();
            // SAFETY: a live section handle (removed from here when closed).
            let opened: Option<ID3D12Heap> = device3.and_then(|d| unsafe {
                match d.OpenExistingHeapFromFileMapping::<ID3D12Heap>(HANDLE(view.section as *mut _)) {
                    Ok(h) => Some(h),
                    Err(e) => {
                        tracing::info!(error = %e, size = *size, "physical pages not opened as a host heap; copied");
                        None
                    }
                }
            });
            if opened.is_some() {
                tracing::info!(size = *size, "physical pages opened as a host heap");
            }
            *heap = Some(opened);
        }
        let heap = heap.clone().flatten()?;
        Some((heap, offset))
    })
}
