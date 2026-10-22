# Fatiro AR — Experimental Housing Structures Simulator

Mobile AR simulation (Android, ARCore) in which construction learners assemble creature-inspired houses on a sloped hillside placed on a real table, then load-test them with earthquakes, wind, rain and occupancy. Members that exceed their capacity fail and bring down whatever rests on them; the learner reinforces the design and tests again.

*Urufatiro* is Kinyarwanda for "foundation".

## Designs

| Design | Inspiration | Teaches | Fails when… | Fixed by… |
|---|---|---|---|---|
| **Ingwe** (PN-01) | Panther crouched down the slope | Rib arches need bracing; cantilevers need depth or a prop | Earthquake topples unbraced ribs like dominoes; people overload the head cantilever | Flank braces; foreleg props or a steel head beam |
| **Inzovu** (EL-01) | Elephant on four legs | Canopies must be tied down; drains must be sized for heavy rain | Wind rips off the ear canopies; the trunk downpipe overflows and the roof ponds | Ear tie-downs; a steel trunk |
| **Imodoka** (CR-01) | Sports car on spider stilts | Downhill stilts on a slope are the longest and buckle first | Earthquake buckles the downhill stilts and the pod collapses | Downhill X-braces and sway braces, or steel stilts |

Every house stands on firm ground: a compacted building platform cut into the slope, clay-brick piers under every column, and brick plinth walls tying the piers together. The plinth walls are real members: they double the footings' resistance to sliding, and the elephant's brick ground-floor walls act as shear walls that stiffen its legs.

## Requirements

- Unity **6000.4.7f1** with Android Build Support (SDK, NDK, OpenJDK)
- An ARCore-supported Android phone running Android 10 (API 29) or later

## Getting started

1. Open the project in Unity 6000.4.7f1 and let packages resolve.
   Import the Unity Asset Store packages listed in [CREDITS.md](CREDITS.md) from **Window › Package Manager › My Assets** (they are not in the repository; without them the app falls back to plain shapes and colours).
2. Run **Fatiro › Build Project Content**. This generates materials, synthesised audio, prefabs, the three design assets, the creature skins, the Addressables groups, the scene (`Assets/Fatiro/Scenes/Fatiro.unity`) and the Android/XR/URP settings. It is safe to re-run.
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
| Creature Skins | One bundle per design | Structure complete | Session end |

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
=======
# FATIRO
Fatiro AR is a mobile augmented-reality simulation. In it, construction and engineering learners build experimental house forms at tabletop scale, then load-test them under Rwandan site conditions. Learners place a hillside plot on a real table and assemble a structure member by member in the correct construction sequence.
>>>>>>> 36e112157c1a8b4e037fd29414f5c88e591d5a75
