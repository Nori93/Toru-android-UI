using System;
using System.Collections;
using UnityEngine;
using Toru.Utility;

namespace Toru.Audio
{
    /// <summary>
    /// Wraps Android's <c>SpeechRecognizer</c> (or a Unity-editor stub) to
    /// deliver recognised speech as a string event on the main thread.
    /// </summary>
    public class VoiceRecognizer : MonoBehaviour
    {
        // ── Events ─────────────────────────────────────────────────────────────
        /// <summary>Fired on the Unity main thread with the final recognised text.</summary>
        public event Action<string> OnRecognitionResult;

        /// <summary>Fired on the Unity main thread when recognition fails.</summary>
        public event Action<string> OnRecognitionError;

        // ── State ──────────────────────────────────────────────────────────────
        /// <summary>True while the microphone is active and listening.</summary>
        public bool IsListening { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        // ── Android implementation ─────────────────────────────────────────────

        private AndroidJavaObject _speechRecognizer;

        private void Awake() => InitializeSpeechRecognizer();

        private void InitializeSpeechRecognizer()
        {
            try
            {
                using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var       activity = player.GetStatic<AndroidJavaObject>("currentActivity");

                var listener = new RecognitionListener(this);
                using var speechClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
                _speechRecognizer = speechClass.CallStatic<AndroidJavaObject>(
                    "createSpeechRecognizer", activity);
                _speechRecognizer.Call("setRecognitionListener", listener);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VoiceRecognizer] Init failed: {e.Message}");
            }
        }

        /// <summary>Start listening for speech input.</summary>
        public void StartListening()
        {
            if (IsListening) return;

            try
            {
                using var speechClass = new AndroidJavaClass("android.speech.RecognizerIntent");
                string ACTION  = speechClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH");
                string MODEL   = speechClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL");
                string FREE    = speechClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM");
                string MAX_RES = speechClass.GetStatic<string>("EXTRA_MAX_RESULTS");
                string PARTIAL = speechClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS");

                using var intent = new AndroidJavaObject("android.content.Intent", ACTION);
                intent.Call<AndroidJavaObject>("putExtra", MODEL, FREE);
                intent.Call<AndroidJavaObject>("putExtra", MAX_RES, 1);
                intent.Call<AndroidJavaObject>("putExtra", PARTIAL, true);

                _speechRecognizer.Call("startListening", intent);
                IsListening = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VoiceRecognizer] StartListening failed: {e.Message}");
                OnRecognitionError?.Invoke(e.Message);
            }
        }

        /// <summary>Stop an in-progress recognition session.</summary>
        public void StopListening()
        {
            if (!IsListening) return;
            _speechRecognizer?.Call("stopListening");
            IsListening = false;
        }

        private void OnDestroy() => _speechRecognizer?.Call("destroy");

        // Called from RecognitionListener (already on main thread via dispatcher)
        internal void HandleResult(string text)
        {
            IsListening = false;
            OnRecognitionResult?.Invoke(text);
        }

        internal void HandleError(string message)
        {
            IsListening = false;
            OnRecognitionError?.Invoke(message);
        }

        // ── Inner Android proxy ────────────────────────────────────────────────

        private class RecognitionListener : AndroidJavaProxy
        {
            private readonly VoiceRecognizer _owner;

            public RecognitionListener(VoiceRecognizer owner)
                : base("android.speech.RecognitionListener")
            {
                _owner = owner;
            }

            // Required interface stubs
            public void onReadyForSpeech(AndroidJavaObject bundle)  { }
            public void onBeginningOfSpeech()                        { }
            public void onRmsChanged(float rmsdB)                    { }
            public void onBufferReceived(byte[] buffer)              { }
            public void onEndOfSpeech()                              { }
            public void onPartialResults(AndroidJavaObject bundle)   { }
            public void onEvent(int eventType, AndroidJavaObject b)  { }

            public void onError(int errorCode)
            {
                string msg = ErrorMessage(errorCode);
                UnityMainThreadDispatcher.Instance.Enqueue(() => _owner.HandleError(msg));
            }

            public void onResults(AndroidJavaObject bundle)
            {
                using var resultClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
                string KEY = resultClass.GetStatic<string>("RESULTS_RECOGNITION");
                using var list = bundle.Call<AndroidJavaObject>("getStringArrayList", KEY);
                string text = list.Call<string>("get", 0);
                UnityMainThreadDispatcher.Instance.Enqueue(() => _owner.HandleResult(text));
            }

            private static string ErrorMessage(int code) => code switch
            {
                1 => "Network operation timed out",
                2 => "Network error",
                3 => "Audio recording error",
                4 => "Server error",
                5 => "Client error",
                6 => "No speech detected",
                7 => "No recognition match",
                8 => "RecognitionService busy",
                9 => "Insufficient permissions",
                _ => $"Unknown error ({code})"
            };
        }

#else
        // ── Unity Editor / non-Android stub ────────────────────────────────────

        /// <summary>Simulates a 2-second listening delay, then returns a canned phrase.</summary>
        public void StartListening()
        {
            if (IsListening) return;
            IsListening = true;
            Debug.Log("[VoiceRecognizer] (Editor stub) listening…");
            StartCoroutine(SimulateRecognition());
        }

        /// <summary>Cancels a simulated listening session.</summary>
        public void StopListening()
        {
            if (!IsListening) return;
            StopAllCoroutines();
            IsListening = false;
            Debug.Log("[VoiceRecognizer] (Editor stub) stopped.");
        }

        private IEnumerator SimulateRecognition()
        {
            yield return new WaitForSeconds(2f);
            IsListening = false;
            OnRecognitionResult?.Invoke("Hello Toru, how are you?");
        }
#endif
    }
}
