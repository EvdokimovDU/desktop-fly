using System.Drawing;
using System.Windows.Forms;
using DesktopFly.Core;

namespace DesktopFly;

public class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _moveDisplayItem;

    public event Action? OnTogglePause;
    public event Action? OnToggleBrain;
    public event Action? OnEscapeTest;
    public event Action? OnMoveToNextDisplay;
    public event Action? OnAddFly;
    public event Action? OnRemoveFly;
    public event Action? OnScareFlies;
    public event Action? OnQuit;

    public TrayIcon(string dataInfo)
    {
        _contextMenu = new ContextMenuStrip();

        var headerItem = new ToolStripMenuItem("Desktop Fly") { Enabled = false };
        var infoItem = new ToolStripMenuItem(dataInfo) { Enabled = false };
        _contextMenu.Items.Add(headerItem);
        _contextMenu.Items.Add(infoItem);
        _contextMenu.Items.Add(new ToolStripSeparator());

        _pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => OnTogglePause?.Invoke());
        _contextMenu.Items.Add(_pauseItem);

        var brainItem = new ToolStripMenuItem("Show/Hide Brain", null, (_, _) => OnToggleBrain?.Invoke());
        _contextMenu.Items.Add(brainItem);

        var escapeItem = new ToolStripMenuItem("Escape Test (loom)", null, (_, _) => OnEscapeTest?.Invoke());
        _contextMenu.Items.Add(escapeItem);

        _moveDisplayItem = new ToolStripMenuItem("Move to Next Display", null, (_, _) => OnMoveToNextDisplay?.Invoke());
        _moveDisplayItem.Visible = Screen.AllScreens.Length > 1;
        _contextMenu.Items.Add(_moveDisplayItem);

        var addItem = new ToolStripMenuItem("Add Fly", null, (_, _) => OnAddFly?.Invoke());
        _contextMenu.Items.Add(addItem);

        var removeItem = new ToolStripMenuItem("Remove Fly", null, (_, _) => OnRemoveFly?.Invoke());
        _contextMenu.Items.Add(removeItem);

        var scareItem = new ToolStripMenuItem("Scare Flies", null, (_, _) => OnScareFlies?.Invoke());
        _contextMenu.Items.Add(scareItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => OnQuit?.Invoke());
        _contextMenu.Items.Add(quitItem);

        // Generate a 16x16 or 32x32 fly icon programmatically
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var font = new Font(FontFamily.GenericSansSerif, 18, FontStyle.Regular);
            g.DrawString("🪰", font, Brushes.Black, new PointF(0, 0));
        }
        var iconHandle = bmp.GetHicon();
        var icon = Icon.FromHandle(iconHandle);

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "DesktopFly 🪰",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };
    }

    public void SetPaused(bool paused)
    {
        _pauseItem.Text = paused ? "Resume" : "Pause";
    }

    public void UpdateScreens()
    {
        _moveDisplayItem.Visible = Screen.AllScreens.Length > 1;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
