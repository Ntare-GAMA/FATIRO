using Fatiro.Core;
using UnityEngine;

namespace Fatiro.Audio
{
    public sealed class SimulationAudioController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip interactionSuccess;
        [SerializeField] private AudioClip interactionFailure;
        [SerializeField] private AudioClip simulationComplete;

        private void OnEnable()
        {
            SimulationEvents.InteractionCompleted += HandleInteractionCompleted;
            SimulationEvents.SimulationCompleted += HandleSimulationCompleted;
        }

        private void OnDisable()
        {
            SimulationEvents.InteractionCompleted -= HandleInteractionCompleted;
            SimulationEvents.SimulationCompleted -= HandleSimulationCompleted;
        }

        private void HandleInteractionCompleted(InteractionResult result)
        {
            Play(result.Successful ? interactionSuccess : interactionFailure);
        }

        private void HandleSimulationCompleted(SimulationSession session)
        {
            Play(simulationComplete);
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
