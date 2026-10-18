# XRmuseum Prototype Design and Technical Note

COMP5424 Extended Reality | Group 2 | 18 October 2026

Repository 6088c736 | Validated Unity source 024d039

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
| Evaluable delivery | Public repository, member identities, modular source, retained model sources and version-bound reports. [3,7,11] | Current-revision build and PQ604 acceptance remain outstanding. |

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

## 3 Implementation and verification

This report describes repository revision 6088c736, including the merged left exhibit. Its committed MuseumVR tree is identical to frozen validation source 024d039 (tree eaa3237d). Later integration changed documentation and evidence only. The table presents archived results; no tests were rerun for this report. The local pre-existing ProjectSettings change was excluded from the report's source baseline. [3,6,7]

| Latest Editor suite at 024d039 | Passed | Recorded result |
| --- | --- | --- |
| Left-plinth ray/direct interaction and trilingual labels | 801 | 0 failed; natural exit 0 |
| Operator panel, including left-plinth placement | 271 | 0 failed; natural exit 0 |
| Localisation module integrity | 11 | 0 failed; natural exit 0 |
| Main-scene localisation integration | 128,389 | 0 failed; natural exit 0 |
| Central era and hotspot behaviour | 989 | 0 failed; natural exit 0 |

Each process exited without forced termination and left 1,299 tracked project files unchanged. Queued synthetic devices exercised production Input Actions and interaction events in saved scenes. Counts include repeated text, glyph and fingerprint assertions; they are not participant numbers or independent usability tasks and must not be added together. Monocular captures support layout inspection, not binocular readability or physical reach. [7]

Earlier evidence at 7e5b539 includes 31 central direct-contact checks for the default era and specified surfaces, plus 318 checks in the isolated comparison scene. These are historical runs. The comparison inputs are unchanged; that suite was retained without rerunning after the left-plinth integration. Coverage must therefore be read by version and task. [8]

### Build and real-device evidence

The latest recorded complete Windows64 Development build is from 806993a on 16 October: build success, zero errors and two existing optional OpenXR migration warnings. Its 272-file runtime directory was copied with matching SHA-256 hashes, but the Player was not launched. It predates the current interaction, panel, early-model and left-plinth integrations. A fresh build, launch check and complete walkthrough on PQ604A/B/C remain required before submission. The ZIP must include the executable and its runtime dependencies. [9]

Limited Quest 3 development feedback through Virtual Desktop/VDXR reported improved head rotation/viewing height, then difficult grabbing, hand-to-object gaps and joystick problems in an earlier hand build. Later repairs lack a complete current-headset retest. This is formative developer feedback, not participant research. Virtual Desktop is not evidence of approval or compatibility in the course's SteamVR/Meta Quest Link lab environment. [2,10]

## 4 Limitations and evaluation priorities

The main unresolved risk is whether visitors can complete the whole journey independently and comfortably. Left-controller smooth movement and right-controller alternatives are not equivalent evidence of intuitive single-controller navigation. Existing welcome text promises seated/single-controller completion; that wording exceeds the present validation and should be qualified before release. Full narration, professional translation review, calibrated ergonomics and target-device performance are not established. [1,3,4]

RESET restores exhibit inspection state; RESET VIEW restores starting horizontal position and direction while retaining tracked eye height. Viewing-height adjustment is a preference, not floor calibration. The legacy NEW VISIT progress-clearing action is hidden in the default station, so staff handover between visitors still needs a verified procedure. For evaluation, restart sessions between participants or expose and verify an explicit operator reset. [4]

| Priority | Representative task | Evidence to collect |
| --- | --- | --- |
| Before submission | Build the chosen final revision; launch and complete the core journey on PQ604 with the approved runtime. | Archive commit, ZIP/hash, dependencies, hardware/runtime, errors and complete-flow outcome. |
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

[9] Windows build and runtime manifest, 806993a, 16 October 2026. [Build record](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-16-visitor-flow.md); [272-file manifest](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/artifacts/helmet-interaction/qa/runs/20261016-visitor-flow/build-output-manifest.json)

[10] Formative device feedback, 14 October 2026; accepted viewing repair and outstanding hand/navigation retest. [View feedback](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-14-pr49-merge.md); [Physical-hand feedback](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/activity/2026-10-14-physical-hands-device-feedback.md)

[11] External resources, reconstruction assumptions and AI assistance, snapshot 6088c736. [Resource ledger](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/external-resources.md)

[12] Trilingual authoring, dynamic text and saved-font contract, snapshot 6088c736. [Localisation guide](https://github.com/Twispra/XRmuseum/blob/6088c736e89d05101e09645c1796556012a6fd5d/docs/localization-authoring.zh-CN.md)
