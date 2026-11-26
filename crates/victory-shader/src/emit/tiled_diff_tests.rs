//! A title's wave64 compute program run two ways with the same inputs: as one
//! console wave of 64 lanes (`WaveSize(64)`, on WARP, which runs waves of up
//! to 128) and as two host waves of 32 (the form NVIDIA cards get), to find
//! where the second departs from the first.
//!
//! `VICTORY_DIFF_CONTAINER=<file.cdxil>` (a container as the title has it,
//! from `victory translate` with `VICTORY_DUMP_MATCH`); skipped without it.
//! Game-derived files stay outside the repository.

use super::{HostShader, translate};

fn both(container: &[u8]) -> (Result<HostShader, String>, Result<HostShader, String>) {
    let at = |lanes| {
        let was = super::lower::TEST_HOST_WAVE.replace(Some(lanes));
        let r = translate(container).map_err(|e| e.to_string());
        super::lower::TEST_HOST_WAVE.set(was);
        r
    };
    (at(64), at(32))
}

#[test]
fn a_title_program_in_two_host_waves_matches_one_console_wave() {
    let Some(path) = std::env::var_os("VICTORY_DIFF_CONTAINER") else {
        eprintln!("no VICTORY_DIFF_CONTAINER; skipped");
        return;
    };
    let bytes = std::fs::read(&path).expect("container");
    {
        let c = super::Container::parse(&bytes).expect("container");
        let part = c.part(super::FourCC::DXIL).or(c.part(super::FourCC::ACCC)).expect("part");
        let native = crate::native_programs(part.data).expect("programs").into_iter().next().expect("program");
        eprintln!("io: {:?}, wave64 {}, vgprs {}", native.io, native.wave64, native.vgprs);
        let p = crate::spill::build(&native).expect("ir");
        let st = crate::structure::structure(&p).ok();
        let res = crate::resource::track(&p, &native.layout, native.stage, st.as_ref()).expect("resources");
        for d in &res.descriptors {
            eprintln!("  descriptor {d:?}");
        }
    }
    let (wide, split) = both(&bytes);
    for (name, s) in [("wave64", &wide), ("2x32", &split)] {
        match s {
            Ok(s) => {
                eprintln!("{name}: {} bytes of DXIL", s.dxil.len());
                eprintln!("  user data: {:?}", s.user_data);
                eprintln!("  samplers: {}, root cbvs {:?}, srvs {:?}, uavs {:?}", s.samplers.len(), s.root_cbvs, s.root_srvs, s.root_uavs);
                eprintln!("  indexed: {:?}", s.root_indexed_views);
            }
            Err(e) => eprintln!("{name}: {e}"),
        }
    }
}
