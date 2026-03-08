using System;
using System.Collections;
using UnityEngine;
using Toru.Utility;

namespace Toru.Audio
{
    /// <summary>
    /// Wraps Android's <c>TextToSpeech</c> engine (or a Unity-editor stub) to
    /// speak strings aloud and fire start/finish events so the avatar can animate.
    /// </summary>
    public class TextToSpeechController : MonoBehaviour
    {
        // ── Events ─────────────────────────────────────────────────────────────
        /// <summary>Fired on the main thread when speech starts.</summary>
        public event Action OnSpeechStarted;

        /// <summary>Fired on the main thread when speech finishes (or is stopped).</summary>
        public event Action OnSpeechFinished;

        // ── State ──────────────────────────────────────────────────────────────
        /// <summary>True while the TTS engine is speaking.</summary>
        public bool IsSpeaking { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        // ── Android implementation ─────────────────────────────────────────────

        private AndroidJavaObject _tts;
        private bool              _ttsReady;

        private void Awake() => InitializeTTS();

        private void InitializeTTS()
        {
            try
            {
                using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var       activity = player.GetStatic<AndroidJavaObject>("currentActivity");

                var listener = new TtsInitListener(this);
                _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, listener);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TTS] Init failed: {e.Message}");
            }
        }

        /// <summary>Speak the given text; queues the speech if something is already playing.</summary>
        public void Speak(string text)
        {
            if (!_ttsReady || _tts == null)
            {
                Debug.LogWarning("[TTS] Engine not ready yet.");
                return;
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                using var ttsClass = new AndroidJavaClass("android.speech.tts.TextToSpeech");
                int QUEUE_FLUSH = ttsClass.GetStatic<int>("QUEUE_FLUSH");
                _tts.Call<int>("speak", text, QUEUE_FLUSH, null, "toru_utterance");

                IsSpeaking = true;
                OnSpeechStarted?.Invoke();

                // Poll for completion on the Unity side because the Android
                // UtteranceProgressListener is hard to proxy reliably in all API levels.
                StartCoroutine(WaitForSpeechEnd());
            }
            catch (Exception e)
            {
                Debug.LogError($"[TTS] Speak failed: {e.Message}");
                IsSpeaking = false;
                OnSpeechFinished?.Invoke();
            }
        }

        /// <summary>Interrupt current speech immediately.</summary>
        public void Stop()
        {
            _tts?.Call("stop");
            IsSpeaking = false;
            OnSpeechFinished?.Invoke();
        }

        private IEnumerator WaitForSpeechEnd()
        {
            // isSpeaking() polls the Android TTS engine.
            yield return null; // let the engine start
            while (_tts != null && _tts.Call<bool>("isSpeaking"))
                yield return new WaitForSeconds(0.1f);

            if (IsSpeaking)
            {
                IsSpeaking = false;
                OnSpeechFinished?.Invoke();
            }
        }

        // Called from TtsInitListener (already on main thread via dispatcher)
        internal void SetReady(bool ready)
        {
            _ttsReady = ready;
            if (!ready) Debug.LogError("[TTS] Initialization reported failure.");
        }

        private void OnDestroy() => _tts?.Call("shutdown");

        // ── Inner Android proxy ────────────────────────────────────────────────

        private class TtsInitListener : AndroidJavaProxy
        {
            private readonly TextToSpeechController _owner;

            public TtsInitListener(TextToSpeechController owner)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                _owner = owner;
            }

            public void onInit(int status)
            {
                using var cls = new AndroidJavaClass("android.speech.tts.TextToSpeech");
                int SUCCESS = cls.GetStatic<int>("SUCCESS");
                bool ready = (status == SUCCESS);
                UnityMainThreadDispatcher.Instance.Enqueue(() => _owner.SetReady(ready));
            }
        }

#else
        // ── Unity Editor / non-Android stub ────────────────────────────────────

        /// <summary>Simulates TTS by logging the text and waiting proportionally.</summary>
        public void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Debug.Log($"[TTS] (Editor stub) Speaking: {text}");
            IsSpeaking = true;
            OnSpeechStarted?.Invoke();
            StartCoroutine(SimulateSpeech(text));
        }

        /// <summary>Cancels the simulated speech.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            if (IsSpeaking)
            {
                IsSpeaking = false;
                OnSpeechFinished?.Invoke();
            }
        }

        private IEnumerator SimulateSpeech(string text)
        {
            // Approximate: ~5 chars per second at normal speech rate
            float duration = Mathf.Max(1f, text.Length / 15f);
            yield return new WaitForSeconds(duration);
            IsSpeaking = false;
            OnSpeechFinished?.Invoke();
        }
#endif
    }
}
