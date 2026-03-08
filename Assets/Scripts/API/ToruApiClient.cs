using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Toru.API
{
    /// <summary>
    /// HTTP client that communicates with the Toru Brain API.
    /// Exposes a single <see cref="AskToru"/> method and fires events for the result or errors.
    /// </summary>
    public class ToruApiClient : MonoBehaviour
    {
        [Header("API Settings")]
        [Tooltip("Base URL of the running Toru API server, e.g. http://192.168.1.10:8000")]
        [SerializeField] private string apiBaseUrl = "http://10.0.2.2:8000";

        [Tooltip("Seconds before the request is considered timed out.")]
        [SerializeField] private int timeoutSeconds = 30;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fired on the main thread when Toru replies successfully.</summary>
        public event Action<string> OnAnswerReceived;

        /// <summary>Fired on the main thread when the request fails.</summary>
        public event Action<string> OnError;

        // ── State ─────────────────────────────────────────────────────────────
        /// <summary>True while a request is in flight.</summary>
        public bool IsLoading { get; private set; }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Send a question to Toru and await the answer asynchronously.</summary>
        public void AskToru(string question)
        {
            if (IsLoading)
            {
                Debug.LogWarning("[ToruApiClient] A request is already in progress.");
                return;
            }

            StartCoroutine(AskCoroutine(question));
        }

        /// <summary>Update the server base URL at runtime (e.g. from the settings panel).</summary>
        public void SetApiUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            apiBaseUrl = url.TrimEnd('/');
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private IEnumerator AskCoroutine(string question)
        {
            IsLoading = true;

            string jsonBody = $"{{\"question\":{JsonEscape(question)}}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using var request = new UnityWebRequest($"{apiBaseUrl}/ask", "POST");
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            IsLoading = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                OnError?.Invoke($"Network error: {request.error}");
                yield break;
            }

            var response = JsonUtility.FromJson<AskResponse>(request.downloadHandler.text);
            if (response != null && !string.IsNullOrEmpty(response.answer))
            {
                OnAnswerReceived?.Invoke(response.answer);
            }
            else
            {
                OnError?.Invoke("Received an empty or malformed response from Toru.");
            }
        }

        /// <summary>Minimal JSON string escape so we don't depend on a full JSON lib.</summary>
        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:   sb.Append(c);      break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ── Serialisation helpers ─────────────────────────────────────────────
        [Serializable]
        private class AskResponse
        {
            public string answer;
        }
    }
}
