using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using Shuyu.Service;

namespace Shuyu
{
    /// <summary>
    /// ホットキー設定を表します。
    /// </summary>
    internal class HotkeySettings
    {
        /// <summary>
        /// モディファイアキー（Ctrl=0x0002, Alt=0x0001, Shift=0x0004, Win=0x0008の組み合わせ）
        /// </summary>
        public uint modifiers { get; set; } = 0x0004; // デフォルト: Shift
        
        /// <summary>
        /// 仮想キーコード
        /// </summary>
        public uint virtualKey { get; set; } = 0x2C; // デフォルト: PrintScreen
        
        /// <summary>
        /// 表示用文字列（例: "Shift + PrintScreen"）
        /// </summary>
        public string displayText { get; set; } = "Shift + PrintScreen";
    }

    /// <summary>
    /// ユーザー設定のシリアライズ対象クラスです。
    /// </summary>
    internal class UserSettings
    {
        /// <summary>
        /// 低レベルキーボードフックを使用するかどうかを示します。
        /// </summary>
        public bool useLowLevelHook { get; set; } = false;
        
        /// <summary>
        /// アプリケーションの言語設定を示します。nullの場合はシステム設定に従います。
        /// </summary>
        public string? language { get; set; } = null;
        
        /// <summary>
        /// ホットキー設定。nullの場合はデフォルト設定（Shift+PrintScreen）を使用します。
        /// </summary>
        public HotkeySettings? hotkey { get; set; } = null;
    }

    /// <summary>
    /// 簡易的な設定ストア。%APPDATA%\Shuyu\settings.json にユーザー設定を永続化します。
    /// </summary>
    internal static class UserSettingsStore
    {
        private static readonly string _filePath = SecurityHelper.GetSafeSettingsPath();

        /// <summary>
        /// 設定ファイルからユーザー設定を読み込みます。ファイルが存在しない場合はデフォルト設定を返します。
        /// </summary>
        /// <returns>読み込まれた <see cref="UserSettings"/> インスタンス。</returns>
        public static UserSettings Load()
        {
            try
            {
                var json = SecurityHelper.SafeReadSettingsFile(_filePath);
                if (string.IsNullOrEmpty(json))
                    return new UserSettings();

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var s = JsonSerializer.Deserialize<UserSettings>(json, opts);
                return s ?? new UserSettings();
            }
            catch (Exception ex)
            {
#if DEBUG
                LogService.LogException(ex, "UserSettingsStore.Load");
#else
                LogService.LogWarning($"設定ファイル読み込みエラー: {SecurityHelper.SanitizeLogMessage(ex.Message)}");
#endif
                return new UserSettings();
            }
        }

        /// <summary>
        /// ユーザー設定を設定ファイルに保存します。
        /// </summary>
        /// <param name="settings">保存する <see cref="UserSettings"/> インスタンス。</param>
        public static void Save(UserSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                if (!SecurityHelper.SafeWriteSettingsFile(_filePath, json))
                {
                    LogService.LogError("設定ファイルの保存に失敗しました");
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                LogService.LogException(ex, "UserSettingsStore.Save");
#else
                LogService.LogError($"設定保存エラー: {SecurityHelper.SanitizeLogMessage(ex.Message)}");
#endif
            }
        }
    }
}
