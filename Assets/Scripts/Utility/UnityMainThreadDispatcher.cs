using System;
using System.Collections.Generic;
using UnityEngine;

namespace Toru.Utility
{
    /// <summary>
    /// Singleton that lets background threads (e.g. Android Java callbacks) safely
    /// schedule work to run on the Unity main thread in the next Update tick.
    /// </summary>
    public sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;

        /// <summary>
        /// Returns the singleton, creating the supporting GameObject if needed.
        /// Safe to call from any thread.
        /// </summary>
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // This branch will only run on the main thread (first access is
                // always from the Unity life-cycle or from Awake/Start of another
                // MonoBehaviour). If accessed from a background thread *before* any
                // MonoBehaviour has triggered creation, the caller must ensure
                // the dispatcher was already created; the typical pattern is to
                // place one in the scene from the start.
                var go = new GameObject("[UnityMainThreadDispatcher]");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        private readonly Queue<Action> _queue = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Enqueue an action to be executed on the Unity main thread.
        /// Thread-safe.
        /// </summary>
        public void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_queue) _queue.Enqueue(action);
        }

        private void Update()
        {
            // Drain and execute all pending actions this frame.
            while (true)
            {
                Action action;
                lock (_queue)
                {
                    if (_queue.Count == 0) break;
                    action = _queue.Dequeue();
                }
                action?.Invoke();
            }
        }
    }
}
