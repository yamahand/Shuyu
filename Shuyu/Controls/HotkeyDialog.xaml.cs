using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Shuyu.Resources;
using Shuyu.Service;

namespace Shuyu.Controls
{
    /// <summary>
    /// ホットキー選択ダイアログ。
    /// ユーザーが修飾キーと主要キーを組み合わせてホットキーを設定できます。
    /// </summary>
    public partial class HotkeyDialog : Window
    {
        // 選択中の修飾キービットフラグと仮想キーコード
        private uint _modifiers;
        private uint _vk;
        private bool _messageHookAttached = false;

        // 修飾キー用フラグ（user32 の定義に合わせる）
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_SNAPSHOT = 0x2C;

        // 選択された修飾キーと仮想キーを外部から取得するためのプロパティ
        public uint SelectedModifiers => _modifiers;
        public uint SelectedVirtualKey => _vk;

        /// <summary>
        /// コンストラクタ。初期値を設定して表示を更新します。
        /// </summary>
        /// <param name="initialModifiers">初期の修飾キーフラグ</param>
        /// <param name="initialVk">初期の仮想キーコード</param>
        public HotkeyDialog(uint initialModifiers, uint initialVk)
        {
            InitializeComponent();
            Title = Strings.HotkeyDialogTitle;
            DescText.Text = Strings.HotkeyDialogDesc;
            ResetButton.Content = Strings.Reset;
            CancelButton.Content = Strings.Cancel;
            SaveButton.Content = Strings.Save;

            _modifiers = initialModifiers;
            _vk = initialVk;
            UpdateView();

            // キー入力のキャプチャ（プレビュー段階で処理）
            PreviewKeyDown += OnPreviewKeyDown;
            PreviewKeyUp += OnPreviewKeyUp;
            AddHandler(Keyboard.PreviewKeyDownEvent, new System.Windows.Input.KeyEventHandler(OnPreviewKeyDown), true);

            // Loaded イベントでメッセージフックを登録（ウィンドウハンドル作成後）
            Loaded += (s, e) =>
            {
                if (!_messageHookAttached)
                {
                    ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
                    _messageHookAttached = true;
                    LogService.Log("[HotkeyDialog] ThreadPreprocessMessage フック登録完了");
                }
            };

            // ボタンイベント
            CancelButton.Click += (s, e) => { DialogResult = false; };
            ResetButton.Click += (s, e) => { _modifiers = 0; _vk = 0; UpdateView(); };
            SaveButton.Click += OnSaveClicked;
        }

        /// <summary>
        /// ウィンドウが閉じられるときの処理。保存やキャンセル以外で閉じることを防止します。
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Save / Cancel を経由しない閉じ方をキャンセルする
            if (DialogResult == null)
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }

