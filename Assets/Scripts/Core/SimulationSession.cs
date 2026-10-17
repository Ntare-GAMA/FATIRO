using System;

namespace Fatiro.Core
{
    [Serializable]
    public sealed class SimulationSession
    {
        public string sessionId;
        public string completedAtUtc;
        public float durationSeconds;
        public int score;
        public int completedActions;
        public int incorrectActions;
        public string outcome;
    }
}
