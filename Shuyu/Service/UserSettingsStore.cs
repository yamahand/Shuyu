using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace Shuyu
{
    /// <summary>
    /// ユーザー設定のシリアライズ対象クラスです。
    /// </summary>
    internal class UserSettings
    {
        /// <summary>
        /// 低レベルキーボードフックを使用するかどうかを示します。
        /// </summary>
        public bool useLowLevelHook { get; set; } = false;
    }

    /// <summary>
    /// 簡易的な設定ストア。%APPDATA%\Shuyu\settings.json にユーザー設定を永続化します。
    /// </summary>
    internal static class UserSettingsStore
    {
        private static readonly string _appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Shuyu");
        private static readonly string _filePath = Path.Combine(_appDir, "settings.json");

        /// <summary>
        /// 設定ファイルからユーザー設定を読み込みます。ファイルが存在しない場合はデフォルト設定を返します。
        /// </summary>
        /// <returns>読み込まれた <see cref="UserSettings"/> インスタンス。</returns>
        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new UserSettings();

                var json = File.ReadAllText(_filePath);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var s = JsonSerializer.Deserialize<UserSettings>(json, opts);
                return s ?? new UserSettings();
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[Shuyu] UserSettingsStore.Load error: {ex}");
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
                Directory.CreateDirectory(_appDir);
                var tmp = _filePath + ".tmp";
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(tmp, json);
                File.Move(tmp, _filePath, true);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[Shuyu] UserSettingsStore.Save error: {ex}");
#endif
            }
        }
    }
}
