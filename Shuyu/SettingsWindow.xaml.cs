using System.Windows;

namespace Shuyu
{
    /// <summary>
    /// 設定ウィンドウ。低レベルキーボードフック使用の選択などを行います。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        /// <summary>
        /// SettingsWindow の新しいインスタンスを初期化します。
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();
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
        /// OK ボタンがクリックされたときの処理。DialogResult を true にして画面を閉じます。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">イベント引数。</param>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
