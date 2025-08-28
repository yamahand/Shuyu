using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Shuyu.Service;

namespace Shuyu
{
    /// <summary>
    /// デバッグログを表示するウィンドウ。キャプチャオーバーレイより前面に表示され、リアルタイムでログを確認できます。
    /// </summary>
    public partial class DebugLogWindow : Window
    {
        // Win32 API定数とインポート
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOW = 5;

        /// <summary>
        /// ログメッセージを蓄積するStringBuilder
        /// </summary>
        private readonly StringBuilder _logBuffer;
        
        /// <summary>
        /// UIスレッド同期用のDispatcher
        /// </summary>
        private readonly Dispatcher _dispatcher;
        
        /// <summary>
        /// 最大ログ行数（メモリ使用量制限のため）
        /// </summary>
        private const int MaxLogLines = 1000;
        
        /// <summary>
        /// 現在のログ行数
        /// </summary>
        private int _currentLogLines = 0;

        /// <summary>
        /// Close を許可するフラグ（通常は OnClosing でキャンセルする）
        /// </summary>
        private bool _allowClose = false;

        // 追加: UI 準備完了フラグ
        private bool _uiReady = false;

        /// <summary>
        /// DebugLogWindow の新しいインスタンスを初期化します。
        /// </summary>
        public DebugLogWindow()
        {
            InitializeComponent();
            
            // ログバッファとDispatcherを初期化
            _logBuffer = new StringBuilder();
            _dispatcher = Dispatcher.CurrentDispatcher;
            
            // ウィンドウの初期設定
            InitializeWindowSettings();
            
            // Loadedイベントで最前面表示を確実にする
            this.Loaded += DebugLogWindow_Loaded;
            
            // 初期ログメッセージを追加
            AddLog("=== Shuyu Debug Log Started ===");
        }

        /// <summary>
        /// 強制的にウィンドウを閉じます（OnClosing のキャンセルを無効化）。
        /// </summary>
        public void ForceClose()
        {
            _allowClose = true;
            this.Close();
        }

        /// <summary>
        /// ウィンドウがロードされたときの処理
        /// </summary>
        private void DebugLogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var cb = this.FindName("VsOutputCheckBox") as System.Windows.Controls.CheckBox;
                if (cb != null)
                {
                    // 一時的に解除してから状態同期（イベント連鎖によるAddLog発火を防止）
                    cb.Checked -= VsOutputCheckBox_Checked;
                    cb.Unchecked -= VsOutputCheckBox_Unchecked;

                    cb.IsChecked = Shuyu.Service.LogService.Instance.OutputToVSOutput;

                    cb.Checked += VsOutputCheckBox_Checked;
                    cb.Unchecked += VsOutputCheckBox_Unchecked;
                }
            }
            catch { }

            BringToAbsoluteFront();

