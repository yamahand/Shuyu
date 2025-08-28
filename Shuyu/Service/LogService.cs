using System;
using System.Collections.Concurrent;
using System.Windows.Threading;
using System.IO;
using System.Runtime.CompilerServices;

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
        /// Visual Studio の出力ウィンドウにも出力するかのフラグ（既定: true）
        /// </summary>
        public bool OutputToVSOutput { get; set; } = true;

        private LogService() { }

        private void RunOnUI(Action action)
        {
            var d = uiDispatcher;
            if (d.CheckAccess()) action();
            else d.BeginInvoke(action);
        }

        public void InitializeLogWindow()
        {
            RunOnUI(() =>
            {
                if (_debugLogWindow == null)
                {
                    _debugLogWindow = new DebugLogWindow();
#if DEBUG
                    _debugLogWindow.Show();
                    _debugLogWindow.Dispatcher.BeginInvoke(
                        DispatcherPriority.ApplicationIdle,
                        new Action(() => { _debugLogWindow.BringToAbsoluteFront(); }));
#endif
                }

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

        public void HideLogWindow() => RunOnUI(() => _debugLogWindow?.Hide());

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

        private void AddLogInternal(string message) => AddLogInternal(LogLevel.None, message);

        private void AddLogInternal(LogLevel level, string message)
        {
            // Visual Studio の出力ウィンドウへも出力（有効時）
            if (OutputToVSOutput)
            {
                System.Diagnostics.Trace.WriteLine(message);
            }

            // ウィンドウ未初期化時はバッファに積む
            if (_debugLogWindow == null)
            {
                _pendingLogs.Enqueue((level, message));
                return;
            }

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

        // 呼び出し元情報付きでメッセージ整形
        private static string WithCaller(string message, string filePath, int lineNumber)
        {
            var file = string.IsNullOrEmpty(filePath) ? "?" : Path.GetFileName(filePath);
            var sanitizedMessage = SecurityHelper.SanitizeLogMessage(message);
            return $"[{file}:{lineNumber}] {sanitizedMessage}";
        }

        // === 静的メソッド（どのクラスからでも呼び出し可能） ===

        public static void Log(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.AddLogInternal(WithCaller(message, filePath, lineNumber));
        }

        public static void LogInfo(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.AddLogInternal(LogLevel.Info, WithCaller(message, filePath, lineNumber));
        }

        public static void LogWarning(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.AddLogInternal(LogLevel.Warning, WithCaller(message, filePath, lineNumber));
        }

        public static void LogError(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.AddLogInternal(LogLevel.Error, WithCaller(message, filePath, lineNumber));
        }

        public static void LogDebug(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
#if DEBUG
            Instance.AddLogInternal(WithCaller($"[DEBUG] {message}", filePath, lineNumber));
#endif
        }

        public static void LogException(Exception ex, string context = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var core = string.IsNullOrEmpty(context)
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{context} - {ex.GetType().Name}: {ex.Message}";

            // リリースビルドでは例外の詳細なスタックトレースを制限
#if DEBUG
            Instance.AddLogInternal(LogLevel.Error, WithCaller($"[EXCEPTION] {core}", filePath, lineNumber));
            Instance.AddLogInternal(LogLevel.Error, WithCaller($"[STACK] {ex.StackTrace}", filePath, lineNumber));
#else
            // リリースビルドではサニタイズされたメッセージのみ
            var sanitizedMessage = SecurityHelper.SanitizeLogMessage(core);
            Instance.AddLogInternal(LogLevel.Error, $"[{Path.GetFileName(filePath)}:{lineNumber}] [EXCEPTION] {sanitizedMessage}");
#endif
        }

        public static void LogMultiple(params string[] messages)
        {
            foreach (var message in messages)
            {
                Instance.AddLogInternal(message);
            }
        }
    }
}
