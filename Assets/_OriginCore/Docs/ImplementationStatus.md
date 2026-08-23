# OriginCore Foundation Status

- Editor: Unity 2022.3.62f3 (96770f904ca7)
- Render Pipeline: Universal Render Pipeline 14.0.12
- Branch: `dev`
- Baseline Commit: `ba35f329e2d5005efdd76d660526f541e6a03174`
- Last Updated: 2026-08-18（P00～P17 表仍保留 2026-07-15 历史证据）

> Maintenance policy (2026-07-15): P01～P17 rows retain historical completion evidence. The former `Tools/OriginCore` Apply/Validate/Build automation and `OriginCore.Editor` assembly have been removed. New requirements are implemented directly in Runtime/scenes/assets and synchronized to documentation; the default gate is compilation plus a clean Unity Console unless the user explicitly requests broader testing.

> Current extension status (2026-08-18): this file keeps the P00～P17 evidence unchanged. Post-foundation work now includes ContentCatalog revision 7, MatchSetup/MatchSession, Y and Z content, the three formal Y weapon ActionSets plus generic weapon/throwable runtime, construction/technology/promotion/gathering, Save schema v3, height-aware volumetric visibility, independent minimaps, enemy macro AI, and `10_LegacyMap`. See [TechnicalDesign.md](TechnicalDesign.md) for the authoritative as-built design and unresolved content/licensing inputs.

> Update design work (2026-08-18): the Runtime foundation for Y weapon actions, height-aware visibility,
> faction-isolated enemy production, enemy macro AI and Save schema v3 is present. SystemTest0 and LegacyMap
> now reference separate elevation revisions and explicit enemy-AI map setups; weighted production, Registry
> unit discovery and AI production-cursor persistence are wired. Three serialized Y weapon ActionSet assets now
> contain 17/16/6 actions and are explicitly referenced; the runtime construction fallback has been removed.
> `FinalWingTargetingController` separates auxiliary auto-targeting from main-hand lock intents; Final Wing passive
> queues one 18-feather sequence for every valid main-weapon hit. Hit definitions now support target/action-start
> anchors, repeat-until-phase-end and source/main/locked-target damage components. Timeslot ground combo 3 uses
> `CrossThroughAndReturn`; Sequence orbit keeps its action-start anchor; the two Final Wing finishers statically
> resolve to 4500/4000 total damage under the current prototype values. Leaving ACT, death, load and target loss use
> unified action cleanup. Enemy production continues in BuildArmy/Assault/Regroup, ignores dead sites and reissues
> one Assault command after restore.
> A follow-up audit corrected Final Wing volley semantics to “one six-hit volley on tap, repeat complete volleys while
> held”; volley/launch require an explicit or visible auto-target, and target skills are restricted to the frozen intent
> target. Timeslot finisher now applies its pre-sequence
> TemporalLock before one 4500 final hit; Sequence finisher performs ten zero-damage pull ticks and one 4000 final
> hit so Armor/energy/hit reactions are not multiplied. `EnemyHeroBrain` now independently acquires visible hostiles
> and issues its own AttackCommand while remaining outside the macro assault roster.
> `FinalWingPresentationController` is wired onto `PF_HeroY` and creates exactly one collisionless six-feather
> prototype set for the equipped Y final form; hit pulses are presentation-only and never author damage. Formal
> models, detached/return trajectories and projectile pooling remain content work.
> Unity import, Catalog/Prefab/scene static validation and Console cleanliness are complete; per-action feel,
> enemy-AI combat, high-ground visuals, real save replay and final art/balance remain manual/content work. Use
> [DesignUpdateTechnicalPlan.md](DesignUpdateTechnicalPlan.md) for the current implementation design and do not
> rewrite the historical P00～P17 evidence below.

