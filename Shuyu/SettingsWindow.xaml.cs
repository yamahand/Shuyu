using System.Windows;
using Shuyu.Resources;
using Shuyu.Service;

namespace Shuyu
{
    /// <summary>
    /// 設定ウィンドウ。低レベルキーボードフック使用の選択や言語設定などを行います。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly bool _isInitializing = true;
        private bool _isUpdatingLanguageCombo = false;
        
        /// <summary>
        /// SettingsWindow の新しいインスタンスを初期化します。
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();
            ApplyLocalization();
            LoadCurrentSettings();
            _isInitializing = false;
        }
        
        private void LoadCurrentSettings()
        {
            var settings = UserSettingsStore.Load();
            
            // 言語設定の読み込み
            var languageCombo = this.FindName("LanguageComboBox") as System.Windows.Controls.ComboBox;
            if (languageCombo != null)
            {
                // 保存された言語設定に基づいてコンボボックスを選択
                var targetTag = settings.language ?? "";
                foreach (System.Windows.Controls.ComboBoxItem item in languageCombo.Items)
                {
                    if ((item.Tag as string) == targetTag)
                    {
                        languageCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        
        private void ApplyLocalization()
        {
            this.Title = Strings.SettingsWindowTitle;
            
            var languageLabel = this.FindName("LanguageLabel") as System.Windows.Controls.Label;
            if (languageLabel != null)
                languageLabel.Content = Strings.Language;
            
            // コンボボックス項目の更新
            var languageCombo = this.FindName("LanguageComboBox") as System.Windows.Controls.ComboBox;
            if (languageCombo != null)
            {
                var selectedIndex = languageCombo.SelectedIndex;

                _isUpdatingLanguageCombo = true;
                try
                {
                    // 変更中は SelectionChanged を抑止
                    languageCombo.SelectionChanged -= LanguageComboBox_SelectionChanged;

                    languageCombo.Items.Clear();
                    
                    var systemItem = new System.Windows.Controls.ComboBoxItem { Tag = "", Content = Strings.LanguageSystem };
                    var japaneseItem = new System.Windows.Controls.ComboBoxItem { Tag = "ja", Content = Strings.LanguageJapanese };
                    var englishItem = new System.Windows.Controls.ComboBoxItem { Tag = "en", Content = Strings.LanguageEnglish };
                    
                    languageCombo.Items.Add(systemItem);
                    languageCombo.Items.Add(japaneseItem);
                    languageCombo.Items.Add(englishItem);
                    
                    // 選択状態を復元（範囲内のみ）
                    if (selectedIndex >= 0 && selectedIndex < languageCombo.Items.Count)
                    {
                        languageCombo.SelectedIndex = selectedIndex;
                    }
                }
                finally
                {
                    languageCombo.SelectionChanged += LanguageComboBox_SelectionChanged;
                    _isUpdatingLanguageCombo = false;
                }
            }
            
            var checkBox = this.FindName("UseHookCheckBox") as System.Windows.Controls.CheckBox;
            if (checkBox != null)
                checkBox.Content = Strings.UseLowLevelHook;
                
            var description = this.FindName("DescriptionText") as System.Windows.Controls.TextBlock;
            if (description != null)
                description.Text = Strings.SettingsDescription;
                
            var okButton = this.FindName("OKButton") as System.Windows.Controls.Button;
            if (okButton != null)
                okButton.Content = Strings.OK;
                
            var cancelButton = this.FindName("CancelButton") as System.Windows.Controls.Button;
            if (cancelButton != null)
                cancelButton.Content = Strings.Cancel;
        }

        /// <summary>
        /// 低レベルキーボードフックを使用するかどうかの設定。
        /// </summary>
        public bool useLowLevelHook
        {
            get
            {
                var cb = this.FindName("UseHookCheckBox") as System.Windows.Controls.CheckBox;
                return cb?.IsChecked == true;
            }
            set
            {
                var cb = this.FindName("UseHookCheckBox") as System.Windows.Controls.CheckBox;
                if (cb != null)
                    cb.IsChecked = value;
            }
        }
        
        /// <summary>
        /// 選択された言語コードを取得します。
        /// </summary>
        public string? SelectedLanguage
        {
            get
            {
                var combo = this.FindName("LanguageComboBox") as System.Windows.Controls.ComboBox;
                var selectedItem = combo?.SelectedItem as System.Windows.Controls.ComboBoxItem;
                var tag = selectedItem?.Tag as string;
                return string.IsNullOrEmpty(tag) ? null : tag;
            }
        }
        
        private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingLanguageCombo) return;
            
            var combo = sender as System.Windows.Controls.ComboBox;
            var selectedItem = combo?.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var languageCode = selectedItem?.Tag as string;
            
            // 言語をすぐに適用
            LocalizationService.SetLanguage(string.IsNullOrEmpty(languageCode) ? null : languageCode);
            
            // UIを更新
            ApplyLocalization();
        }

        /// <summary>
        /// OK ボタンがクリックされたときの処理。設定を保存してDialogResult を true にして画面を閉じます。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">イベント引数。</param>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 設定を保存
            var settings = new UserSettings
            {
                useLowLevelHook = this.useLowLevelHook,
                language = this.SelectedLanguage
            };
            UserSettingsStore.Save(settings);
            
            this.DialogResult = true;
            this.Close();
        }
    }
}