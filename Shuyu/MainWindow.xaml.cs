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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TrayService? _trayService;

        public MainWindow()
        {
            InitializeComponent();
            this.Hide();
            this.ShowInTaskbar = false;

            InitializeTrayService();
        }

        private void InitializeTrayService()
        {
            _trayService = new TrayService(
                onCapture: StartCapture,
                onSettings: ShowSettings,
                onExit: ExitApplication
            );
        }

        private void StartCapture()
        {
            // キャプチャ機能を呼び出し
            // キャプチャ機能を呼び出し
            var overlay = new CaptureOverlayWindow();
            overlay.Show();
            overlay.StartCaptureAndShow();
        }

        private void ShowSettings()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplication()
        {
            //trayIcon.Visible = false;
            //trayIcon.Dispose();
            Application.Current.Shutdown();
        }
    }
}