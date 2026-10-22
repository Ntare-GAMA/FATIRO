<<<<<<< HEAD
using System.Collections.Generic;
using UnityEngine;

namespace Fatiro.Core
{
    public sealed class FeedbackPool : MonoBehaviour
    {
        [SerializeField] private GameObject feedbackPrefab;
        [SerializeField] private int initialCapacity = 4;

        private readonly Queue<GameObject> available = new Queue<GameObject>();

        private void Awake()
        {
            for (var index = 0; index < initialCapacity; index++)
            {
                available.Enqueue(CreateInstance());
            }
        }

        public GameObject Acquire(Vector3 position)
        {
            var feedback = available.Count > 0 ? available.Dequeue() : CreateInstance();
            feedback.transform.position = position;
            feedback.SetActive(true);
            return feedback;
        }

        public void Release(GameObject feedback)
        {
            if (feedback == null)
            {
                return;
            }

            feedback.SetActive(false);
            available.Enqueue(feedback);
        }

        private GameObject CreateInstance()
        {
            if (feedbackPrefab == null)
            {
                throw new MissingReferenceException("FeedbackPool requires a feedback prefab.");
            }

            var instance = Instantiate(feedbackPrefab, transform);
            instance.SetActive(false);
            return instance;
        }
    }
}
=======
using System.Collections.Generic;
using UnityEngine;

namespace Fatiro.Core
{
    public sealed class FeedbackPool : MonoBehaviour
    {
        [SerializeField] private GameObject feedbackPrefab;
        [SerializeField] private int initialCapacity = 4;

        private readonly Queue<GameObject> available = new Queue<GameObject>();

        private void Awake()
        {
            for (var index = 0; index < initialCapacity; index++)
            {
                available.Enqueue(CreateInstance());
            }
        }

        public GameObject Acquire(Vector3 position)
        {
            var feedback = available.Count > 0 ? available.Dequeue() : CreateInstance();
            feedback.transform.position = position;
            feedback.SetActive(true);
            return feedback;
        }

        public void Release(GameObject feedback)
        {
            if (feedback == null)
            {
                return;
            }

            feedback.SetActive(false);
            available.Enqueue(feedback);
        }

        private GameObject CreateInstance()
        {
            if (feedbackPrefab == null)
            {
                throw new MissingReferenceException("FeedbackPool requires a feedback prefab.");
            }

            var instance = Instantiate(feedbackPrefab, transform);
            instance.SetActive(false);
            return instance;
        }
    }
}
>>>>>>> 36e112157c1a8b4e037fd29414f5c88e591d5a75
