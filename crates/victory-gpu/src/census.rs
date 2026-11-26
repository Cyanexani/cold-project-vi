//! Which of our buffers in the PC's memory are alive, by kind, and their
//! bytes (the memory line's "shared with the card" and write-combined
//! regions held 4-12 GB at the cell and nothing said whose).
//!
//! Each buffer gets a tag: a minimal COM object set as the resource's
//! private data. D3D12 releases it when the resource is destroyed, and the
//! tag takes its bytes off its kind then. Setting another tag on the same
//! resource (a caller naming its kind after the maker's default) releases
//! the first.

use std::ffi::c_void;
use std::sync::atomic::{AtomicU32, AtomicU64, Ordering};

use windows::Win32::Graphics::Direct3D12::ID3D12Resource;
use windows::core::{GUID, HRESULT, IUnknown, Interface};

/// The kinds counted.
pub(crate) const KINDS: [&str; 13] = [
    "upload buffers (other)",
    "shared buffers (other)",
    "host copies of title memory",
    "texture staging",
    "list scratch",
    "upload rings of copies on the card",
    "descriptor uploads",
    "fetches of copies on the card",
    "card compare results",
    "title memory buffers (memory.rs)",
    "indirect argument clamps",
    "one-off uploads (textures, buffers)",
    "windows in the card's memory the CPU maps (not the PC's RAM)",
];
pub(crate) const UPLOAD: usize = 0;
pub(crate) const SHARED: usize = 1;
pub(crate) const COPIES: usize = 2;
pub(crate) const STAGING: usize = 3;
pub(crate) const SCRATCH: usize = 4;
pub(crate) const RING: usize = 5;
pub(crate) const DESCRIPTORS: usize = 6;
pub(crate) const FETCH: usize = 7;
pub(crate) const COMPARE: usize = 8;
pub(crate) const MEMORY: usize = 9;
#[allow(dead_code)]
pub(crate) const INDIRECT: usize = 10;
pub(crate) const ONE_OFF: usize = 11;
pub(crate) const REBAR: usize = 12;

/// Bytes and buffers alive, by kind.
static BYTES: [AtomicU64; 13] = [const { AtomicU64::new(0) }; 13];
static COUNT: [AtomicU64; 13] = [const { AtomicU64::new(0) }; 13];

/// The private data key of the tags.
const KEY: GUID = GUID::from_u128(0x5f3c_2a71_9d4e_4b8a_a1c3_7e2f_6b0d_9c15);

#[repr(C)]
struct Vtbl {
    query: unsafe extern "system" fn(*mut c_void, *const GUID, *mut *mut c_void) -> HRESULT,
    add: unsafe extern "system" fn(*mut c_void) -> u32,
    release: unsafe extern "system" fn(*mut c_void) -> u32,
}

#[repr(C)]
struct Tag {
    vtbl: *const Vtbl,
    refs: AtomicU32,
    kind: usize,
    bytes: u64,
}

static VTBL: Vtbl = Vtbl { query, add, release };

unsafe extern "system" fn query(this: *mut c_void, iid: *const GUID, out: *mut *mut c_void) -> HRESULT {
    // SAFETY: COM's contract: valid pointers.
    unsafe {
        if out.is_null() {
            return HRESULT(0x8000_4003u32 as i32); // E_POINTER
        }
        if !iid.is_null() && *iid == IUnknown::IID {
            add(this);
            *out = this;
            return HRESULT(0);
        }
        *out = std::ptr::null_mut();
        HRESULT(0x8000_4002u32 as i32) // E_NOINTERFACE
    }
}

unsafe extern "system" fn add(this: *mut c_void) -> u32 {
    // SAFETY: a live tag.
    let tag = unsafe { &*this.cast::<Tag>() };
    tag.refs.fetch_add(1, Ordering::Relaxed) + 1
}

unsafe extern "system" fn release(this: *mut c_void) -> u32 {
    // SAFETY: a live tag; freed once, at its last reference.
    unsafe {
        let tag = &*this.cast::<Tag>();
        let left = tag.refs.fetch_sub(1, Ordering::AcqRel) - 1;
        if left == 0 {
            BYTES[tag.kind].fetch_sub(tag.bytes, Ordering::Relaxed);
            COUNT[tag.kind].fetch_sub(1, Ordering::Relaxed);
            drop(Box::from_raw(this.cast::<Tag>()));
        }
        left
    }
}

/// Counts `resource` (`bytes`) as alive in `kind` until it is destroyed.
pub(crate) fn tag(resource: &ID3D12Resource, kind: usize, bytes: u64) {
    let kind = kind.min(KINDS.len() - 1);
    BYTES[kind].fetch_add(bytes, Ordering::Relaxed);
    COUNT[kind].fetch_add(1, Ordering::Relaxed);
    let raw = Box::into_raw(Box::new(Tag {
        vtbl: &VTBL,
        refs: AtomicU32::new(1),
        kind,
        bytes,
    }));
    // SAFETY: a COM object of ours (vtable of IUnknown), one reference
    // owned here; D3D12 takes its own and releases it with the resource,
    // ours goes when `unknown` drops.
    unsafe {
        let unknown = IUnknown::from_raw(raw.cast());
        // Not kept: our reference, dropped below, is the last, and the
        // release counts it out again.
        let _ = resource.SetPrivateDataInterface(&KEY, &unknown);
    }
}

/// The memory line's census: kinds with buffers alive, largest first.
pub(crate) fn line() -> String {
    let mut kinds: Vec<(u64, u64, &str)> = (0..KINDS.len())
        .map(|i| (BYTES[i].load(Ordering::Relaxed), COUNT[i].load(Ordering::Relaxed), KINDS[i]))
        .filter(|k| k.1 > 0)
        .collect();
    kinds.sort_unstable_by_key(|k| std::cmp::Reverse(k.0));
    let total: u64 = kinds.iter().map(|k| k.0).sum();
    let parts: Vec<String> = kinds.iter().map(|(b, n, k)| format!("{k} {} MB in {n}", b >> 20)).collect();
    format!("our buffers in the PC's memory alive: {} MB: {}", total >> 20, parts.join("; "))
}
