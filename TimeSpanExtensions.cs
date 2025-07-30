public static class TimeSpanExtensions
{
    public static TimeSpan RoundTo(this TimeSpan ts, TimeSpan rounding)
    {
        return TimeSpan.FromTicks((long)(Math.Round(ts.Ticks / (double)rounding.Ticks) * rounding.Ticks));
    }
}
