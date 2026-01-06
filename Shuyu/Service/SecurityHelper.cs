using System;
using System.IO;
using System.Security; // SecurityException 用
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shuyu.Service
{
    /// <summary>
    /// セキュリティ関連の機能を提供するヘルパークラスです。
    /// </summary>
    public static class SecurityHelper
    {
        private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".dds" };
        // Windows では : はドライブレター（C:）で有効なので、ドライブレター以外での : をチェック
        private static readonly Regex InvalidPathCharsRegex = new(@"[<>""|?*]", RegexOptions.Compiled);
        // ドライブレターパス（C:\, D:/ など）を検出
        private static readonly Regex DriveLetterPathRegex = new(@"^[a-zA-Z]:[/\\]", RegexOptions.Compiled);
        private static readonly string ApplicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Shuyu");

        /// <summary>
        /// ファイルパスが安全かどうかを検証します。
        /// </summary>
        /// <param name="filePath">検証するファイルパス</param>
        /// <param name="allowedExtensions">許可する拡張子の配列（nullの場合は画像ファイルのみ許可）</param>
        /// <returns>安全なパスの場合はtrue、そうでなければfalse</returns>
        public static bool IsValidFilePath(string filePath, string[]? allowedExtensions = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                // パスの正規化
                var normalizedPath = Path.GetFullPath(filePath);
                
                // 危険な文字をチェック
                if (InvalidPathCharsRegex.IsMatch(filePath))
                    return false;
                
                // コロン（:）はドライブレター以外では無効
                // ドライブレターパス（C:\, D:/ など）の場合、3文字目以降にコロンがないかチェック
                if (DriveLetterPathRegex.IsMatch(filePath))
                {
                    // ドライブレター後（3文字目以降）にコロンがあれば無効
                    if (filePath.Length > 2 && filePath.Substring(2).Contains(':'))
                        return false;
                }
                else
                {
                    // ドライブレターパスでない場合、コロンが含まれていれば無効
                    if (filePath.Contains(':'))
                        return false;
                }

                // 相対パス攻撃をチェック
                if (filePath.Contains("..") || filePath.Contains("./") || filePath.Contains(".\\"))
                    return false;

                // システムディレクトリへの書き込みを禁止
                var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                if (normalizedPath.StartsWith(systemPath, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(windowsPath, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(programFilesPath, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(programFilesX86Path, StringComparison.OrdinalIgnoreCase))
                    return false;

                // 拡張子のチェック
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var extensionsToCheck = allowedExtensions ?? AllowedImageExtensions;
                
                bool extensionAllowed = false;
                foreach (var allowedExt in extensionsToCheck)
                {
                    if (extension == allowedExt.ToLowerInvariant())
                    {
                        extensionAllowed = true;
                        break;
                    }
                }
                
                if (!extensionAllowed)
                    return false;

                // ディレクトリが存在するかチェック
                var directory = Path.GetDirectoryName(normalizedPath);
                return !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                LogService.LogError($"ファイルパス検証エラー: {SanitizeLogMessage(ex.Message)}");
                return false;
            }
        }

        /// <summary>
        /// 設定ファイルのJSONが有効かどうかを検証します。
        /// </summary>
        /// <param name="jsonContent">検証するJSON文字列</param>
        /// <returns>有効なJSONの場合はtrue、そうでなければfalse</returns>
        public static bool IsValidSettingsJson(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
                return false;

            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                // 必要な設定項目の存在をチェック
                // 設定ファイルの構造に応じて調整が必要
                return root.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException ex)
            {
                LogService.LogError($"設定ファイルJSON検証エラー: {SanitizeLogMessage(ex.Message)}");
                return false;
            }
        }

        /// <summary>
        /// 安全な設定ファイルパスを取得します。
        /// </summary>
        /// <returns>安全な設定ファイルパス</returns>
        public static string GetSafeSettingsPath()
        {
            Directory.CreateDirectory(ApplicationDataPath);
            return Path.Combine(ApplicationDataPath, "settings.json");
        }

        /// <summary>
        /// ログメッセージを安全にサニタイズします。
        /// </summary>
        /// <param name="message">サニタイズするメッセージ</param>
        /// <returns>サニタイズされたメッセージ</returns>
        public static string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            // 機密情報の可能性があるパターンをマスク
            var sanitized = message;
            
            // ファイルパスの一部を隠す
            sanitized = Regex.Replace(sanitized, @"[A-Za-z]:\\[^\\]*\\", @"<PATH>\", RegexOptions.IgnoreCase);
            
            // 環境変数やユーザー名を隠す（ユーザー名は正規表現エスケープ + 大文字小文字無視）
            var escapedUser = Regex.Escape(Environment.UserName);
            sanitized = Regex.Replace(sanitized, escapedUser, "<USER>", RegexOptions.IgnoreCase);
            
            // 長すぎるメッセージを切り詰める
            if (sanitized.Length > 500)
                sanitized = sanitized.Substring(0, 497) + "...";

            return sanitized;
        }

        /// <summary>
        /// 設定ファイルを安全に読み込みます。
        /// </summary>
        /// <param name="filePath">設定ファイルパス</param>
        /// <returns>設定内容、読み込み失敗時はnull</returns>
        public static string? SafeReadSettingsFile(string filePath)
        {
            try
            {
                if (!IsValidFilePath(filePath, new[] { ".json" }))
                {
                    LogService.LogWarning("設定ファイルパスが無効です");
                    return null;
                }

                if (!File.Exists(filePath))
                    return null;

                var content = File.ReadAllText(filePath, Encoding.UTF8);
                
                if (!IsValidSettingsJson(content))
                {
                    LogService.LogWarning("設定ファイルのJSON形式が無効です");
                    return null;
                }

                return content;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                LogService.LogError($"設定ファイル読み込みエラー: {SanitizeLogMessage(ex.Message)}");
                return null;
            }
        }

        /// <summary>
        /// 設定ファイルを安全に書き込みます。
        /// </summary>
        /// <param name="filePath">設定ファイルパス</param>
        /// <param name="content">設定内容</param>
        /// <returns>成功時はtrue、失敗時はfalse</returns>
        public static bool SafeWriteSettingsFile(string filePath, string content)
        {
            try
            {
                if (!IsValidFilePath(filePath, new[] { ".json" }))
                {
                    LogService.LogWarning("設定ファイルパスが無効です");
                    return false;
                }

                if (!IsValidSettingsJson(content))
                {
                    LogService.LogWarning("設定ファイルのJSON形式が無効です");
                    return false;
                }

                // 一時ファイルに書き込んでから移動（アトミック操作）
                var tempFile = filePath + ".tmp";
                File.WriteAllText(tempFile, content, Encoding.UTF8);
                File.Move(tempFile, filePath);

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                LogService.LogError($"設定ファイル書き込みエラー: {SanitizeLogMessage(ex.Message)}");
                return false;
            }
        }
    }
}