using System;
using System.Windows.Threading;

namespace Shuyu.Service
{
    /// <summary>
    /// アプリケーション全体のログ管理を行うシングルトンサービス。
    /// どのクラスからでも静的メソッドでログ出力可能です。
    /// </summary>
    public class LogService
    {
        /// <summary>
        /// シングルトンインスタンス
        /// </summary>
        private static LogService? _instance;
        
        /// <summary>
        /// スレッドセーフなインスタンス取得用のロックオブジェクト
        /// </summary>
        private static readonly object _lock = new object();
        
        /// <summary>
        /// ログウィンドウのインスタンス
        /// </summary>
        private DebugLogWindow? _debugLogWindow;
        
        /// <summary>
        /// UIスレッドのDispatcher
        /// </summary>
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// プライベートコンストラクタ（シングルトンパターン）
        /// </summary>
        private LogService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// シングルトンインスタンスを取得します。
        /// </summary>
        public static LogService Instance
        {
            get
            {
                // ダブルチェックロッキングパターンでスレッドセーフに初期化
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LogService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// ログウィンドウを初期化します。MainWindowから呼び出します。
        /// </summary>
        public void InitializeLogWindow()
        {
            if (_debugLogWindow == null)
            {
                _debugLogWindow = new DebugLogWindow();
                
                // デバッグビルド時のみ表示
#if DEBUG
                _debugLogWindow.Show();
                // 遅延実行で確実に最前面に表示
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    new Action(() => {
                        _debugLogWindow.BringToAbsoluteFront();
                    }));
#endif
            }
        }

        /// <summary>
        /// ログウィンドウを表示します。
        /// </summary>
        public void ShowLogWindow()
        {
            if (_debugLogWindow != null)
            {
                _debugLogWindow.Show();
                _debugLogWindow.Activate();
                _debugLogWindow.Topmost = true;
            }
        }

        /// <summary>
        /// ログウィンドウを最前面に再配置します。
        /// </summary>
        public void BringLogWindowToFront()
        {
            if (_debugLogWindow != null && _debugLogWindow.IsVisible)
            {
                _debugLogWindow.BringToAbsoluteFront();
            }
        }

        /// <summary>
        /// 最前面表示維持を開始します
        /// </summary>
        public void StartKeepingLogWindowFront()
        {
            _debugLogWindow?.StartKeepingFront();
        }

        /// <summary>
        /// 最前面表示維持を停止します
        /// </summary>
        public void StopKeepingLogWindowFront()
        {
            _debugLogWindow?.StopKeepingFront();
        }

        /// <summary>
        /// ログウィンドウを非表示にします。
        /// </summary>
        public void HideLogWindow()
        {
            _debugLogWindow?.Hide();
        }

        /// <summary>
        /// ログウィンドウを破棄します。
        /// </summary>
        public void DisposeLogWindow()
        {
            _debugLogWindow?.Close();
            _debugLogWindow = null;
        }

        /// <summary>
        /// 内部的にログを追加する処理
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        private void AddLogInternal(string message)
        {
            // UIスレッドでない場合はInvokeで切り替え
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(new Action<string>(AddLogInternal), message);
                return;
            }

            // ログウィンドウにメッセージを追加
            _debugLogWindow?.AddLog(message);
        }

        // === 静的メソッド（どのクラスからでも呼び出し可能） ===

        /// <summary>
        /// 一般的なログメッセージを出力します。
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        public static void Log(string message)
        {
            Instance.AddLogInternal(message);
        }

        /// <summary>
        /// 情報レベルのログを出力します。
        /// </summary>
        /// <param name="message">情報メッセージ</param>
        public static void LogInfo(string message)
        {
            Instance.AddLogInternal($"[INFO] {message}");
        }

        /// <summary>
        /// 警告レベルのログを出力します。
        /// </summary>
        /// <param name="message">警告メッセージ</param>
        public static void LogWarning(string message)
        {
            Instance.AddLogInternal($"[WARNING] {message}");
        }

        /// <summary>
        /// エラーレベルのログを出力します。
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        public static void LogError(string message)
        {
            Instance.AddLogInternal($"[ERROR] {message}");
        }

        /// <summary>
        /// デバッグレベルのログを出力します（DEBUGビルド時のみ）。
        /// </summary>
        /// <param name="message">デバッグメッセージ</param>
        public static void LogDebug(string message)
        {
#if DEBUG
            Instance.AddLogInternal($"[DEBUG] {message}");
#endif
        }

        /// <summary>
        /// 例外の詳細ログを出力します。
        /// </summary>
        /// <param name="ex">例外オブジェクト</param>
        /// <param name="context">例外が発生したコンテキスト</param>
        public static void LogException(Exception ex, string context = "")
        {
            var message = string.IsNullOrEmpty(context) 
                ? $"[EXCEPTION] {ex.GetType().Name}: {ex.Message}"
                : $"[EXCEPTION] {context} - {ex.GetType().Name}: {ex.Message}";
            
            Instance.AddLogInternal(message);
            
            // スタックトレースも出力（デバッグビルド時のみ）
#if DEBUG
            Instance.AddLogInternal($"[STACK] {ex.StackTrace}");
#endif
        }

        /// <summary>
        /// 複数のログメッセージを一度に出力します。
        /// </summary>
        /// <param name="messages">ログメッセージの配列</param>
        public static void LogMultiple(params string[] messages)
        {
            foreach (var message in messages)
            {
                Instance.AddLogInternal(message);
            }
        }

        /// <summary>
        /// フォーマット文字列を使ってログを出力します。
        /// </summary>
        /// <param name="format">フォーマット文字列</param>
        /// <param name="args">フォーマット引数</param>
        public static void LogFormat(string format, params object[] args)
        {
            try
            {
                var message = string.Format(format, args);
                Instance.AddLogInternal(message);
            }
            catch (Exception ex)
            {
                // フォーマットエラーの場合は元の文字列をそのまま出力
                Instance.AddLogInternal($"[FORMAT_ERROR] {format} - {ex.Message}");
            }
        }
    }
}