| Phase | Status | Main Files | Tests / Evidence | Notes |
|---|---|---|---|---|
| P00 | Done | `Docs/ImplementationStatus.md` | Unity full asset refresh and script compilation; baseline filesystem/Git audit | Existing project; no Gameplay asset changed by P00 |
| P01 | Done | `OriginCore.Runtime.asmdef`, `Packages/manifest.json`, ProjectSettings, foundation scenes | Unity Console `[OriginCore P01] APPLY PASS`; post-pass log and filesystem verification | Foundation applied and validated in Unity; generator retired |
| P02 | Done | `Core/*`, `SceneFlow/*`, `Debug/DebugOverlayService.cs` | Console `[OriginCore P02] APPLY PASS`; all three manual scene-entry checks passed; EditMode and corrected PlayMode runs completed with zero errors or warnings | Persistent root, catalog, prefab, and scene flow validated; generator retired |
| P03 | Done | `Art/Materials/MAT_*.mat`, placeholder prefabs, `90_SystemTest.unity` | Console `[OriginCore P03] APPLY PASS` and `VALIDATION PASS`; hierarchy/rendered-frame checks and user Play Mode/Test Runner validation passed | Primitive-only reusable SystemTest graybox and baked NavMesh complete; generator retired |
| P04 | Done | `Input/IA_OriginCore.inputactions`, generated `OriginCoreInputActions.cs`, `Runtime/Input/*` | Console `[OriginCore P04] APPLY PASS` and `VALIDATION PASS`; compile, asset contract, prefab, scene UI-module inspection, user Play Mode/Input Debugger, and Test Runner validation passed | Single input foundation and binding persistence complete; generator retired |
| P05 | Done | `Runtime/Gameplay/*`, `Runtime/Combat/*`, `Data/Definitions/SO_Unit_*.asset` | Console `[OriginCore P05] APPLY PASS` and `VALIDATION PASS`; clean Runtime/Editor/test compilation; user Test Runner and Play Mode validation passed | Entity, faction, unified vitals, damage, selection indicators and registry complete; generator retired |
| P06 | Done | `Runtime/Core/GameMode*.cs`, `Runtime/Gameplay/PossessionService.cs`, `HybridControlDriver.cs`, `Runtime/Cameras/*`, `Runtime/UI/ModeHudPresenter.cs` | Console `[OriginCore P06] APPLY PASS` and `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user Test Runner and manual Play Mode validation passed | Unified mode authority, camera/HUD switching and auto-to-manual possession foundation complete; generator retired |
| P07 | Done | `Runtime/RTS/*`, `Runtime/UI/RTS/*` | Console `[OriginCore P07] APPLY PASS` and `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user Test Runner and manual Play Mode validation passed | Bounded RTS camera, click/box/shortcut selection and event-driven HUD complete; generator retired |
| P08 | Done | `Runtime/RTS/Commands/*`, `Runtime/Units/NavMeshMovementDriver.cs`, `Runtime/UI/RTS/RtsCommandPanelView.cs`, `Runtime/UI/RTS/CommandQueuePathView.cs` | Idempotent Console `[OriginCore P08] APPLY PASS`; P06/P07/P08 `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user-confirmed Test Runner and manual Play Mode acceptance | Extensible per-unit command queue, Move/Stop targeting, NavMesh movement, event-driven command HUD, left-click target confirmation and white dashed queue route complete; generator retired |
| P09 | Done | `Runtime/Combat/AttackCapability.cs`, `AutoTargetScanner.cs`, `SimpleEnemyBrain.cs`, `CombatDeathHandler.cs`, `HitFlash.cs`, `Runtime/RTS/Commands/Attack*.cs`, `Runtime/UI/World/WorldHealthBar.cs` | Console `[OriginCore P09] APPLY PASS`; P05-P09 `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user-confirmed Test Runner and manual Play Mode acceptance | Generic hostile attack, Attack Move, low-frequency fixed-buffer targeting, enemy brain, health bars, hit flash, and deactivate-on-death complete; generator retired |
| P10 | Done | `Runtime/Economy/*`, `Runtime/Buildings/*`, `Runtime/Units/UnitSpawner.cs`, `PopulationOwner.cs`, `Runtime/UI/RTS/ResourceHudView.cs`, `RtsRosterView.cs`, `ProductionPanelView.cs` | Idempotent Console `[OriginCore P10] APPLY PASS`; P05-P10 `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user-confirmed EditMode Run All, PlayMode rerun, and final acceptance | Atomic three-resource wallet, Influence used/cap, Worker/Combat production, one-shot population release, contextual RMB/direct and left-release targeted rally, and event-driven HUD complete; generator retired |
| P11 | Done | `Runtime/ACT/*`, `Runtime/Gameplay/HybridPawnMotor.cs`, `Data/Configs/SO_ActMovementConfig.asset`, `Tests/PlayMode/ActMovementPlayModeTests.cs` | Idempotent Console `[OriginCore P11] APPLY PASS`; P05-P11 `VALIDATION PASS`; clean Runtime/Editor/PlayMode compilation; user-confirmed targeted PlayMode tests and manual Play Mode acceptance | Camera-relative third-person run, acceleration, buffered/coyote jump, one air jump, obstruction-safe crouch, assisted jump, and independent mouse orbit complete; generator retired |
| P12 | Done | `Runtime/FPS/*`, `Runtime/UI/FPS/CrosshairView.cs`, `Data/Configs/SO_FpsMovementConfig.asset`, `Tests/PlayMode/FpsMovementPlayModeTests.cs` | Repeated Console `[OriginCore P12] APPLY PASS`; P05-P12 `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user-confirmed updated PlayMode tests and threshold-latched sprint manual acceptance | Shared-Motor FPS walk/threshold-latched sprint/crouch/slide/jump, independent look, ADS-only FOV, reversible first-person visibility, and settings-backed dot crosshair complete; generator retired |
| P13 | Done | `Runtime/UI/Common/*`, `Runtime/UI/MainMenu/*`, `Runtime/UI/Pause/*`, `Runtime/UI/Settings/*`, `Runtime/Settings/*`, `Runtime/Core/PauseService.cs`, P13 UI/audio/Volume assets | Repeated Console `[OriginCore P13] APPLY PASS`; P01-P13 `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user-confirmed Test Runner and manual acceptance | Routed menus, persistent settings, Esc-priority pause and mutually exclusive three-mode HUD complete; generator retired |
| P14 | Done | `Runtime/Save/*`, `Runtime/UI/Save/*`, `Runtime/Units/UnitSpawner.cs`, `Runtime/Gameplay/EntityIdentity.cs`, `PF_LoadGamePanel.prefab`, P14 EditMode/PlayMode tests | Repeated Console `[OriginCore P14] APPLY PASS`; P01-P14 `VALIDATION PASS`; clean Runtime/Editor/EditMode/PlayMode compilation; user confirmed the full P14 EditMode/PlayMode regression set and manual acceptance passed | Versioned atomic slots, centralized scene restore, rename/delete/load/save-and-exit UI, stable scene/runtime ids and visibility persistence seam implemented; generator retired |
| P15 | Done | `Runtime/Visibility/*`, `Runtime/UI/RTS/Minimap*`, `SO_SystemTestMinimapMap.asset`, P15 EditMode/PlayMode tests | Historical P15 acceptance plus current clean compilation/serialization audit; legacy camera-capture assets retired | Shared low-frequency visibility grid, enemy information suppression, building memory, independently drawn interactive minimap, and fog texture persistence bridge complete |
| P16 | Done | `Tests/PlayMode/IntegrationRegressionPlayModeTests.cs`, `Docs/TestMatrix.md` | Historical Console `[OriginCore P16] VALIDATION PASS` and `BUILD PASS`; clean compilation/build with 0 project Error/Warning; user-confirmed EditMode/PlayMode Run All, continuous manual matrix, Profiler and Player path; clean Player.log | Historical build-readiness audit and its dedicated EditMode test were retired with the tools; regression test, matrix and verified Windows build evidence remain |
| P17 | Done | `Docs/Architecture.md`, `Docs/ImplementationStatus.md`, `Docs/TestMatrix.md`, `Docs/RequirementsAddendum.md` | Architecture/source/license/TODO/temp-file audits plus final Unity refresh; Console `[OriginCore P16] VALIDATION PASS`, 0 Error / 0 Warning | Final architecture, extension guide, reproduction runbook and handoff audit complete |

## P00 Baseline Audit

### Project Baseline

- This is an existing Unity project, not an empty project.
- `ProjectVersion.txt` exactly matches Unity 2022.3.62f3.
- The project already uses URP. The implementation must preserve URP and must not migrate render pipelines.
- The active editor scene at audit time was `Assets/Scenes/TestScene.unity`.
- Build Settings currently contain only:
  1. `Assets/Scenes/StartMenu.unity`
  2. `Assets/Scenes/TestScene.unity`
- The required `00_Bootstrap`, `01_MainMenu`, and `90_SystemTest` scenes do not exist yet.
- `Assets/Scripts` contains 78 C# files. There are no project-owned `.asmdef` files.
- `Assets/_OriginCore` did not exist before P00.
- One input asset exists at `Assets/Input/GameplayInputActions.inputactions` with maps `Common`, `ModeSwitch`, and `Command`. It does not match the required `Global`, `RTS`, `ACT`, `FPS`, and `UI` map layout.

### Package Baseline

| Package | Version / Source | P00 Finding |
|---|---|---|
| Input System | 1.14.2 | Installed and locked |
| Cinemachine | Not installed | P01 dependency gap; install a Unity 2022.3-compatible 2.x version without upgrading unrelated packages |
| AI Navigation | 1.1.7 | Installed and locked |
| TextMeshPro | 3.0.9 | Installed and locked |
| Unity Test Framework | 1.1.33 | Installed and locked |
| uGUI | 1.0.0 | Installed |
| Universal RP | 14.0.12 | Existing render pipeline; preserve |
| MCP for Unity | Git `main`, package UI reported 10.0.0 | Existing user tooling; current lock-file update must be preserved and reviewed, not overwritten |

### Compilation and Console Baseline

- The current Unity session completed a synchronous full Asset Pipeline refresh in 50.750 seconds.
- Unity reported script compile time of 16.597 seconds followed by a domain reload.
- `Library/ScriptAssemblies/Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll` were generated during this session.
- Current-session `Editor.log` audit results:
  - C# compiler errors: 0
  - C# compiler warnings: 0
  - compilation-failed markers: 0
  - `NullReferenceException`, `MissingReferenceException`, or `TypeLoadException`: 0
- Before the editor restart, the Unity Console showed 0 Errors, 0 Warnings, and four informational MCP startup messages.
- No PlayMode/manual gameplay test was run in P00. Per user direction, later Unity tests will pause for user execution.

### Existing Systems and Collision Risks

The legacy project already defines systems whose names or responsibilities overlap the new foundation:

- `ControlModeManager`, `IControlMode`, `RtsMode`, `ActMode`, and `FpsMode`
- `CameraRig`
- `SelectionManager` and `Selectable`
- `CommandExecutor`, `IUnitCommand`, `MoveCommand`, `AttackCommand`, and `StopCommand`
- `TeamAffiliation` and `Health`
- `BuildingProduction`
- menu, pause, HUD, health-bar, and command-card controllers

P01+ must use the new `OriginCore` namespace and `Assets/_OriginCore` asmdefs to avoid type collisions. Legacy systems must be inspected before reuse; matching names do not prove matching behavior.

The existing project also contains formal-content systems explicitly excluded from the current foundation milestone, including weapons, projectiles, hero weapon abilities, Blink/AOE/Aura abilities, Dash, Flight, weapon prefabs, and related VFX/model assets. P00 does not delete or move them. New foundation scenes and services must not depend on them, and no additional formal character/weapon/map content may be created.

### Git and User-Change Baseline

Initial user-owned working-tree changes detected before P00:

- `.gitignore` — adds `DesignDocs/` to ignored generated/local documents.
- `Assets/Scripts/Modes/ACTMode.cs` — ongoing movement/slide refactor.
- `Assets/Scripts/Modes/FPSMode.cs` — ongoing movement/slide refactor.
- `Packages/packages-lock.json` — MCP for Unity dependency/hash update.
- `UserSettings/Layouts/default-2022.dwlt` — personal Unity layout.

After Unity's audit refresh, the tracked generated file `Logs/shadercompiler-UnityShaderCompiler.exe0.log` also changed. It is generated editor churn, not a Gameplay modification. All listed changes are preserved and must not be overwritten or reverted by later phases.

## Baseline Issues

- Required `_OriginCore` directory and four asmdefs are absent.
- Cinemachine is absent.
- Required scene names, Bootstrap scene, AppRoot, SceneContext, and build order are absent.
- Existing Input Actions do not implement the required mode-separated maps and bindings.
- Existing architecture is concentrated in the default `Assembly-CSharp` and overlaps many planned type responsibilities.
- Legacy character, weapon, projectile, skill, and non-graybox map content exists outside the new milestone boundary.
- The CJK font file `Assets/Resources/Fonts/NotoSansSC-VF.ttf` exists, but its license/provenance has not yet been verified.
- `DesignDocs/` is ignored and therefore the implementation guide is not currently versioned with the project.
- MCP for Unity is configured locally, but its tools were not exposed to this Codex task; P00 used Unity's current-session compile log instead of editor mutation.

## P01 Implementation

- Added the four required assembly definitions and an Editor asmref for the `Scripts/Editor` branch.
- Added Cinemachine 2.9.7 to `Packages/manifest.json`; this is the released 2.x package for Unity 2022.3.
- Kept existing package versions fixed and preserved the user's MCP lock-file changes.
- Confirmed the existing `.gitignore` already excludes Library, Temp, Logs, obj and UserSettings while retaining Unity metadata and project settings.
- Found remaining Legacy Input usage in `PauseMenuController`, `CommandCardController`, and `TooltipController`; P01 therefore migrates Active Input Handling from `Input System Package (New)` to `Both` until P04 removes that debt.
- Added the idempotent menu commands:
  - Historical P01 Apply: Foundation Setup (retired)
  - Historical P01 Validate: Foundation Setup (retired)
- The apply command creates the standard directory tree, reuses or creates the eight required Layers, creates three empty scenes through `EditorSceneManager`, replaces Build Settings with the required order, and sets 1920x1080/resizable/Both input settings.
- The command refuses to run in Play Mode or while any loaded scene is dirty, so it cannot silently discard user scene edits.

### P01 Acceptance Evidence

- Unity completed the idempotent apply command and logged `[OriginCore P01] APPLY PASS`; no compiler or fatal errors were logged after that result.
- Cinemachine 2.9.7 is present in both `Packages/manifest.json` and `Packages/packages-lock.json`; existing Input System 1.14.2, AI Navigation 1.1.7, TextMeshPro 3.0.9, Test Framework 1.1.33, and uGUI 1.0.0 versions remain locked.
- The required Layers are assigned as follows: `Unit` 6 (reused), `Ground` 9, `Selectable` 10, `Building` 11, `VisionTarget` 12, `Minimap` 13, `FogOverlay` 14, and `BoostSurface` 15.
- Build Settings contains exactly `00_Bootstrap`, `01_MainMenu`, and `90_SystemTest`, enabled in that order, and all three scene assets exist under `Assets/_OriginCore/Scenes`.
- Player settings are `TMC` / `OriginCore`, 1920x1080, resizable, with Active Input Handling set to `Both` (`activeInputHandler: 2`).
- All four assembly definitions exist, the standard `_OriginCore` directory tree passed validation, and the existing case-insensitive Unity `.gitignore` patterns cover Library, Temp, Logs, obj, and UserSettings.

## P02 Implementation

- Added an explicit persistent-service chain: `AppRoot` serializes `GameBootstrap` and `GameServices`; `GameServices` serializes `SceneFlowService` and `DebugOverlayService`. Runtime code performs no name-based or per-frame service lookup.
- `GameBootstrap` uses a reset-safe singleton guard, keeps only the first root with `DontDestroyOnLoad`, rejects duplicates with one diagnostic, and only the Bootstrap-scene prefab override auto-loads MainMenu.
- `SceneCatalog` maps `MainMenu` and `system_test_0` to full auditable asset paths while normalizing the runtime load path for Unity 2022.3.
- `SceneFlowService` provides synchronous and asynchronous single-scene loading, re-entry prevention, completion/failure events, Build Settings checks, and failure diagnostics. Catalog asset paths are resolved to Build Settings indices before loading so Unity Editor and player builds use the same unambiguous target.
- `SceneContext` registers and unregisters the scene camera, HUD root, mode camera targets, default Pawn, MinimapBounds, VisibilityBounds, and spawn points through the typed service registry; disabled or destroyed contexts cannot remain registered.
- `SceneBootProxy` creates `PF_AppRoot` only when no persistent bootstrap/root exists, so direct play of MainMenu or SystemTest cannot intentionally create a second root.
- Added a development-only debug-overlay data interface for mode, selection count, queued commands, and resource state without introducing an Update loop.
- Added the idempotent menu commands:
  - Historical P02 Apply: Bootstrap and Scene Flow (retired)
  - Historical P02 Validate: Bootstrap and Scene Flow (retired)
- The apply command creates `SO_SceneCatalog` and `PF_AppRoot`, configures Bootstrap/MainMenu/SystemTest through Unity APIs, restores the user's open scene setup, and validates serialized references, Build Settings, proxy/context counts, EventSystems, and AudioListeners.
- `00_Bootstrap` includes a dedicated clear-only `BootstrapCamera` and one `AudioListener`, preventing the Game view's `No cameras rendering` overlay while the loading canvas is visible.
- Added EditMode coverage for catalog mappings/path normalization and PlayMode coverage for Bootstrap startup, direct SystemTest play, round-trip context cleanup, unique AppRoot, EventSystem, and AudioListener counts.

### P02 Validation Evidence

- Manual validation passed for `00_Bootstrap` loading `01_MainMenu`, direct Play Mode from `01_MainMenu`, and direct Play Mode from `90_SystemTest`; Console remained free of errors and warnings.
- The EditMode run completed without errors. The first PlayMode run exposed three test-harness scene-load failures caused by extensionless paths; after the harness was updated to resolve full catalog paths to Build Settings indices, the PlayMode rerun completed with zero errors and warnings.

## P03 Implementation

- Added ten solid-color placeholder materials: friendly, hero, enemy, neutral, ground, obstacle, selected, hidden fog, explored fog, and ghost building.
- Adapted the guide's Standard/Unlit wording to the existing URP 14 project: body/environment materials use `Universal Render Pipeline/Lit`, while selection, fog, rally/debug, and ghost materials use `Universal Render Pipeline/Unlit`. This preserves the required visual roles without introducing incompatible pink materials or changing render pipelines.
- Added eight reusable primitive-only prefabs: Hero, friendly unit, enemy unit, worker, producer building, resource crystal, rally marker, and selection indicator.
- The Hero prefab contains the required `VisualRoot`, `HeadMarker`, `ForwardMarker`, `SelectionIndicator`, `ActCameraTarget`, `FpsCameraTarget`, `HealthBarAnchor`, and `GroundProbe` children. P03 intentionally limits these placeholders to Transform, primitive Renderer/MeshFilter, and Collider components.
- Rebuilt `90_SystemTest` as a reusable graybox testbed while preserving its P02 `SceneBootProxy` and `SceneContext`. The testbed contains a 50x50 primitive ground, four boundary walls, six navigation/vault/visibility obstacles, one Hero, three friendly units, two enemies, one worker, one producer, and one crystal.
- Added seven named spawn-point anchors, a placeholder Main Camera and directional light, `HUDRoot`, RTS/ACT/FPS camera targets, Minimap/Visibility bounds, selection and world-health-bar anchors, and a `NavMeshSurface` using the Ground layer.
- Baked and persisted the navigation data at `Scenes/90_SystemTest/NavMesh-Navigation.asset`; all seven spawn points pass `NavMesh.SamplePosition` validation.
- Added the idempotent menu commands:
  - Historical P03 Apply: Placeholder Testbed (retired)
  - Historical P03 Validate: Placeholder Testbed (retired)
- Added EditMode coverage for material shaders, prefab composition, required Hero anchors, missing scripts, scene context references, persistent NavMesh data, and walkable spawn points. Extended PlayMode coverage to verify the populated SystemTest scene, unique camera/listener, required actors, and walkable spawn points.

### P03 Validation Evidence

- Unity performed a full AssetDatabase import and compiled the new Editor and test assemblies with zero Console errors.
- The apply command logged `[OriginCore P03] APPLY PASS`; the separate validation command logged `[OriginCore P03] VALIDATION PASS`, with zero errors or warnings after both operations.
- MCP hierarchy inspection confirmed three clean scene roots, 11 environment primitives, nine actor instances, seven spawn points, and seven infrastructure objects. The Hero instance exposes all eight required child anchors and has no Missing Script.
- Camera inspection confirmed one active `MainCamera` with one `AudioListener`; a rendered-frame capture showed the complete graybox arena and correctly colored placeholders without missing/pink materials.
- User validation passed for `90_SystemTest` Play Mode and the requested EditMode/PlayMode Test Runner runs; P03 is complete.

## P04 Implementation

- Added `IA_OriginCore.inputactions` as the single main input asset for the new `_OriginCore` foundation. It contains exactly the five required Action Maps: `Global`, `RTS`, `ACT`, `FPS`, and `UI`.
- Implemented every Appendix A action and default keyboard/mouse binding, including F1/F2/F3 mode keys, Escape, RTS pointer/selection/command/queue shortcuts, ACT/FPS movement and reserved actions, and standard UI navigation/submit/cancel/pointer/click/scroll actions.
- Enabled Input System `Generate C# Class` and versioned the generated `OriginCore.Input.OriginCoreInputActions` wrapper beside the asset.
- Added an early-execution polling `InputRouter`. It always evaluates `Global` before the active Gameplay map, keeps `Global` and `UI` enabled, enables exactly one of RTS/ACT/FPS, publishes an immutable per-frame `InputSnapshot`, and can disable only Gameplay input while retaining UI/global input for future pause integration.
- Added `GameplayInputSuppressionFrame`: a successful mode-map change arms only the current frame, so no old-map command or new-map jump is dispatched during the switch frame.
- Added explicit direct-control intent evaluation. Mouse Look is excluded, while Move requires magnitude strictly greater than `0.1`; relevant button/hold states such as Jump and Aim count as meaningful input for later P06 possession logic.
- Added `InputRebindService` with interactive rebinding, Escape cancellation, per-binding reset, reset-all, and an explicit override API. It performs no conflict rejection, so duplicate effective bindings are intentionally allowed.
- Added `InputBindingPersistence` using `SaveBindingOverridesAsJson` / `LoadBindingOverridesFromJson`. Binding JSON is stored in the initial `SettingsData.BindingOverridesJson` field and persisted through a PlayerPrefs adapter until P13 expands the settings service.
- Added `InputRouter` and `InputRebindService` to `PF_AppRoot`, registered both through `GameServices`, and bound all three foundation-scene EventSystems to the same `IA_OriginCore/UI` actions through `InputSystemUIInputModule`.
- Added the idempotent menu commands:
  - Historical P04 Apply: Input Foundation (retired)
  - Historical P04 Validate: Input Foundation (retired)
- Added EditMode coverage for generated-map/binding contracts, binding JSON round-trip, duplicate bindings, reset defaults, map enable/disable state, switch-frame suppression, and meaningful-input thresholds. Extended PlayMode scene-flow coverage to require the persistent input services and one correctly bound UI module.

### P04 Validation Evidence

- Unity imported `IA_OriginCore`, generated the 132 KB C# wrapper through Input System 1.14.2, and compiled the Runtime, Editor, EditMode, and PlayMode assemblies with zero compiler errors.
- The apply command logged `[OriginCore P04] APPLY PASS`; the separate validation command logged `[OriginCore P04] VALIDATION PASS` with no project errors or warnings.
- MCP prefab inspection confirmed `PF_AppRoot` contains `InputRouter` and `InputRebindService` alongside the P02 services. Scene hierarchy inspection confirmed `90_SystemTest` now has one EventSystem with one `InputSystemUIInputModule`; the validator independently checked the same invariant and all six UI action references in Bootstrap, MainMenu, and SystemTest.
- The legacy `Assets/Input/GameplayInputActions.inputactions` and legacy controllers outside `_OriginCore` remain untouched to preserve existing user work and the phase boundary. New OriginCore scenes/services use only `IA_OriginCore`; Active Input Handling remains `Both` until the remaining legacy-input debt is retired in its owning integration work.
- Reserved ACT/FPS weapon, slot, grenade, wheel, and summon actions are bindings only. P04 does not implement any excluded weapon, skill, projectile, or worker-summoning Gameplay.
- User validation passed for Play Mode/Input Debugger behavior and the requested Test Runner run. The initial EditMode run exposed generated-wrapper cleanup misuse plus two real assertions (active-map setup in the EditMode harness and strict `0.1` movement-threshold precision); all were corrected, recompiled, and the user confirmed the rerun passed. P04 is complete.

## P05 Implementation

- Added the namespaced entity foundation: `EntityIdentity` keeps a stable archetype ID plus a per-runtime GUID, resolves display name/roles through `UnitDefinition`, and registers through the typed `GameServices.EntityRegistry` reference. It performs no tag or name based faction lookup.
- Added `FactionId` (`Friendly`, `Enemy`, `Neutral`), `FactionMember`, and the centralized `FactionRelationService`. Same-faction entities are allied, Neutral interactions remain neutral, and Friendly/Enemy hostility is a serialized service policy.
- Added flag-based `UnitRole` values for Worker, Combat, Hero, and Building, plus allocation-free role predicates and registry collection APIs that fill caller-owned lists.
- Added unified `VitalsComponent` state for Health, Shield, and Energy. Values clamp to configured maxima; damage consumes Shield before Health; Energy is not damaged; value, world-health-bar-data, and death events publish only when state actually changes; death cannot repeat while already dead.
- Added `DamageInfo`, `DamageResult`, and `DamageReceiver` with source/faction/hit-point placeholders and Inspector context commands for controlled damage/reset checks.
- Added `Selectable` and `SelectionIndicatorController` with Normal, Preview, Selected, and Inspected states. Indicator visibility/materials are event-driven and can be switched from component context menus without a per-frame loop.
- Added five placeholder definition assets for Hero, Friendly, Enemy, Worker, and Producer Building. The producer definition has zero movement values; the neutral resource crystal uses a fallback stable identity and intentionally has no Vitals, DamageReceiver, Selectable, or movement component.
- Bound the P05 components to all P03 actor prefabs. `PF_AppRoot` now owns and registers `FactionRelationService` and `EntityRegistry`; SystemTest inherits nine entities: six Friendly, two Enemy, one Neutral, with eight selectable/damageable actors.
- Added the idempotent menu commands:
  - Historical P05 Apply: Gameplay Foundation (retired)
  - Historical P05 Validate: Gameplay Foundation (retired)
- Added EditMode coverage for faction symmetry/neutrality, shield-first atomic damage, change-only vitals events, one death event per life, registry registration/query/unregistration/stale-reference cleanup, all four indicator states, and serialized asset/prefab service contracts.

### P05 Validation Evidence

- Unity compiled the new Runtime, Editor, and EditMode test sources without compiler errors or project warnings. The only observed warning was the MCP package's transient WebSocket reconnect message during domain reload.
- The apply command logged `[OriginCore P05] APPLY PASS`; a separate validation command logged `[OriginCore P05] VALIDATION PASS`.
- The validator independently confirmed unique definition archetype IDs, AppRoot service references, component bindings and default vitals on every actor prefab, an inactive Normal selection indicator, a non-damageable Neutral resource crystal, no producer `NavMeshAgent`, no Missing Scripts, and the expected SystemTest entity/faction/selectable counts.
- Per the requested workflow, Codex did not run Test Runner or Play Mode tests. The user subsequently ran the requested P05 validation and confirmed it passed; P05 is complete.

## P06 Implementation

- Added one namespaced `GameModeController` as the only OriginCore runtime authority for RTS/ACT/FPS changes. F1/F2/F3 are still read by the persistent `InputRouter`, but it now raises a request that the controller accepts or rejects before changing the active Gameplay map.
- Added `PossessionService` and `HybridControlDriver` with the explicit `Autopilot`, `PendingManualOverride`, and `Manual` states. Entering ACT/FPS from RTS preserves the active `NavMeshAgent`; Look input is ignored; the first meaningful direct input publishes one cancellation event and transfers transform ownership to the `CharacterController` exactly once.
- ACT-to-FPS and FPS-to-ACT presentation changes preserve the current pending/manual ownership state. Returning to RTS from pending simply disarms takeover; returning from manual samples and warps to nearby NavMesh, resets the path, and preserves the original position with a diagnostic if attachment fails instead of jumping to world origin.
- Added an event-driven cursor policy: RTS is visible/unlocked, ACT/FPS hidden/locked, and a pause override API restores visible/unlocked state for P13 without adding a polling loop.
- Added one `CinemachineBrain` on the existing SystemTest `MainCamera` and three virtual cameras: independent RTS, hero-follow ACT, and hero-mounted FPS. `CameraModeCoordinator` changes priorities with an EaseInOut blend while preserving exactly one real output Camera and one AudioListener.
- Added a small event-driven placeholder mode HUD beneath the existing `HUDRoot`; it shows the current mode and exposes separate RTS/ACT/FPS roots without rebuilding UI every frame.
- Updated `PF_AppRoot` with the persistent mode/possession/cursor services and updated `PF_HeroPlaceholder` with one root `CharacterController`, one `NavMeshAgent`, and the hybrid ownership driver. The old root `CapsuleCollider` was removed so only one root Collider owns hero collision.
- Added the idempotent menu commands:
  - Historical P06 Apply: Mode and Camera Foundation (retired)
  - Historical P06 Validate: Mode and Camera Foundation (retired)
- Added EditMode coverage for mode mappings, cursor rules, Look exclusion, one-shot takeover, pending-to-RTS return, serialized services/pawn contracts, camera counts and HUD references. Added PlayMode input-fixture coverage for real F1/F2/F3 bindings, preserved autopilot path, Look-only observation, one-shot W takeover, ACT/FPS continuity, safe RTS return, and no-pawn rejection.

### P06 Validation Evidence

- Unity compiled the Runtime, Editor, EditMode, and PlayMode assemblies without compiler errors or project warnings. The only observed warning was the MCP package's transient WebSocket reconnect message during domain reload.
- The apply command logged `[OriginCore P06] APPLY PASS`; a separate validation command logged `[OriginCore P06] VALIDATION PASS`.
- The validator independently confirmed AppRoot service wiring, hero controller/agent ownership defaults, one root Collider, one output Camera/AudioListener/Brain, exactly three virtual cameras with the required follow/look targets and pipeline types, one active priority, and event-driven HUD/context references.
- Per the requested workflow, Codex did not run Test Runner or manual Play Mode validation. The user completed the requested checks, reported the PlayMode input-harness failure, and confirmed the corrected rerun passed. P06 is complete.

## P07 Implementation

- Added `RtsCameraBounds` and an event-driven `RtsCameraController` on the existing RTS camera target. Edge movement is bounded by the SystemTest Minimap bounds and is suppressed outside RTS, while unfocused, or while the pointer is over UI. Current user requirements make translation/recenter immediate and keep a fixed 60° tilt while wheel zoom changes only height/distance.
- Added a scene-local `SelectionService` that consumes the persistent `InputRouter` snapshot and queries the existing `EntityRegistry`. It supports click selection, empty-click clear, additive Shift click without toggle removal, drag-threshold box preview/commit, hostile single inspection, Shift+1 idle-worker selection, and Shift+2 Combat/Hero selection.
- Box selection accepts only alive Friendly commandable `Selectable` instances. Preview uses the existing Normal/Preview/Selected visual states and always restores or commits state on release, cancellation, mode changes, service disable, or entity removal. Hostile inspection remains outside the command group.
- Added `IRtsCommandStatus` as the read-only P08 integration boundary for the current-command concept; P07 treats a Worker with no implementing current-command status as idle and does not introduce movement or command execution early.
- Added a CanvasScaler-safe light-green `SelectionBoxView` and an event-driven `SelectionSummaryView`. The summary subscribes to selection and presented-mode changes, shows count plus a bounded entry list, and performs no per-frame UI polling.
- Extended `SceneContext` with typed P07 references and added the idempotent menu commands:
  - Historical P07 Apply: RTS Camera and Selection (retired)
  - Historical P07 Validate: RTS Camera and Selection (retired)
- Added EditMode coverage for rectangle normalization, drag thresholds, bounds clamping, zoom defaults, focus/UI gates, and serialized scene contracts. Added PlayMode coverage for click, clear, Shift append, live box preview cleanup/commit, hostile inspection, commandable-only groups, and both selection shortcuts.

### P07 Validation Evidence

- Unity imported and compiled the Runtime, Editor, EditMode, and PlayMode sources with zero compiler errors. The only Console warning observed after the domain reload was MCP for Unity's transient WebSocket reconnect message, not a project-code diagnostic.
- The apply command logged `[OriginCore P07] APPLY PASS`; a separate validation command logged `[OriginCore P07] VALIDATION PASS`, with zero Console errors.
- MCP hierarchy inspection confirmed one `RtsCameraBounds` and `RtsCameraController` on `RTSCameraTarget`, one scene-local `SelectionService`, and the saved event-driven selection overlay/summary under the existing HUD. The validator independently checked unique component counts, typed references, layer mask, thresholds, camera defaults, hidden box state, and Missing Scripts.
- Per the requested workflow, Codex did not run Test Runner or the manual Play Mode acceptance checks. The first user PlayMode run exposed a multi-pointer test-fixture conflict: the low-magnitude synthetic drag start could lose `RTS/Point` control to the real mouse even though the synthetic click button entered dragging. The fixture now temporarily disables pre-existing Pointer/Keyboard devices, restores them in teardown, retains the full InputRouter drag path, and reports drag/control/projection diagnostics on failure. The user confirmed both the corrected automated rerun and the manual Play Mode acceptance checks passed. P07 is complete.

## P08 Implementation

- Added the extensible `IRtsCommand` lifecycle with explicit Pending, Running, Succeeded, Failed, and Cancelled states. `UnitCommandQueue` owns one current command plus a strict five-entry waiting FIFO: non-Shift replacement cancels the current and every waiting command, Shift appends without overwriting, a sixth waiting command is rejected, Stop clears both collections, and a terminal current command advances the next entry automatically.
- Added a distinct `MoveCommand` instance per selected movable unit. Commands receive a typed `UnitCommandContext` and delegate movement to the unit's `NavMeshMovementDriver`; no mutable command instance is shared between units.
- Added `NavMeshMovementDriver` around `NavMeshAgent`. It applies speed, acceleration, angular speed, and stopping distance from `UnitDefinition`, samples and pre-validates a complete path, calls `SetDestination` once when a move begins, detects arrival/path failure, and safely resets the path on completion or cancellation without per-frame destination calls or error-log spam.
- Added the scene-local `RtsCommandIssuer`. In RTS, right-click on valid Ground/NavMesh issues Move, Shift appends, the Move panel button enters an explicit target-wait state, Cancel exits targeting, and S or Stop immediately clears selected movable queues. Buildings and other unmovable selections are ignored safely.
- Added one-shot feedback for no selection, no movable unit, invalid ground, queue full, and path failure. The debug overlay's queued-command value and the new command panel update from selection, queue, targeting, feedback, and mode events; neither UI component polls state every frame.
- Integrated P06 possession ownership: changing from RTS to ACT/FPS preserves the Hero's current RTS command while direct control is only pending; the first meaningful direct input clears the Hero current/waiting queue before `HybridControlDriver` disables its Agent. Look-only observation remains excluded and takeover remains one-shot.
- Applied the user-confirmed RTS interaction override recorded in `Docs/RequirementsAddendum.md`: buttons enter a target-wait state, then a subsequent world-space left-button release confirms Move and future targeted commands/skills. Selection click/drag handling is suppressed for the whole target-wait state, while untargeted right-click keeps its default Move meaning.
- Added `IRtsCommandRoutePointProvider` and `CommandQueuePathView`. Selected movable units display an allocation-free white dashed world-space route from their current position through the active command and all five waiting targets. The route uses a self-contained overlay shader and hides for unselected, empty, invalid, or dead units; future Attack/AttackMove/ability/rally commands share the same route target contract.
- Added `NavMeshAgent`, `NavMeshMovementDriver`, and `UnitCommandQueue` to Hero, friendly unit, enemy unit, and worker prefabs. `PF_ProducerBuilding` explicitly remains free of all three movable-unit components. SystemTest now contains one typed command issuer and one Move/Stop/Cancel panel bound through `SceneContext`, while reusing the existing baked `NavMeshSurface`.
- Added the idempotent menu commands:
  - Historical P08 Apply: RTS Command and Movement (retired)
  - Historical P08 Validate: RTS Command and Movement (retired)
- Added EditMode coverage for the exact waiting capacity, sixth-command rejection, replacement, Stop, automatic advance, independent command state, prefab contracts, scene references, and baked NavMesh. Added PlayMode coverage for real right-click/Shift/S input, one-time destination requests, arrival advance, invalid-path recovery, per-unit command instances, and the RTS-to-direct-control cancellation boundary.

### P08 Validation Evidence

- Unity compiled the Runtime, Editor, EditMode, and PlayMode assemblies with zero compiler errors. The only warning observed during a domain reload was MCP for Unity's transient WebSocket-not-initialized message, not a project-code diagnostic.
- The apply command completed successfully twice, confirming the serialized setup remains valid across a repeated application. Separate validation runs after each application logged `[OriginCore P08] VALIDATION PASS`; the validator checked all movable/immovable prefab contracts, unique scene references, the command panel, the baked NavMesh, movement-definition values, and Missing Scripts.
- The first user PlayMode run exposed two integration gaps. Hero had no enabled raycast collider while its root `CharacterController` was disabled for RTS autopilot, and the earlier P07 click fixture could choose a unit projected beneath the new P08 command panel. Hero now owns an always-enabled trigger `SelectionHitbox` explicitly referenced by `Selectable`; selection raycasts include triggers and choose the nearest resolvable Selectable without allocating. Selection/box projection uses that typed collider, while the P07 fixture rejects UI-covered candidate points. P06, P07, and P08 validators all pass after the corrective apply. The next rerun reduced the suite to one Stop-state assertion: Unity 2022.3's observed `ResetPath()` ordering left `isStopped` false, so cancellation now resets the path first and explicitly stops the Agent afterward. The user confirmed the corrected PlayMode rerun passed.
- Per the requested workflow, Codex did not run Test Runner or manual Play Mode acceptance. The user confirmed both the corrected automated rerun and the full P08 manual Play Mode checklist passed. The later user-requested left-click targeting and white dashed queue-route additions were explicitly exempted from another test gate; Unity recompiled them with zero project diagnostics, the idempotent P08 apply passed, and the P06/P07/P08 validators all passed. P08 is complete.

## P09 Implementation

- Added a generic `AttackCapability` driven only by `UnitDefinition` range, damage, cooldown, and vision values. It centralizes hostile-target validation through `FactionRelationService`, planar range checks, optional non-allocating line-of-sight checks, cooldown state, facing, and `DamageInfo` delivery without introducing a weapon, projectile, ammunition, or equipment system.
- Added independent `AttackCommand` and `AttackMoveCommand` lifecycles. Attack pursues one explicit living hostile and finishes when that target dies or becomes invalid. Attack Move preserves its original NavMesh destination, scans at a bounded interval, engages the nearest visible hostile inside its aggro radius, drops targets beyond `AttackRange + 2`, and resumes the original route.
- Extended `UnitCommandContext` and `UnitCommandQueue` with typed attack/scanner dependencies and explicit rejection reasons. Attack and Attack Move each create a separate mutable command instance per selected unit, support Shift append/replacement/Stop, and expose their current route point through the same dashed-route interface introduced in P08.
- Extended `RtsCommandIssuer` and the existing command panel with Attack targeting and the A shortcut. In accordance with `Docs/RequirementsAddendum.md`, the Attack button enters a wait state and a subsequent left-button release selects a hostile for Attack or reachable ground for Attack Move; allied/neutral targets are rejected, while untargeted right-click remains the default Move shortcut.
- Added `AutoTargetScanner` with a fixed 64-collider `OverlapSphereNonAlloc` buffer and stable nearest-target selection. Added `SimpleEnemyBrain`, which scans only at a low frequency and issues the same generic `AttackCommand` only while its command queue is idle; no separate enemy-only combat executor exists.
- Added event-driven `WorldHealthBar` UI beneath every existing `HealthBarAnchor`. It shows only while an entity is injured, selected, or inspected, displays health and optional shield fill, and billboards against the typed `SceneContext.MainCamera` without `Camera.main` or name-based lookup.
- Added `HitFlash` using cached `MaterialPropertyBlock` state and exact restoration, plus `CombatDeathHandler`, which clears commands, stops navigation, leaves selection/registry state, and deactivates the entity with `SetActive(false)` instead of destroying it.
- Applied the combat pipeline to Hero, friendly unit, enemy unit, and worker prefabs. Only `PF_UnitEnemy` receives `SimpleEnemyBrain`; `PF_ProducerBuilding` remains immobile and receives only damage feedback, health-bar, and death handling.
- Added the idempotent menu commands:
  - Historical P09 Apply: RTS Combat and Feedback (retired)
  - Historical P09 Validate: RTS Combat and Feedback (retired)
- Added EditMode source coverage for hostile/allied/neutral Attack behavior, cooldown, damage, and completion on death. Added PlayMode source coverage for Attack Move discovery, engagement, hit feedback, deactivate-on-death, and resumption of the original route. These tests are compiled but intentionally not run by Codex.

### P09 Validation Evidence

- Unity compiled Runtime, Editor, EditMode, and PlayMode assemblies with zero compiler errors; the Console contains no project errors.
- The apply command logged `[OriginCore P09] APPLY PASS`. Separate P05, P06, P07, P08, and P09 structural validators all logged `VALIDATION PASS`, confirming that the combat additions preserve prior prefab, scene, selection, mode, movement, command-panel, NavMesh, and Missing Script contracts.
- Per the requested workflow, Codex stopped before Test Runner and manual Play Mode validation. The user subsequently confirmed the P09 automated and manual checks passed; P09 is complete.

## P10 Implementation

- Added `ResourceType`, serializable `ResourceCost`, a pure `ResourceWallet`, and persistent `ResourceService`. Commander Resource and Crystal are ordinary balances, while Influence is represented only as used/cap. Multi-resource payment and Influence reservation are committed as one transaction; any failed affordability check leaves every value unchanged and publishes no partial change.
- Added event snapshots for every successful resource mutation and bound Commander Resource into the existing debug snapshot. Runtime HUD consumers subscribe to resource changes instead of polling.
- Added stable `ProductionRecipe` assets for the existing Worker and friendly Combat `UnitDefinition` archetypes only. Their placeholder contracts are 50 Commander/0 Crystal/1 Influence/2 seconds and 75 Commander/25 Crystal/2 Influence/3 seconds respectively.
- Added a five-order `ProductionQueue` to the producer. Enqueue atomically pays ordinary costs and reserves Influence, countdown advances with `Time.deltaTime` so `Time.timeScale == 0` freezes it, temporary SpawnPoint blockage retries at a bounded scaled interval, and producer disable/death refunds all unfinished orders without leaking reservations.
- Added `UnitSpawner` with a fixed-size occupancy buffer and bounded NavMesh candidate search around a typed `SpawnPoint`. It accepts only the configured Worker/Combat definitions and prefabs, inherits the producer faction, and never produces a Hero, building, resource node, or harvesting content.
- Added `PopulationOwner` to the two producible prefabs. A completed order transfers its already-reserved Influence to the unit without reserving twice; death, disable, or destruction releases that reservation through a one-shot guard.
- Added `RallyPointController` to the immobile producer. The Set Rally button enters the shared targeting state and, following `Docs/RequirementsAddendum.md`, a subsequent world-space left-button release confirms reachable NavMesh ground while right-click cannot release the target. Outside targeting, right-click is contextual: movable selections receive Move, while a selection containing a producer directly updates the selected producers' rally point. Spawned units receive the normal `MoveCommand`; an invalid rally leaves them at SpawnPoint and emits one bounded warning.
- Generalized the white dashed route view through `IRtsRouteSource`. Movable units still expose their current/waiting command queue, while the producer exposes its building-to-rally route without receiving a `NavMeshAgent`, movement driver, or unit command queue. P08 apply/validation now explicitly preserves this non-movement P10 route binding.
- Added event-driven P10 HUD views: top-right Commander/Crystal/Influence, left-side clickable Idle Worker and Combat/Hero counts, and a producer-only Worker/Combat queue, progress, feedback, and Set Rally panel.
- Added the idempotent menu commands:
  - Historical P10 Apply: Economy Production and Rally (retired)
  - Historical P10 Validate: Economy Production and Rally (retired)
- Added EditMode source coverage for atomic mixed-resource payment, Influence capacity/release, and production rejection without partial deduction. Added PlayMode source coverage for resource payment, scaled pause, spawn, automatic rally movement, full-Influence rejection, and exactly-once Influence release on death. These tests are compiled but intentionally not run by Codex.
- Kept the phase boundary explicit: no harvesting, resource-node output, worker summon action/key 5, formal building placement, or additional unit archetypes were introduced.

### P10 Validation Evidence

- Unity compiled Runtime, Editor, EditMode, and PlayMode assemblies with zero compiler errors. After validation the Console contained zero errors and zero warnings.
- The P10 apply command logged `[OriginCore P10] APPLY PASS` on repeated execution, confirming the recipe, prefab, service, scene, and HUD setup is idempotent.
- Separate P05, P06, P07, P08, P09, and P10 structural validators all logged `VALIDATION PASS`. This confirms the P10 producer remains immobile, its rally route is compatible with P08, prior combat/selection/mode contracts remain intact, and all generated assets and typed references are present without Missing Scripts.
- Per the requested workflow, Codex did not run Test Runner. The user confirmed the corrected EditMode Run All and P10 PlayMode rerun passed, then accepted the completed phase. The later user-requested direct RMB rally shortcut was compiled and structurally validated without adding tests, as requested.

## P11 Implementation

- Added `MovementConfig` as the single ACT tuning asset. It owns run speed, acceleration, air control, gravity, ground/air jump heights, crouch height, jump buffer and coyote windows, assisted-jump radius/force/cooldown, rotation speed, ground probe, and third-person look limits.
- Added `HybridPawnMotor` as the only `_OriginCore` class that calls `CharacterController.Move`. It owns direct-control planar/vertical velocity, collision state, grounded probing, smooth facing, standing/crouched capsule geometry, obstruction-safe stand-up checks, and allocation-free nearby boost-contact queries.
- Added `ActMovementController` over the existing P06 possession foundation. It consumes the current ACT input snapshot after the high-priority mode/possession pass, so the first Move/Jump/Crouch/Lock intent can activate Pending manual control and the same frame's movement or jump is retained. Mouse Look remains observation-only and never performs takeover.
- Implemented camera-relative WASD with acceleration and reduced air control. The ACT camera target keeps an independent yaw/pitch in world space and reapplies it in `LateUpdate`, preventing pawn facing changes from feeding back into the movement basis.
- Implemented grounded/coyote/jump-buffer handling, one additional air jump, landing reset, grounded stick velocity, and ceiling collision cancellation. Leaving ACT always clears buffered input; ACT-to-FPS preserves motor velocity for P12, while returning to RTS clears direct motion after NavMesh ownership is restored.
- Implemented hold-to-crouch by changing `CharacterController.height/center` while preserving the capsule bottom. Releasing Ctrl only restores the standing capsule when the expanded head volume is clear.
- Implemented assisted jump against the independent BoostSurface/Unit search mask. Jump chooses the nearest valid contact, applies horizontal velocity away from that point plus an upward component, resets the one-air-jump counter, and arms a short cooldown. Existing selectable Worker/Combat/Hero colliders are recognized as units without using string tags.
- Added one cyan `P11_BoostSurface` and one low `P11_CrouchRoof` to `90_SystemTest`, plus a concise ACT control prompt. No formal art or map content was introduced.
- Added the idempotent menu commands:
  - Historical P11 Apply: ACT Movement (retired)
  - Historical P11 Validate: ACT Movement (retired)
- Added PlayMode coverage for camera-relative movement, same-frame Pending takeover, ground/air jump limits and landing reset, blocked stand-up, assisted-jump direction/reset/cooldown, and one-shot jump buffering. The tests are compiled but intentionally not run by Codex.
- Kept the phase boundary explicit: no ACT weapon, attack, skill, dash, slide, flight, worker summon, animation, or formal character content was added. The optional target-lock service remains deferred.

### P11 Validation Evidence

- Unity compiled Runtime, Editor, EditMode, and PlayMode assemblies with zero compiler errors. The final isolated Console contains only `[OriginCore P11] VALIDATION PASS` plus the MCP command log, with no project errors or warnings.
- The P11 apply command logged `[OriginCore P11] APPLY PASS` on repeated execution, confirming that the config, Hero prefab bindings, HUD text, and acceptance fixtures are idempotent.
- Separate P05, P06, P07, P08, P09, P10, and P11 structural validators all logged `VALIDATION PASS`, confirming that the direct-control additions preserve entity, mode, selection, command, combat, production, prefab, scene, and Missing Script contracts.
- MCP scene resources independently confirmed exactly one `P11_BoostSurface`, one `P11_CrouchRoof`, and one scene Hero with one `HybridPawnMotor` and one `ActMovementController` bound to `SO_ActMovementConfig` and `ActCameraTarget`.
- A source audit confirmed `HybridPawnMotor.cs` contains the only `CharacterController.Move` call under `Assets/_OriginCore`. Per the requested workflow, Codex did not run Test Runner; the user confirmed all six targeted `ActMovementPlayModeTests` and the complete P11 manual Play Mode checklist passed. P11 is complete.

## P12 Implementation

- Added `FpsMovementConfig` and `SO_FpsMovementConfig` as the single FPS-specific tuning source for walk/sprint speed, sprint latch minimum speed, acceleration, slide threshold/impulse/friction/duration, base/ADS FOV, and FOV transition speed. Gravity, ground probing, jump height, crouch geometry, look sensitivity, and pitch limits continue to reuse the P11 ACT movement config instead of creating a second shared-movement truth source.
- Added `FpsMovementController` over the P06 possession and P11 motor foundations. It consumes only the active FPS snapshot, preserves same-frame Pending takeover, treats Move/Jump/Crouch/Aim plus a new Sprint press as meaningful direct input, and keeps Look, a merely held Sprint latch, plus all reserved weapon/slot/grenade/summon actions observation-only.
- Implemented camera-relative walk and user-overridden threshold-latched sprint with acceleration, grounded single jump, obstruction-safe hold crouch, independent FPS yaw/pitch, body-yaw alignment, and a smoothly lowered first-person camera point while crouched or sliding. A new Sprint press starts acceleration; release below `sprintLatchMinSpeed` restores walk, release at or above it latches sprint, subsequent presses do not turn a latch off, and a complete stop or FPS exit resets to walk.
- Added `SlideState`: grounded Crouch press enters only at or above `slideMinSpeed`, applies one forward impulse, decays by friction, and exits by duration, low speed, side collision, Jump, or mode exit. Exit attempts to stand only when headroom is clear; Jump can end the slide and jump in the same simulation step.
- Added `AimState`: holding RMB exposes `IsAiming` and smoothly changes only the existing `CM_FPS` lens from base FOV to ADS FOV. Releasing RMB returns to base FOV, while leaving FPS restores the exact lens value captured on entry. No shooting or spread path exists.
- Added `FirstPersonVisibility` with per-Renderer state capture. FPS hides only Hero `VisualRoot`, `HeadMarker`, and `ForwardMarker`; leaving FPS restores each Renderer to its exact pre-entry enabled state without changing the selection indicator or other world state.
- Added an event-driven `CrosshairView` on `P12_CrosshairCanvas`. It listens to the existing mode presenter, shows a centered white dot only in FPS, and reads RGB from the existing persistent `SettingsData` seam; P13 can refresh it when the full SettingsService changes values.
- Added the idempotent menu commands:
  - Historical P12 Apply: FPS Movement And Aim (retired)
  - Historical P12 Validate: FPS Movement And Aim (retired)
- Added six compiled PlayMode tests covering walk/threshold-latched-sprint separation, below-threshold release, full-stop reset, and low-speed crouch; slide entry/duration/Jump exit; ADS FOV and exact restoration; crosshair mode/color behavior; exact Renderer restoration; and FPS Pending takeover exclusions/one-shot Aim takeover. The tests are intentionally not run by Codex.
- Kept the phase boundary explicit: no shooting, spread, weapon slots, weapon switching, projectiles, grenades, remote-weapon movement penalty, worker summon, formal character art, or animation was added.

### P12 Validation Evidence

- Unity compiled Runtime, Editor, EditMode, and PlayMode assemblies with zero project compiler errors or warnings. The only observed warning is the MCP package's transient WebSocket reconnect message during domain reload.
- The P12 apply command logged `[OriginCore P12] APPLY PASS` on repeated execution, confirming the config, Hero prefab bindings, CM_FPS lens, FPS prompt, and crosshair hierarchy are idempotent. A separate command logged `[OriginCore P12] VALIDATION PASS`.
- Separate P05, P06, P07, P08, P09, P10, and P11 validators still logged `VALIDATION PASS` after P12, confirming the FPS additions preserve prior entity, camera, mode, selection, command, combat, production, ACT, scene, prefab, and Missing Script contracts.
- MCP scene resources independently confirmed one scene Hero with one inherited `FpsMovementController` and `FirstPersonVisibility`, plus one active `P12_CrosshairCanvas` whose centered `CrosshairDot` is inactive in the default RTS mode.
- A source audit confirmed `HybridPawnMotor.cs` remains the only file under `Assets/_OriginCore` that invokes `CharacterController.Move`; the P12 runtime contains no shooting, bullet, projectile, weapon, grenade, spread, or fire implementation. Per the requested workflow, Codex did not run tests. The user confirmed the updated six-test PlayMode run and manually accepted below-threshold release, at-threshold latch, repeated-Shift retention, and full-stop walk reset. P12 is complete.

## P13 Implementation

- Added reusable `PageStack`, `ModalStack`, and `ConfirmDialog` components. Main-menu and pause pages now route through these stacks, so inactive pages cannot retain raycasts or input, while Yes/No confirmations own a single modal lifecycle.
- Added a complete MainMenu route: Start opens Story/Map selection, Story New and Map `SYSTEM TEST 0` both load the catalog key `system_test_0`, Options opens the shared settings page, and Quit requires confirmation. Load remains an explicit P14 boundary notice.
- Added `SettingsData` schema fields for resolution, `FullScreenWindow`/`Windowed`, brightness, normalized Master Volume, crosshair RGB, and binding override JSON. Unsupported exclusive-fullscreen values normalize to borderless fullscreen.
- Added `SettingsRepository` at `Application.persistentDataPath/OriginCore/settings.json`. It writes indented UTF-8 JSON to a temporary file and atomically replaces/moves the destination, with `schemaVersion` normalization on load.
- Added persistent `SettingsService` to `PF_AppRoot`. It shares one `SettingsData` instance with `InputRebindService`, applies and saves interactive rebinds without rejecting duplicate paths, enumerates available display resolutions, maps normalized volume zero to the -80 dB mute floor, and refreshes the FPS crosshair through a settings event.
- Created `AM_Main.mixer` with exposed `MasterVolume`. Created a global URP `Volume` and `ColorAdjustments.postExposure` profile as the guide-approved URP equivalent of the brightness image effect; post-processing is enabled on all three foundation cameras.
- Added a reusable Options UI for resolution, borderless/windowed mode, brightness, Master Volume, crosshair RGB preview, every implemented Input System binding, interactive rebind/cancel, duplicate bindings, restore defaults, Apply, and Back.
- Added `PauseService` with the required Escape priority: active rebind cancellation, UI modal/page back, RTS command-target cancellation, then pause toggle. Pause sets `Time.timeScale = 0`, disables Gameplay maps while preserving Global/UI pause input, suppresses F-key mode changes, unlocks the cursor, and preserves the active game mode for resume.
- Added Pause UI for Continue, Options, Load, Save, and Save-and-Exit. The three persistence buttons are wired to `IPersistenceMenuActions` and public request events; their data implementation remains intentionally deferred to P14.
- Added mutually exclusive P13 HUD overlays: RTS retains the existing resource/selection/command/production views and gains a minimap placeholder; ACT and FPS receive event-driven Health/Shield/Energy plus explicit unconfigured weapon status; the crosshair remains FPS-only.
- Added the idempotent menu commands:
  - Historical P13 Apply: Menus Settings Pause And HUD (retired)
  - Historical P13 Validate: Menus Settings Pause And HUD (retired)
- Added EditMode coverage for page/modal exclusivity, settings normalization, UTF-8 atomic persistence, audio conversion, prefab/service wiring, and scene ownership. Added PlayMode coverage for Escape targeting/pause priority, pause state restoration, three-mode HUD/crosshair exclusivity, and MainMenu page/modal routing. These tests are compiled but intentionally not run by Codex.
- The existing `NotoSansSC-VF.ttf` provenance remains unverified, so all generated P13 player-facing text uses English TMP labels. No text is baked into an image.
- Kept the phase boundary explicit: no character-selection screen, weapon wheel, save-slot data, actual save/load serialization, or functional minimap/visibility system was introduced.

### P13 Validation Evidence

- Unity completed a clean domain reload after compiling Runtime, Editor, EditMode, and PlayMode assemblies. The generated AudioMixer YAML contains the exposed `MasterVolume` parameter.
- The P13 Apply command logged `[OriginCore P13] APPLY PASS` on two consecutive executions, confirming idempotent service, mixer, Volume, prefab, and scene generation.
- Every structural validator from P01 through P13 logged `VALIDATION PASS` after the P13 changes. This confirms the settings/pause integration preserves foundation, bootstrap, input, gameplay, modes, RTS, economy, ACT, FPS, prefab, scene, and Missing Script contracts.
- Per the requested workflow, Codex did not run Test Runner. P13 remains awaiting user-run EditMode/PlayMode tests and the manual UI/pause/settings checklist.

The user subsequently confirmed the P13 EditMode/PlayMode and manual checks passed. P13 is complete.

## P14 Implementation

- Added `SaveGameData` schema version 1 with stable slot metadata, catalog `sceneKey`, saved `GameMode`, resources, active entity transforms/factions/vitals, removed static-entity ids, runtime-spawn ownership, producer rally points, and an explicit explored-visibility payload. Selection, pointer state, camera interpolation, command queues, cooldown instants, animation, and particles remain intentionally transient.
- Added `SaveRepository` under `Application.persistentDataPath/OriginCore/Saves`. Slot ids are path-safe and independent of display names; files are indented UTF-8 JSON; existing files are replaced through a temporary file plus `File.Replace`; failed replacement preserves the prior file; metadata enumeration catches corrupt/unknown-version files and keeps them selectable for deletion; slots sort by `savedAtUtc` descending.
- Added persistent `SaveService` to `PF_AppRoot`. Save operations preserve the prior gameplay-input state, capture through a centralized participant pipeline, update the slot list only after a successful write, and request exit only after success. In Editor, successful save-and-exit stops Play Mode through a runtime-safe reflection boundary; player builds call `Application.Quit`. A failure leaves the application open.
- Added `ISaveParticipant` and concrete entity, rally, resource, visibility, and mode participants. Gameplay objects never write files themselves. Load validates JSON, schema, slot id, and scene key before starting, normalizes the persistent mode to RTS for transition, waits for the new `SceneContext`, restores in deterministic order, clears selection/queues, and finally reapplies the saved mode.
- Reused the existing `EntityIdentity.RuntimeId` as the static persistent-id store instead of introducing a duplicate identity component. The P14 apply command writes deterministic `scene.system_test_0.*` ids to every SystemTest entity. Produced units are explicitly marked runtime-spawned and restore from `archetypeId + runtimeId`; unknown archetypes are skipped with a bounded warning.
- Extended `UnitSpawner` with restore-only archetype resolution and NavMesh placement. Restored produced units reclaim their already-snapshotted Influence ownership without reserving twice; final resource restoration then writes the exact saved Commander/Crystal/Influence used-cap snapshot.
- Extended `ResourceWallet`/`ResourceService` with an exact `Restored` mutation so event-driven HUD/debug consumers refresh after load. Producer rally points restore through the existing NavMesh validation path. `IVisibilitySaveBridge` and `VisibilitySaveData` establish the P15 explored-fog persistence boundary without inventing a visibility implementation early.
- Added reusable `LoadGamePanel`, `SaveNameDialog`, and `SaveSlotRowView`. MainMenu Load and Pause Load/Save/Save-and-Exit now open the same slot UI with selection, new save, overwrite confirmation, rename, load, confirmed delete, corrupt-file error display, and close/Escape handling. The panel is generated as `PF_LoadGamePanel` and nested once in each owning UI prefab.
- Added the idempotent menu commands:
  - Historical P14 Apply: Save Load And Slots (retired)
  - Historical P14 Validate: Save Load And Slots (retired)
- Added compiled EditMode source coverage for schema/UTF-8 round-trip, rename invariants, newest-first metadata, corrupt/unknown-version deletion, atomic-replacement failure preservation, prefab/service wiring, and stable scene ids. Added compiled PlayMode source coverage for resources/mode/positions/vitals/runtime-unit restoration, selection/command transience, duplicate prevention, unknown-archetype skipping, and failed save-and-exit preserving the previous slot without quitting. The tests are intentionally not run by Codex.
- Updated the P13 structural validator to validate its configured main-menu modal stack instead of forbidding nested modal stacks added by later UI phases. This keeps the original P13 contract while allowing the P14 slot panel to own an isolated confirmation stack.

### P14 Validation Evidence

- Unity compiled Runtime, Editor, EditMode, and PlayMode assemblies with zero project compiler errors or warnings. The only observed warning is the MCP package's transient WebSocket reconnect message during domain reload.
- The P14 apply command logged `[OriginCore P14] APPLY PASS` on repeated execution, and the separate validator logged `[OriginCore P14] VALIDATION PASS`, confirming idempotent AppRoot, prefab, UI, stable-id, and scene wiring.
- Every structural validator from P01 through P14 logged `VALIDATION PASS` after the forward-compatible P13 validator adjustment. No Test Runner suite or manual Play Mode flow was executed by Codex, per the requested workflow.

The user subsequently confirmed the complete P14 EditMode/PlayMode regression set and manual acceptance passed. P14 is complete.

## P15 Implementation

- Added `VisibilityGrid` with `Hidden`, `Explored`, and `Visible` cell states; configurable 2 m cells and 5 Hz updates; allocation-free steady-state circle stamping; stable explored-cell ids; and restore semantics that never persist transient visibility.
- Added scene-local `VisibilitySystem` implementing the existing `IVisibilitySaveBridge`. Friendly `VisionEmitter` components recompute Visible each tick, while a single runtime `Texture2D` drives both the main-camera volumetric renderer feature and the minimap mask. Pause-time zero delta stops scheduled updates, while tests and restore can request an explicit deterministic tick.
- Added `VisibilityTarget` to hostile fixtures. Hidden targets suppress renderers, world-space UI, the selectable interface, and selection collider while leaving the entity, vitals, AI, and other simulation components active. `AttackCapability`, `AutoTargetScanner`, `Selectable.TryResolve`, and `WorldHealthBar` now honor the visibility boundary so mode switches cannot leak a target through selection, lock-on, or health UI.
- Added detached `BuildingMemoryGhost` state records. A building first seen live records only its last-known transform, faction and role; hidden source movement or health never refreshes it; reacquisition immediately restores live state; and a source destroyed outside vision keeps its memory until the last-known cell is visible again. Presentation is now a non-selectable UI memory marker rather than a copied world mesh.
- Added `MinimapBounds`, `MinimapMapDefinition` and `MinimapView`. The RTS HUD draws a map-owned base texture independently, overlays the shared three-state fog texture, and maps registered entities to UI markers. Left-click maps the UI rectangle to world XZ and centers the clamped RTS rig only while the current mode is RTS.
- The historical top-down `P15_MinimapCamera`, Minimap-layer mesh markers, `FogOverlayPlane`, `SH_FogOverlay`, `MAT_FogOverlay` and `RT_Minimap` were removed. `90_SystemTest` now owns one `SO_SystemTestMinimapMap` definition and exactly one Unity Camera: MainCamera.
- Added a static enemy-building fixture with a stable P14-compatible runtime id for building-memory acceptance. Current tests distinguish the single MainCamera from the data-driven minimap without weakening actor, audio, Cinemachine or post-processing contracts.
- Added the idempotent menu commands:
  - Historical P15 Apply: Minimap And Visibility (retired)
  - Historical P15 Validate: Minimap And Visibility (retired)
- Added three EditMode tests for grid transitions/persistence, minimap coordinate mapping, and complete asset/prefab/scene wiring. Added three PlayMode tests for hidden target suppression and reacquisition, stale building memory and visible-area cleanup, and RTS-only minimap centering within one grid cell. The tests are compiled but intentionally not run by Codex.
- Kept the remaining phase boundary explicit: no LOS obstacle occlusion, logical height layers or final hand-painted minimap art was added; the SystemTest map currently uses its serialized 256×256 grid plus eight manually mapped static regions.

### P15 Validation Evidence

- Unity compiled Runtime, Editor, EditMode, and PlayMode assemblies without project compiler errors or warnings. The only observed warning is the MCP package's transient WebSocket reconnect message during domain reload.
- The P15 apply command logged `[OriginCore P15] APPLY PASS` on repeated execution, and the separate validator logged `[OriginCore P15] VALIDATION PASS`.
- Every structural validator from P01 through P15 logged `VALIDATION PASS` after the forward-compatible camera and fixture adjustments.
- Per the requested workflow, Codex did not run Test Runner or enter manual Play Mode. The user subsequently confirmed that the automated rerun and manual visibility/minimap checks passed. P15 is complete.

## P16 Implementation

- Added `OriginCoreIntegrationSetup` with the menu commands:
  - Historical P16 Validate: Integration And Build Readiness (retired)
  - Historical P16 Build: Windows x86_64 Development (retired)
- The historical P16 validator audited the exact three Build Settings scenes, required Layers and Player Settings, `_OriginCore` prefabs/scenes, Missing Script components, broken serialized references and infrastructure ownership. Its old RT-only minimap-camera clause has been superseded by the current single-MainCamera plus `MinimapMapDefinition` contract.
- The build command refuses to run from Play Mode, during compilation/refresh, during another build, or with dirty loaded scenes. It reruns the validator and then creates a strict LZ4 Windows x86_64 Development Build at `Builds/Windows64Development/OriginCore.exe`; build binaries remain covered by the project `.gitignore`.
- Added `IntegrationFoundationTests.ProjectIsStructurallyReadyForTheP16DevelopmentBuild`, which executes the same build-readiness contract from EditMode.
- Added explicit save forward-compatibility coverage proving unknown root JSON object/array fields are ignored without changing known versioned save state.
- Added `IntegrationRegressionPlayModeTests.ThreeModeAndPauseCyclesDoNotMultiplyCallbacksOrInfrastructure`, covering three complete RTS→ACT→RTS→FPS→RTS plus Pause/Resume cycles with exact event counts and stable AppRoot, SceneContext, EventSystem, AudioListener, Camera and HUD ownership.
- Added `TestMatrix.md` mapping every required EditMode/PlayMode group, the user requirement addendum, the 20-step manual path, Profiler checks, build checks, Console policy, and the exact user execution order. The current source contains 57 EditMode and 36 PlayMode test declarations before parameter expansion.
- Audited Selection, Visibility, AutoTargetScanner and the dashed command route at source level. Their steady-state paths reuse preallocated lists/arrays/property blocks; construction-only GameObject/material/texture allocations are outside per-frame steady state. Event-driven RTS views and services pair subscriptions with explicit unsubscription. Runtime Profiler confirmation remains a P16 user gate.

### P16 Validation Evidence

- Unity imported all new assets and generated their `.meta` files, then compiled Runtime, Editor, EditMode and PlayMode assemblies with zero project compiler errors or warnings. The only observed warning was the Unity MCP package's transient WebSocket reconnect message during the script domain reload.
- The P16 menu validator logged `[OriginCore P16] VALIDATION PASS`; therefore the current project has no detected Missing Script, broken serialized reference, duplicate foundation infrastructure, invalid required Layer, Build Settings ordering issue, Runtime Editor-assembly dependency, or persistence write through `Application.dataPath`.
- Unity MCP independently reported exactly the three required enabled Build Settings scenes, active `StandaloneWindows64`, x86_64 architecture, Mono scripting backend, product `OriginCore`, and company `TMC`.
- The user confirmed the complete P16 EditMode Run All passed, including the new build-readiness audit and unknown-save-field compatibility assertion.
- The user confirmed the complete P16 PlayMode Run All passed, including the new three-cycle mode/pause subscription and infrastructure stability regression.
- The user confirmed all 20 continuous manual integration steps and the Selection/Visibility/TargetScanner/subscription Profiler checks passed without blocking failure or sustained forbidden allocation.
- Unity MCP invoked the strict P16 build entry and logged `[OriginCore P16] BUILD PASS`. The Windows x86_64 Development Build contains 300 files totaling 122,625,346 bytes at `Builds/Windows64Development/OriginCore.exe`; the build completed with 0 Console Error and 0 Warning.
- The Player Managed output includes `OriginCore.Runtime.dll` and excludes `OriginCore.Editor` plus both test assemblies, providing direct evidence that Editor-only APIs did not leak into the runtime assembly boundary.
- The user confirmed the built Player completed Bootstrap→Menu→Options→SystemTest→three modes→Pause→Save/Load→MainMenu/Exit, restarted with settings/save persistence, rendered its English placeholder text correctly, and quit successfully.
- Codex inspected `C:/Users/elysia/AppData/LocalLow/TMC/OriginCore/Player.log`: the final run contained no Error, Warning, Exception, NullReference, MissingReference, Assertion, or Crash match. `settings.json` and the schema-version-1 save JSON were both valid under `Application.persistentDataPath/OriginCore`.
- Per the requested workflow, Codex did not run Test Runner, enter Play Mode, capture Profiler samples, or launch the executable. All such P16 runtime checks were user-executed and accepted. P16 is complete.

## P17 Implementation

- Added `Architecture.md` as the single handoff entry point. It records the assembly and build-scene boundary, AppRoot/SceneContext ownership, input and mode state machine, Hero NavMeshAgent/CharacterController dual-driver rules, RTS selection/command/route flow, combat/economy/production seams, UI/pause/settings ownership, ordered save participants, and visibility/fog/minimap data flow.
- Documented concrete extension procedures for a new character, composable weapon/skill capabilities without a monolithic `Weapon` class, a new map, a new RTS command and a new production item.
- Added a `90_SystemTest` reproduction runbook with actual controls, the continuous end-to-end path, validation/build menu locations and links to the exact expected/actual matrix.
- Added a complete P00-P17 guide-to-implementation mapping and retained every excluded formal-content area as an explicit deferred item rather than treating reserved input or placeholder UI as finished Gameplay.
- Audited `_OriginCore` asset provenance. Placeholder visuals are Unity primitives plus project-authored materials/shaders; `Art/Generated/2D` contains only a Unity RenderTexture; Audio contains a mixer but no clips; no imported bitmap/AI-generated image is present.
- Resolved all serialized asset GUIDs. The only cross-directory content dependency is TextMesh Pro's default `LiberationSans SDF`, whose source font has `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`; all component dependencies resolve to declared Unity packages. The remaining AudioMixer value is its internal exposed-parameter id, not an asset reference.
- Confirmed the legacy `Assets/Resources/Fonts/NotoSansSC-VF.ttf` is not referenced anywhere under `_OriginCore`; old QuickOutline and formal legacy assets are likewise outside the new serialized dependency boundary. The handoff requires a recorded license/source manifest for every future formal asset.
- Audited `_OriginCore` for TODO/FIXME/HACK/NotImplemented markers and `.tmp/.bak/.orig/.rej/.old` files. Matches were only intentional atomic-write/test temporary-path code and explanatory documentation; no unexplained temporary artifact or unfinished runtime stub was found.
- Audited Runtime declarations for types with neither code usage nor serialized script GUID usage. The only name-level candidates were the three static extension containers `GameModeExtensions`, `UnitRoleExtensions` and `CommandStatusExtensions`; their extension methods are actively used throughout Runtime and tests, so no orphan Runtime script remains.

### P17 Validation Evidence

- `Architecture.md` contains all P17-required module relationships, ownership rules, state/data flows, extension points, asset/license boundary, reproduction steps, known limitations and deferred work.
- `ImplementationStatus.md` contains a status/evidence row and detailed section for every phase P00 through P17. `TestMatrix.md` contains the final expected/actual automated, manual, Profiler, build and Player evidence.
- The serialized-dependency audit resolved package components to AI Navigation, Cinemachine, Input System, URP/Core RP, TextMesh Pro and UGUI; no unknown third-party runtime content was found under `_OriginCore`.
- Unity imported `Architecture.md` and its `.meta`, completed a forced AssetDatabase refresh/domain reload, and returned to an idle, ready state outside Play Mode.
- After clearing Console, the final menu rerun logged `[OriginCore P16] VALIDATION PASS` with 0 Error and 0 Warning. No new Test Runner pass was requested because P17 changed documentation only and the complete P16 EditMode/PlayMode plus runtime gates were already user-confirmed.
- P17 and the full P00-P17 implementation-guide objective are complete.

## Post-P17 Gameplay HUD Refinement (2026-07-14)

- Added `RQ-UI-001` through `RQ-UI-003` to `RequirementsAddendum.md` for the user-approved edge layout, hidden mode/key hints, graphic direct-control vitals, one-line RTS resources, and live world-health synchronization.
- Rebuilt `PF_HUDRoot` so the 256x256 minimap is bottom-left, ACT graphic vitals are top-left, and FPS graphic vitals are bottom-left. `DirectControlHudView` now binds six `Image` fills to the current pawn instead of rendering numeric placeholder/status text.
- Moved `P07_SelectionSummary` to bottom-center, `P08_CommandPanel` to bottom-right, `P10_ResourceHud` to top-right, `P10_Roster` to the left edge, and `P10_ProductionPanel` to the right edge directly above the command panel. All outer HUD offsets are zero on their owning edge.
- Removed visible mode/F-key and persistent input-hint presentation. Command/production button captions no longer append keyboard or mouse bindings.
- Changed `ResourceHudView` to a single-line presentation. Strengthened `WorldHealthBar` with event-compatible LateUpdate change detection before its disabled-canvas early exit, so damage can reveal and resize a previously hidden full-health bar.

### Post-P17 HUD Validation Evidence

- Unity completed a forced script refresh and compilation with no project compiler error or warning.
- The applied scene/prefab data contains a 256x256 minimap with a 1:1 `AspectRatioFitter`, zero-offset edge anchors for every affected HUD panel, an inactive P06 mode panel and inactive command prompt, and all six serialized ACT/FPS fill references.
- P06, P07, P08, P09, P10, P13 and P15 validators each logged `VALIDATION PASS`; Console contained no project Error or Warning afterward.
- Per the established workflow, Codex did not run Test Runner or enter Play Mode. Runtime visual/damage confirmation remains a user manual acceptance step.

## Post-P17 Superseded Planar Visibility Refinement (2026-07-14)

- Added `RQ-VIS-001` while retaining the existing `VisibilityGrid`, target suppression, building memory, minimap and save/load boundaries.
- Upgraded `SH_FogOverlay` from a flat tint to two-layer GPU procedural fog with independent flow directions, density wisps, texel-scaled edge distortion and separate base/highlight colors; no external noise texture or per-frame CPU allocation is required.
- Changed the low-frequency runtime fog mask to bilinear filtering and supplied explicit texel dimensions to the material, producing softened reveal edges while preserving zero Alpha in logically visible cells.
- Extended the P15 material generator and validator so applying Foundation Setup reproduces and verifies every fog-presentation parameter instead of relying on hand-edited material state.

### Post-P17 Visibility Validation Evidence

- Unity completed the forced C#/Shader refresh and returned idle outside Play Mode. The historical P15 Apply logged `[OriginCore P15] APPLY PASS`.
- P15 and P16 validators logged `[OriginCore P15] VALIDATION PASS` and `[OriginCore P16] VALIDATION PASS`; the final filtered Console contained 0 Error and 0 Warning.
- The historical `MAT_FogOverlay` tuning was accepted for that rejected planar experiment; the material and its shader have since been removed with the camera-capture minimap path.
- Per the established workflow, Codex does not enter Play Mode. Fog motion, density, edge quality and readability remain a user visual acceptance step in `90_SystemTest`.

This planar world-view presentation was rejected by the user as incompatible with the project's art direction and is no longer rendered by Main Camera. It remains in the history only; the following refinement supersedes it.

## Post-P17 Volumetric Fog And Direct RTS Camera Refinement (2026-07-14)

- Replaced the Main Camera's planar fog with `VolumetricFogRendererFeature` and `SH_VolumetricFog`: the pass reconstructs world rays from Camera Depth and performs 16 bounded samples through `VisibilityBounds`, with spatial density noise and height falloff.
- `VisibilitySystem` continues to update one bilinear three-state texture at 5 Hz, now publishing it and the XZ/Y volume bounds through global Shader properties. Target suppression, building memory, minimap and save/load logic remain unchanged.
- Installed exactly one active fog feature in Performant, Balanced and HighFidelity URP renderer data. `MAT_VolumetricFog` is the serialized tuning surface; the obsolete world plane and small-map camera no longer exist.
- Updated RTS camera behavior to fixed 60° tilt, zero Transposer/Composer damping and zero Lookahead. Edge motion is direct translation and minimap recenter is an immediate position jump without follow-turn animation.
- Extended P07/P15 generation and validation so rerunning setup reproduces the fixed-angle camera, camera culling masks, material values and all three renderer features.

### Post-P17 Volumetric/Camera Validation Evidence

- Unity compiled the Runtime/Editor changes and imported both Shader assets. P07 and P15 Apply each logged `APPLY PASS`.
- P07, P15 and P16 logged `VALIDATION PASS`; the final filtered Console contained 0 Error and 0 Warning. Serialized inspection confirmed fixed 60° tilt, zero RTS damping, Main Camera fog-layer exclusion, the volume material values and synchronized renderer-feature maps in all three quality renderers.
- Per the established workflow, Codex does not enter Play Mode. Volumetric appearance, GPU cost and direct-camera feel remain user visual acceptance steps in `90_SystemTest`.

## Post-P17 4x4 Command Panel, Typed Routes And Tool Retirement (2026-07-15)

- Rebuilt `P08_CommandPanel` as a 360x360 bottom-right 4x4 command grid. Move, Attack, Stop and Cancel occupy the complete first row; `ReservedCommandSlots` owns twelve visible but non-interactive placeholders for later formal commands or abilities.
- Removed the command panel's `Prompt`, `Queue` and `Feedback` scene objects plus the corresponding serialized fields, event subscriptions and runtime label updates. The entire panel area now belongs to command slots.
- Exposed a typed `ReservedCommandSlotsRoot` expansion seam. Reserved slots contain no `Button`, do not intercept raycasts and do not claim that deferred commands are implemented.
- Moved `P10_ProductionPanel` to Y=360 so its bottom edge remains directly above the enlarged command panel without overlap.
- Added `RtsRouteStyle` and styled route segments. Move and Rally render white dashed segments, Attack and AttackMove render red dashed segments, and ability commands must use `None` or omit the route-point interface.
- Replaced the single route LineRenderer with a construction-only segment renderer pool. Mixed Shift queues retain per-command colors while steady-state refresh continues to reuse lists, shared material and dashed texture without managed allocation.
- Deleted all former P01～P16 automation sources, the empty `OriginCore.Editor` assembly/asmref and the tool-dependent `IntegrationFoundationTests`. Existing test failure messages now point to scene-asset configuration instead of removed menus.
- Updated `RequirementsAddendum.md`, `Architecture.md` and `TestMatrix.md` with the direct-implementation maintenance policy. Later changes no longer update or execute phase Apply/Validator paths.

### Post-P17 Command Panel/Route Verification Evidence

- Unity MCP directly saved one `ReservedCommandSlots` root with twelve `ReservedCommandSlot` children into `90_SystemTest`; the old skill root and command-panel status text objects are absent.
- `OriginCore.Runtime`, `OriginCore.Tests.EditMode` and `OriginCore.Tests.PlayMode` compiled serially with 0 Error / 0 Warning. A forced Unity refresh/domain reload completed with 0 Console Error / 0 Warning.
- Verification is intentionally limited to compilation, serialized scene inspection and a clean Unity Console. Test Runner and Play Mode were not started.
- The remaining optional visual check is the rendered 4x4 panel shape plus white Move/Rally, red Attack/AttackMove and mixed Shift-queue segment colors.

## Post-P17 Resource Alignment, Slot Borders And Legacy UI Migration (2026-07-15)

- Kept `P10_ResourceHud` anchored at the top-right zero-offset edge and changed its `Resources` label from a left-anchored 576px rectangle to a full-container stretch using `MidlineRight` and zero margin.
- Added the same complete `Outline` contract to all four functional command buttons and normalized the twelve reserved-slot outlines. All sixteen cells now own four visible edges instead of relying on the background color of the first row.
- Copied three legacy command sprites and four legacy RTS ability sprites from `Assets/Resources` into `Assets/_OriginCore/Art/UI/Legacy`, creating independent GUIDs while leaving the old system untouched.
- Added 44x44 non-raycast `LegacyIcon` children to Move, Attack and Stop and retained their explicit text labels below the icons. Cancel remains text-only because no matching old command asset exists.
- Kept the four migrated ability sprites unreferenced: their old `Assembly-CSharp` behaviors were not imported into `OriginCore.Runtime`, so the 4x4 reserved command slots still cannot masquerade as implemented skills.
- Added `LegacyUiAssetManifest.md` and updated requirements, architecture and the test matrix with the migration boundary and provenance caveat.

### Post-P17 Resource/Border/Migration Verification Evidence

- Unity serialized one right-aligned resource label, sixteen slot outlines and three command-icon references that resolve only to `_OriginCore` GUIDs.
- The four ability sprites import as single Sprites under `_OriginCore` but have no scene or Runtime references.
- `OriginCore.Runtime`, `OriginCore.Tests.EditMode` and `OriginCore.Tests.PlayMode` compiled with 0 Error / 0 Warning; the final Unity Console query also returned 0 Error / 0 Warning.
- Verification remains limited to compilation, serialized structure and Unity Console cleanliness; Test Runner and Play Mode are not part of this change.

## Building-Aware Command Panel Polish (2026-08-19)

- Changed `RtsCommandPanelView` from a generic `SelectedCount > 0` Move rule to capability-based presentation. Move is shown only when the current selection contains a `UnitCommandQueue`; a pure `BuildingRuntime` selection hides it.
- Reflowed the reserved grid in building mode so the first valid production, research, build, deploy, promotion or weapon command occupies the vacated top-left cell instead of leaving a misleading Move command there. Unit selection restores the original first-row base-command layout.
- Added runtime-owned visual chrome without adding bitmap dependencies: a darker translucent panel, cyan outer edge, shadow, complete per-slot outlines, top accent strips, stronger typography, category colors and lower-contrast empty/disabled states.
- Live Unity inspection selected `Z_TransportBeacon_Start` and confirmed `moveActive=false`, selection count 1 and the first slot at `(12,-12)` displaying `PRODUCE / Produce Z.Worker`. The relevant two command-panel foundation tests passed and the final Unity Console contained 0 errors.
- A broader existing EditMode asset test still reports all four movable-unit Prefabs with serialized `NavMeshAgent.speed = 0` while their definitions expect 6/4.5/4.2/4. This is outside the command-panel change and was not modified here.

## Post-P17 Data-Driven Minimap Refinement (2026-07-15)

- Added `RQ-MAP-001` and replaced the camera-capture path with `MinimapMapDefinition`. `SO_SystemTestMinimapMap` owns a 256×256 static grid plus eight separately mapped obstacle/interaction regions, and can later reference a painted texture without changing runtime marker or input code.
- `MinimapView` now creates a UI fog overlay from `VisibilitySystem.FogTexture` and maps `EntityRegistry` entries to faction-colored UI markers using `MinimapBounds.WorldToNormalized`. Unit movement changes only its marker; Camera movement, zoom and culling never redraw the base map.
- Building memory remains simulation-owned but no longer clones world meshes. It records the last-known transform, faction and role, and the minimap draws a faded UI building marker while that memory is valid.
- Removed `P15_MinimapCamera`, `FogOverlayPlane`, five prefab `P15_MinimapMarker` meshes, `RT_Minimap.renderTexture`, `MAT_FogOverlay` and `SH_FogOverlay`. `90_SystemTest` now contains exactly one Unity Camera and the HUD RawImage no longer references a RenderTexture.
- Kept left-click behavior unchanged: normalized UI coordinates map to world XZ and directly reposition the bounded RTS Camera Rig only in RTS mode. The controlled Camera is an output of the interaction, never an input to map drawing.
- Updated the existing EditMode/PlayMode source contracts from the obsolete two-camera/RenderTexture assumptions to the single-MainCamera plus map-definition architecture; Test Runner was not started.

### Data-Driven Minimap Verification Evidence

- Unity MCP saved the map definition, scene bindings and five cleaned prefabs directly in the Editor. Serialized searches found no remaining `P15_MinimapCamera`, `FogOverlayPlane`, `P15_MinimapMarker` or retired asset GUID reference under `_OriginCore` runtime scenes/prefabs.
- Runtime, EditMode and PlayMode assemblies compile after the contract update. Verification remains limited to compilation, serialized-reference inspection and Unity Console cleanliness; Play Mode visual confirmation is intentionally left to the user.

## 2026-08-17 Design Update Implementation (2026-08-18)

- Implemented data-driven ACT action execution for Y's Timeslot Key, Sequence Key and Final Wing: 17/16/6 serialized actions, ground/air combos, input arbitration, phase priorities, movement, hit shapes, status effects, persistent cross-weapon actions and deterministic cleanup.
- Added Final Wing auxiliary targeting, four passives, target-only six-hit volleys, hold-to-repeat complete volleys, focus cancellation, two main-weapon-gated finishers and a single six-feather graybox presentation set on `PF_HeroY`.
- Added per-map height-aware visibility definitions/solver and retained the same authoritative three-state VisibilityGrid for world fog and the camera-independent minimap.
- Added isolated enemy production accounts, map setup/installer, deterministic weighted production, five-phase macro director, strategic targets, ordinary-unit-only command groups, independent enemy-hero target/attack behavior and schema-v3 save restoration.
- Corrected the Final Wing volley hit binding from the absent Channel phase to its existing Active phase after Unity's real asset validation exposed the mismatch.

### Design Update Unity Verification Evidence

- Unity 2022.3.62f3 completed refresh, compilation and Domain Reload. After the required Burst cache restart, Console contained 0 Error / 0 Warning.
- AssetDatabase loaded all three ActionSets and the ContentCatalog. All 39 action IDs are unique, all 15 combo references resolve, every ActionSet passes `TryValidate`, and the full Catalog passes `TryValidate`.
- `PF_HeroY` contains exactly one each of `FinalWingPresentationController`, `FinalWingTargetingController` and `FinalWingPassiveController`. A full AssetDatabase scan loaded all 39 `_OriginCore/Prefabs` assets (733 GameObjects) with 0 Missing Script.
- Bootstrap, MainMenu, LegacyMap and SystemTest opened successfully, remained non-dirty, contained 0 Missing Script and restored to `10_LegacyMap` after inspection.
- Both maps resolve through SceneCatalog to enabled Build Settings scenes and have independent minimap data, versioned runtime-sampled elevation, valid `FreeRecipes` enemy setup, stable producer ID/prefab and non-empty baked NavMesh triangulation.
- An in-memory Unity migration probe upgraded v2/dev-6 to v3/dev-7 while preserving all four ACT weapon IDs and an existing Crystal value, and initialized the new Enemy AI collection without inventing player property.
- Test Runner and Play Mode were not started for this maintenance pass. Per-action feel, macro-AI combat, visual high-ground behavior and real save replay remain explicit manual acceptance work, not silently claimed by the static checks above.

## Unified Sphere Character Presentation (2026-08-18)

- Audited all 18 character Prefabs under `Prefabs/Units`: none contained Animator or Animation. Existing action presentation was limited to procedural selection, hit flash, projectiles/combat displacement and Hero Y Final Wing visuals.
- Replaced every character body `VisualRoot` mesh with one built-in Sphere while retaining the existing Hero/Friendly/Enemy material. Removed the Hero-only head and forward graybox markers; selection indicators, world health bars, camera targets, collision and Gameplay components remain separate and unchanged.
- Added `MovementBobPresentation` to all 18 character roots. It moves only the body visual on local Y while RTS, direct-control or fallback transform displacement is active, returns to its baseline after stopping, freezes with scaled time and restores the baseline on Disable.
- Rebound Hero first-person hiding and every unit's `HitFlash` to the retained sphere Renderer so the visual replacement does not leave stale Renderer references.
- Added `AttackEffectPresentation` and shared transparent URP material `MAT_AttackEffect` to all 18 character Prefabs. Existing normal-attack, gun-shot, WeaponAction-start and action-hit events now drive a short faction-colored beam and impact pulse as the graybox substitute for attack animation.
- Attack effects are presentation-only: runtime children have no active Collider, use scaled time, and never call damage, hitbox, cooldown, resource, movement or command APIs. No Animator or AnimationClip was introduced.
- Added one `WeaponSkillVfxPresentation` to `PF_HeroY` and exhaustively mapped all 39 serialized Y WeaponAction IDs. Timeslot actions now use cyan sword arcs, dash trails, wave/rift/launch/slam shapes; Sequence actions use violet scythe arcs, five phantom scythes, throw/return and persistent orbit paths; Final Wing actions use six gold/white wing paths, rear/launch trajectories, a frozen hexagon and opposed finisher orbits.
- Hero Y's specialized WeaponAction visuals suppress the generic action beam while retaining actual-hit pulses. They are event consumers only, create runtime LineRenderers without Colliders, and clean up on every ActionEnded/Disable path; no animation asset or Gameplay timing was added.

### Sphere Presentation Verification Evidence

- Unity imported and compiled `MovementBobPresentation` with 0 Console Error. AssetDatabase validation reported 18/18 character Prefabs with exactly one non-selection body Renderer, Sphere mesh, one correctly bound bob component and no leftover Hero head/forward marker.
- A complete scan of all 39 `_OriginCore/Prefabs` reported 0 Missing Script. Loaded `10_LegacyMap` enemy instances inherited `MovementBobPresentation` from their Prefabs, and a Scene View capture confirmed the sphere body renders with its existing material.
- The first runtime movement probe entered Play Mode and issued a valid NavMesh destination, but sampled before Unity completed its transition and therefore did not prove visible bobbing. A follow-up probe was abandoned when the UnityMCP WebSocket stopped answering ping; Editor log contained only MCP reconnect warnings and no Gameplay exception. Manual movement appearance remains the outstanding acceptance check.

## Resource Crystal Visual Integration (2026-08-18)

- Renamed the newly supplied hash-named FBX to `SM_ResourceCrystal.fbx` and moved it under `Art/Models/Environment/Resources`, preserving its Unity GUID and `.meta` through AssetDatabase migration.
- Normalized the model importer to `globalScale = 100` and no animation. Extracted its four embedded 2048×2048 PBR textures, renamed them to `T_ResourceCrystal_BaseColor/Normal/Metallic/Roughness`, and packed Metallic + inverse Roughness into a URP-compatible `T_ResourceCrystal_MetallicSmoothness` without changing the supplied BaseColor.
- Copied the FBX material description into standard external material `MAT_ResourceCrystal`, assigned the extracted BaseColor/Normal/MetallicSmoothness maps, and remapped the model's source material to it. This uses the supplied purple crystal material while removing the accidental name-based dependency on the legacy Void building textures.
- Replaced the two cube placeholders in `PF_ResourceCrystal` with one nested `VisualRoot` model instance. Root identity, faction, `ResourceNode` and authoritative BoxCollider remain on the Gameplay Prefab.
- Static scene inspection confirmed all three `10_LegacyMap` CrystalNode instances inherit mesh `node_0` and `MAT_ResourceCrystal`, with 0 Missing Script and no scene save required.

## Gathering Beacon Visual Integration (2026-08-18)

- Renamed the newly supplied hash-named FBX to `SM_Z_GatheringBeacon.fbx` and moved it to `Art/Models/Buildings/Z`, preserving the model GUID through AssetDatabase migration.
- Extracted the FBX's own BaseColor, Normal, Metallic and Roughness textures into `Art/Textures/Buildings/Z/GatheringBeacon`, standardized them as `T_Z_GatheringBeacon_*`, and packed Metallic plus inverse Roughness into `T_Z_GatheringBeacon_MetallicSmoothness` for URP Lit without recoloring the supplied asset.
- Created `Art/Materials/Buildings/Z/MAT_Z_GatheringBeacon.mat` from the imported material description, assigned the extracted maps and remapped both known FBX source-material identifiers to it. Model dependency validation found no fallback reference to legacy `Assets/Art`.
- Replaced only the placeholder cube and top marker of `PF_Z_gather_beacon` with one nested `VisualRoot` model instance. Its visual bounds are approximately 3.99×3.73×3.98; the original 4×3×4 authoritative BoxCollider, root GUID, extraction/building components, selection indicator, health bar and rally/spawn anchors are unchanged.
- AssetDatabase validation reported one `node_0` Renderer using `MAT_Z_GatheringBeacon`, all standardized assets present, 0 Missing Script and 0 Console Error. A `PreviewRenderUtility` render confirmed the supplied purple mineral cluster and surrounding collector structure render correctly with their own material.

## Transport Beacon Visual Integration (2026-08-18)

- Identified the newly supplied hash-named file as a complete FBX with one mesh and four embedded PBR textures, renamed it to `SM_Z_TransportBeacon.fbx`, and moved it under `Art/Models/Buildings/Z` while preserving its Unity metadata.
- Extracted and standardized its BaseColor, Normal, Metallic and Roughness maps under `Art/Textures/Buildings/Z/TransportBeacon`; packed Metallic plus inverse Roughness into `T_Z_TransportBeacon_MetallicSmoothness` for URP Lit.
- Created `Art/Materials/Buildings/Z/MAT_Z_TransportBeacon.mat` from the supplied material description and remapped the FBX to it. The gray-white structure and light-blue core come from the supplied asset rather than the former friendly-color placeholder material, with no dependency on legacy `Assets/Art`.
- Replaced only the placeholder cube and top marker in `PF_Z_transport_beacon` with a nested `VisualRoot`. The resulting visual bounds are approximately 3.57×3.98×3.81; the Prefab GUID, 4×3×4 BoxCollider, `UnitSpawner`, `ProductionQueue`, `ResourceDropoff`, rally/building systems, selection indicator, health bar and anchors remain unchanged.
- AssetDatabase validation reported all standardized assets present, one `node_0` Renderer using `MAT_Z_TransportBeacon`, 0 Missing Script and correct model/material Prefab dependencies. `PreviewRenderUtility` confirmed the finished beacon renders correctly.

## Cohesion Beacon Visual Integration (2026-08-18)

- Identified the newly supplied hash-named file as a complete FBX with one mesh and four embedded PBR textures, renamed it to `SM_Z_CohesionBeacon.fbx`, and moved it under `Art/Models/Buildings/Z` through AssetDatabase.
- Extracted and standardized BaseColor, Normal, Metallic and Roughness under `Art/Textures/Buildings/Z/CohesionBeacon`; packed Metallic plus inverse Roughness into `T_Z_CohesionBeacon_MetallicSmoothness` for URP Lit.
- Created and remapped `Art/Materials/Buildings/Z/MAT_Z_CohesionBeacon.mat`, retaining the supplied gray-white metal, purple energy and surface detail with no fallback dependency on legacy `Assets/Art`.
- Replaced only the placeholder cube and top marker in `PF_Z_cohesion_beacon`. The new `VisualRoot` bounds are approximately 3.91×3.11×3.91; the Prefab GUID, 4×3×4 BoxCollider, `ProductionQueue`, `ProductionResourceConverter`, spawner/rally/building systems, selection indicator, health bar and anchors remain unchanged.
- AssetDatabase validation reported every standardized asset present, one `node_0` Renderer using `MAT_Z_CohesionBeacon`, 0 Missing Script and correct model/material Prefab dependencies. `PreviewRenderUtility` confirmed the completed cohesion structure renders correctly.

## Armory Visual Integration (2026-08-18)

- Identified the newly supplied hash-named file as a complete FBX with one mesh and four embedded PBR textures, renamed it to `SM_Z_Armory.fbx`, and moved it under `Art/Models/Buildings/Z` through AssetDatabase.
- Extracted and standardized BaseColor, Normal, Metallic and Roughness under `Art/Textures/Buildings/Z/Armory`; packed Metallic plus inverse Roughness into `T_Z_Armory_MetallicSmoothness` for URP Lit.
- Created and remapped `Art/Materials/Buildings/Z/MAT_Z_Armory.mat`, retaining the supplied gray-white structures, purple energy platforms and crystal details without fallback dependencies on legacy `Assets/Art`.
- Replaced only the placeholder cube and top marker in `PF_Z_armory`. The new `VisualRoot` bounds are approximately 3.89×3.54×3.96; the Prefab GUID, 4×3×4 BoxCollider, identity/vitals, building/construction systems, selection indicator and health bar remain unchanged.
- AssetDatabase validation reported every standardized asset present, one `node_0` Renderer using `MAT_Z_Armory`, 0 Missing Script and correct model/material Prefab dependencies. `PreviewRenderUtility` confirmed the completed armory renders correctly.

## Stable Rift Ground-Decal Integration (2026-08-18)

- Generated a top-down circular magical rift with the built-in ImageGen workflow: violet/lavender energy, pale-cyan runes, a dark swirling center and no environment or physical structure. The initially rendered checkerboard was detected as baked RGB rather than Alpha, so it was deterministically removed before Unity import.
- Saved the project asset as `Art/Textures/Buildings/Z/StableRift/T_Z_StableRift_MagicDecal.png` with a verified source Alpha channel, `alphaIsTransparency`, Clamp wrapping and mipmaps.
- Created `Art/Materials/Buildings/Z/MAT_Z_StableRift.mat` using URP Unlit Transparent with Alpha blending, double-sided rendering, ZWrite off and no shadow participation.
- Replaced the placeholder cube and top marker in `PF_Z_stable_rift` with a horizontal 4.8×4.8 Quad at Y=0.025. The Prefab retains its original GUID, `HeroSpawnAnchor`, building/construction state and zero Colliders.
- AssetDatabase validation reported correct texture/material dependencies, no old friendly-placeholder material, 0 Missing Script and a transparent 1024×1024 imported texture. `PreviewRenderUtility` confirmed the flat magical portal reads correctly from an angled RTS view.

## Turret Visual Integration (2026-08-18)

- Identified the newly supplied hash-named file as a complete single-mesh FBX with four embedded PBR textures, renamed it to `SM_Z_Turret.fbx`, and moved it under `Art/Models/Buildings/Z` through AssetDatabase.
- Extracted and standardized BaseColor, Normal, Metallic and Roughness under `Art/Textures/Buildings/Z/Turret`; packed Metallic plus inverse Roughness into `T_Z_Turret_MetallicSmoothness` for URP Lit.
- Created and remapped `Art/Materials/Buildings/Z/MAT_Z_Turret.mat`, retaining the supplied gray-white floating weapon body, circular base and purple energy details with no legacy `Assets/Art` dependency.
- Replaced only the placeholder cube and top marker in `PF_Z_turret`. The new `VisualRoot` bounds are approximately 3.95×3.35×3.67; the Prefab GUID, 4×3×4 BoxCollider, `AttackCapability`, `AutoTargetScanner`, building/construction systems, vitals, selection indicator and health bar remain unchanged.
- AssetDatabase validation reported every standardized asset present, one `node_0` Renderer using `MAT_Z_Turret`, 0 Missing Script and correct model/material Prefab dependencies. `PreviewRenderUtility` confirmed the completed turret renders correctly.

## Sky Altar Visual Integration (2026-08-18)

- Identified the newly supplied hash-named file as a complete low-profile FBX with one mesh and four embedded PBR textures, renamed it to `SM_Z_SkyAltar.fbx`, and moved it under `Art/Models/Buildings/Z` through AssetDatabase.
- Extracted and standardized BaseColor, Normal, Metallic and Roughness under `Art/Textures/Buildings/Z/SkyAltar`; packed Metallic plus inverse Roughness into `T_Z_SkyAltar_MetallicSmoothness` for URP Lit.
- Created and remapped `Art/Materials/Buildings/Z/MAT_Z_SkyAltar.mat`, retaining the supplied gray-white rings, purple runes and central star details with no legacy `Assets/Art` dependency.
- Replaced only the placeholder cube and top marker in `PF_Z_sky_altar`. The new `VisualRoot` bounds are approximately 3.94×0.35×3.95; the Prefab GUID, 4×3×4 authoritative BoxCollider, identity/vitals, building/construction systems, selection indicator and health bar remain unchanged.
- AssetDatabase validation reported every standardized asset present, one `node_0` Renderer using `MAT_Z_SkyAltar`, 0 Missing Script and correct model/material Prefab dependencies. `PreviewRenderUtility` confirmed the completed altar renders correctly.

## Terrain Analysis Lab Visual Integration (2026-08-19)

- Identified the newly supplied hash-named file as a complete single-mesh FBX with four embedded PBR textures, renamed it to `SM_Z_TerrainAnalysisLab.fbx`, and moved it under `Art/Models/Buildings/Z` through AssetDatabase.
- Extracted and standardized BaseColor, Normal, Metallic and Roughness under `Art/Textures/Buildings/Z/TerrainAnalysisLab`; packed Metallic plus inverse Roughness into `T_Z_TerrainAnalysisLab_MetallicSmoothness` for URP Lit.
- Created and remapped `Art/Materials/Buildings/Z/MAT_Z_TerrainAnalysisLab.mat`, retaining the supplied gray-white ring structures, purple energy and crystal details without any legacy `Assets/Art` dependency.
- Replaced only the placeholder cube and top marker in `PF_Z_terrain_lab`. The new `VisualRoot` bounds are approximately 3.95×2.76×3.95; the Prefab GUID, 4×3×4 BoxCollider, Adaptive Enhancement content binding, building/construction systems, vitals, selection indicator, health bar and anchors remain unchanged.
- AssetDatabase validation reported every standardized asset present, one Renderer using `MAT_Z_TerrainAnalysisLab`, 0 Missing Script and correct model/material Prefab dependencies. `PreviewRenderUtility` confirmed the completed analysis structure renders correctly.

## Explicitly Deferred

- Formal character/weapon models, animations, authored VFX assets, audio, icons and licensed final presentation assets; current weapon-skill VFX is the implemented programmatic presentation layer
- Final weapon timing, hitbox, economy/refund and balance values; formal grenade and firearm content built on the existing runtime framework
- Formal maps, objectives, story, enemy content, bosses, multiplayer, progression and final balance
- Enemy-hero skill/retreat policy and final macro-AI production thresholds/ratios

## Black Fog Of War And Runtime World Boundaries (2026-08-20)

- Replaced the former 16-step volumetric raymarch in `SH_VolumetricFog` with a single-sample black fog-of-war pass. The pass reconstructs the visible surface from Main Camera Depth, samples the existing Visibility texture in world XZ, and returns transparent for sky or geometry outside the authoritative map bounds. This removes the camera-dependent visual offset without changing selection, target suppression, building memory, minimap, or save semantics.
- Hidden and Explored now render fully black while Visible remains clear. `VolumetricFogRendererFeature` retains its serialized class name for compatibility with the three existing URP Renderer Data assets, but its runtime pass is now `BlackFogOfWarPass` and the material resolves to `Hidden/OriginCore/BlackFogOfWar` without Shader errors.
- Added `WorldBoundaryWalls`: four invisible solid BoxColliders are generated at scene root and aligned to the same bounds used by Visibility. Match loading applies `MapDefinition.WorldBounds` to both systems; direct system-test play falls back to the scene Visibility collider. Walls extend 100m above and below the bounds and contain no Renderer or save state.
- Added a URP `DepthOnly` pass and direct world-XZ Visibility sampling to `SH_AnimeGrassTerrain`. The direct path is independent of whether a quality renderer uses CopyDepth or DepthPrepass, so hidden ground and standard Lit entities now consume the same texture instead of only units/buildings receiving the mask.

## Hero Y Required Signature Weapons (2026-08-20)

- Added ordered `HeroDefinition.RequiredActWeapons` and assigned `SO_Hero_Y` to Timeslot Key, Sequence Key and Final Wing.
- ACT configuration slots 1～3 are canonical and reserved. MatchSetup labels all three `REQUIRED`, offers no empty/replacement choice, and excludes all required weapons from slot 4.
- `MatchConfiguration` normalizes menu, external and restored configurations before validation: the three signature weapons are inserted in declaration order, duplicates are removed, and at most one other unique weapon retains its original order. `WeaponInventory` still separates Final Wing into its Auxiliary slot after loading.
- Unity compilation and Catalog validation passed with 0 project Error. Configuration normalization produced `timeslot-key | sequence-key | final-wing | gun`; inventory loading kept Timeslot/Sequence/Gun as main-hand choices and routed Final Wing to Auxiliary. A MainMenu runtime probe confirmed all first three slots show the matching `REQUIRED` choice without `EMPTY`, while slot 4 offers no required weapon.

## Options Binding Contrast and Thin-Glyph Repair (2026-08-20)

- Added a dedicated Input Binding row theme: pale row background with dark action text, and a deep-blue Rebind button with bold white binding text and cyan inset border.
- Confirmed the active `F_NotoSansSC_SDF` asset is generated from the Thin face; uppercase `I` exists but its 90-point glyph width is only 3.328. Screen UI text now receives synthetic Bold so narrow Latin glyphs remain visible at the current 13～17px sizes.
- MainMenu runtime screenshot confirmed `WINDOW MODE`, `OPTIONS` and `INPUT BINDINGS` render complete letters, while action and binding labels have clearly separated luminance. Unity compilation and final Console check reported 0 project Error.

## Known Limitations

- The deliverable contains complete configurable Runtime/data paths but still uses graybox presentation where formal art/audio/licensing input is absent.
- Visibility remains a low-frequency XZ authority grid, filtered by per-map height/occluder sampling. The URP black surface mask is presentation driven by that grid and is not a physically simulated atmosphere.
- Save supports schema v3 with explicit v1→v2→v3 migration. Unknown fields are ignored and unknown future schema versions are rejected; a general migration registry is not implemented.
- Only Windows x86_64 Mono Development Build is acceptance-tested. IL2CPP, Release and other platform builds are not claimed.
- English placeholder strings remain in parts of the UI, but the shared runtime TMP font now supports Simplified Chinese through `F_NotoSansSC_SDF`; final localization copy and the font source/license record still require release-content review.
- MCP for Unity may emit a transient Editor-only WebSocket reconnect warning during domain reload; it does not enter Player. The accepted Development Player log was clean.
- Existing legacy code/assets and user-owned uncommitted changes outside `_OriginCore` remain outside this refactor boundary and were not deleted or reformatted.

## P00 Acceptance

- [x] ProjectVersion, render pipeline, relevant package versions, and compilation/Console baseline recorded.
- [x] Existing overlapping systems and user-owned uncommitted changes identified.
- [x] No non-document Gameplay asset modified by P00.

## Unified UI Theme (2026-08-19)

- Added persistent runtime `OriginCoreUiTheme` to bring the main menu, play/story/map/setup pages, options, gameplay HUD, pause menu and dynamically generated controls into one light magical sci-fi visual system with pale crystal surfaces, dark blue-violet type and cyan/violet energy accents.
- Added semantic action accents, consistent outlines, top highlights, typography treatment and interaction states without changing layout anchors or input behavior.
- Protected health/shield fills, minimap/fog, crosshair, selection graphics, icons and the RTS command panel's command-category styling from global recoloring; world-space canvases only receive health-bar background/frame treatment.
- Runtime verification covered MainMenu, MatchSetup, LegacyMap HUD, Pause and both Options instances. Theme singleton count was 1 in each scene; all sampled controls were styled and Console reported 0 project Error.

### Asset diagnostic resolution

- Confirmed the serialized YAML itself was intact and isolated the zero-speed EditMode state to editor-lifetime RuntimeStat cache drift.
- `RuntimeStatBlock.OnValidate` now invalidates `_initialized`; `NavMeshMovementDriver` uses static UnitDefinition values for editor serialization and RuntimeStat values only while playing.
- AssetDatabase verification now reports 18 movable Prefabs and 0 mismatches. The previously failing foundation test passes, and LegacyMap runtime agents retain their expected non-zero speed.

## RTS Initial Focus, Stable Building Slots and Turret Combat (2026-08-19)

- `RtsCameraController` now resolves the active Friendly base deterministically, preferring operational spawn/production/dropoff buildings, and immediately centers the rig above it. Initial pointer state is sampled before edge panning is armed so the Input System default `(0,0)` cannot drag the camera away during scene startup.
- Building/resource selection no longer compacts the command grid when Move is unavailable. The Move cell remains empty and all remaining commands retain their stable 4×4 positions.
- `PF_ResourceCrystal` now carries the `Building` role while remaining Neutral, non-selectable and free of movement/command components.
- The turret failure was traced to three independent gaps: zero range/damage data, no component consuming `AutoTargetScanner`, and a non-operational placed LegacyMap instance. `TurretAutoAttackController`, a provisional Range 10 / Damage 10 / Cooldown 0.5 definition and an operational map instance now close the normal Faction → AttackCapability → DamageReceiver pipeline; attack presentation remains effect-only.

## Base Production and Rally Overlay Reliability (2026-08-19)

- Confirmed the normal Bootstrap → configured Legacy match path: selecting `Z_TransportBeacon_Start` exposes an interactable Produce command, spends 30 Crystal / reserves 1 Influence, completes the ten-second order and creates one `z.w` Worker.
- Fixed the direct-Gameplay-scene timing gap by making `UnitSpawner` retry its `ContentCatalogService` binding at archetype resolution time. A `SceneBootProxy`-created AppRoot may initialize after the spawner's `Awake`; production no longer becomes dependent on that ordering.
- Added `MAT_RallyMarkerOverlay` using `SH_CommandQueueOverlay` and assigned it to `PF_RallyMarker`; the marker uses render queue 5000, sorting order 96, no dynamic occlusion and a slightly raised visual root so terrain cannot hide the rally destination.

## UI Pixel Alignment, CJK Commands and Unit-Priority Box Selection (2026-08-19)

- Enabled pixel-perfect rendering on every screen-space Canvas and replaced outward uGUI outlines with reusable four-sided inset borders. Panels and adjacent 4×4 command cells now retain all sides when touching the viewport edge or scaling to a non-integer resolution.
- Migrated the existing Noto Sans SC variable font into `_OriginCore`, created a Dynamic TMP SDF resource, and bound all themed/static/dynamic command text to it. Runtime inspection reported 16/16 command-panel labels using `F_NotoSansSC_SDF`; Chinese ability names rendered without tofu boxes.
- Simplified command-cell labels and moved target, range, cooldown, energy, resource cost, charge, queue, research, weapon and construction details into the bottom-center `INFORMATION` frame through pointer/focus relays.
- Changed the former selection-summary frame to display entity vitals by default and temporary command details on hover/focus; it is no longer described or presented as a selection box.
- Box selection now filters preview and commit to units whenever any unit lies inside the rectangle, falling back to buildings only when no unit is present. A full-screen mixed runtime probe returned one unit and zero buildings in both preview and committed selection.
- Unity compilation completed with 0 project errors. LegacyMap visual verification confirmed 3/3 screen canvases pixel-perfect, all command labels using the CJK font, visible inset cell borders and the command detail frame. The editor was returned to stopped `00_Bootstrap`.

### Slider track endpoint correction

- Corrected the Options sliders' mismatched geometry: the visible Background previously spanned 300 reference pixels while FillArea and HandleArea spanned 284, exposing an eight-pixel pale segment at the left edge even at full value.
- `OriginCoreUiTheme` now aligns each Slider Background horizontally to its FillArea while preserving the serialized vertical track thickness and handle travel range. The rule applies consistently to Brightness, Master Volume and Crosshair RGB sliders in MainMenu and Pause Options.

## LegacyMap Manual Selection Fix (2026-08-19)

- Fixed `MatchSetupView` treating the entry `preferredMapId` as a permanent lock. Selecting Legacy Terrain previously assigned `map.legacy`, then `EnsureDefaults` immediately restored the hard-coded `map.system-test-0` preference.
- A manual map choice now clears the one-shot preferred value before Commander/Hero defaults are recalculated. The selected map therefore remains authoritative for the resulting `MatchConfiguration`.

## LegacyMap Navigation, Teleport and Construction UX (2026-08-19)

- Added a shared adaptive `NavMeshPositionResolver` and routed normal movement, RTS ground commands, ability teleport and weapon-action teleport through reachable-point validation. Failed teleports no longer strand an Agent outside the NavMesh, and weapon teleports now perform a true position transfer instead of a collision-constrained long `CharacterController.Move`.
- Rebuilt LegacyMap navigation from the actual Ground coverage. The sampled Ground grid improved from 594/1072 points near NavMesh to 1072/1072, with all sampled points returning a complete path from the gameplay start region.
- Building validation separates the placement center from the worker's movement destination. Workers choose a reachable point outside the footprint, while Gathering Beacon placement snaps to a live Crystal node. The former 4×4 footprint was superseded on 2026-08-19 by the smaller node-matched footprint described below.
- Added a two-level worker build UI: the primary page exposes only `BUILD`; the secondary page exposes seven building names plus `BACK`. Dynamic labels use the shared CJK font, bold centered text, a readable 13–17 auto-size range and ellipsis fallback. Costs and state remain in the information frame.
- Added live construction presentation to the bottom information frame. A selected construction site reports percentage and remaining time and drives a dedicated in-panel progress bar; nothing is added to the world-space health bar.
- `BuildingRuntime` now supplies a missing runtime `Selectable` contract for legacy/incomplete building Prefabs. A runtime probe successfully placed a Gathering Beacon, selected it, and observed `50% / 5.0s remaining` with progress fill `0.50`.
- Updated the RTS selection test drag origin because the intentionally edge-aligned minimap now occupies `(5,5)`; drag selection starts immediately to its right and still covers every candidate used by the test.
- Unity compilation completed with 0 project errors. Targeted EditMode suites passed 16/16 and targeted PlayMode movement, production, selection and attack-move suites passed 9/9.

## Building Preview, Construction Particles and Initial Hero Deployment (2026-08-19)

- Added a runtime-only building placement ghost driven by the same placement validation used for command submission. It copies render meshes and footprint only, uses cyan/green for valid positions and red for invalid positions, and is hidden on UI hover, cancel, confirm, mode exit or missing world hits.
- Confirmed the BuildCommand pipeline still spends resources and moves the worker to a reachable point outside the footprint before `Builder.TryPlace` creates the ConstructionSite. No building is created while movement remains in progress.
- ConstructionSite now creates a white/cold-white footprint particle system when construction starts, emits continuously throughout progress, stops emission on completion while existing particles fade, and clears/releases runtime resources on teardown.
- New LegacyMap matches no longer spawn Y immediately. The original `Z_TransportBeacon_Start` host assignment was corrected on 2026-08-19: `HeroDeploymentController` is now hosted only by the friendly Stable Rift, advances for the data-defined 240 seconds, pauses with scaled time, and exposes its percentage/remaining time through the selected rift's INFORMATION progress bar.
- Deployment completion spawns `PF_HeroY_MatchHero` at the friendly Stable Rift anchor and only then binds possession. A runtime accelerated probe observed `activeHero=false/pending=true/total=240` before completion and `activeHero=true/pending=false` after completion, with the spawned hero within 0.30 world units of the anchor.
- Added schema-v3-compatible hero-deployment fields and a restore-order-155 participant so pending state, remaining time and host Runtime ID survive save/load without duplicate hero creation.
- Unity 2022.3.62f3 full asset refresh compiled with 0 project Error. Runtime probes confirmed the placement preview had active render geometry, construction particles were white and playing during construction, emission stopped at completion, and Console contained 0 project Error after the final particle configuration fix.

## Gathering Footprint and Tiered Build Commands (2026-08-19)

- Reduced `PF_Z_gather_beacon` visual bounds to approximately 2.4×2.25×2.4, its BoxCollider to 2.4×2.4 XZ and `SO_Building_Z_gather_beacon` footprint to 2.4×2.4. The result is close to the Resource Crystal's approximately 2.24×1.75 XZ bounds instead of the former 4×4 structure.
- Reworked extractor placement into two phases: resolve the clicked non-depleted Crystal node with a height-tolerant collider query, then validate blockers and navigation from the node root position. A runtime probe clicked the collider top at Y=2.48, resolved the node root at Y=0.08 and produced a valid reachable approach point.
- Split the worker's catalogue into bottom-row `BASIC BUILD` and `ADVANCED BUILD` entries. The basic submenu contains five original basic structures and bottom-aligns across its last six slots including BACK; the advanced submenu contains Sky Altar and Terrain Analysis Lab in the last three slots including BACK.
- Unavailable building commands remain clickable and report their exact state in the INFORMATION frame. A runtime probe clicked the locked Sky Altar and received `This building is still locked` instead of a silent no-op.
- End-to-end LegacyMap probing issued Gathering Beacon construction from a click on the mineral top, moved the worker to the resolved approach point, consumed the worker only after arrival, created the site at the node root and started construction particles. Unity compilation completed with 0 project Error.

## Stable Rift Hero Queue Ownership (2026-08-19)

- Restricted initial and restored hero deployment hosting to an operational friendly `z.building.stable_rift`. A saved Transmission Beacon Runtime ID is no longer accepted as a deployment host.
- Kept the Stable Rift at zero Colliders and added a 36-pixel screen-space selection fallback for friendly colliderless entities. This makes the flat rift selectable without exposing its 1-HP untargetable representation to combat or navigation physics.
- Extended the INFORMATION panel to read a selected Transmission Beacon's `ProductionQueue`. Active Worker production now displays percentage, remaining seconds, queue count and the shared progress bar; the base no longer displays Hero deployment state.

## RTS Unit Hover Indicator (2026-08-19)

- Added one runtime `UnitHoverIndicatorView` owned by `SelectionService`. It builds a 16-segment cyan dashed ring once, follows the hovered unit's collider footprint and rotates at 82 degrees per unscaled second.
- Hover ray resolution accepts living Hero, Worker, Combat and Building roles while leaving `Selectable.State` untouched. Buildings and mineral nodes now receive the same footprint-scaled preview; the colliderless Stable Rift uses the existing screen-space fallback. UI hover, drag selection, command targeting, non-RTS modes and entity removal hide the indicator immediately.
- Faction relation drives the single material tint: allied and neutral targets remain cyan, while hostile units and buildings use a red dashed ring.
- The presentation uses the existing command-overlay shader, creates no Collider and is excluded from entity registration and save data.

## Building Command Capability Filtering (2026-08-19)

- Building-only selections now hide Move, Attack and Stop while preserving their fixed 4×4 cells. Dynamic production, research and other building commands do not shift into the vacated positions.
- Attack is interactable for units only when a selected `UnitCommandQueue` owns an `AttackCapability`; Stop is interactable only when a selected unit owns a command queue.
- Turrets retain their existing operational auto-target attack pipeline and do not expose manual Attack/Stop buttons that the building cannot execute.
- Placement remains footprint/blocker plus reachable-worker-approach validation rather than a global flatness constraint. Production maps should provide locally flat authoritative collision surfaces under buildable footprints; the rest of the terrain may remain uneven.

## Anime Grass Flow Test Map (2026-08-20)

- Added `11_AnimeGrassFlowTest.unity` as a selectable, build-enabled map without modifying LegacyMap. It contains 285 normalized 4×4 terrain Prefab instances and exercises all six current Anime Grass module types.
- The layout starts above the southwest Transmission Beacon/Stable Rift base, provides two nearby Crystal nodes, a central expansion and first enemy pair, a ramped northern high ground, two high-ground defenders and an automatically installed enemy producer. Five total Crystal nodes cover opening, expansion, side-route and high-ground gathering checks.
- Added `SO_Map_AnimeGrassFlowTest`, a generated square minimap definition and a dedicated EnemyAiMapSetup. SceneCatalog, ContentCatalog and Editor Build Settings include the new scene/key.
- Added map-scoped initial-Hero and unit-production time overrides. This test map resolves Y's initial deployment and every friendly/enemy ProductionRecipe to 1 second while all existing maps keep their Definition values; building construction and technology research are unchanged.
- Editor validation reported 0 missing scripts, 0 broken Prefabs and a clean scene. The baked NavMesh contains 126 triangulation vertices; base-to-high-center and base-to-high-side probes both returned `PathComplete`.
- Bootstrap runtime validation successfully started the configured Z + Y match, installed one EnemyAiDirector, spawned its producer on northern high-ground NavMesh, completed the 1-second Hero deployment, queued a Worker with `Effective=1.00 / Remaining=1.00`, and produced exactly one Worker with an empty queue. The final runtime Console contained 0 Error.

### Asset gaps exposed by the map

- P0: an authored enemy production building. Enemy AI still uses the generic `PF_ProducerBuilding` placeholder, so faction/readability is weaker than the completed Z building set.
- P0: map perimeter/void presentation pieces—outer cliff caps, underside/end caps and a background ground/sky treatment. The playable rectangle currently ends abruptly.
- P0: a wider straight ramp or ramp-edge pair. The current 4 m ramp is navigable but is a visible and tactical bottleneck for four-unit production batches.
- P1: two or three Flat tile visual variants plus grass tufts, flowers, small rocks and edge dressing. The current single flat source repeats clearly over a 76×60 map.
- P1: path/road and build-zone decals, resource-site dressing and one or two large landmark props for base, expansion and enemy high ground readability.
- P1: an Anime Grass lighting/skybox/volume profile and cliff-wall material variation; the cloned Legacy lighting works functionally but does not finish the new terrain kit's presentation.
- P2: authored minimap topology art and decorative map-border graphics. The current minimap is correctly camera-independent and functional, but intentionally schematic.
- Unit/hero character models and animation are not counted as missing map assets because the current approved presentation intentionally uses spheres and effect-only attacks.

### Non-asset content gap

- The map currently validates economy, production, construction, navigation, mode switching and enemy pressure, but its MapDefinition has no MissionDefinition. A victory/defeat objective and matching enemy-base objective contract are still required before calling it a complete playable mission rather than a flow test map.

## Energy Sanctuary Special Building (2026-08-20)

- Added `PF_Special_EnergySanctum` as a neutral, selectable special landmark in `11_AnimeGrassFlowTest`, positioned on the flat central route at `(0, 0.36, 3)` with a non-blocking trigger footprint.
- Generated and standardized `T_Special_EnergySanctum_MagicCircle.png`; `M_Special_EnergySanctum_Ground` uses `OriginCore/Energy Sanctuary Ground` to convert the uniform black background into luminance-derived transparency while preserving cyan, lavender and white-gold detail on pale terrain.
- `EnergyRestorationSanctum` implements `IIncomingDamageModifier` and returns zero incoming damage through the existing `DamageReceiver` pipeline. It performs a 0.1-second `OverlapSphereNonAlloc` scan, deduplicates `VitalsComponent` instances and fills Energy only for living non-building units inside 3.25 m.
- Unity scene validation reports 0 issues, 0 missing scripts and 0 broken Prefabs. A Play Mode runtime probe restored one unit from 0/100 to 100/100 Energy; 9999 attempted damage applied 0 and left the sanctuary at 1/1 Health. Final compilation and Console check reported 0 project Error.

## Runtime ACT/FPS Camera Rebinding (2026-08-20)

- Fixed ACT/FPS virtual cameras retaining null targets after the initial Hero changed from a scene object to Stable Rift runtime deployment. `GameModeController` now forwards every `PossessionService.PawnChanged` event to the active `CameraModeCoordinator` and performs an immediate synchronization whenever a scene is bound.
- ACT Follow/LookAt now resolve to the spawned Hero's `ActCameraTarget`; FPS Follow resolves to `FpsCameraTarget` with no LookAt target. Scene unbind and shutdown clear stale references, and Cinemachine cached states are invalidated on every rebind.
- Added PlayMode regression assertions for all four target contracts. `GameModePlayModeTests` passed 2/2. A Bootstrap-started Anime Grass match reached Running with `PF_HeroY_MatchHero`; runtime inspection reported `ACT_FOLLOW=True`, `ACT_LOOK=True`, `FPS_FOLLOW=True`, and `FPS_LOOK_NULL=True`.

## ACT Launch, Flight and Glide Repair (2026-08-20)

- Replaced the former NavMesh launch path that discarded vertical velocity with a complete parabolic Launch/LaunchPair displacement. Runtime Agent ownership is captured and restored, including `updatePosition`, `nextPosition` and the previous Stop state; cancellation and teardown use the same landing cleanup path.
- Extended `FlightController` with available/active separation, idle glide descent, a 0.3-second Space double-tap toggle and transient-input cleanup. ACT and FPS share the same behavior: Space ascends, Crouch descends, no vertical input glides, and double Space cancels or re-enters flight without also firing a normal jump.
- Added `ActAbilityPlayModeTests`; both launch-arc/landing and glide/double-Space cases passed 2/2. A live Anime Grass probe launched `Enemy_01` from Y=0.417 to Y=4.063 and returned it to Y=0.417 while keeping the Agent on NavMesh and restoring `updatePosition=True`.

## RTS Bottom-Row Skills and Range Preview (2026-08-20)

- Moved the four logical RTS ability slots to the bottom row of the 4×4 command panel and added an explicit visual-slot-to-ability-slot mapping, so Q/W/E/B still activate indices 0/1/2/3 after the layout change.
- Replaced per-skill button names with a uniform `SKILL` label. The selected/hovered command now reports the real ability name plus shortcut, target type, cast range, effect radius, cooldown, Energy, resource cost and current readiness in the bottom INFORMATION panel.
- Added one runtime `AbilityRangePreview` owned by `RtsCommandIssuer`. Ability targeting shows a blue caster-centered cast-range ring and a cursor/entity-centered effect ring; valid targets are gold and invalid, hidden or out-of-range targets are red. The preview uses the existing terrain-through overlay shader and is cleared on every targeting exit or input/mode suppression path.
- Unity compilation completed with 0 project Error. Existing RTS movement/selection PlayMode regressions passed 6/6. An editor runtime probe against `PF_HeroY` reported visual slots `8/9/10/11 -> ability 0/1/2/3`, labels all `SKILL`, Q/W/E/B in the INFORMATION details, and a visible `OriginCore/Command Queue Overlay` preview that preserved cast range 8, effect radius 3 and hid cleanly.

## ACT Finisher and Visibility Radius Baseline (2026-08-20)

- Doubled both Final Wing finisher hit volumes: Frozen Hexagon is now Radius 16 / Size 32×32×16 for both control and recovery, while Opposed Orbit is Radius 20 / Size 40×40×20 for pull and final damage. Damage formulas and target/status semantics are unchanged.
- Final Wing finisher VFX now resolves its outer radius from the action's serialized Hit definitions instead of retaining a separate approximately 3 m hard-coded visual size.
- Doubled every UnitDefinition vision value, including buildings and generic fixtures. The resulting values are 20 for former 10 m definitions, 24 for former 12 m definitions, 32 for the generic 16 m Hero, and 40 for Z.E. All serialized VisionEmitter overrides remain disabled (`-1`), so ACT, FPS, RTS, volumetric fog and minimap visibility consume the same expanded data.
- Unity asset probing confirmed Final Wing radii 16/20, all 27 UnitDefinitions in the expected 20/24/32/40 groups, and zero Prefab-local vision overrides. Visibility EditMode tests passed 3/3 and PlayMode tests passed 3/3 after replacing the old fixed-distance fixture assumption with a data-driven move of every friendly emitter outside the tested target's expanded vision radius.
