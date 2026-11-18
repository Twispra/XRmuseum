# XRmuseum Prototype Design and Technical Note

COMP5424 Extended Reality | Group 2 | 18 October 2026 | Final build update

Prototype and ZIP source 6088c736 | Editor suites at 024d039

## 1 Scope and requirements

XRmuseum is a Windows PC VR maritime gallery centred on diving helmets. The core question is whether visitors can inspect an unfamiliar object and explain its connections to surface air and communication equipment. Phase 2 concentrates on a testable exploration journey. Seated access and operation with one controller are team design hypotheses: the client has not confirmed a priority audience or minimum accessibility specification. [1,2]

### Visitor journey

Enter and choose a language; approach the central exhibit; rotate it, inspect a window or cutaway, and select component explanations; optionally compare eras and AIR/VOICE demonstrations; restore the exhibit or viewpoint and leave. The left-plinth photo study is optional. Exploration has no compulsory order, time limit or knowledge score. A visited mark records exposure to content, not understanding.

| Requirement or need | Implemented response | Boundary |
| --- | --- | --- |
| Museum context and credible content | Maritime gallery; central early, Mark V and SuperLite studies; separate 1988 photo study. Interpretive mechanisms are qualified. [3,5,6] | Model estimates remain; right-plinth expansion is deferred. |
| Principal XR exploration | Fixed-centre rotation, inspection window, fixed cutaway slider, three component hotspots and optional equipment demonstrations. [4] | Manipulation is implemented; learning benefit is unmeasured. |
| Reduced motor demand | Single-controller exhibit manipulation, ray/direct selection, precision rotation and adjustable viewing height. [4] | Complete single-controller navigation and seated reach are unverified. |
| Information access | Simplified Chinese, Traditional Chinese and English; hover outlines and visited checks; captions, sound toggle and reduced motion. [4,12] | Translation quality, binocular reading and sensory coverage need evaluation. |
| Guidance and recovery | Contextual hints, press/release feedback, move-away cancellation, exhibit RESET and separate RESET VIEW. [4] | Legacy NEW VISIT is hidden in the default interface. |
| Evaluable delivery | Public repository, six member identities, source, Windows ZIP and version-bound reports. [3,7,9,11] | Build/package checks passed; PQ604 and headset acceptance remain pending. |

Course expectations concern a coherent journey, principal XR behaviour, recovery and delivery. Motor and information provisions are this team's selected responses, not a client-approved checklist or evidence of universal accessibility. [1,2]

## 2 Design and technical decisions

MuseumStart is the enabled build scene. Unity 2022.3.62f3c1, OpenXR 1.14.3, XRI 2.6.4, Input System 1.14.0 and URP 14.0.12 provide the PC VR foundation. The Editor simulator gate retains physical headset input when a real XR device is present. Runtime features live under Assets/Museum/Features; input adapters, interaction coordination, exhibit state and presentation have separate responsibilities. [3]

### Constrain manipulation and preserve state

HelmetDiegeticStation reuses the inspection, era and demonstration engines while replacing default free grabbing with rotation around a fixed centre. A fixed slider changes cut depth. This reduces translation and coordination demands at the cost of free handling; its accessibility benefit is a hypothesis. Earlier two-hand grabbing and FLOAT remain a development configuration when the station is disabled. They are not simultaneously available in the default visitor mode. [4]

Gesture controls stop on release. Diegetic and left-plinth buttons confirm on release at the original target; moving away cancels. Cross-target release guards and cancellation before view recovery guard against accidental commands. After the left exhibit was added, its buttons gained solid-occlusion checks during selection, holding and release: a cone-assisted target alone did not prove an unobstructed interaction. [6]

### Coordinate models and information

Era profiles and an event channel drive distinct early, Mark V and SuperLite geometries. RefinedHelmetBinding updates visible renderers, window triggers, section data and demonstration endpoints; HelmetEraVisitorContent aligns captions and hotspots with the model actually applied. Inspection Shell and Glass shaders preserve cutaway and hover effects. This avoids a colour-only change or a caption announcing geometry that has not appeared. [3,5]

