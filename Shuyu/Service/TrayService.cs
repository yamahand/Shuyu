using System.Windows;
using Shuyu; // SettingsWindow参照用
using System.Windows.Forms; // NotifyIcon用
using System.Drawing;       // Icon用
using System.Reflection;

public class TrayService : IDisposable
{
    private NotifyIcon? _trayIcon;
    private readonly Action _onCaptureRequested;
    private readonly Action _onSettingsRequested;
    private SettingsWindow? _settingsWindow;
    private readonly Action _onExitRequested;

    public TrayService(Action onCapture, Action onSettings, Action onExit)
    {
        _onCaptureRequested = onCapture;
        _onSettingsRequested = onSettings;
        _onExitRequested = onExit;
        
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new NotifyIcon
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
        
        menu.Items.Add("キャプチャ開始 (F1)", null, (s, e) => _onCaptureRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("設定", null, (s, e) => ShowSettingsWindow());
        menu.Items.Add("バージョン情報", null, (s, e) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (s, e) => _onExitRequested?.Invoke());
        
        return menu;
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
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
        _trayIcon?.Dispose();
    }
}