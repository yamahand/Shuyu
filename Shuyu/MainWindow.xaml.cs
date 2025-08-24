using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Application = System.Windows.Application;

namespace Shuyu
{
    /// <summary>
    /// MainWindow.xaml のコードビハインド。アプリケーションのメインウィンドウですが、通常は非表示でトレイで動作します。
    /// </summary>
    public partial class MainWindow : Window
    {
        private TrayService? _trayService;

        /// <summary>
        /// MainWindow の新しいインスタンスを初期化します。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.Hide();
            this.ShowInTaskbar = false;

            InitializeTrayService();
        }

        /// <summary>
        /// トレイサービスを初期化します。キャプチャ、設定、終了のコールバックを設定します。
        /// </summary>
        private void InitializeTrayService()
        {
            _trayService = new TrayService(
                onCapture: StartCapture,
                onSettings: ShowSettings,
                onExit: ExitApplication
            );
        }

        /// <summary>
        /// キャプチャ機能を開始します。CaptureOverlayWindow を表示してキャプチャを実行します。
        /// </summary>
        private void StartCapture()
        {
            // キャプチャ機能を呼び出し
            // キャプチャ機能を呼び出し
            var overlay = new CaptureOverlayWindow();
            overlay.Show();
            overlay.StartCaptureAndShow();
        }

        /// <summary>
        /// 設定画面を表示します。メインウィンドウを表示して前面に持ってきます。
        /// </summary>
        private void ShowSettings()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        /// <summary>
        /// アプリケーションを終了します。Application.Current.Shutdown() を呼び出します。
        /// </summary>
        private void ExitApplication()
        {
            //trayIcon.Visible = false;
            //trayIcon.Dispose();
            Application.Current.Shutdown();
        }
    }
}