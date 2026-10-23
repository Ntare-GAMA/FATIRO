using Fatiro.Domain;
using Fatiro.States;
using UnityEngine;

namespace Fatiro.Core
{
    // Typed events carried by the EventBus (SRS section 4.2). Readonly structs avoid garbage per publish.

    public readonly struct StateChanged
    {
        public StateChanged(SimulationStateId previous, SimulationStateId current)
        {
            Previous = previous;
            Current = current;
        }

        public SimulationStateId Previous { get; }
        public SimulationStateId Current { get; }
    }

    public readonly struct PlanesUpdated
    {
        public PlanesUpdated(int count) => Count = count;
        public int Count { get; }
    }

    public readonly struct TrackingChanged
    {
        public TrackingChanged(bool isTracking, string reason)
        {
            IsTracking = isTracking;
            Reason = reason;
        }

        public bool IsTracking { get; }
        public string Reason { get; }
    }

    /// <summary>Tracking has been lost for longer than the pause threshold (FR-05).</summary>
    public readonly struct TrackingLostTooLong { }

    public readonly struct TrackingRecovered { }

    public readonly struct SitePlaced
    {
        public SitePlaced(Transform site) => Site = site;
        public Transform Site { get; }
    }

    public readonly struct SiteCleared { }

    public readonly struct MemberPlaced
    {
        public MemberPlaced(MemberSpec spec, bool isRedoOrUndo)
        {
            Spec = spec;
            IsRedoOrUndo = isRedoOrUndo;
        }

        public MemberSpec Spec { get; }
        public bool IsRedoOrUndo { get; }
    }

    public readonly struct PlacementRejected
    {
        public PlacementRejected(MemberSpec spec, string reason)
        {
            Spec = spec;
            Reason = reason;
        }

        public MemberSpec Spec { get; }
        public string Reason { get; }
    }

    public readonly struct MemberRemoved
    {
        public MemberRemoved(MemberSpec spec) => Spec = spec;
        public MemberSpec Spec { get; }
    }

    public readonly struct MaterialChanged
    {
        public MaterialChanged(string memberId, string materialId)
        {
            MemberId = memberId;
            MaterialId = materialId;
        }

        public string MemberId { get; }
        public string MaterialId { get; }
    }

    public readonly struct MemberSelected
    {
        public MemberSelected(string memberId) => MemberId = memberId;

        /// <summary>Null when the selection was cleared.</summary>
        public string MemberId { get; }
    }

    public readonly struct TraySelectionChanged
    {
        public TraySelectionChanged(BuildComponent component) => Component = component;
        public BuildComponent Component { get; }
    }

    public readonly struct StageCompleted
    {
        public StageCompleted(BuildStage stage, bool buildComplete)
        {
            Stage = stage;
            BuildComplete = buildComplete;
        }

        public BuildStage Stage { get; }
        public bool BuildComplete { get; }
    }

    public readonly struct BuildProgressChanged { }

    public readonly struct LoadTestStarted
    {
        public LoadTestStarted(LoadCaseConfig config) => Config = config;
        public LoadCaseConfig Config { get; }
    }

    public readonly struct MemberFailed
    {
        public MemberFailed(string memberId, int wave, string reason)
        {
            MemberId = memberId;
            Wave = wave;
            Reason = reason;
        }

        public string MemberId { get; }
        public int Wave { get; }
        public string Reason { get; }
    }

    public readonly struct LoadTestCompleted
    {
        public LoadTestCompleted(LoadTestResult result, LoadTestResult previous)
        {
            Result = result;
            Previous = previous;
        }

        public LoadTestResult Result { get; }

        /// <summary>Last result for the same load case, for before/after comparison (FR-21). May be null.</summary>
        public LoadTestResult Previous { get; }
    }

    public readonly struct SessionStarted
    {
        public SessionStarted(SessionRecord record) => Record = record;
        public SessionRecord Record { get; }
    }

    public readonly struct SessionEnded
    {
        public SessionEnded(SessionRecord record, ScoreBreakdown score)
        {
            Record = record;
            Score = score;
        }

        public SessionRecord Record { get; }
        public ScoreBreakdown Score { get; }
    }

    public enum ToastKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    public readonly struct ToastRequested
    {
        public ToastRequested(string message, ToastKind kind)
        {
            Message = message;
            Kind = kind;
        }

        public string Message { get; }
        public ToastKind Kind { get; }
    }

    public readonly struct SettingsChanged { }

    /// <summary>The finished hologram has turned into the real, textured structure.</summary>
    public readonly struct StructureRevealed { }

    public readonly struct CameraPermissionResult
    {
        public CameraPermissionResult(bool granted) => Granted = granted;
        public bool Granted { get; }
    }
}