The language channel is the single source of language state. Static labels bind to keys; dynamic text uses MuseumText.Set. Saved translation tables and baked TMP glyphs are checked alongside runtime text. Contextual hints and a summonable settings/about panel reduce permanent clutter. The panel faces the viewer once, then stays fixed; it checks visible bounds and obstacles, including the left exhibit. Placement may be refused when no suitable space exists. [4,12]

### Keep interpretation distinguishable

The early model references Science Museum Group object 1881-47; the 1988 photo study is a separate object. Unseen geometry, some dimensions and material choices are estimates. The early moving panel and SuperLite side-opening window are teaching aids, while air routes, signal timing and synthetic sounds include interpretation. The left gallery offers only 30-degree rotation, reset and qualified labels; section preview belongs to an isolated comparison scene. Editable sources, exports and Unity copies retain their provenance and GUIDs. [5,6,11]

## 3 Prototype checks and Windows build

During integration, we used the saved MuseumStart scene to check that the features still worked together. We concentrated on actions a visitor would take: changing an era, inspecting a component, recalling the settings panel and recovering the exhibit or viewpoint. The linked reports retain the detailed checks and their versions. [6,7,8]

| Area | What we checked and changed |
| --- | --- |
| Central exhibit | Era changes kept the visible helmet, captions and hotspots aligned. Editor interaction checks also covered fixed-centre rotation, the window, section controls and reset. Direct-contact coverage was narrower and focused on the default helmet. [7,8] |
| Left display and settings | We checked button selection, cancellation and panel placement. Integration repairs blocked selection through solid objects and kept the panel clear of the added model. [6,7] |
| Language | Saved translations and glyphs were checked against runtime text as the language changed. This checks whether text is present; translation quality and headset readability remain evaluation tasks. [7,12] |

These checks helped us find integration faults before packaging. They used simulated inputs and project data, so they do not show whether visitors can find the controls unaided or understand the content.

### The submitted Windows build

We built revision 6088c736 for Windows x64 using the agreed Unity version. The build completed without errors, with two existing optional OpenXR migration warnings. We included the runtime dependencies and startup instructions, then extracted the ZIP and verified that the packaged files matched the build output. [9]

The extracted application stayed responsive during a 40-second desktop startup check and reached scene initialization. No headset was available, so OpenXR did not establish a headset session. We actively stopped the test process afterward; normal shutdown and VR operation were not verified. A full walkthrough on PQ604A/B/C with a headset and controllers is still required. [9]

Earlier Quest 3 development feedback helped us identify viewing-height, hand-control and joystick problems. Those findings concern earlier builds; the current combined prototype still needs a complete headset retest for navigation, reach and comfort. [10]

## 4 Limitations and evaluation priorities

The main unresolved risk is whether visitors can complete the whole journey independently and comfortably. Left-controller smooth movement and right-controller alternatives are not equivalent evidence of intuitive single-controller navigation. Existing welcome text promises seated/single-controller completion; that wording exceeds the present validation and should be qualified before release. Full narration, professional translation review, calibrated ergonomics and target-device performance are not established. [1,3,4]

RESET restores exhibit inspection state; RESET VIEW restores starting horizontal position and direction while retaining tracked eye height. Viewing-height adjustment is a preference, not floor calibration. The legacy NEW VISIT progress-clearing action is hidden in the default station, so staff handover between visitors still needs a verified procedure. For evaluation, restart sessions between participants or expose and verify an explicit operator reset. [4]

