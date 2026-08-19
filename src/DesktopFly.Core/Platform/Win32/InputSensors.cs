using System.Numerics;
using System.Runtime.InteropServices;

namespace DesktopFly.Core.Platform.Win32;

public class InputSensors
{
    private bool _prevLeftDown = false;
    private bool _prevRightDown = false;

    public static Vector2? GetMouseScene(float screenWidth, float screenHeight, float screenLeft = 0, float screenTop = 0)
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return null;

        float midX = screenLeft + screenWidth * 0.5f;
        float midY = screenTop + screenHeight * 0.5f;

        // Convert screen coordinates (top-left origin, Y down) to scene coordinates (center origin, Y up)
        return new Vector2(pt.X - midX, midY - pt.Y);
    }

    public static float GetUserIdleSeconds()
    {
        var lii = new NativeMethods.LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf(lii);
        if (NativeMethods.GetLastInputInfo(ref lii))
        {
            uint currentTick = NativeMethods.GetTickCount();
            return (currentTick - lii.dwTime) / 1000f;
        }
        return 0f;
    }

    public bool PollClick(out Vector2 clickPos, float screenWidth, float screenHeight, float screenLeft = 0, float screenTop = 0)
    {
        clickPos = Vector2.Zero;
        bool leftDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0;
        bool rightDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0;

        bool clicked = (leftDown && !_prevLeftDown) || (rightDown && !_prevRightDown);
        _prevLeftDown = leftDown;
        _prevRightDown = rightDown;

        if (clicked)
        {
            var p = GetMouseScene(screenWidth, screenHeight, screenLeft, screenTop);
            if (p.HasValue)
            {
                clickPos = p.Value;
                return true;
            }
        }
        return false;
    }
}
