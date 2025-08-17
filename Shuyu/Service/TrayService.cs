using System.Windows;
using Shuyu; // SettingsWindow参照用
using System.Windows.Forms; // NotifyIcon用
using System.Drawing;       // Icon用
using System.Reflection;

public class TrayService : IDisposable
{
    private NotifyIcon? trayIcon;
    private readonly Action onCaptureRequested;
    private readonly Action onSettingsRequested;
    private SettingsWindow? settingsWindow;
    private readonly Action onExitRequested;

    public TrayService(Action onCapture, Action onSettings, Action onExit)
    {
        onCaptureRequested = onCapture;
        onSettingsRequested = onSettings;
        onExitRequested = onExit;
        
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        trayIcon = new NotifyIcon
        {
            Icon = LoadIconFromResource(),
            Visible = true,
            Text = "Shuyu",
            ContextMenuStrip = CreateContextMenu()
        };
    }

    private Icon LoadIconFromResource()
    {
        // リソースからアイコンを読み込み
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Shuyu.Resources.Icons.tray.ico");
        if (stream == null)
            throw new InvalidOperationException("アイコンリソースが見つかりません。");
        return new Icon(stream);
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        
        menu.Items.Add("キャプチャ開始 (F1)", null, (s, e) => onCaptureRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("設定", null, (s, e) => ShowSettingsWindow());
        menu.Items.Add("バージョン情報", null, (s, e) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (s, e) => onExitRequested?.Invoke());
        
        return menu;
    }

    private void ShowSettingsWindow()
    {
        if (settingsWindow == null || !settingsWindow.IsLoaded)
        {
            settingsWindow = new SettingsWindow();
        }
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void ShowAbout()
    {
        System.Windows.Forms.MessageBox.Show(
            "Shuyu v1.0\nスクリーンキャプチャツール",
            "バージョン情報",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    public void Dispose()
    {
        trayIcon?.Dispose();
    }
}