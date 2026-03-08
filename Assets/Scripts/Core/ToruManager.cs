using Toru.API;
using Toru.Audio;
using Toru.Avatar;
using Toru.UI;
using UnityEngine;

namespace Toru.Core
{
    /// <summary>
    /// Central coordinator that wires together the API client, voice recogniser,
    /// TTS controller, avatar, and UI.  Place this on a dedicated Manager GameObject.
    /// </summary>
    public class ToruManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Required Components")]
        [SerializeField] private ToruApiClient          apiClient;
        [SerializeField] private VoiceRecognizer        voiceRecognizer;
        [SerializeField] private TextToSpeechController tts;
        [SerializeField] private AvatarController       avatarController;
        [SerializeField] private UIManager              uiManager;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            ValidateReferences();
            SubscribeEvents();
        }

        private void OnDestroy() => UnsubscribeEvents();

        // ── Event wiring ───────────────────────────────────────────────────────

        private void SubscribeEvents()
        {
            apiClient.OnAnswerReceived += HandleAnswerReceived;
            apiClient.OnError          += HandleApiError;

            voiceRecognizer.OnRecognitionResult += HandleVoiceResult;
            voiceRecognizer.OnRecognitionError  += HandleVoiceError;

            tts.OnSpeechStarted  += HandleSpeechStarted;
            tts.OnSpeechFinished += HandleSpeechFinished;

            uiManager.OnSendText           += SendToToru;
            uiManager.OnVoiceButtonPressed += StartVoiceInput;
            uiManager.OnStopButtonPressed  += StopAll;
            uiManager.OnApiUrlChanged      += apiClient.SetApiUrl;
        }

        private void UnsubscribeEvents()
        {
            if (apiClient != null)
            {
                apiClient.OnAnswerReceived -= HandleAnswerReceived;
                apiClient.OnError          -= HandleApiError;
            }

            if (voiceRecognizer != null)
            {
                voiceRecognizer.OnRecognitionResult -= HandleVoiceResult;
                voiceRecognizer.OnRecognitionError  -= HandleVoiceError;
            }

            if (tts != null)
            {
                tts.OnSpeechStarted  -= HandleSpeechStarted;
                tts.OnSpeechFinished -= HandleSpeechFinished;
            }

            if (uiManager != null)
            {
                uiManager.OnSendText           -= SendToToru;
                uiManager.OnVoiceButtonPressed -= StartVoiceInput;
                uiManager.OnStopButtonPressed  -= StopAll;
                uiManager.OnApiUrlChanged      -= apiClient.SetApiUrl;
            }
        }

        // ── Public actions ─────────────────────────────────────────────────────

        /// <summary>Open the microphone and start listening for a spoken question.</summary>
        public void StartVoiceInput()
        {
            if (apiClient.IsLoading) return;

            tts.Stop();
            voiceRecognizer.StartListening();
            avatarController.SetState(AvatarState.Listening);
            uiManager.SetStatusText("Listening…");
            uiManager.SetInputInteractable(false);
        }

        /// <summary>Send a text message to Toru and await her response.</summary>
        public void SendToToru(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (apiClient.IsLoading) return;

            tts.Stop();
            uiManager.AddUserMessage(text);
            avatarController.SetState(AvatarState.Thinking);
            uiManager.SetStatusText("Toru is thinking…");
            uiManager.SetInputInteractable(false);
            apiClient.AskToru(text);
        }

        /// <summary>Stop listening, TTS, and return to idle.</summary>
        public void StopAll()
        {
            voiceRecognizer.StopListening();
            tts.Stop();
            avatarController.SetState(AvatarState.Idle);
            uiManager.SetStatusText("Ready");
            uiManager.SetInputInteractable(true);
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleVoiceResult(string recognisedText)
        {
            uiManager.SetInputText(recognisedText);
            uiManager.SetInputInteractable(true);
            SendToToru(recognisedText);
        }

        private void HandleVoiceError(string error)
        {
            avatarController.SetState(AvatarState.Idle);
            uiManager.SetStatusText($"Voice error: {error}");
            uiManager.SetInputInteractable(true);
            Debug.LogWarning($"[ToruManager] Voice error: {error}");
        }

        private void HandleAnswerReceived(string answer)
        {
            uiManager.AddToruMessage(answer);
            tts.Speak(answer);
        }

        private void HandleApiError(string error)
        {
            avatarController.SetState(AvatarState.Idle);
            uiManager.SetStatusText($"Error: {error}");
            uiManager.SetInputInteractable(true);
            Debug.LogError($"[ToruManager] API error: {error}");
        }

        private void HandleSpeechStarted()
        {
            avatarController.SetState(AvatarState.Talking);
            uiManager.SetStatusText("Toru is speaking…");
        }

        private void HandleSpeechFinished()
        {
            avatarController.SetState(AvatarState.Idle);
            uiManager.SetStatusText("Ready");
            uiManager.SetInputInteractable(true);
        }

        // ── Validation ─────────────────────────────────────────────────────────

        private void ValidateReferences()
        {
            if (apiClient       == null) Debug.LogError("[ToruManager] apiClient is not assigned.");
            if (voiceRecognizer == null) Debug.LogError("[ToruManager] voiceRecognizer is not assigned.");
            if (tts             == null) Debug.LogError("[ToruManager] tts is not assigned.");
            if (avatarController== null) Debug.LogError("[ToruManager] avatarController is not assigned.");
            if (uiManager       == null) Debug.LogError("[ToruManager] uiManager is not assigned.");
        }
    }
}
