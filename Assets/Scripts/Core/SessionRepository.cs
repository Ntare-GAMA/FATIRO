using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fatiro.Core
{
    public sealed class SessionRepository
    {
        private const int MaximumStoredSessions = 20;
        private readonly string filePath;

        public SessionRepository()
        {
            filePath = Path.Combine(Application.persistentDataPath, "fatiro-sessions.json");
        }

        public IReadOnlyList<SimulationSession> LoadRecent(int count)
        {
            if (!File.Exists(filePath))
            {
                return Array.Empty<SimulationSession>();
            }

            try
            {
                var wrapper = JsonUtility.FromJson<SessionCollection>(File.ReadAllText(filePath));
                if (wrapper?.sessions == null)
                {
                    throw new InvalidDataException("Session file does not contain a sessions collection.");
                }

                var sessions = new List<SimulationSession>(wrapper.sessions);
                sessions.Sort((left, right) => string.CompareOrdinal(right.completedAtUtc, left.completedAtUtc));
                return sessions.GetRange(0, Mathf.Min(count, sessions.Count));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Unable to load saved simulation sessions: {exception.Message}");
                return Array.Empty<SimulationSession>();
            }
        }

        public void Save(SimulationSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var sessions = new List<SimulationSession>(LoadRecent(MaximumStoredSessions));
            sessions.Insert(0, session);
            if (sessions.Count > MaximumStoredSessions)
            {
                sessions.RemoveRange(MaximumStoredSessions, sessions.Count - MaximumStoredSessions);
            }

            var collection = new SessionCollection { sessions = sessions.ToArray() };
            try
            {
                File.WriteAllText(filePath, JsonUtility.ToJson(collection, true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"Unable to save simulation session: {exception.Message}");
            }
        }

        [Serializable]
        private sealed class SessionCollection
        {
            public SimulationSession[] sessions;
        }
    }
}
