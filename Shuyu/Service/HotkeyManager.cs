using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using System.Diagnostics;

namespace Shuyu
{
    /// <summary>
    /// Hotkey 管理クラス。
    /// - デフォルトで Shift+PrintScreen を扱うための RegisterHotKey と低レベルキーボードフックを提供します。
    /// - ホットキーが押されたときに <see cref="HotkeyPressed"/> イベントを発火します。
    /// </summary>
    internal sealed class HotkeyManager : IDisposable
    {
        /// <summary>
        /// ホットキーが押されたときに発火するイベント。
        /// </summary>
        public event Action? HotkeyPressed;

    /// <summary>
    /// メッセージ受信用のウィンドウソース（RegisterHotKey の通知を受け取るため）。
    /// </summary>
    private HwndSource? _hwndSource;

    /// <summary>
    /// 低レベルキーボードフックのハンドル。
    /// </summary>
    private IntPtr _keyboardHookId = IntPtr.Zero;
    /// <summary>
    /// コールバックデリゲート（GCによる解放を防ぐため保持する）。
    /// </summary>
    private LowLevelKeyboardProc? _keyboardProc;

    /// <summary>
    /// イベントをポストするための SynchronizationContext（UI スレッドに戻すため）。
    /// </summary>
    private readonly SynchronizationContext? _syncContext;
        private readonly object _lock = new object();

        // 定数 (命名規則に合わせて _camelCase )
        private const int _wmHotkey = 0x0312;
        private const int _hotkeyId = 0x9000;
        private const uint _modShift = 0x0004;
        private const uint _vkSnapshot = 0x2C; // PrintScreen
        private const int _whKeyboardLl = 13;
        private const int _wmKeydown = 0x0100;
        private const int _wmSysKeydown = 0x0104;

    /// <summary>
    /// 低レベルフックが現在有効かどうかを示します。
    /// </summary>
    public bool useLowLevelHook { get; private set; }

    /// <summary>
    /// HotkeyManager の新しいインスタンスを作成します。
    /// </summary>
    /// <param name="syncContext">イベントを通知する SynchronizationContext。省略時は現在のコンテキストを使用します。</param>
    public HotkeyManager(SynchronizationContext? syncContext = null)
        {
            _syncContext = syncContext ?? SynchronizationContext.Current;
        }

    /// <summary>
    /// RegisterHotKey を用いて Shift+PrintScreen を登録します。
    /// </summary>
    /// <returns>登録に成功した場合は true。</returns>
    public bool RegisterShiftPrintScreenHotkey()
        {
            lock (_lock)
            {
                EnsureMessageWindow();
                var ok = RegisterHotKey(_hwndSource!.Handle, _hotkeyId, _modShift, _vkSnapshot);
#if DEBUG
                if (!ok)
                {
                    Debug.WriteLine($"[HotkeyManager] RegisterHotKey failed. Err={Marshal.GetLastWin32Error()}");
                }
#endif
                return ok;
            }
        }

    /// <summary>
    /// 登録したホットキーを解除します。
    /// </summary>
    public void UnregisterHotkey()
        {
            lock (_lock)
            {
                if (_hwndSource != null)
                {
                    _ = UnregisterHotKey(_hwndSource.Handle, _hotkeyId);
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }
            }
        }

    /// <summary>
    /// 低レベルキーボードフックをインストールします。Shift+PrintScreen を検出して抑止します。
    /// </summary>
    public void InstallLowLevelHook()
        {
            lock (_lock)
            {
                if (_keyboardHookId != IntPtr.Zero) return;
                _keyboardProc = HookCallback; // GC 対策
                try
                {
                    IntPtr module = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty);
                    _keyboardHookId = SetWindowsHookEx(_whKeyboardLl, _keyboardProc, module, 0);
#if DEBUG
                    if (_keyboardHookId == IntPtr.Zero)
                        Debug.WriteLine("[HotkeyManager] Install hook failed: " + Marshal.GetLastWin32Error());
#endif
                    useLowLevelHook = true;
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine("[HotkeyManager] InstallLowLevelHook exception: " + ex);
#endif
                }
            }
        }

    /// <summary>
    /// インストールした低レベルキーボードフックを解除します。
    /// </summary>
    public void UninstallLowLevelHook()
        {
            lock (_lock)
            {
                if (_keyboardHookId != IntPtr.Zero)
                {
                    var ok = UnhookWindowsHookEx(_keyboardHookId);
#if DEBUG
                    if (!ok) Debug.WriteLine("[HotkeyManager] UnhookWindowsHookEx failed: " + Marshal.GetLastWin32Error());
#endif
                    _keyboardHookId = IntPtr.Zero;
                    _keyboardProc = null;
                }
                useLowLevelHook = false;
            }
        }

    /// <summary>
    /// 要求に応じて、RegisterHotKey と低レベルフックの使用を切り替えます。
    /// </summary>
    /// <param name="wantHook">低レベルフックを使用したい場合は true。</param>
    public void ApplyUseLowLevelHook(bool wantHook)
        {
            if (wantHook == useLowLevelHook) return;
            if (wantHook)
            {
                UnregisterHotkey();
                InstallLowLevelHook();
            }
            else
            {
                UninstallLowLevelHook();
                RegisterShiftPrintScreenHotkey();
            }
        }

    /// <summary>
    /// RegisterHotKey の通知を受け取るためのメッセージ専用ウィンドウを作成します。
    /// </summary>
    private void EnsureMessageWindow()
        {
            if (_hwndSource != null) return;
            var p = new HwndSourceParameters("HotkeyMsgWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                ParentWindow = new IntPtr(-3) // HWND_MESSAGE
            };
            _hwndSource = new HwndSource(p);
            _hwndSource.AddHook(WndProc);
        }

    /// <summary>
    /// メッセージウィンドウの WndProc。WM_HOTKEY を受け取ってイベントを発火します。
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _wmHotkey && wParam.ToInt32() == _hotkeyId)
            {
                PostHotkeyEvent();
                handled = true;
            }
            return IntPtr.Zero;
        }

    /// <summary>
    /// HotkeyPressed イベントを適切なスレッドコンテキストで発火します。
    /// </summary>
    private void PostHotkeyEvent()
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ => HotkeyPressed?.Invoke(), null);
            }
            else
            {
                HotkeyPressed?.Invoke();
            }
        }

    /// <summary>
    /// 低レベルフックのコールバック。PrintScreen と Shift の組み合わせを検出してイベントを発火し、抑止します。
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)_wmKeydown || wParam == (IntPtr)_wmSysKeydown))
                {
                    int vk = Marshal.ReadInt32(lParam);
                    if (vk == (int)_vkSnapshot)
                    {
                        // Shift が押されているか確認
                        short s = GetAsyncKeyState((int)Keys.ShiftKey);
                        bool shiftDown = (s & 0x8000) != 0;
                        if (shiftDown)
                        {
                            PostHotkeyEvent();
                            // 抑止して他プロセスに渡さない
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine("[HotkeyManager] HookCallback exception: " + ex);
#endif
            }
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

    /// <summary>
    /// 使用中のフックとホットキーを解除してリソースを解放します。
    /// </summary>
    public void Dispose()
        {
            UninstallLowLevelHook();
            UnregisterHotkey();
        }

        // --- P/Invoke ---
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
