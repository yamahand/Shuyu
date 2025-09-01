using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using System.Diagnostics;
using Shuyu.Service;

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
        /// <summary>
        /// スレッドセーフのためのロックオブジェクト。
        /// </summary>
        private readonly object _lock = new object();

        // 定数 (命名規則に合わせて _camelCase )
        private const int WmHotkey = 0x0312;          // WM_HOTKEY メッセージ
        private const int HotkeyId = 0x9000;          // ホットキーの識別ID
        private const uint ModShift = 0x0004;         // Shift モディファイア
        private const uint VkSnapshot = 0x2C;         // PrintScreen キーの仮想キーコード
        private const int WhKeyboardLl = 13;          // WH_KEYBOARD_LL (低レベルキーボードフック)
        private const int WmKeydown = 0x0100;         // WM_KEYDOWN メッセージ
        private const int WmSysKeydown = 0x0104;      // WM_SYSKEYDOWN メッセージ

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
            // UIスレッドでイベントを発火するためのコンテキストを保存
            _syncContext = syncContext ?? SynchronizationContext.Current;
        }

        /// <summary>
        /// RegisterHotKey を用いて Shift+PrintScreen を登録します。
        /// </summary>
        /// <returns>登録に成功した場合は true。</returns>
        public bool RegisterShiftPrintScreenHotkey()
        {
            lock (_lock) // スレッドセーフ処理
            {
                // メッセージウィンドウが未作成の場合は作成
                EnsureMessageWindow();
                // Win32 API でホットキーを登録（Shift + PrintScreen）
                var ok = RegisterHotKey(_hwndSource!.Handle, HotkeyId, ModShift, VkSnapshot);
#if DEBUG
                if (!ok)
                {
                    // デバッグビルド時はエラー情報を出力
                    LogService.LogWarning($"[HotkeyManager] RegisterHotKey failed. Err={Marshal.GetLastWin32Error()}");
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
            lock (_lock) // スレッドセーフ処理
            {
                if (_hwndSource != null)
                {
                    // Win32 API でホットキーの登録を解除
                    _ = UnregisterHotKey(_hwndSource.Handle, HotkeyId);
                    // メッセージフックを解除
                    _hwndSource.RemoveHook(WndProc);
                    // ウィンドウソースを破棄
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
            lock (_lock) // スレッドセーフ処理
            {
                // 既にフックがインストールされている場合は何もしない
                if (_keyboardHookId != IntPtr.Zero) return;
                
                // GCによるデリゲート解放を防ぐためインスタンス変数に保持
                _keyboardProc = HookCallback;
                try
                {
                    // 現在のプロセスのモジュールハンドルを取得
                    IntPtr module = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty);
                    // 低レベルキーボードフックをインストール
                    _keyboardHookId = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, module, 0);
#if DEBUG
                    if (_keyboardHookId == IntPtr.Zero)
                        LogService.LogError("[HotkeyManager] Install hook failed: " + Marshal.GetLastWin32Error());
#endif
                    // フラグを更新
                    useLowLevelHook = true;
                }
                catch (Exception ex)
                {
#if DEBUG
                    LogService.LogException(ex, "[HotkeyManager] InstallLowLevelHook exception");
#endif
                }
            }
        }

        /// <summary>
        /// インストールした低レベルキーボードフックを解除します。
        /// </summary>
        public void UninstallLowLevelHook()
        {
            lock (_lock) // スレッドセーフ処理
            {
                if (_keyboardHookId != IntPtr.Zero)
                {
                    // Win32 API でフックを解除
                    var ok = UnhookWindowsHookEx(_keyboardHookId);
#if DEBUG
                    if (!ok) LogService.LogError("[HotkeyManager] UnhookWindowsHookEx failed: " + Marshal.GetLastWin32Error());
#endif
                    // ハンドルとデリゲートをクリア
                    _keyboardHookId = IntPtr.Zero;
                    _keyboardProc = null;
                }
                // フラグを更新
                useLowLevelHook = false;
            }
        }

        /// <summary>
        /// 要求に応じて、RegisterHotKey と低レベルフックの使用を切り替えます。
        /// </summary>
        /// <param name="wantHook">低レベルフックを使用したい場合は true。</param>
        /// <param name="initialize">初期化は true。</param>
        public void ApplyUseLowLevelHook(bool wantHook, bool initialize)
        {
            // 既に要求された状態の場合は何もしない
            if (wantHook == useLowLevelHook && !initialize) return;
            
            if (wantHook)
            {
                // 低レベルフックに切り替え：RegisterHotKeyを解除してフックをインストール
                UnregisterHotkey();
                InstallLowLevelHook();
            }
            else
            {
                // RegisterHotkeyに切り替え：フックを解除してホットキーを登録
                UninstallLowLevelHook();
                RegisterShiftPrintScreenHotkey();
            }
        }

        /// <summary>
        /// RegisterHotKey の通知を受け取るためのメッセージ専用ウィンドウを作成します。
        /// </summary>
        private void EnsureMessageWindow()
        {
            // 既にウィンドウが作成されている場合は何もしない
            if (_hwndSource != null) return;
            
            // メッセージ専用ウィンドウの設定（非表示、サイズ0）
            var p = new HwndSourceParameters("HotkeyMsgWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                ParentWindow = new IntPtr(-3) // HWND_MESSAGE（メッセージ専用ウィンドウ）
            };
            
            // ウィンドウソースを作成してメッセージフックを追加
            _hwndSource = new HwndSource(p);
            _hwndSource.AddHook(WndProc);
        }

        /// <summary>
        /// メッセージウィンドウの WndProc。WM_HOTKEY を受け取ってイベントを発火します。
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // WM_HOTKEY メッセージかつ登録したホットキーIDの場合
            if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
            {
                // ホットキー押下イベントをポスト
                PostHotkeyEvent();
                // メッセージを処理済みとしてマーク
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
                // UIスレッドなど指定されたコンテキストでイベントを発火
                _syncContext.Post(_ => HotkeyPressed?.Invoke(), null);
            }
            else
            {
                // 同期コンテキストがない場合は現在のスレッドで直接発火
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
                // フックチェーンの処理が必要で、キーダウンメッセージの場合
                if (nCode >= 0 && (wParam == (IntPtr)WmKeydown || wParam == (IntPtr)WmSysKeydown))
                {
                    // lParam から仮想キーコードを取得
                    int vk = Marshal.ReadInt32(lParam);
                    
                    // PrintScreen キーが押された場合
                    if (vk == (int)VkSnapshot)
                    {
                        // Shift キーが同時に押されているかチェック
                        short s = GetAsyncKeyState((int)Keys.ShiftKey);
                        bool shiftDown = (s & 0x8000) != 0; // 最上位ビットが1なら押下中
                        
                        if (shiftDown)
                        {
                            // Shift+PrintScreen の組み合わせなのでイベントを発火
                            PostHotkeyEvent();
                            // 他のプロセス（例：Snipping Tool）に渡さずに抑止
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                LogService.LogException(ex, "[HotkeyManager] HookCallback exception");
#endif
            }
            
            // 該当しない場合は次のフックプロシージャに処理を委譲
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 使用中のフックとホットキーを解除してリソースを解放します。
        /// </summary>
        public void Dispose()
        {
            // 低レベルフックを解除
            UninstallLowLevelHook();
            // ホットキー登録を解除
            UnregisterHotkey();
        }

        // --- P/Invoke （Win32 API の宣言）---
        
        /// <summary>
        /// システム全体のホットキーを登録する Win32 API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        /// <summary>
        /// 登録したホットキーを解除する Win32 API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        /// <summary>
        /// 低レベルキーボードフックのコールバックデリゲート型
        /// </summary>
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        /// <summary>
        /// 低レベルフックをインストールする Win32 API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        
        /// <summary>
        /// インストールしたフックを解除する Win32 API
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        /// <summary>
        /// 次のフックプロシージャを呼び出す Win32 API
        /// </summary>
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        /// <summary>
        /// 指定されたモジュールのハンドルを取得する Win32 API
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        /// <summary>
        /// キーの押下状態を非同期で取得する Win32 API
        /// </summary>
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
