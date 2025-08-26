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
using Shuyu.Service;

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

            // LogServiceの初期化
#if DEBUG
            LogService.Instance.InitializeLogWindow();
            LogService.LogInfo("MainWindow初期化完了 - デバッグモード");
#endif

            InitializeTrayService();
        }

        /// <summary>
        /// トレイサービスを初期化します。キャプチャ、設定、終了のコールバックを設定します。
        /// </summary>
        private void InitializeTrayService()
        {
            LogService.LogInfo("TrayService初期化開始");
            
            _trayService = new TrayService(
                onCapture: StartCapture,
                onSettings: ShowSettings,
                onExit: ExitApplication
            );
            
            LogService.LogInfo("TrayService初期化完了");
        }

        /// <summary>
        /// キャプチャ機能を開始します。CaptureOverlayWindow を表示してキャプチャを実行します。
        /// </summary>
        private void StartCapture()
        {
            LogService.LogInfo("キャプチャ機能開始");
            
            // キャプチャ機能を呼び出し
            var overlay = new CaptureOverlayWindow();
            overlay.Show();
            overlay.StartCaptureAndShow();
            
            LogService.LogInfo("CaptureOverlayWindow表示完了");
        }

        /// <summary>
        /// 設定画面を表示します。メインウィンドウを表示して前面に持ってきます。
        /// </summary>
        private void ShowSettings()
        {
            LogService.LogInfo("設定画面表示");
            
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        /// <summary>
        /// アプリケーションを終了します。Application.Current.Shutdown() を呼び出します。
        /// </summary>
        private void ExitApplication()
        {
            LogService.LogInfo("アプリケーション終了");
            
            //trayIcon.Visible = false;
            //trayIcon.Dispose();
            Application.Current.Shutdown();
        }
    }
}