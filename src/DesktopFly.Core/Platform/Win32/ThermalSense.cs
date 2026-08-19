namespace DesktopFly.Core.Platform.Win32;

public static class ThermalSense
{
    public static float GetThermalTempo()
    {
        if (NativeMethods.GetSystemPowerStatus(out var status))
        {
            // If running on battery with low percentage or power throttling
            if (status.ACLineStatus == 0 && status.BatteryLifePercent < 20)
                return 1.15f;
            if (status.BatteryFlag == 4) // Critical battery
                return 1.35f;
        }
        return 1.0f;
    }
}
