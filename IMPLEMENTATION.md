# Fatiro AR Simulation Implementation

This project is structured for the Assignment 2 AR Foundation implementation.

## Current architecture

- `SimulationStateMachine` manages Start, AR setup, placement, running, pause, and completion states.
- `SimulationEvents` provides event-driven communication between placement, simulation, UI, audio, and persistence systems.
- `SimulationManager` tracks actions, score, errors, timing, and completion.
- `SimulationEntity` and `InteractionEntity` provide the entity abstraction and polymorphic interaction entry point.
- `FeedbackPool` provides reusable runtime feedback objects.
- `SessionRepository` stores completed sessions as JSON in `Application.persistentDataPath`.
- `ARPlacementController` performs plane raycasts, preview placement, anchoring, confirmation, and repositioning.
- `AddressableSimulationLoader` loads and releases simulation prefabs through Addressables.
- `SimulationAudioController` reacts to interaction and completion events with meaningful audio feedback.

## Unity setup

1. Open the project with Unity `6000.4.7f1`.
2. Allow Unity Package Manager to resolve AR Foundation, ARCore, XR Management, and Addressables.
3. Create an XR Origin with an AR Camera, `ARPlaneManager`, `ARRaycastManager`, and `ARAnchorManager`.
4. Add `ARPlacementController` and assign those managers, a preview prefab, and the simulation prefab.
5. Add `SimulationStateMachine` and `SimulationManager` to a scene object and connect the state-machine reference.
6. Configure the UI panels through `SimulationUIController`.
7. Mark the simulation prefab as Addressable and assign it to `AddressableSimulationLoader`.

The approved Assignment 1 scenario should determine the concrete entity names, interaction rules, task text, and scoring strategy. The reusable architecture intentionally keeps those domain-specific rules out of the AR placement layer.

## Runtime data

Completed sessions are saved to `fatiro-sessions.json` under `Application.persistentDataPath`. The repository keeps the twenty most recent sessions and the UI can request the five most recent records.
