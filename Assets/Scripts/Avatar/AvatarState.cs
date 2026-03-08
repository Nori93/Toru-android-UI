namespace Toru.Avatar
{
    /// <summary>All states the 2-D avatar can be in.</summary>
    public enum AvatarState
    {
        /// <summary>Default resting state — gentle idle bounce and random blinking.</summary>
        Idle = 0,

        /// <summary>Microphone is open and waiting for the user to speak.</summary>
        Listening = 1,

        /// <summary>Waiting for Toru's API response.</summary>
        Thinking = 2,

        /// <summary>TTS engine is speaking — mouth phoneme animation is active.</summary>
        Talking = 3,
    }

    /// <summary>Simplified mouth shapes mirroring the server-side avatar phoneme set.</summary>
    public enum MouthShape
    {
        Closed = 0,
        A      = 1,
        E      = 2,
        O      = 3,
        U      = 4,
    }
}
