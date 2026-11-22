# Low-view device occlusion refinement — 2026-11-22

Base: `main@e4a43db`
Branch: `low-view-occlusion`

## Scope

- `MuseumPhysicalHandsRig` checks eye height, hand-to-camera distance, viewport position, and interaction state. It suppresses an idle glove only when it is close to the centre of a low viewpoint.
- Grip, trigger, an active selection, a standing viewpoint, a peripheral hand, or a distant hand keeps the glove visible. Separate enter and exit thresholds reduce flicker.
- `MuseumHandVisual` changes only `Renderer.forceRenderingOff`. The tracked hand object, colliders, rays, and direct interaction remain active. Restoration preserves each renderer's authored force-off state.
- No scene, prefab, Input Actions, package, project-setting, material, or LFS asset was changed.

## Initial validation at `b5f362c3`

- Unity 2022.3 Roslyn compilation passed for the current runtime assembly and the edited validation script.
- `MuseumPhysicalHandsValidation` now checks renderer restoration, policy boundaries, a synthetic low HMD pose, retained colliders/direct interactors, grip-based restoration, and return to the original view.
- The automated Play Mode run could not be completed in this restricted workspace because Unity Hub could not write its local settings. Real-headset comfort, stereo acceptance, device performance, and a player build remain outside this validation.

## PR #95 review fixes

The low-view check now uses the bounds of the rendered palm and finger segments. Height and palm-to-camera distance still use separate entry and exit thresholds. When a nearby idle hand qualifies, the check tests its parts against the central viewing region, using each eye's projection in stereo. It changes renderer flags only when visibility changes and restores the flags recorded on entry.

Grip and trigger input at or above 0.10, a pressed digital button, or a selection held by that hand's Direct, Poke or Ray interactor keeps its glove visible. The 0.10 input dead zone is a prototype setting. A nested teleport ray can hold a selection while the primary controller's grip and trigger are released.

The saved `MuseumStart` regression uses a 1.20 m eye height and the original controller bindings. It checks a fingertip at screen centre with the palm below the entry rectangle, a fully peripheral hand, a distant centred hand, a hand behind the near clip plane, and both directions through the height thresholds. It also checks partial trigger and grip input at 0.35, resting input at 0.01, and a right-stick selection of the floor while the idle left glove stays hidden. The new fingertip assertion failed against the original PR runtime at `30914012`, with only the regression added.

The fixed station remains active during the low-view checks. The validation then switches the running scene to its supported free-grab mode for the existing two-hand, window and Home-reset checks. The simulated hand aims at the exposed window edge and approaches the reset shell with an open palm. The runner restores the fixed station and checks its active interactables before leaving Play Mode.

## Re-review evidence

On Windows 11 with Unity 2022.3.62f3c1, Win64 and D3D11, three batch runs tested `9a8965717c5276be51ddd171161f242a1846c33d`.

| Editor batch entry point | Passed checks |
| --- | ---: |
| `Museum.PhysicalHands.EditorTools.MuseumPhysicalHandsValidation.RunBatch` | 216/216 |
| `Museum.Diegetic.Editor.HelmetDiegeticDirectValidation.RunBatch` | 43/43 |
| `Museum.PhysicalHands.EditorTools.MuseumHandSelfCollisionValidation.RunBatch` | 19/19 |

The palm projected to (0.47, 0.03) and the fingertip to (0.50, 0.50). Only the right glove reappeared when the thumbstick selected `Floor - Teleport Area`. At 1.40 m eye height the gloves retained their previous visibility when approached from above and below. All three Editor processes exited with code 0. Each run verified 43 expanded project LFS files and compared SHA-256 for 1,464 tracked project files before startup and after exit. The files matched. These runs cover desktop simulation and isolated collision checks. Real-headset stereo, comfort and performance, and a new Windows Player build remain untested. [Raw reports and fingerprints](../../artifacts/helmet-interaction/qa/runs/20261122-pr95-low-view/)
