# Helmet hotspots migration to current main

- Base: `b4e1886c9762a85926ef3bf58db92500a29176f3` (`origin/main`)
- Local branch: `codex/helmet-hotspots-latest`
- Source feature: `8fe3142dc4ed1e3b31e6955cbbf81150d2d39fec` (`add-helmet-hotspots`)
- Scope: migrated the standalone helmet hotspot prefab, materials, runtime scripts, editor integration and EditMode tests; integrated the prefab beneath `HelmetExhibit` in the current `MuseumStart.unity` without replacing the scene.
- Behavior: three selectable helmet points, hover and visited colours, information/progress feedback, completion feedback, XR Select/Activate support, trigger-query compatibility and mouse fallback.
- Verification: Unity `2022.3.62f3c1`, Windows Editor, `Museum.Hotspots.Tests.HelmetHotspotTourTests`; red run 9/10 before scene integration, green run 10/10 after integration. The green run compiled without C# errors or recorded null/missing-reference exceptions.
- Limits: no physical headset, executable build, accessibility study or manual visual review was performed. The work remains uncommitted and unpushed for member review.
