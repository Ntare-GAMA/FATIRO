# Fatiro AR — Experimental Housing Structures Simulator

Mobile AR simulation (Android, ARCore) in which construction learners assemble houses (biomimetic animal and car houses, and Rwandan classics) on a sloped hillside placed on a real table, then load-test them with earthquakes, wind, rain and occupancy. Members that exceed their capacity fail and bring down whatever rests on them; the learner reinforces the design and tests again.

*Urufatiro* is Kinyarwanda for "foundation".

## Designs

Six houses, built component by component (fast build: one card raises a whole component such as "Ground-floor walls" or "Roof sheeting"). Each is built as a hologram and revealed as the real house when the test bench opens.

**Biomimetic**: the animal or object is the building's outside, a curved ferrocement shell with bands of glass, over a real concrete and brick home with floors and rooms.

| Design | The house | Fails when… | Fixed by… |
|---|---|---|---|
| **Ingwe** (PN-01) | A black panther crouched down the slope: concrete rib arches on brick paws, split-level floors, a viewing room in its raised head | Earthquake topples unbraced ribs like dominoes; people overload the head cantilever | Flank braces; foreleg props |
| **Inzovu** (EL-01) | An elephant on four concrete legs: a home on the belly floor under a domed shell, ear canopies, a trunk downpipe | Wind rips off the ear canopies; the trunk overflows and the roof floods | Ear tie-downs; a steel trunk |
| **Imodoka** (CR-01) | A house that is a sports car, on raked concrete stilts: kitchen behind the windscreen, living room, bedroom | Earthquake buckles the long downhill stilts | Downhill X-braces and sway braces |

**Rwandan classic**: reinforced-concrete frames with fired-brick infill, real window and door openings, on stepped brick foundation walls.

| Design | The house | Fails when… | Fixed by… |
|---|---|---|---|
| **Urugo** (BG-01) | Single-storey family home with an iron-sheet roof | Wind lifts the nailed iron sheets off | Storm straps |
| **Igorofa** (TS-01) | Two-storey house with a concrete stair, clay-tile roof and a balcony | A crowd on the balcony overloads its cantilevers | Balcony props |
| **Amacumbi** (AP-01) | Three storeys of flats over open parking | Earthquake: the open parking floor is a soft storey | Ground-floor shear walls |

Every house stands on firm ground: a compacted building platform cut into the slope, concrete footings or brick piers, and brick plinth or foundation walls that tie the footings together and add no shaking mass.

## Requirements

- Unity **6000.4.7f1** with Android Build Support (SDK, NDK, OpenJDK)
- An ARCore-supported Android phone running Android 10 (API 29) or later

## Getting started

1. Open the project in Unity 6000.4.7f1 and let packages resolve.
   Import the Unity Asset Store packages listed in [CREDITS.md](CREDITS.md) from **Window › Package Manager › My Assets** (they are not in the repository; without them the app falls back to plain shapes and colours).
2. Run **Fatiro › Build Project Content**. This generates materials, synthesised audio, prefabs, the six design assets, their fit-outs (furniture and details), the Addressables groups, the scene (`Assets/Fatiro/Scenes/Fatiro.unity`) and the Android/XR/URP settings. It is safe to re-run.
3. Press Play to try the flow in the editor: without an AR device, a virtual floor stands in for plane detection (mouse: click/drag; scroll to scale; right-drag to rotate).
4. Run **Fatiro › Build Android APK** to build Addressables and `Builds/Android/FatiroAR.apk`.

Batch-mode equivalents:

```bash
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Fatiro.EditorTools.FatiroProjectSetup.BuildEverything
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Fatiro.EditorTools.FatiroProjectSetup.BuildAndroid
```

## Architecture

