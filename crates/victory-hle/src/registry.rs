//! The table of everything we implement.

use std::sync::LazyLock;

use crate::modules;

/// One function we implement, under the name the title imports (`#n` for an
/// ordinal).
#[derive(Debug, Clone, Copy)]
pub struct HleExport {
    pub name: &'static str,
    pub address: usize,
}

/// A group of implementations and the DLL names they answer for.
#[derive(Debug)]
pub struct HleModule {
    /// Human-readable name, e.g. "kernel".
    pub name: &'static str,
    /// Lower-case DLL names served. A trailing `*` matches a prefix, which
    /// covers versioned API sets (`api-ms-win-core-libraryloader-l1-2-0.dll`).
    pub dlls: &'static [&'static str],
    pub exports: Vec<HleExport>,
}

/// Title DLLs of which we take over some exports only: the DLL is still
/// mapped from the title's folder and its other exports are its own (the
/// imports are looked up here first, then in the title's DLLs).
const PARTIAL: &[&str] = &["amd_fidelityfx_loader_xbox.dll"];

impl HleModule {
    /// Whether the title's own DLL of this name is not mapped (its exports
    /// are all ours); false for a DLL we take over in part ([`PARTIAL`]).
    pub fn replaces(&self, dll: &str) -> bool {
        self.serves(dll) && !PARTIAL.iter().any(|p| p.eq_ignore_ascii_case(dll))
    }

    pub fn serves(&self, dll: &str) -> bool {
        let dll = dll.to_ascii_lowercase();
        self.dlls
            .iter()
            .any(|pattern| match pattern.strip_suffix('*') {
                Some(prefix) => dll.starts_with(prefix),
                None => dll == *pattern,
            })
    }
}

/// Declares an export whose Rust name equals the imported name.
#[macro_export]
macro_rules! hle_export {
    ($f:ident) => {
        $crate::HleExport {
            name: stringify!($f),
            address: $f as *const () as usize,
        }
    };
}

static REGISTRY: LazyLock<Vec<HleModule>> = LazyLock::new(modules::all);

/// All modules we implement.
pub fn registry() -> &'static [HleModule] {
    &REGISTRY
}

pub(crate) fn lookup(dll: &str, name: &str) -> Option<usize> {
    registry()
        .iter()
        .filter(|m| m.serves(dll))
        .flat_map(|m| m.exports.iter())
        .find(|e| e.name == name)
        .map(|e| e.address)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn export_names_are_unique_per_dll_pattern() {
        for module in registry() {
            let mut names: Vec<_> = module.exports.iter().map(|e| e.name).collect();
            names.sort_unstable();
            let before = names.len();
            names.dedup();
            assert_eq!(
                before,
                names.len(),
                "duplicate export in module {}",
                module.name
            );
        }
    }

    #[test]
    fn api_set_prefix_matching() {
        let m = HleModule {
            name: "t",
            dlls: &["api-ms-win-core-libraryloader-*"],
            exports: vec![],
        };
        assert!(m.serves("API-MS-WIN-CORE-LIBRARYLOADER-L1-2-0.dll"));
        assert!(!m.serves("api-ms-win-core-file-l1-1-0.dll"));
    }
}