            // ここからログ出力を許可
            _uiReady = true;
        }

        /// <summary>
        /// 最前面表示維持タイマーのイベントハンドラー
        /// </summary>
        private void KeepFrontTimer_Tick(object? sender, EventArgs e)
        {
            // ウィンドウが表示されている場合のみ最前面に保持
            if (this.IsVisible && this.WindowState != WindowState.Minimized)
            {
                BringToAbsoluteFront();
            }
        }

        /// <summary>
        /// ウィンドウの初期設定を行います。
        /// </summary>
        private void InitializeWindowSettings()
        {
            // ウィンドウを右上角に配置
            this.Left = SystemParameters.WorkArea.Right - this.Width - 20;
            this.Top = 20;
            
            // 常に最前面に表示（デバッグ時の視認性向上）
            this.Topmost = true;
            
            // タスクバーには表示しない
            this.ShowInTaskbar = false;
        }

        /// <summary>
        /// ウィンドウを確実に最前面に配置します。
        /// </summary>
        public void BringToAbsoluteFront()
        {
            // まずウィンドウを表示
            if (!this.IsVisible)
            {
                this.Show();
            }

            // ウィンドウハンドルを取得
            var windowHandle = new WindowInteropHelper(this).Handle;
            if (windowHandle != IntPtr.Zero)
            {
                // 複数の方法で最前面に表示を試行
                
                // 1. まずTopmostを解除してから再設定
                this.Topmost = false;
                
                // 2. Win32 APIでTopmostに設定
                SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, 
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                
                // 3. フォアグラウンドウィンドウに設定
                SetForegroundWindow(windowHandle);
                
                // 4. ウィンドウを表示
                ShowWindow(windowHandle, SW_SHOW);
                
                // 5. WPFのTopmostプロパティを設定
                this.Topmost = true;
                
                // 6. アクティベート
                this.Activate();
                
                // 7. フォーカスを設定
                this.Focus();
            }
            else
            {
                // ハンドルが取得できない場合はWPFの方法のみ使用
                this.Topmost = false;
                this.Topmost = true;
                this.Activate();
                this.Focus();
            }
        }

        /// <summary>
        /// ログメッセージを追加します。UIスレッド以外からも安全に呼び出せます。
        /// </summary>
        /// <param name="message">追加するログメッセージ</param>
        public void AddLog(string message)
        {
            var disp = _dispatcher ?? Dispatcher.CurrentDispatcher;

            if (!disp.CheckAccess())
            {
                disp.BeginInvoke(new Action<string>(AddLog), message);
                return;
            }

            var timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

            _logBuffer?.AppendLine(timestampedMessage);
            _currentLogLines++;

            if (_currentLogLines > MaxLogLines)
            {
                TrimOldLogs();
            }

            // UI要素が未生成なら描画はスキップ
            if (LogTextBlock != null)
                LogTextBlock.Text = _logBuffer?.ToString() ?? string.Empty;

            if (LogScrollViewer != null)
                LogScrollViewer.ScrollToEnd();
        }

        /// <summary>
        /// 古いログを削除してメモリ使用量を制限します。
        /// </summary>
        private void TrimOldLogs()
        {
            var lines = _logBuffer.ToString().Split('\n');
            var linesToKeep = MaxLogLines - 100; // 100行分余裕を持って削除
            
            // 新しいバッファを作成
            _logBuffer.Clear();
            for (int i = lines.Length - linesToKeep; i < lines.Length; i++)
            {
                if (i >= 0 && !string.IsNullOrEmpty(lines[i]))
                {
                    _logBuffer.AppendLine(lines[i]);
                }
            }
            
            _currentLogLines = linesToKeep;
        }

        /// <summary>
        /// ログ表示を更新します。
        /// </summary>
        private void UpdateLogDisplay()
        {
            LogTextBlock.Text = _logBuffer.ToString();
        }

        /// <summary>
        /// スクロールビューアを最下部までスクロールします。
        /// </summary>
        private void ScrollToBottom()
        {
            LogScrollViewer.ScrollToEnd();
        }

        /// <summary>
        /// クリアボタンのクリックイベント。ログをクリアします。
        /// </summary>
        /// <param name="sender">イベント送信者</param>
        /// <param name="e">イベント引数</param>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // ログバッファをクリア
            _logBuffer.Clear();
            _currentLogLines = 0;
            
            // UI表示をクリア
            LogTextBlock.Text = string.Empty;
            
            // クリア完了のログを追加
            AddLog("=== Log Cleared ===");
        }

        /// <summary>
        /// 保存ボタンのクリックイベント。ログをファイルに保存します。
        /// </summary>
        /// <param name="sender">イベント送信者</param>
        /// <param name="e">イベント引数</param>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ファイル保存ダイアログを表示
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "デバッグログを保存",
                    Filter = "テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"Shuyu_DebugLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // ログをファイルに保存
                    File.WriteAllText(saveDialog.FileName, _logBuffer.ToString(), Encoding.UTF8);
                    AddLog($"ログを保存しました: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"ログ保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 閉じるボタンのクリックイベント。ウィンドウを非表示にします。
        /// </summary>
        /// <param name="sender">イベント送信者</param>
        /// <param name="e">イベント引数</param>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // ウィンドウを非表示（完全に閉じずに隠すだけ）
            this.Hide();
        }

        /// <summary>
        /// ウィンドウのClosingイベント。完全に閉じる代わりに非表示にします。
        /// </summary>
        /// <param name="e">イベント引数</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
            {
                // 閉じる操作をキャンセルして非表示にする
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        /// <summary>
        /// 複数行のログを一度に追加します。
        /// </summary>
        /// <param name="messages">追加するログメッセージの配列</param>
        public void AddLogs(params string[] messages)
        {
            foreach (var message in messages)
            {
                AddLog(message);
            }
        }

        /// <summary>
        /// エラーレベルのログを追加します（赤色で表示される予定）。
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        public void AddErrorLog(string message)
        {
            AddLog($"[ERROR] {message}");
        }

        /// <summary>
        /// 警告レベルのログを追加します（黄色で表示される予定）。
        /// </summary>
        /// <param name="message">警告メッセージ</param>
        public void AddWarningLog(string message)
        {
            AddLog($"[WARNING] {message}");
        }

        /// <summary>
        /// 情報レベルのログを追加します。
        /// </summary>
        /// <param name="message">情報メッセージ</param>
        public void AddInfoLog(string message)
        {
            AddLog($"[INFO] {message}");
        }

        /// <summary>
        /// VS出力チェックの変更イベント。LogService のフラグを更新します。
        /// </summary>
        private void VsOutputCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            Shuyu.Service.LogService.Instance.OutputToVSOutput = true;
            if (_uiReady && _logBuffer != null)
                AddLog("[INFO] Visual Studio 出力: 有効");
        }

        private void VsOutputCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            Shuyu.Service.LogService.Instance.OutputToVSOutput = false;
            if (_uiReady && _logBuffer != null)
                AddLog("[INFO] Visual Studio 出力: 無効");
        }
    }
}
