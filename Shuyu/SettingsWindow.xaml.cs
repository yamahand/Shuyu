using System.Windows;

namespace Shuyu
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        // 設定: 低レベルキーボードフックを使うかどうか
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

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