```
Assets/Fatiro/Scripts
├── Domain/      Fatiro.Domain — pure C#, no Unity references
│   ├── Model/        MemberSpec, HouseBlueprint, SiteConditions, SiteLayout (slope geometry)
│   ├── Members/      StructuralMember + Foundation/Column/Beam/Arch/Cantilever/Brace/Panel/Drain,
│   │                 ConnectionGraph, MemberFactory, Calibration
│   ├── Loads/        ILoadCase strategies (Dead/Live/Wind/Rain/Seismic), StructuralSolver
│   ├── Building/     BuildSequence (stage order and prerequisite rules)
│   ├── Scoring/      IScoringPolicy, WeightedScoringPolicy (40/40/20)
│   ├── Session/      SessionRecord (DMP schema), LearnerProfile
│   └── Authoring/    Blueprints for the three designs, MaterialLibrary
├── Runtime/     Fatiro.Runtime — Unity layer
│   ├── Bootstrap/    AppBootstrap composition root (dependency injection, no singletons)
│   ├── Core/         EventBus (Observer), typed events, ObjectPool<T>
│   ├── States/       SimulationManager + one SimulationState per session state (State)
│   ├── AR/           ARManager : IARService (planes, raycasts, anchors, tracking loss)
│   ├── Build/        EntityManager, TaskManager, BuildManager, commands (Command, undo/redo)
│   ├── Simulation/   LoadTestManager, ScoreManager
│   ├── Content/      HouseDesign / MaterialData ScriptableObjects, ContentCatalogue (Addressables)
│   ├── World/        MemberView, SiteController, pooled effects, procedural arch and plot meshes
│   ├── Data/         Repositories (JSON, atomic writes), DataManager, autosave, export
│   ├── Audio/        AudioManager (event-driven cues), haptics
│   └── UI/           UIManager and screens built in code
└── Editor/      Fatiro.Editor — project and content generator, audio synthesiser
```

Commands flow down through interfaces (`ISimulationController`, `IARService`, `IStructuralSolver`, `ISessionRepository`, `IContentCatalogue`); notifications flow up as typed events on the `EventBus`. The UI, audio and persistence only listen, so simulation code never references them.

### Design patterns

| Pattern | Where | Problem solved |
|---|---|---|
| State | `SimulationManager` + `SimulationState` subclasses | Each state accepts only legal inputs; pause returns to the previous state (shallow history) |
| Observer | `EventBus` | UI, audio and autosave react to simulation events without coupling |
| Command | `PlaceMemberCommand`, `RemoveMemberCommand`, `ChangeMaterialCommand` | Undo/redo of the last 20 build actions |
| Strategy | `ILoadCase`, `IScoringPolicy` | Load cases and scoring rules can be added without touching the solver or score manager |
| Factory Method | `MemberFactory` | One place maps a member kind to its subclass and resolves its slope-dependent geometry |
| Object Pool | `ObjectPool<T>` for member views, ghosts, debris, rain drops and sparks | No instantiate/destroy spikes during collapse and rain |
| Repository | `ISessionRepository`, `IProfileRepository` | File format and paths stay out of game logic |
| Flyweight | `MaterialData`, `HouseDesign` ScriptableObjects | Members share one material definition; designs are data (NFR-12) |

### Addressables

| Group | Contents | Loaded | Released |
|---|---|---|---|
| Catalogue | Designs (label `design`), materials (label `material`) | App start | App exit |
| Audio | 16 synthesised cues (label `audio`) | App start | App exit |
| Session Prefabs | Member prefabs, site plot, ghost material, effects | Session start | Session end / restart / menu |
| Fit-outs | One bundle per design: furniture, lamps, wheels, eyes | House revealed (test bench) | Session end |

`ContentCatalogue` tracks every handle by scope; the debug overlay (Start screen › Debug overlay) shows live handle counts and pool usage.

### Data (on device only)

```
persistentDataPath/
├── profiles.json                         learner display names
├── sessions/{learnerId}/{sessionId}.json one file per finished session (latest 50 or 180 days kept)
└── autosave.json                         in-progress snapshot after every stage and load test
```

Files are written to `*.tmp` and swapped in atomically. Settings (mute, last learner, debug overlay) live in PlayerPrefs. Reports export as JSON or CSV through the Android share sheet.

## Tests

- **EditMode** (`Assets/Fatiro/Tests/EditMode`): solver benchmarks against hand calculations (NFR-14), solver behaviour, regression tests for every teaching scenario, build-sequence rules and scoring.
- **PlayMode** (`Assets/Fatiro/Tests/PlayMode`): drives a full car session through the real scene: setup, placement, drag-and-snap assembly with a rejected out-of-order member, undo/redo, a failing earthquake test, reinforcement, a passing re-test, saving, and release of session assets.

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode.xml
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode.xml
```

## Disclaimer

Fatiro AR is an educational simulator. Its structural model is deliberately simplified and must not be used for real construction decisions.
