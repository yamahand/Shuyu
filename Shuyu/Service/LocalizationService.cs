using System;
using System.Globalization;
using System.Threading;
using Shuyu.Resources;

namespace Shuyu.Service
{
    /// <summary>
    /// アプリケーションの多言語機能を管理するサービスです。
    /// </summary>
    internal static class LocalizationService
    {
        /// <summary>
        /// 言語が変更された時に発生するイベント
        /// </summary>
        public static event Action? LanguageChanged;

        /// <summary>
        /// 指定された言語コードに基づいて言語を設定します。
        /// </summary>
        /// <param name="languageCode">言語コード（"ja", "en", null=システム設定）</param>
        public static void SetLanguage(string? languageCode)
        {
            try
            {
                var culture = string.IsNullOrEmpty(languageCode)
                    ? CultureInfo.CurrentUICulture // システム設定を使用
                    : new CultureInfo(languageCode);
                
                // UIスレッドとバックグラウンドスレッドの両方に設定
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
                
                // リソース管理のカルチャを設定
                Strings.Culture = culture;
                
                LogService.LogInfo($"言語設定を変更しました: {culture.DisplayName} ({culture.Name})");
                
                // イベントを発火
                LanguageChanged?.Invoke();
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "言語設定の変更に失敗しました");
                
                // フォールバックとしてシステム設定を使用
                if (!string.IsNullOrEmpty(languageCode))
                {
                    SetLanguage(null);
                }
            }
        }
        
        /// <summary>
        /// 現在の言語設定から言語コードを取得します。
        /// </summary>
        /// <returns>現在の言語コード（"ja", "en", など）</returns>
        public static string GetCurrentLanguageCode()
        {
            var culture = Strings.Culture ?? Thread.CurrentThread.CurrentUICulture;
            return culture.TwoLetterISOLanguageName;
        }
        
        /// <summary>
        /// 保存された言語設定を読み込んで適用します。
        /// </summary>
        public static void LoadSavedLanguage()
        {
            try
            {
                var settings = UserSettingsStore.Load();
                SetLanguage(settings.language);
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "保存された言語設定の読み込みに失敗しました");
                SetLanguage(null); // フォールバックとしてシステム設定を使用
            }
        }
    }
}