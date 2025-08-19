using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Shuyu
{
    public partial class CaptureOverlayWindow : Window
    {
        private System.Drawing.Bitmap? _capturedBitmap;
        private System.Windows.Point _startPoint;
        private RectangleGeometry _selectionGeometry;
        private System.Windows.Shapes.Rectangle _selectionRect;

        public CaptureOverlayWindow()
        {
            InitializeComponent();
            _selectionGeometry = new RectangleGeometry();
            _selectionRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Cyan,
                StrokeThickness = 2,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 0))
            };
            SelectionCanvas.Children.Add(_selectionRect);

            // フォーカスを確保して Esc を受け取れるようにする
            this.Focusable = true;
            this.Loaded += (s, e) =>
            {
                this.Activate();
                System.Windows.Input.Keyboard.Focus(this);
            };
            this.PreviewKeyDown += CaptureOverlayWindow_PreviewKeyDown;

            // フル仮想スクリーンに合わせる（DIP単位。DPI差がある場合は調整が必要）
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
        }

        public void StartCaptureAndShow()
        {
            CaptureVirtualScreen();
            if (_capturedBitmap != null)
            {
                PreviewImage.Source = BitmapToImageSource(_capturedBitmap);
            }
        }

        private void CaptureVirtualScreen()
        {
            var vs = SystemInformation.VirtualScreen; // pixel 単位の Rectangle
            _capturedBitmap?.Dispose();
            _capturedBitmap = new System.Drawing.Bitmap(vs.Width, vs.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(_capturedBitmap))
            {
                g.CopyFromScreen(vs.Left, vs.Top, 0, 0, new System.Drawing.Size(vs.Width, vs.Height), System.Drawing.CopyPixelOperation.SourceCopy);
            }
        }

        private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(SelectionCanvas);
            Canvas.SetLeft(_selectionRect, _startPoint.X);
            Canvas.SetTop(_selectionRect, _startPoint.Y);
            _selectionRect.Width = 0;
            _selectionRect.Height = 0;
            CaptureMouse();
        }

        private void SelectionCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (IsMouseCaptured)
            {
                var p = e.GetPosition(SelectionCanvas);
                var x = Math.Min(p.X, _startPoint.X);
                var y = Math.Min(p.Y, _startPoint.Y);
                var w = Math.Abs(p.X - _startPoint.X);
                var h = Math.Abs(p.Y - _startPoint.Y);
                Canvas.SetLeft(_selectionRect, x);
                Canvas.SetTop(_selectionRect, y);
                _selectionRect.Width = w;
                _selectionRect.Height = h;
            }
        }

        private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsMouseCaptured) return;
            ReleaseMouseCapture();
            var end = e.GetPosition(SelectionCanvas);
            var x = (int)Math.Min(_startPoint.X, end.X);
            var y = (int)Math.Min(_startPoint.Y, end.Y);
            var w = (int)Math.Abs(end.X - _startPoint.X);
            var h = (int)Math.Abs(end.Y - _startPoint.Y);
            if (w > 0 && h > 0)
            {
                // NOTE: PreviewImage shown in WPF DIPs; capturedBitmap はピクセル。DPI 差がある環境ではここで換算が必要。
                // 単純実装：ウィンドウ左上が仮想スクリーン左上に対応すると仮定してピクセルにキャスト
                CropAndPin(new System.Drawing.Rectangle((int)x, (int)y, w, h));
            }
            this.Close();
        }

        private void CropAndPin(System.Drawing.Rectangle rect)
        {
            try
            {
                if (_capturedBitmap == null)
                {
                    return;
                }

                // rect を安全にクリップ
                rect.Intersect(new System.Drawing.Rectangle(0, 0, _capturedBitmap.Width, _capturedBitmap.Height));
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    return;
                }
                var cropped = _capturedBitmap.Clone(rect, _capturedBitmap.PixelFormat);
                //var pinned = new PinnedWindow(cropped);
                //pinned.Show();
            }
            catch { }
        }

        private BitmapSource BitmapToImageSource(System.Drawing.Bitmap bmp)
        {
            var hBitmap = bmp.GetHbitmap();
            try
            {
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                return src;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private void CaptureOverlayWindow_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                this.Close();
            }
        }

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}