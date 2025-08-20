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

public class TrayService : IDisposable
{
    private NotifyIcon? _trayIcon;
    private HwndSource? _hwndSource;
    private readonly Action _onCaptureRequested;
    private readonly Action _onSettingsRequested;
    private SettingsWindow? _settingsWindow;
    private readonly Action _onExitRequested;

    // Win32 メッセージとホットキー定数
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_PRINTSCREEN = 9000;
    private const uint MOD_NONE = 0x0000;
    private const uint VK_SNAPSHOT = 0x2C; // PrintScreen
    // 低レベルキーボードフック定数
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // 低レベルフック用フィールド
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _keyboardProc;
    private bool _useLowLevelHook = false; // 現在のモード

    public TrayService(Action onCapture, Action onSettings, Action onExit)
    {
        _onCaptureRequested = onCapture;
        _onSettingsRequested = onSettings;
        _onExitRequested = onExit;

        InitializeTrayIcon();
    // 起動時はまず RegisterHotKey を登録する（設定で低レベルフックを選択可能）
    RegisterPrintScreenHotkey();
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

        menu.Items.Add("キャプチャ開始 (PrintScreen)", null, (s, e) => _onCaptureRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("設定", null, (s, e) => ShowSettingsWindow());
        menu.Items.Add("バージョン情報", null, (s, e) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (s, e) => _onExitRequested?.Invoke());

        return menu;
    }

    private void RegisterPrintScreenHotkey()
    {
        // ホットキー登録: PrintScreen をグローバルホットキーとして登録する
        try
        {
            // HWND_MESSAGE を親にしてメッセージ専用ウィンドウとして作る（画面に表示されない）
            var parameters = new HwndSourceParameters("ShuyuHotkeyWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                // メッセージ専用ウィンドウのハンドルを指定（-3 = HWND_MESSAGE）
                ParentWindow = new IntPtr(-3)
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);

            // PrintScreen (VK_SNAPSHOT) をモディファイア無しで登録
            var ok = RegisterHotKey(_hwndSource.Handle, HOTKEY_ID_PRINTSCREEN, MOD_NONE, VK_SNAPSHOT);
#if DEBUG
            if (!ok)
            {
                // RegisterHotKey に失敗した場合、GetLastWin32Error で理由を確認してデバッグ出力
                var err = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[Shuyu] RegisterHotKey failed. GetLastWin32Error={err}");
            }
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[Shuyu] RegisterPrintScreenHotkey exception: {ex}");
#endif
            // 例外は抑止してトレイサービスを停止させない
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WM_HOTKEY を受け取ったらキャプチャ要求を呼び出す
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_PRINTSCREEN)
        {
            try
            {
                _onCaptureRequested?.Invoke();
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[Shuyu] Hotkey handler exception: {ex}");
#endif
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void UnregisterPrintScreenHotkey()
    {
        // ホットキーを解除して HwndSource を破棄する
        try
        {
            if (_hwndSource != null)
            {
                var ok = UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID_PRINTSCREEN);
#if DEBUG
                if (!ok)
                {
                    var err = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"[Shuyu] UnregisterHotKey failed. GetLastWin32Error={err}");
                }
#endif
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[Shuyu] UnregisterPrintScreenHotkey exception: {ex}");
#endif
        }
    }

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
        }
    }

    // 設定を適用: RegisterHotKey と低レベルフックを切り替える
    private void ApplyHookSetting(bool wantHook)
    {
        if (wantHook == _useLowLevelHook) return;

        if (wantHook)
        {
            // フックを有効にする: まず既存のホットキー登録を解除
            UnregisterPrintScreenHotkey();
            InstallKeyboardHook();
            _useLowLevelHook = true;
        }
        else
        {
            // フックを解除してホットキー登録に戻す
            UninstallKeyboardHook();
            RegisterPrintScreenHotkey();
            _useLowLevelHook = false;
        }
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
    // フックとホットキーは両方解放しておく
    UninstallKeyboardHook();
    UnregisterPrintScreenHotkey();
    _trayIcon?.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // 低レベルキーボードフック用 P/Invoke とデリゲート
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    // 低レベルフックをインストールする
    private void InstallKeyboardHook()
    {
        try
        {
            _keyboardProc = HookCallback; // GC 対策で保持
            IntPtr moduleHandle = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty);
            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
#if DEBUG
            if (_keyboardHookId == IntPtr.Zero)
                Debug.WriteLine("[Shuyu] InstallKeyboardHook failed. GetLastWin32Error=" + Marshal.GetLastWin32Error());
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine("[Shuyu] InstallKeyboardHook exception: " + ex);
#endif
        }
    }

    // 低レベルフックを解除する
    private void UninstallKeyboardHook()
    {
        try
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                var ok = UnhookWindowsHookEx(_keyboardHookId);
#if DEBUG
                if (!ok)
                    Debug.WriteLine("[Shuyu] UnhookWindowsHookEx failed. GetLastWin32Error=" + Marshal.GetLastWin32Error());
#endif
                _keyboardHookId = IntPtr.Zero;
                _keyboardProc = null;
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine("[Shuyu] UninstallKeyboardHook exception: " + ex);
#endif
        }
    }

    // フック処理本体: PrintScreen を検出したらキャプチャを呼び出し、以降の処理を抑止する
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == (int)VK_SNAPSHOT)
                {
                    try { _onCaptureRequested?.Invoke(); } catch { }
                    // スニッピングツールなどへキーを渡さない（抑止）
                    return (IntPtr)1;
                }
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine("[Shuyu] HookCallback exception: " + ex);
#endif
        }
        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }
}