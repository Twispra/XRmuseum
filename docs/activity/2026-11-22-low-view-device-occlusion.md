# Low-view device occlusion refinement — 2026-11-22

Base: `main@e4a43db`
Branch: `low-view-occlusion`

## Scope

- `MuseumPhysicalHandsRig` checks eye height, hand-to-camera distance, viewport position, and interaction state. It suppresses an idle glove only when it is close to the centre of a low viewpoint.
- Grip, trigger, an active selection, a standing viewpoint, a peripheral hand, or a distant hand keeps the glove visible. Separate enter and exit thresholds reduce flicker.
- `MuseumHandVisual` changes only `Renderer.forceRenderingOff`. The tracked hand object, colliders, rays, and direct interaction remain active. Restoration preserves each renderer's authored force-off state.
- No scene, prefab, Input Actions, package, project-setting, material, or LFS asset was changed.

## Validation

- Unity 2022.3 Roslyn compilation passed for the current runtime assembly and the edited validation script.
- `MuseumPhysicalHandsValidation` now checks renderer restoration, policy boundaries, a synthetic low HMD pose, retained colliders/direct interactors, grip-based restoration, and return to the original view.
- The automated Play Mode run could not be completed in this restricted workspace because Unity Hub could not write its local settings. Real-headset comfort, stereo acceptance, device performance, and a player build remain outside this validation.
