# Presentation license map

This register forms part of [ASSET-LICENSES.md](../ASSET-LICENSES.md). Copyright
(c) 2026 Dennis Nilsson for his original material. Studio credit: Core Voltage Studios.
It identifies authored presentation expressed in code without reserving whole files.

## Mixed software/presentation files

C# paths below are relative to `viewer/DarkColony.Viewer/`. Reserved scope means
the identified authored graphic/compositional/choreographic expression, to the
extent protected and owned by Dennis Nilsson. It receives the source-fork,
private-build and private-run permission. Software implementation remains MIT;
merely appearing in a drawing method does not reserve an algorithm.

| File / identifying symbol | Reserved presentation expression | MIT machinery remains reusable |
| --- | --- | --- |
| `StudioIntro/CoreVoltageIntro.cs` — `CoreVoltageIntro` | Logo geometry; ring/arc/color composition, named scene timing, strike/lock choreography, title reveal and handoff | Lifecycle, time evaluation, interpolation, scaling, drawing and audio integration |
| `Landfall.cs` — `OdcUiTokens`, `LandfallButton`, `LandfallHeading`, `DrawLandfallCampaign`, `DrawLandfallMissions` | Landfall palette as a composition, typography hierarchy, focus appearance, page/card/button arrangement | Hit handling, navigation stack, dispatch, scrolling, availability and state logic |
| `MainMenu.cs` — `DrawLandfallMain`; `SurfaceScreens.cs` — `DrawSurfaceLogo` and page drawing | Main-menu/wordmark placement, title/footer hierarchy, save/load and submenu compositions | Mission/profile selection, persistence, Continue routing, save-slot management |
| `SurfacePresentation.cs` — `DrawSurfaceBackdrop`; `SurfaceOptions.cs`, `LandfallOptions.cs` — options drawing | Backdrop/scrim treatment and options-page composition | Resource handling, bindings, accessibility, settings and viewport adaptation |
| `FirstStrikeInterface.cs` — `DrawLandfallMissionPanel`, `DrawLandfallFrontend`, authored palette/font selection | Landfall briefing/results/dialog presentation and original UI visual choices | Event/order routing, modal state, command validation, reusable text/image/drawing primitives |
| `UiLayout.cs` — `MissionGrid`, `DrawBriefing` | Specific portrait/globe/text/footer arrangement and authored spacing | Ellipsis/cache, line layout, scrolling, bounds and text rendering algorithms |
| `BattlefieldSidebar.cs` — `DrawBattlefieldSidebar` | Relay composition, dial treatment, group/command/status arrangement | Selection queries, availability, minimap and gameplay state binding |
| `RelayPresentation.cs` — `DrawRelayGroup`, `DrawRelayProduction` | Compact production/group arrangement, typography, frame/indicator composition | `PrepareRelayProducts`, state cache, economy/prerequisite/queue calculations and pagination |
| `RelayControls.cs` — `UiMetal`, tooltip styling | Metallic gradient/frame treatment and tooltip appearance | `RelayInputControl`, dispatch, focus, retained controls/pooling, positioning and lifecycle |
| `WarSetup.cs` — `WarTheme`, `WarStyle`, setup/lobby layout; `WarMultiplayer.cs` — host/join layout | Landfall-derived theme and screen composition | Theme/control helpers, validation, search/filter, network permissions and responsive layout algorithms |
| `ContentSetup.cs` — panel composition, `LayoutContentSetup` | Content-management page arrangement and visual hierarchy | Entire importer pipeline, validation, transactions, progress contract, tasks, cancellation and recovery |
| `Restoration.cs` — charge drawing and authored wake/cue sequence; `ColonyScene.cs` — `_Draw`, scene-placement choices in `Build` | Electrical handoff, awakening sequence, scene composition, movement paths and cue placement | Readiness gating, lifecycle, reduced-motion/skip behavior, lookup/cleanup, generic raster/sprite animation and compositing |
| `ColonyAmbience.cs` — sound-selection/spacing choices | Authored ambience arrangement only | Async loading, scheduling and cleanup; imported sounds are proprietary, not project-owned |

Original mission wording, fonts, sprite images, original-game command artwork,
globe frames and original sampled sounds referenced here are not project-owned
presentation. Their original/third-party terms apply. Functional labels, instructions,
numeric rules and game behavior are not reserved as visual design. Other original
engine/importer and reusable UI code remains MIT unless carrying a third-party
license. This register makes no blanket claim over all output from a file.

## Generators and separate assets

| Repository path | Treatment |
| --- | --- |
| `tools/prepare_core_voltage_intro_audio.py` | Generic synthesis, mixing, envelope and WAV-writing code is MIT. The signature-sound score, authored tonal/envelope composition and lock/strike timing are reserved presentation. The resulting studio WAV has the same source/private-use permission. |
| `tools/prepare_restoration_audio.py` | Software is MIT; authored cue definitions/outputs for the five approved Landfall cues are CC BY 4.0, not reserved studio identity. |
| `viewer/DarkColony.Viewer/Branding/CoreVoltage/core-voltage-intro.wav` | Reserved studio signature sound |
| `viewer/DarkColony.Viewer/Branding/Icon/odc-icon.ico` | Reserved OpenDC icon |
| `viewer/DarkColony.Viewer/Branding/Icon/png/odc-icon-512.png` | Reserved OpenDC icon |
| `viewer/DarkColony.Viewer/Branding/Landfall/opendc-wordmark.svg` | Reserved OpenDC wordmark |
| Five `cue-*.wav` files individually listed in `ASSET-LICENSES.md` | CC BY 4.0; no other files included by this notation |

The studio intro's procedural origin and the cues' no-sample generators are
documented in the dependency record. The icon and wordmark are supplied original
project designs; original-game art is imported separately. Public source forks may
retain the listed source presentation under the express permission. A public build
retaining reserved material needs separate permission; a fork replacing it may
redistribute the MIT engine subject to remaining licenses. No application behavior
or asset bytes were changed to create this register.
