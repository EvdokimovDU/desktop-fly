using System.Diagnostics;
using System.Numerics;
using System.Text;
using DesktopFly.Core.Behavior;

namespace DesktopFly.Core.Platform.Win32;

public class WindowSense
{
    public record struct Snapshot(List<Ledge> Ledges, List<(Vector2 Center, float Size)> NewWindows);

    private readonly HashSet<int> _knownIDs = new();
    private bool _first = true;
    private readonly uint _myPID = (uint)Process.GetCurrentProcess().Id;

    public Snapshot Poll(float screenWidth, float screenHeight, float screenLeft = 0, float screenTop = 0)
    {
        var ledges = new List<Ledge>();
        var newWins = new List<(Vector2 Center, float Size)>();
        var ids = new HashSet<int>();

        float midX = screenLeft + screenWidth * 0.5f;
        float midY = screenTop + screenHeight * 0.5f;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _myPID) return true;

            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
                return true;

            int w = rect.Width;
            int h = rect.Height;
            if (w < 160 || h < 60) return true;

            var className = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, className, 256);
            string cls = className.ToString();
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Windows.UI.Core.CoreWindow")
                return true;

            int id = (int)hWnd.ToInt64();
            ids.Add(id);

            // Screen intersection
            if (rect.Right < screenLeft || rect.Left > screenLeft + screenWidth ||
                rect.Bottom < screenTop || rect.Top > screenTop + screenHeight)
                return true;

            // Scene coordinates: centered on this display, +Y up, +X right
            float topY = midY - rect.Top;
            float x0 = Math.Max(rect.Left - midX, -screenWidth * 0.5f + 15f);
            float x1 = Math.Min(rect.Right - midX, screenWidth * 0.5f - 15f);

            if (topY < screenHeight * 0.5f - 8f && topY > -screenHeight * 0.5f + 8f && x1 - x0 > 100f && ledges.Count < 12)
            {
                ledges.Add(new Ledge(topY, x0, x1, id));
            }

            if (!_first && !_knownIDs.Contains(id))
            {
                float winMidX = (rect.Left + rect.Right) * 0.5f - midX;
                float winMidY = midY - (rect.Top + rect.Bottom) * 0.5f;
                newWins.Add((new Vector2(winMidX, winMidY), Math.Max(w, h)));
            }

            return true;
        }, IntPtr.Zero);

        _knownIDs.Clear();
        foreach (var id in ids) _knownIDs.Add(id);
        _first = false;

        return new Snapshot(ledges, newWins);
    }
}
