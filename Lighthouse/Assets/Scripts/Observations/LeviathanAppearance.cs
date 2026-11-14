namespace LighthouseGame
{
    public sealed class LeviathanAppearance
    {
        public const int FirstDay = 6;
        public const float DailyChance = .1f;
        public int Day { get; private set; }
        public bool Selected { get; private set; }
        public bool Consumed { get; private set; }
        public bool Pending => Selected && !Consumed;

        public void BeginDay(int day, float roll)
        {
            if (Day == day) return;
            Day = day;
            Selected = day >= FirstDay && roll >= 0 && roll < DailyChance;
            Consumed = false;
        }

        public bool TryConsume()
        {
            if (!Pending) return false;
            Consumed = true;
            return true;
        }

        public void Reset()
        {
            Day = 0;
            Selected = Consumed = false;
        }
    }
}
