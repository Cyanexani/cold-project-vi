//! `TRANSLATOR_ID`: a hash of this crate's sources, its manifest and the
//! features it is built with. The host's shader cache is keyed by it
//! instead of by the whole executable, which changed with every rebuild,
//! translator or not, and sent every shader through the translator again
//! on the next run (minutes of a black or broken picture).

use std::path::{Path, PathBuf};

fn files(dir: &Path, out: &mut Vec<PathBuf>) {
    let Ok(entries) = std::fs::read_dir(dir) else { return };
    for entry in entries.flatten() {
        let path = entry.path();
        if path.is_dir() {
            files(&path, out);
        } else {
            out.push(path);
        }
    }
}

/// FNV-1a, 64 bits, from `seed`.
fn fnv(seed: u64, bytes: &[u8], mut h: u64) -> u64 {
    if h == 0 {
        h = seed;
    }
    for &b in bytes {
        h ^= u64::from(b);
        h = h.wrapping_mul(0x0000_0100_0000_01b3);
    }
    h
}

fn main() {
    let root = PathBuf::from(std::env::var_os("CARGO_MANIFEST_DIR").expect("cargo sets it"));
    println!("cargo:rerun-if-changed=src");
    println!("cargo:rerun-if-changed=Cargo.toml");
    println!("cargo:rerun-if-changed=build.rs");
    let mut list = Vec::new();
    files(&root.join("src"), &mut list);
    list.push(root.join("Cargo.toml"));
    list.sort();
    let (mut a, mut b) = (0u64, 0u64);
    for path in &list {
        let name = path.strip_prefix(&root).unwrap_or(path).to_string_lossy().replace('\\', "/");
        let bytes = std::fs::read(path).unwrap_or_default();
        for part in [name.as_bytes(), &(bytes.len() as u64).to_le_bytes(), &bytes] {
            a = fnv(0xcbf2_9ce4_8422_2325, part, a);
            b = fnv(0x6c62_272e_07bb_0142, part, b);
        }
    }
    // The features change what is translated (diagnostics switches).
    let mut features: Vec<String> = std::env::vars().map(|(k, _)| k).filter(|k| k.starts_with("CARGO_FEATURE_")).collect();
    features.sort();
    for f in &features {
        a = fnv(0xcbf2_9ce4_8422_2325, f.as_bytes(), a);
        b = fnv(0x6c62_272e_07bb_0142, f.as_bytes(), b);
    }
    println!("cargo:rustc-env=VICTORY_TRANSLATOR_ID={a:016x}{b:016x}");
}