| Priority | Representative task | Evidence to collect |
| --- | --- | --- |
| Before submission | Use the packaged 6088c736 build to complete the core journey on PQ604 with the approved runtime. | Record headset/controller tracking, complete-flow outcome, hardware/runtime, errors and target-device performance. |
| Independent access | Enter, find controls, inspect and recover using a seated setup and one controller. | Completion, prompts, reach failures, unwanted movement, cancellation/recovery errors and discomfort. |
| Readable and credible learning | Read a component label, compare eras, use AIR/VOICE, then explain the physical relationship and reconstruction limits. | Observed reading/selection problems; explanation accuracy; confusion between historical fact and virtual teaching mechanisms. |
| Museum operation | Hide/recall the panel, recover a poor viewpoint, reset the exhibit and prepare for the next visitor. | Operator steps, residual selections/progress, failures to restore state and repeatability between sessions. |

Agree the intended audience and success criteria before evaluation. Phase 3 should follow the course's external-participant, consent and anonymisation requirements, using observation and brief interviews alongside task outcomes. Compare the default interaction with an agreed alternative where feasible, analyse recurring problems, revise the prototype and repeat affected tasks. The existing visitor flags and assertion totals are unsuitable as learning or accessibility success measures. [2]

Historical accuracy remains bounded by references and estimated geometry; the full two-plinth scheme is still under review. Repository history separates original contributions from repairs and integration. Codex assisted implementation, model scripts, validation and this report; sources, known licences and assistance are recorded in the resource ledger. These records support inspection of the work, while user benefit remains an evaluation question. [6,11]

## References

[1] COMP5424 Accessible Museum Experience scenario, 202627A, pp. 2-4. Course material supplied to the team.

[2] COMP5424 Group Project Description, 202627A, pp. 6-8 and 11-12; Phase 2 Assessment Rubric, p. 2. Course materials.

[3] XRmuseum source snapshot 6088c736: enabled scene, platform packages and feature modules. [Source revision](https://github.com/Twispra/XRmuseum/commit/6088c736e89d05101e09645c1796556012a6fd5d); [Feature modules](https://github.com/Twispra/XRmuseum/tree/6088c736e89d05101e09645c1796556012a6fd5d/MuseumVR/Assets/Museum/Features)

[4] Default controls, recovery and panel behaviour, snapshot 6088c736. [Diegetic interaction guide](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/diegetic-ui.zh-CN.md); [Panel guide](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/operator-panel.zh-CN.md)

[5] Three-era geometry and historical interpretation boundaries, snapshot 6088c736. [Era and model guide](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/helmet-eras.zh-CN.md)

[6] PR 67 review and integration, frozen source 024d039; saved-scene scope and retained negative evidence. [Integration and validation record](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-18-pr67-review-and-merge.md)

[7] Final audit of five Editor suites, source 024d039, 18 October 2026. [Audit and report directory](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/artifacts/helmet-interaction/qa/runs/20261018-pr67-review/final-audit.json)

[8] Earlier combined Editor evidence, 7e5b539; historical Direct and isolated comparison coverage. [Combined validation record](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-18-pr65-66-69-fix-and-validation.md)

[9] Windows x64 build, complete ZIP and limited desktop startup observation, source 6088c736, 18 October 2026. Package SHA-256: 3914fb87cb3e1974…096b1cdbdfd4f102 (full value in manifest). [Build and observation record](https://github.com/Twispra/XRmuseum/blob/d05def503ad286815575ed7038c1f50ddc21dd9c/docs/activity/2026-10-18-phase2-windows-submission.md); [Package verification](https://github.com/Twispra/XRmuseum/blob/d05def503ad286815575ed7038c1f50ddc21dd9c/artifacts/phase2-submission/20261018-windows-build/package-verification.json)

[10] Formative device feedback, 14 October 2026; accepted viewing repair and outstanding hand/navigation retest. [View feedback](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-14-pr49-merge.md); [Physical-hand feedback](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-14-physical-hands-device-feedback.md)

[11] External resources, reconstruction assumptions and AI assistance, snapshot 6088c736. [Resource ledger](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/external-resources.md)

[12] Trilingual authoring, dynamic text and saved-font contract, snapshot 6088c736. [Localisation guide](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/localization-authoring.zh-CN.md)
