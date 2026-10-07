using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace OVSD.Host;

/// <summary>System tray icon. Runs on its own STA thread with a WinForms message loop.</summary>
public sealed class TrayApp(int port, Action onExit)
{
    public void Run()
    {
        Application.EnableVisualStyles();
        using var icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Open Virtual Stream Deck",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };

        var editorUrl = $"http://localhost:{port}/editor";
        icon.ContextMenuStrip.Items.Add("Abrir editor", null, (_, _) => OpenUrl(editorUrl));
        icon.ContextMenuStrip.Items.Add("Conectar dispositivo (QR)", null, (_, _) => OpenUrl($"http://localhost:{port}/pair"));
        icon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        icon.ContextMenuStrip.Items.Add("Salir", null, (_, _) =>
        {
            icon.Visible = false;
            Application.ExitThread();
        });
        icon.DoubleClick += (_, _) => OpenUrl(editorUrl);

        Application.Run();
        onExit();
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
