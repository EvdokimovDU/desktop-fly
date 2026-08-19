namespace DesktopFly.Core.Behavior;

public static class Circadian
{
    private static readonly (double Hour, float Value)[] Pts = new[]
    {
        (0.0, 0.25f), (5.0, 0.25f), (8.0, 1.0f), (10.0, 1.0f), (13.0, 0.55f),
        (15.0, 0.55f), (17.0, 1.0f), (20.0, 1.0f), (23.0, 0.3f), (24.0, 0.25f)
    };

    public static float Activity(double hour)
    {
        for (int i = 0; i < Pts.Length - 1; i++)
        {
            if (hour >= Pts[i].Hour && hour <= Pts[i + 1].Hour)
            {
                float t = (float)((hour - Pts[i].Hour) / Math.Max(0.001, Pts[i + 1].Hour - Pts[i].Hour));
                return Pts[i].Value + (Pts[i + 1].Value - Pts[i].Value) * t;
            }
        }
        return 0.25f;
    }
}
