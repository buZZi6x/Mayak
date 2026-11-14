namespace LighthouseGame
{
    // A brief boundary tremor pauses exposure; a deliberate glance away clears it.
    public sealed class GazeExposure
    {
        public const float Limit = 3f;
        public const float LookAwayReset = .15f;
        private float _away;
        public float Seconds { get; private set; }
        public bool Triggered { get; private set; }

        public bool Step(float deltaTime, bool looking, bool eligible)
        {
            if (Triggered || deltaTime <= 0) return false;
            if (!eligible) { Reset(); return false; }
            if (!looking)
            {
                _away += deltaTime;
                if (_away >= LookAwayReset) Seconds = 0;
                return false;
            }
            _away = 0;
            Seconds += deltaTime;
            if (Seconds <= Limit) return false;
            Triggered = true;
            return true;
        }

        public void Reset() { Seconds = 0; _away = 0; Triggered = false; }
    }
}
