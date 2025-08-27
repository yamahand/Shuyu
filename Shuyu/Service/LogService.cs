using System;
using System.Collections.Concurrent;
using System.Windows.Threading;

namespace Shuyu.Service
{
    /// <summary>
    /// アプリケーション全体のログ管理を行うシングルトンサービス。
    /// どのクラスからでも静的メソッドでログ出力可能です。
    /// </summary>
    public class LogService
    {
        // ログレベルの内部表現
        private enum LogLevel { None, Info, Warning, Error }

        /// <summary>
        /// シングルトンインスタンス（Lazy 初期化）
        /// </summary>
        private static readonly Lazy<LogService> _lazy = new(() => new LogService());

        /// <summary>
        /// シングルトンインスタンスを取得します。
        /// </summary>
        public static LogService Instance => _lazy.Value;

        /// <summary>
        /// ログウィンドウのインスタンス
        /// </summary>
        private DebugLogWindow? _debugLogWindow;

        /// <summary>
        /// UI Dispatcher を動的に解決します。
        /// </summary>
        private Dispatcher uiDispatcher =>
            _debugLogWindow?.Dispatcher
            ?? System.Windows.Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher;

        /// <summary>
        /// ウィンドウ未初期化時に蓄積する保留ログ
        /// </summary>
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _pendingLogs = new();

        /// <summary>
        /// プライベートコンストラクタ（シングルトンパターン）
        /// </summary>
        private LogService()
        {
        }

        /// <summary>
        /// UI スレッドで処理を実行するヘルパー
        /// </summary>
        private void RunOnUI(Action action)
        {
            var d = uiDispatcher;
            if (d.CheckAccess()) action();
            else d.BeginInvoke(action);
        }

        /// <summary>
        /// ログウィンドウを初期化します。MainWindowから呼び出します。
        /// </summary>
        public void InitializeLogWindow()
        {
            RunOnUI(() =>
            {
                if (_debugLogWindow == null)
                {
                    _debugLogWindow = new DebugLogWindow();
#if DEBUG
                    _debugLogWindow.Show();
                    // 遅延実行で確実に最前面に表示
                    _debugLogWindow.Dispatcher.BeginInvoke(
                        DispatcherPriority.ApplicationIdle,
                        new Action(() => { _debugLogWindow.BringToAbsoluteFront(); }));
#endif
                }

                // 保留ログをフラッシュ
                if (!_pendingLogs.IsEmpty && _debugLogWindow != null)
                {
                    while (_pendingLogs.TryDequeue(out var item))
                    {
                        switch (item.Level)
                        {
                            case LogLevel.Info:
                                _debugLogWindow.AddInfoLog(item.Message);
                                break;
                            case LogLevel.Warning:
                                _debugLogWindow.AddWarningLog(item.Message);
                                break;
                            case LogLevel.Error:
                                _debugLogWindow.AddErrorLog(item.Message);
                                break;
                            default:
                                _debugLogWindow.AddLog(item.Message);
                                break;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// ログウィンドウを表示します。
        /// </summary>
        public void ShowLogWindow()
        {
            RunOnUI(() =>
            {
                if (_debugLogWindow != null)
                {
                    _debugLogWindow.Show();
                    _debugLogWindow.Activate();
                    _debugLogWindow.Topmost = true;
                }
            });
        }

        /// <summary>
        /// ログウィンドウを最前面に再配置します。
        /// </summary>
        public void BringLogWindowToFront()
        {
            RunOnUI(() =>
            {
                if (_debugLogWindow != null && _debugLogWindow.IsVisible)
                {
                    _debugLogWindow.BringToAbsoluteFront();
                }
            });
        }

        /// <summary>
        /// ログウィンドウを非表示にします。
        /// </summary>
        public void HideLogWindow()
        {
            RunOnUI(() => _debugLogWindow?.Hide());
        }

        /// <summary>
        /// ログウィンドウを破棄します。
        /// </summary>
        public void DisposeLogWindow()
        {
            RunOnUI(() =>
            {
                if (_debugLogWindow != null)
                {
                    _debugLogWindow.ForceClose();
                    _debugLogWindow = null;
                }
            });
        }

        /// <summary>
        /// 内部的にログを追加する処理（レベルなし")
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        private void AddLogInternal(string message)
        {
            AddLogInternal(LogLevel.None, message);
        }

        /// <summary>
        /// 内部的にログを追加する処理（レベル付き）
        /// </summary>
        private void AddLogInternal(LogLevel level, string message)
        {
            // コンソールにも出力
            System.Diagnostics.Trace.WriteLine(message);

            // ウィンドウ未初期化時はバッファに積む
            if (_debugLogWindow == null)
            {
                _pendingLogs.Enqueue((level, message));
                return;
            }

            // UI スレッドで実行
            RunOnUI(() =>
            {
                if (_debugLogWindow == null)
                {
                    _pendingLogs.Enqueue((level, message));
                    return;
                }

                switch (level)
                {
                    case LogLevel.Info:
                        _debugLogWindow.AddInfoLog(message);
                        break;
                    case LogLevel.Warning:
                        _debugLogWindow.AddWarningLog(message);
                        break;
                    case LogLevel.Error:
                        _debugLogWindow.AddErrorLog(message);
                        break;
                    default:
                        _debugLogWindow.AddLog(message);
                        break;
                }
            });
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
            Instance.AddLogInternal(LogLevel.Info, message);
        }

        /// <summary>
        /// 警告レベルのログを出力します。
        /// </summary>
        /// <param name="message">警告メッセージ</param>
        public static void LogWarning(string message)
        {
            Instance.AddLogInternal(LogLevel.Warning, message);
        }

        /// <summary>
        /// エラーレベルのログを出力します。
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        public static void LogError(string message)
        {
            Instance.AddLogInternal(LogLevel.Error, message);
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
    }
}
