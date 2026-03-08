# Toru Android UI

A Unity (C#) Android application that connects to the
[Toru Brain API](https://github.com/Nori93/Toru) and provides:

- 🎙 **Voice recognition** — tap the microphone button and speak; the app uses
  Android's built-in `SpeechRecognizer` to convert your voice to text.
- 🤖 **AI chat** — recognised text (or typed text) is sent to the Toru `/ask`
  endpoint and the AI response is shown in a chat bubble.
- 🔊 **Text-to-speech** — Toru's answer is read aloud using Android's
  `TextToSpeech` engine.
- 🎭 **2-D animated avatar** — a sprite-based avatar bounces, blinks and
  cycles through mouth shapes (A / E / O / U) while Toru speaks.

---

## Repository structure

```
Assets/
├── Plugins/
│   └── Android/
│       ├── AndroidManifest.xml          # INTERNET + RECORD_AUDIO permissions
│       └── res/xml/
│           └── network_security_config.xml  # Allows HTTP to LAN / emulator
├── Scenes/
│   └── MainScene.unity                  # (create in Unity Editor — see below)
├── Sprites/                             # Place Toru sprite assets here
│   │   toru_idle.png
│   │   toru_listening.png
│   │   toru_thinking.png
│   │   toru_talking.png
│   │   toru_blink_half.png
│   │   toru_blink_closed.png
│   │   mouth_closed.png / mouth_A.png / mouth_E.png / mouth_O.png / mouth_U.png
└── Scripts/
    ├── API/
    │   └── ToruApiClient.cs             # UnityWebRequest → POST /ask
    ├── Audio/
    │   ├── VoiceRecognizer.cs           # Android SpeechRecognizer wrapper
    │   └── TextToSpeechController.cs   # Android TextToSpeech wrapper
    ├── Avatar/
    │   ├── AvatarState.cs              # Enum: Idle / Listening / Thinking / Talking
    │   └── AvatarController.cs         # Sprite swap + bounce + blink + mouth anim
    ├── Core/
    │   └── ToruManager.cs              # Main orchestrator wiring all components
    ├── UI/
    │   └── UIManager.cs               # Chat bubbles, buttons, status bar
    └── Utility/
        └── UnityMainThreadDispatcher.cs # Safely routes Android callbacks to Unity
Packages/
└── manifest.json                        # Package dependencies
```

---

## Prerequisites

| Tool | Version |
|------|---------|
| Unity | 2022.3 LTS or newer |
| Unity Android Build Support module | installed via Unity Hub |
| Android SDK + NDK | bundled with Unity or standalone |
| TextMeshPro | installed via Package Manager |

---

## Quick start

### 1 — Start the Toru API server

Follow the instructions in [Nori93/Toru](https://github.com/Nori93/Toru) to run
the FastAPI server:

```bash
# In the Toru repo
pip install -r requirements.txt
uvicorn api_app:app --host 0.0.0.0 --port 8000
```

- Running on a **physical device**: set the API URL to your machine's LAN IP,
  e.g. `http://192.168.1.42:8000`.
- Running on an **Android emulator**: the host machine is reachable at
  `http://10.0.2.2:8000` (default value in `ToruApiClient`).

### 2 — Open the project in Unity

1. Open **Unity Hub → Add project from disk** and point it at this folder.
2. Switch the build target to **Android**:
   *File → Build Settings → Android → Switch Platform*
3. Install **TextMeshPro Essentials** when prompted.

### 3 — Create the Main Scene

Because Unity scene files are binary, the scene must be assembled in the editor.
Follow the hierarchy below:

```
MainScene
├── [UnityMainThreadDispatcher]   ← add UnityMainThreadDispatcher component
├── Managers
│   ├── ToruManager               ← add ToruManager component
│   ├── ToruApiClient             ← add ToruApiClient component
│   ├── VoiceRecognizer           ← add VoiceRecognizer component
│   └── TextToSpeechController    ← add TextToSpeechController component
├── Avatar
│   ├── Body                      ← SpriteRenderer + AvatarController
│   └── Mouth                     ← SpriteRenderer (child of Body)
└── UI (Canvas, Screen Space — Overlay)
    ├── ChatPanel
    │   ├── ScrollView            ← ScrollRect component
    │   │   └── Content           ← chat bubbles are spawned here
    ├── InputRow
    │   ├── TextInput             ← TMP_InputField
    │   ├── SendButton            ← Button
    │   ├── VoiceButton           ← Button (🎙)
    │   └── StopButton            ← Button (⏹)
    ├── StatusLabel               ← TMP_Text ("Ready")
    └── SettingsPanel
        └── ApiUrlInput           ← TMP_InputField (pre-fill with server URL)
```

Wire the Inspector references:
- **ToruManager** → drag each component into its slot.
- **UIManager** → drag scroll rect, content transform, prefabs and labels.
- **AvatarController** → assign sprites and the Mouth SpriteRenderer child.

### 4 — Create chat-bubble prefabs

1. Create a UI panel with a `TMP_Text` child (right-align for user, left-align
   for Toru).
2. Drag it into `Assets/Prefabs/` and set it as `userBubblePrefab` /
   `toruBubblePrefab` in **UIManager**.

### 5 — Add avatar sprites

Place the following PNGs in `Assets/Sprites/` and assign them in
**AvatarController**'s Inspector:

| Slot | File |
|------|------|
| Body Idle | `toru_idle.png` |
| Body Listening | `toru_listening.png` |
| Body Thinking | `toru_thinking.png` |
| Body Talking | `toru_talking.png` |
| Eye Half Blink | `toru_blink_half.png` |
| Eye Full Blink | `toru_blink_closed.png` |
| Mouth Closed | `mouth_closed.png` |
| Mouth A | `mouth_A.png` |
| Mouth E | `mouth_E.png` |
| Mouth O | `mouth_O.png` |
| Mouth U | `mouth_U.png` |

> **Tip:** If you only have one body sprite, leave the optional slots blank;
> the controller falls back gracefully.

### 6 — Build & deploy

*File → Build Settings → Build* (or *Build and Run* with a connected device).

Android permissions (`RECORD_AUDIO`, `INTERNET`) are declared in
`AndroidManifest.xml` and will be requested at runtime on Android 6+.

---

## Architecture overview

```
┌─────────────┐  OnSendText / OnVoiceButtonPressed
│   UIManager │ ─────────────────────────────────────────────────┐
└─────────────┘                                                   │
                                                                  ▼
┌──────────────┐  OnRecognitionResult   ┌──────────────┐  AskToru()  ┌───────────────┐
│VoiceRecognizer│ ──────────────────────▶  ToruManager  │ ──────────▶ │ ToruApiClient │
└──────────────┘                        └──────┬───────┘             └───────┬───────┘
                                               │                             │ OnAnswerReceived
                                        SetState()                           │
                                               │             Speak()         ▼
                                               ▼         ┌──────────────────────────────┐
                                    ┌──────────────────┐ │ TextToSpeechController        │
                                    │ AvatarController  │ │  OnSpeechStarted/Finished    │
                                    └──────────────────┘ └──────────────────────────────┘
```

### Toru API endpoints used

| Method | Path | Body | Response |
|--------|------|------|----------|
| `POST` | `/ask` | `{"question": "…"}` | `{"answer": "…"}` |

---

## Editor testing (no Android device needed)

Both `VoiceRecognizer` and `TextToSpeechController` contain `#else` stubs that
activate outside of an Android build:

- **VoiceRecognizer** — after 2 seconds returns `"Hello Toru, how are you?"`.
- **TextToSpeechController** — logs the spoken text and fires
  `OnSpeechFinished` after a proportional delay.

Press Play in the Unity Editor, click **Voice** or type in the input field, and
verify the full pipeline works before building for Android.

---

## Configuration

| Setting | Where | Default |
|---------|-------|---------|
| Toru API base URL | `ToruApiClient` Inspector | `http://10.0.2.2:8000` |
| Request timeout | `ToruApiClient` Inspector | 30 s |
| Mouth changes / second | `AvatarController` Inspector | 8 |
| Bounce amplitude | `AvatarController` Inspector | 0.04 |
| Blink interval | `AvatarController` Inspector | 3 – 7 s |

The API URL can also be changed at runtime via the **Settings** input field in
the UI.

---

## License

MIT — see the [Toru](https://github.com/Nori93/Toru) repository for the server
licence.
