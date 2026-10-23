using System.Collections.Generic;
using Fatiro.Core;
using Fatiro.Data;
using Fatiro.Domain;
using Fatiro.States;
using UnityEngine;

namespace Fatiro.Audio
{
    /// <summary>Names of the Addressable audio clips (label "audio").</summary>
    public static class Cues
    {
        public const string Tap = "ui_tap";
        public const string SitePlaced = "site_placed";
        public const string Snap = "snap";
        public const string Reject = "reject";
        public const string StageComplete = "stage_complete";
        public const string BuildComplete = "build_complete";
        public const string Crack = "crack";
        public const string Collapse = "collapse";
        public const string Survived = "survived";
        public const string Warning = "warning";
        public const string SessionPass = "session_pass";
        public const string SessionFail = "session_fail";
        public const string QuakeLoop = "loop_quake";
        // Field recordings from the Nature - Essentials pack.
        public const string WindLoop = "Ambiance_Wind_Forest_Loop_Stereo";
        public const string RainLoop = "Ambiance_Rain_Strong_Loop_Stereo";
        public const string Birds = "Ambiance_Forest_Birds_Loop_Stereo";
        public const string CrowdLoop = "loop_crowd";
    }

    /// <summary>
    /// Plays sound cues in response to bus events, so no simulation system knows about audio.
    /// One-shots go through one source, the load-test ambience through a looping source, and
    /// birdsong plays quietly on a third source while the learner is out on the hillside.
    /// Every cue has a visual equivalent on screen (NFR-06).
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioSource effects;
        [SerializeField] private AudioSource ambience;
        [SerializeField] private AudioSource world;

        private IReadOnlyDictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private IEventBus events;
        private SettingsStore settings;
        private float lastSnap = -1f;

        public void Initialise(IEventBus eventBus, SettingsStore settingsStore, IReadOnlyDictionary<string, AudioClip> audioClips)
        {
            events = eventBus;
            settings = settingsStore;
            clips = audioClips ?? clips;
            ApplyMute();

            events.Subscribe<SitePlaced>(_ => Play(Cues.SitePlaced));
            events.Subscribe<MemberPlaced>(OnMemberPlaced);
            events.Subscribe<PlacementRejected>(_ => Play(Cues.Reject));
            events.Subscribe<StageCompleted>(message => Play(message.BuildComplete ? Cues.BuildComplete : Cues.StageComplete));
            events.Subscribe<StructureRevealed>(_ => Play(Cues.SitePlaced));
            events.Subscribe<LoadTestStarted>(OnLoadTestStarted);
            events.Subscribe<LoadTestCompleted>(OnLoadTestCompleted);
            events.Subscribe<StateChanged>(message =>
            {
                if (message.Current == SimulationStateId.Paused && message.Previous != SimulationStateId.Paused) Play(Cues.Warning);
                UpdateWorldAmbience(message.Current);
            });
            events.Subscribe<SessionEnded>(message => Play(message.Score.Passed ? Cues.SessionPass : Cues.SessionFail));
            events.Subscribe<SettingsChanged>(_ => ApplyMute());
        }

        public void PlayTap() => Play(Cues.Tap, 0.5f);

        /// <summary>Birds on the hillside while building and reviewing; quiet in menus and while paused.</summary>
        private void UpdateWorldAmbience(SimulationStateId state)
        {
            if (world == null || !clips.TryGetValue(Cues.Birds, out var birds)) return;
            var outdoors = state == SimulationStateId.Placement || state == SimulationStateId.Building ||
                           state == SimulationStateId.Reviewing || state == SimulationStateId.LoadTesting;
            if (outdoors && !world.isPlaying)
            {
                world.clip = birds;
                world.loop = true;
                world.volume = 0.25f;
                world.Play();
            }
            else if (!outdoors && world.isPlaying)
            {
                world.Stop();
            }
        }

        public void StopAmbience()
        {
            if (ambience != null) ambience.Stop();
        }

        private void OnMemberPlaced(MemberPlaced message)
        {
            if (message.IsRedoOrUndo) return;
            // A whole component lands in one frame: one snap, not one per piece.
            if (Time.unscaledTime - lastSnap < 0.1f) return;
            lastSnap = Time.unscaledTime;
            Play(Cues.Snap);
            if (!settings.Muted) Haptics.Pulse(50);
        }

        private void OnLoadTestStarted(LoadTestStarted message)
        {
            string loop;
            switch (message.Config.Kind)
            {
                case LoadCaseKind.Seismic: loop = Cues.QuakeLoop; break;
                case LoadCaseKind.Wind: loop = Cues.WindLoop; break;
                case LoadCaseKind.Rain: loop = Cues.RainLoop; break;
                case LoadCaseKind.Live: loop = Cues.CrowdLoop; break;
                default: loop = null; break;
            }

            if (loop == null || ambience == null || !clips.TryGetValue(loop, out var clip)) return;
            ambience.clip = clip;
            ambience.volume = Mathf.Lerp(0.35f, 0.9f, message.Config.Magnitude / Mathf.Max(1f, LoadCaseConfig.Maximum(message.Config.Kind)));
            ambience.loop = true;
            ambience.Play();
        }

        private void OnLoadTestCompleted(LoadTestCompleted message)
        {
            if (message.Result.Survived) Invoke(nameof(PlaySurvived), 2.4f);
        }

        /// <summary>Called by the collapse animation for each failure wave.</summary>
        public void PlayFailureWave(int wave)
        {
            Play(wave == 0 ? Cues.Crack : Cues.Collapse, wave == 0 ? 1f : 0.8f);
            if (!settings.Muted) Haptics.Pulse(wave == 0 ? 120 : 60);
        }

        private void PlaySurvived()
        {
            StopAmbience();
            Play(Cues.Survived);
        }

        private void Play(string cue, float volume = 1f)
        {
            if (effects == null || !clips.TryGetValue(cue, out var clip)) return;
            effects.PlayOneShot(clip, volume);
        }

        private void ApplyMute()
        {
            var muted = settings != null && settings.Muted;
            if (effects != null) effects.mute = muted;
            if (ambience != null) ambience.mute = muted;
            if (world != null) world.mute = muted;
        }
    }
}
