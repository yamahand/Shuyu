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
        private bool _isInitializing = true;
        private bool _isUpdatingLanguageCombo = false;
        private string? _originalLanguage;
        
        /// <summary>
        /// SettingsWindow の新しいインスタンスを初期化します。
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();
            
            // 現在の言語設定を保存（キャンセル時に復元するため）
            var settings = UserSettingsStore.Load();
            _originalLanguage = settings.language;
            
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
                // 現在選択されている言語のTagを保存
                var selectedItem = languageCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
                var selectedTag = selectedItem?.Tag as string;

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
                    
                    // Tagベースで選択状態を復元
                    if (selectedTag != null)
                    {
                        foreach (System.Windows.Controls.ComboBoxItem item in languageCombo.Items)
                        {
                            if ((item.Tag as string) == selectedTag)
                            {
                                languageCombo.SelectedItem = item;
                                break;
                            }
                        }
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
            // 言語の適用はOKボタンクリック時に行うため、ここでは何もしない
            // これにより、ユーザーがキャンセルした場合に言語が変更されないようにする
        }

        /// <summary>
        /// OK ボタンがクリックされたときの処理。設定を保存してDialogResult を true にして画面を閉じます。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">イベント引数。</param>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 選択された言語を適用
            var selectedLanguage = this.SelectedLanguage;
            LocalizationService.SetLanguage(selectedLanguage);
            
            // 設定を保存
            var settings = new UserSettings
            {
                useLowLevelHook = this.useLowLevelHook,
                language = selectedLanguage
            };
            UserSettingsStore.Save(settings);
            
            this.DialogResult = true;
            this.Close();
        }
        
        /// <summary>
        /// ウィンドウが閉じられる前の処理。キャンセル時は元の言語設定に戻します。
        /// </summary>
        /// <param name="e">イベント引数。</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // OKで閉じられていない場合（キャンセルまたはX）、元の言語に戻す
            if (this.DialogResult != true)
            {
                LocalizationService.SetLanguage(_originalLanguage);
            }
            
            base.OnClosing(e);
        }
    }
}