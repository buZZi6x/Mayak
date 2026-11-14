namespace LighthouseGame
{
    // One decision per day; looking away or resetting the encounter cannot reroll it.
    public sealed class WeepingGirlAppearance
    {
        public const int FirstDay = 4;
        public const float DailyChance = .1f;
        public int Day { get; private set; }
        public bool Appears { get; private set; }

        public void BeginDay(int day, float roll)
        {
            if (Day == day) return;
            Day = day;
            Appears = day >= FirstDay && roll >= 0 && roll < DailyChance;
        }

        public void Reset()
        {
            Day = 0;
            Appears = false;
        }
    }
}