        /// <summary>
        /// ウィンドウが閉じられたときの処理。メッセージフックを解除します。
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (_messageHookAttached)
            {
                ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
                _messageHookAttached = false;
            }
            base.OnClosed(e);
        }

        /// <summary>
        /// プレビューキーイベント（押下）。Esc はリセットとして扱います。
        /// 修飾キー状態を集め、主要キー（修飾キー以外）を仮想キーコードとして設定します。
        /// </summary>
        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Esc はリセット
            if (e.Key == Key.Escape)
            {
                _modifiers = 0;
                _vk = 0;
                UpdateView();
                e.Handled = true;
                return;
            }

            if(e.Key == Key.PrintScreen)
            {
                LogService.Log("press PrintScreen");
            }

            // 現在の修飾キー状態を取得
            _modifiers = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) _modifiers |= MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) _modifiers |= MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _modifiers |= MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) _modifiers |= MOD_WIN;

            // 実際のキー（SystemKey を考慮）を取得し、純粋な修飾キーは無視する
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                // 純粋な修飾キーは主要キーではないため更新のみ
                UpdateView();
                e.Handled = true;
                return;
            }

            // 主要キーを仮想キーコードに変換して設定
            _vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            UpdateView();
            e.Handled = true;
        }

        /// <summary>
        /// プレビューキーアップイベント。特別な処理は行いません（現在の値を保持）。
        /// </summary>
        private void OnPreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 特に処理なし
            var key = e.Key;
            LogService.Log($"KeyUp: {key.ToString()}");
        }

        /// <summary>
        /// ThreadPreprocessMessage イベントハンドラ。PrintScreen キー押下をキャプチャします。
        /// </summary>
        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled) return;

                        // デバッグ: すべてのキーメッセージをログ出力
            if (msg.message == WM_KEYDOWN || msg.message == WM_SYSKEYDOWN)
            {
                LogService.Log($"[OnThreadPreprocessMessage] msg={msg.message:X}, wParam={msg.wParam:X}, lParam={msg.lParam:X}");
            }

            if ((msg.message == WM_KEYDOWN || msg.message == WM_SYSKEYDOWN) && msg.wParam == (IntPtr)VK_SNAPSHOT)
            {
                LogService.Log("[OnThreadPreprocessMessage] PrintScreen キー検出");
                
                // 現在の修飾キー状態を取得
                _modifiers = 0;
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) _modifiers |= MOD_CONTROL;
                if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) _modifiers |= MOD_ALT;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _modifiers |= MOD_SHIFT;
                if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) _modifiers |= MOD_WIN;

                _vk = VK_SNAPSHOT;
                UpdateView();
                handled = true;
            }
        }

        /// <summary>
        /// 表示を更新して、選択中のホットキー文字列やエラーメッセージ、保存可否を反映します。
        /// </summary>
        private void UpdateView()
        {
            // 表示文字列の構築
            if (_vk == 0)
            {
                CurrentText.Text = Strings.HotkeyNoneSelectedInfo;
            }
            else
            {
                var parts = new System.Collections.Generic.List<string>();
                if ((_modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
                if ((_modifiers & MOD_ALT) != 0) parts.Add("Alt");
                if ((_modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
                if ((_modifiers & MOD_WIN) != 0) parts.Add("Win");
                parts.Add(((Key)KeyInterop.KeyFromVirtualKey((int)_vk)).ToString());
                CurrentText.Text = string.Join(" + ", parts);
            }

            // 検証を実行してエラーメッセージと保存ボタンの状態を更新
            var result = HotkeyValidator.ValidateShortcut(_modifiers, _vk);
            ErrorText.Text = result.IsValid ? string.Empty : GetErrorText(result.Reason);
            SaveButton.IsEnabled = result.IsValid; // 無効な組合せは保存不可
        }

        /// <summary>
        /// 検証結果の理由に対応する日本語のエラーメッセージを返します。
        /// </summary>
        private string GetErrorText(HotkeyInvalidReason reason)
        {
            return reason switch
            {
                HotkeyInvalidReason.RequiresModifier => Strings.HotkeyErrorRequiresModifier,
                HotkeyInvalidReason.TooManyKeys => Strings.HotkeyErrorTooManyKeys,
                HotkeyInvalidReason.NoMainKey => Strings.HotkeyErrorNoMainKey,
                HotkeyInvalidReason.ForbiddenKey => Strings.HotkeyErrorForbiddenKey,
                HotkeyInvalidReason.OSReservedCombo => Strings.HotkeyErrorOSReservedCombo,
                HotkeyInvalidReason.PrintScreenNeedsModifier => Strings.HotkeyErrorPrintScreenNeedsModifier,
                _ => Strings.HotkeyInvalid
            };
        }

        /// <summary>
        /// 保存ボタン押下時の処理。登録可能かどうかを実際に RegisterHotKey を使って事前確認します。
        /// 成功すれば DialogResult を true に設定して閉じます。
        /// </summary>
        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            // 一時的な HwndSource を使って RegisterHotKey を試行する（事前検証）
            try
            {
                var srcParams = new System.Windows.Interop.HwndSourceParameters("HotkeyPreflightWindow")
                {
                    Width = 0,
                    Height = 0,
                    PositionX = 0,
                    PositionY = 0,
                    ParentWindow = new IntPtr(-3) // HWND_MESSAGE
                };
                using var src = new System.Windows.Interop.HwndSource(srcParams);
                bool ok = RegisterHotKey(src.Handle, 0x9001, _modifiers, _vk);
                if (ok)
                {
                    UnregisterHotKey(src.Handle, 0x9001);
                    DialogResult = true;
                }
                else
                {
                    ErrorText.Text = Strings.HotkeyPreflightFailed;
                    SaveButton.IsEnabled = false;
                }
            }
            catch (Exception)
            {
                ErrorText.Text = Strings.HotkeyPreflightFailed;
                SaveButton.IsEnabled = false;
            }
        }

        // user32 の RegisterHotKey / UnregisterHotKey の宣言
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
