using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Toru.UI
{
    /// <summary>
    /// Manages the main UI: chat history, text input, voice/send/stop buttons,
    /// status label, and the API-URL settings field.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Input Row")]
        [SerializeField] private TMP_InputField textInput;
        [SerializeField] private Button         sendButton;
        [SerializeField] private Button         voiceButton;
        [SerializeField] private Button         stopButton;

        [Header("Chat")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private Transform  chatContent;
        [SerializeField] private GameObject userBubblePrefab;
        [SerializeField] private GameObject toruBubblePrefab;

        [Header("Status")]
        [SerializeField] private TMP_Text statusLabel;

        [Header("Settings")]
        [Tooltip("Optional field that lets the user change the Toru API base URL at runtime.")]
        [SerializeField] private TMP_InputField apiUrlInput;

        // ── Events ─────────────────────────────────────────────────────────────
        /// <summary>User submitted text (either via Send button or keyboard Return).</summary>
        public event Action<string> OnSendText;

        /// <summary>Voice button was tapped.</summary>
        public event Action OnVoiceButtonPressed;

        /// <summary>Stop button was tapped.</summary>
        public event Action OnStopButtonPressed;

        /// <summary>API URL field lost focus with a new value.</summary>
        public event Action<string> OnApiUrlChanged;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            sendButton?.onClick.AddListener(HandleSendClicked);
            voiceButton?.onClick.AddListener(() => OnVoiceButtonPressed?.Invoke());
            stopButton?.onClick.AddListener(() => OnStopButtonPressed?.Invoke());

            if (textInput != null)
                textInput.onSubmit.AddListener(HandleTextSubmitted);

            if (apiUrlInput != null)
                apiUrlInput.onEndEdit.AddListener(HandleApiUrlChanged);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Append a user chat bubble to the history.</summary>
        public void AddUserMessage(string text) => SpawnBubble(userBubblePrefab, text);

        /// <summary>Append a Toru chat bubble to the history.</summary>
        public void AddToruMessage(string text) => SpawnBubble(toruBubblePrefab, text);

        /// <summary>Update the status bar text (e.g. "Listening…", "Thinking…").</summary>
        public void SetStatusText(string text)
        {
            if (statusLabel != null) statusLabel.text = text;
        }

        /// <summary>Pre-fill the text input (e.g. after voice recognition).</summary>
        public void SetInputText(string text)
        {
            if (textInput != null) textInput.text = text;
        }

        /// <summary>Enable or disable the interactive input controls.</summary>
        public void SetInputInteractable(bool interactable)
        {
            if (sendButton  != null) sendButton.interactable  = interactable;
            if (voiceButton != null) voiceButton.interactable = interactable;
            if (textInput   != null) textInput.interactable   = interactable;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void HandleSendClicked()
        {
            string text = textInput != null ? textInput.text : string.Empty;
            HandleTextSubmitted(text);
        }

        private void HandleTextSubmitted(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            OnSendText?.Invoke(text.Trim());

            if (textInput != null)
            {
                textInput.text = string.Empty;
                textInput.ActivateInputField();
            }
        }

        private void HandleApiUrlChanged(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
                OnApiUrlChanged?.Invoke(url.Trim());
        }

        private void SpawnBubble(GameObject prefab, string text)
        {
            if (prefab == null || chatContent == null) return;

            var go    = Instantiate(prefab, chatContent);
            var label = go.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = text;

            // Scroll to the bottom after layout is recalculated
            Canvas.ForceUpdateCanvases();
            if (chatScrollRect != null)
                chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
