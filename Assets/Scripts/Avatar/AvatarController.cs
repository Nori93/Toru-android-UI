using System.Collections;
using UnityEngine;

namespace Toru.Avatar
{
    /// <summary>
    /// Controls a 2-D sprite-based avatar that mirrors the Python <c>ToruAvatar</c>:
    /// <list type="bullet">
    ///   <item>Idle state — gentle vertical bounce + random eye-blink.</item>
    ///   <item>Listening state — colour tint + subtle vibration.</item>
    ///   <item>Thinking state — slow spin overlay indicator.</item>
    ///   <item>Talking state — cycles mouth-shape sprites driven by a pseudo-phoneme
    ///         scheduler (A → E → O → U → A …) at a configurable rate.</item>
    /// </list>
    ///
    /// <para>
    /// Attach this to the root GameObject of the avatar.  Assign <see cref="bodyRenderer"/>
    /// and the five mouth-shape sprites in the Inspector.  An <see cref="Animator"/>
    /// is <b>optional</b>: when present it receives <c>State</c> (int) and
    /// <c>Blink</c> (trigger) parameters so you can drive a separate animation graph.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AvatarController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Renderers")]
        [Tooltip("Main body sprite renderer (required).")]
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Tooltip("Mouth sprite renderer (child object). If null, mouth animation is skipped.")]
        [SerializeField] private SpriteRenderer mouthRenderer;

        [Header("Mouth Sprites")]
        [SerializeField] private Sprite mouthClosed;
        [SerializeField] private Sprite mouthA;
        [SerializeField] private Sprite mouthE;
        [SerializeField] private Sprite mouthO;
        [SerializeField] private Sprite mouthU;

        [Header("Body Sprites (optional overrides)")]
        [Tooltip("Sprite shown while idle. If null the current sprite is kept.")]
        [SerializeField] private Sprite bodyIdle;

        [Tooltip("Sprite shown while listening.")]
        [SerializeField] private Sprite bodyListening;

        [Tooltip("Sprite shown while thinking.")]
        [SerializeField] private Sprite bodyThinking;

        [Tooltip("Sprite shown while talking.")]
        [SerializeField] private Sprite bodyTalking;

        [Header("Idle Bounce")]
        [SerializeField] private float bounceAmplitude  = 0.04f;
        [SerializeField] private float bounceFrequency  = 1.2f;   // Hz

        [Header("Talking Bounce")]
        [SerializeField] private float talkBounceAmplitude  = 0.06f;
        [SerializeField] private float talkBounceFrequency  = 2.8f;

        [Header("Listening Wobble")]
        [SerializeField] private float wobbleAmplitude = 0.015f;
        [SerializeField] private float wobbleFrequency = 8f;

        [Header("Blink")]
        [SerializeField] private float blinkIntervalMin = 3f;
        [SerializeField] private float blinkIntervalMax = 7f;
        [SerializeField] private Sprite eyeHalfBlink;
        [SerializeField] private Sprite eyeFullBlink;

        [Header("Talking Mouth Speed")]
        [Tooltip("How many mouth-shape changes per second while talking.")]
        [SerializeField] private float mouthChangesPerSecond = 8f;

        [Header("State Tints")]
        [SerializeField] private Color tintIdle      = Color.white;
        [SerializeField] private Color tintListening = new Color(0.7f, 0.9f, 1.0f);
        [SerializeField] private Color tintThinking  = new Color(1.0f, 0.95f, 0.7f);
        [SerializeField] private Color tintTalking   = Color.white;

        // ── Optional Animator ──────────────────────────────────────────────────
        private static readonly int AnimStateId = Animator.StringToHash("State");
        private static readonly int AnimBlinkId = Animator.StringToHash("Blink");

        // ── Runtime ────────────────────────────────────────────────────────────
        private Animator       _animator;
        private Vector3        _basePosition;
        private AvatarState    _state = AvatarState.Idle;
        private float          _blinkTimer;
        private float          _blinkInterval;
        private float          _mouthTimer;
        private int            _mouthIndex;
        // Mouth shape cycle order: Closed → A → E → O → U → A …
        private static readonly MouthShape[] MouthCycle =
        {
            MouthShape.A, MouthShape.E, MouthShape.O, MouthShape.U
        };

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (bodyRenderer == null)
                bodyRenderer = GetComponent<SpriteRenderer>();

            _animator     = GetComponent<Animator>();
            _basePosition = transform.localPosition;
            _blinkInterval = Random.Range(blinkIntervalMin, blinkIntervalMax);
        }

        private void Update()
        {
            UpdateBounce();
            UpdateBlink();
            UpdateMouth();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Transition the avatar to a new state immediately.</summary>
        public void SetState(AvatarState newState)
        {
            if (_state == newState) return;
            _state = newState;

            // Body sprite
            Sprite bodySprite = newState switch
            {
                AvatarState.Listening => bodyListening != null ? bodyListening : bodyIdle,
                AvatarState.Thinking  => bodyThinking  != null ? bodyThinking  : bodyIdle,
                AvatarState.Talking   => bodyTalking   != null ? bodyTalking   : bodyIdle,
                _                     => bodyIdle,
            };
            if (bodySprite != null) bodyRenderer.sprite = bodySprite;

            // Colour tint
            bodyRenderer.color = newState switch
            {
                AvatarState.Listening => tintListening,
                AvatarState.Thinking  => tintThinking,
                AvatarState.Talking   => tintTalking,
                _                     => tintIdle,
            };

            // Mouth
            if (newState != AvatarState.Talking)
                SetMouth(MouthShape.Closed);

            // Optional Animator
            _animator?.SetInteger(AnimStateId, (int)newState);
        }

        /// <summary>Current avatar state.</summary>
        public AvatarState CurrentState => _state;

        // ── Private helpers ────────────────────────────────────────────────────

        private void UpdateBounce()
        {
            float amp, freq;
            if (_state == AvatarState.Talking)
            {
                amp  = talkBounceAmplitude;
                freq = talkBounceFrequency;
            }
            else if (_state == AvatarState.Listening)
            {
                amp  = wobbleAmplitude;
                freq = wobbleFrequency;
            }
            else
            {
                amp  = bounceAmplitude;
                freq = bounceFrequency;
            }

            float offsetY = Mathf.Sin(Time.time * freq * Mathf.PI * 2f) * amp;
            transform.localPosition = _basePosition + new Vector3(0f, offsetY, 0f);
        }

        private void UpdateBlink()
        {
            // Blinking only makes sense when idle or thinking
            if (_state == AvatarState.Listening || _state == AvatarState.Talking) return;

            _blinkTimer += Time.deltaTime;
            if (_blinkTimer < _blinkInterval) return;

            _blinkTimer    = 0f;
            _blinkInterval = Random.Range(blinkIntervalMin, blinkIntervalMax);

            // Trigger Animator blink if available
            _animator?.SetTrigger(AnimBlinkId);

            // Also swap body sprite if blink sprites are assigned
            if (eyeHalfBlink != null || eyeFullBlink != null)
                StartCoroutine(BlinkSequence());
        }

        private IEnumerator BlinkSequence()
        {
            Sprite original = bodyRenderer.sprite;

            if (eyeHalfBlink != null)
            {
                bodyRenderer.sprite = eyeHalfBlink;
                yield return new WaitForSeconds(0.05f);
            }

            if (eyeFullBlink != null)
            {
                bodyRenderer.sprite = eyeFullBlink;
                yield return new WaitForSeconds(0.08f);
            }

            if (eyeHalfBlink != null)
            {
                bodyRenderer.sprite = eyeHalfBlink;
                yield return new WaitForSeconds(0.05f);
            }

            bodyRenderer.sprite = original;
        }

        private void UpdateMouth()
        {
            if (_state != AvatarState.Talking) return;

            _mouthTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(1f, mouthChangesPerSecond);

            if (_mouthTimer < interval) return;
            _mouthTimer = 0f;

            _mouthIndex = (_mouthIndex + 1) % MouthCycle.Length;
            SetMouth(MouthCycle[_mouthIndex]);
        }

        private void SetMouth(MouthShape shape)
        {
            if (mouthRenderer == null) return;

            mouthRenderer.sprite = shape switch
            {
                MouthShape.A      => mouthA,
                MouthShape.E      => mouthE,
                MouthShape.O      => mouthO,
                MouthShape.U      => mouthU,
                _                 => mouthClosed,
            };
        }
    }
}
