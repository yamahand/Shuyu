using System;
using System.Windows;
using Application = System.Windows.Application;
using Shuyu; // SettingsWindow参照用
using System.Windows.Forms; // NotifyIcon用
using System.Drawing;       // Icon用
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Diagnostics;

/// <summary>
/// トレイアイコンと関連する操作（キャプチャ開始、設定表示、終了）を管理するサービスです。
/// </summary>
public class TrayService : IDisposable
{
    private NotifyIcon? _trayIcon;
    private HotkeyManager? _hotkeyManager;
    private readonly Action _onCaptureRequested;
    private readonly Action _onSettingsRequested;
    private SettingsWindow? _settingsWindow;
    private readonly Action _onExitRequested;
    private bool _useLowLevelHook = false; // 現在のモード

    /// <summary>
    /// TrayService の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="onCapture">キャプチャ要求時に呼び出されるコールバック。</param>
    /// <param name="onSettings">設定表示要求時に呼び出されるコールバック（未使用）。</param>
    /// <param name="onExit">終了要求時に呼び出されるコールバック。</param>
    public TrayService(Action onCapture, Action onSettings, Action onExit)
    {
        _onCaptureRequested = onCapture;
        _onSettingsRequested = onSettings;
        _onExitRequested = onExit;

        InitializeTrayIcon();
        // HotkeyManager を作成し、設定を読み込んで適用する
        _hotkeyManager = new HotkeyManager();
        var settings = UserSettingsStore.Load();
        _useLowLevelHook = settings.useLowLevelHook;
        _hotkeyManager.ApplyUseLowLevelHook(_useLowLevelHook);
        _hotkeyManager.HotkeyPressed += () => _onCaptureRequested?.Invoke();
    }

    /// <summary>
    /// トレイアイコンを初期化して表示します。
    /// </summary>
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

    /// <summary>
    /// 埋め込みリソースからアイコンを読み込みます。
    /// </summary>
    /// <returns>読み込まれた <see cref="Icon"/> オブジェクト。</returns>
    private Icon LoadIconFromResource()
    {
        // リソースからアイコンを読み込み
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Shuyu.Resources.Icons.tray.ico");
        if (stream == null)
            throw new InvalidOperationException("アイコンリソースが見つかりません。");
        return new Icon(stream);
    }

    /// <summary>
    /// トレイのコンテキストメニューを作成します。
    /// </summary>
    /// <returns>作成された <see cref="ContextMenuStrip"/> インスタンス。</returns>
    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("キャプチャ開始 (Shift+PrintScreen)", null, (s, e) => _onCaptureRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("設定", null, (s, e) => ShowSettingsWindow());
        menu.Items.Add("バージョン情報", null, (s, e) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (s, e) => _onExitRequested?.Invoke());

        return menu;
    }

    // ... existing code for hotkey handled by HotkeyManager ...

    /// <summary>
    /// 設定ウィンドウを表示し、ユーザーが変更した設定を適用・永続化します。
    /// </summary>
    private void ShowSettingsWindow()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
        }

        // 現在の選択を反映して表示（モーダル）
        _settingsWindow.useLowLevelHook = _useLowLevelHook;
        _settingsWindow.Owner = Application.Current?.MainWindow;
        var res = _settingsWindow.ShowDialog();
        if (res == true)
        {
            // 設定に応じてモードを切り替える
            var wantHook = _settingsWindow.useLowLevelHook;
            ApplyHookSetting(wantHook);

            // 変更を永続化
            var s = new UserSettings { useLowLevelHook = wantHook };
            UserSettingsStore.Save(s);
        }
    }

    // 設定を適用: RegisterHotKey と低レベルフックを切り替える
    /// <summary>
    /// RegisterHotKey と低レベルキーボードフックのいずれかを有効/無効に切り替えます。
    /// </summary>
    /// <param name="wantHook">低レベルフックを使用する場合は true。</param>
    private void ApplyHookSetting(bool wantHook)
    {
        if (wantHook == _useLowLevelHook) return;

        if (wantHook)
        {
            // フックを有効にする: HotkeyManager に切り替え
            _hotkeyManager?.ApplyUseLowLevelHook(true);
            _useLowLevelHook = true;
        }
        else
        {
            _hotkeyManager?.ApplyUseLowLevelHook(false);
            _useLowLevelHook = false;
        }
    }

    /// <summary>
    /// バージョン情報ダイアログを表示します。
    /// </summary>
    private void ShowAbout()
    {
        System.Windows.Forms.MessageBox.Show(
            "Shuyu v1.0\nスクリーンキャプチャツール",
            "バージョン情報",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    /// <summary>
    /// 使用中のリソースを解放します。
    /// </summary>
    public void Dispose()
    {
        // HotkeyManager を破棄
        _hotkeyManager?.Dispose();
        _trayIcon?.Dispose();
    }

    // HotkeyManager が責務を持つため、ここに P/Invoke は不要になった
}