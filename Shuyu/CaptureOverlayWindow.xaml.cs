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
using System.Diagnostics;
using Shuyu.Service; // LogService用

namespace Shuyu
{
    /// <summary>
    /// キャプチャオーバーレイウィンドウ。仮想スクリーン全体をキャプチャして表示し、矩形選択を可能にします。
    /// </summary>
    public partial class CaptureOverlayWindow : Window
    {
        /// <summary>
        /// キャプチャした画面全体のビットマップ（ピクセル単位）
        /// </summary>
        private System.Drawing.Bitmap? _capturedBitmap;

        /// <summary>
        /// マウス選択の開始点（WPF座標系、DIP単位）
        /// </summary>
        private System.Windows.Point _startPoint;

        /// <summary>
        /// 選択領域のジオメトリ（現在未使用）
        /// </summary>
        private RectangleGeometry _selectionGeometry;

        /// <summary>
        /// 選択範囲を表示する矩形図形（シアン色の枠線）
        /// </summary>
        private System.Windows.Shapes.Rectangle _selectionRect;

        // クラスメンバに追加（開始スクリーン座標(px)）
        private System.Drawing.Point _startScreenPx;

        /// <summary>
        /// CaptureOverlayWindow の新しいインスタンスを初期化します。
        /// </summary>
        public CaptureOverlayWindow()
        {
            InitializeComponent();

            LogService.LogDebug("CaptureOverlayWindow を初期化しています");

            // デバッグ時はTopmostをfalseにしてDebugLogWindowを前面に保つ
#if DEBUG
            this.Topmost = false;
            LogService.LogInfo("デバッグモード: CaptureOverlayWindowのTopmostをfalseに設定");
#endif

            // 選択領域のジオメトリを初期化（現在未使用）
            _selectionGeometry = new RectangleGeometry();

            // 選択矩形の見た目を設定（シアン色の枠線、半透明の黒い背景）
            _selectionRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Cyan,        // 枠線色：シアン
                StrokeThickness = 2,                               // 枠線の太さ：2px
                Fill = new System.Windows.Media.SolidColorBrush(   // 塗りつぶし：半透明の黒
                    System.Windows.Media.Color.FromArgb(50, 0, 0, 0))
            };
            // キャンバスに選択矩形を追加
            SelectionCanvas.Children.Add(_selectionRect);

            // フォーカスを確保して Esc キーを受け取れるようにする設定
            this.Focusable = true;
            this.Loaded += (s, e) =>
            {
                // ウィンドウをアクティブ化してキーボードフォーカスを設定
                this.Activate();
                System.Windows.Input.Keyboard.Focus(this);
                LogService.LogDebug("CaptureOverlayWindow がアクティブ化されました");
            };
            // Escape キー押下イベントを登録
            this.PreviewKeyDown += CaptureOverlayWindow_PreviewKeyDown;

            // フル仮想スクリーンに合わせてウィンドウサイズと位置を設定（DIP単位）
            // 注意：DPI差がある環境では調整が必要
            this.Left = SystemParameters.VirtualScreenLeft;     // 仮想スクリーンの左端
            this.Top = SystemParameters.VirtualScreenTop;       // 仮想スクリーンの上端
            this.Width = SystemParameters.VirtualScreenWidth;   // 仮想スクリーンの幅
            this.Height = SystemParameters.VirtualScreenHeight; // 仮想スクリーンの高さ

            LogService.LogInfo($"ウィンドウサイズ設定: ({this.Left}, {this.Top}, {this.Width}, {this.Height})");
        }

        /// <summary>
        /// キャプチャを開始し、オーバーレイに表示します。
        /// </summary>
        public void StartCaptureAndShow()
        {
            LogService.LogInfo("画面キャプチャを開始します");

            // 仮想スクリーン全体をキャプチャ
            CaptureVirtualScreen();
            if (_capturedBitmap != null)
            {
                // キャプチャした画像をWPFのImageコントロールに表示
                PreviewImage.Source = BitmapToImageSource(_capturedBitmap);
                LogService.LogInfo($"キャプチャ完了: {_capturedBitmap.Width}x{_capturedBitmap.Height} ピクセル");
            }
            else
            {
                LogService.LogError("画面キャプチャに失敗しました");
            }
        }

        /// <summary>
        /// 仮想スクリーン全体をキャプチャします。
        /// </summary>
        private void CaptureVirtualScreen()
        {
            // 仮想スクリーンの範囲を取得（ピクセル単位のRectangle）
            var vs = SystemInformation.VirtualScreen;

            // 既存のビットマップがあれば破棄
            _capturedBitmap?.Dispose();

            // 仮想スクリーン全体のサイズでビットマップを作成（32bit ARGB形式）
            _capturedBitmap = new System.Drawing.Bitmap(vs.Width, vs.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Graphics オブジェクトを使って画面をコピー
            using (var g = System.Drawing.Graphics.FromImage(_capturedBitmap))
            {
                // 仮想スクリーン全体をビットマップにコピー
                g.CopyFromScreen(vs.Left, vs.Top, 0, 0, new System.Drawing.Size(vs.Width, vs.Height), System.Drawing.CopyPixelOperation.SourceCopy);
            }
        }

        /// <summary>
        /// マウス左ボタンが押されたときの処理。選択開始点を記録し、マウスキャプチャを開始します。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">マウスイベント引数。</param>
        private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // マウス押下位置を選択開始点として記録（キャンバス座標系）
            _startPoint = e.GetPosition(SelectionCanvas);

            LogService.LogInfo($"Selection started at {_startPoint}");

            // 選択矩形を開始点に配置（幅・高さは0で初期化）
            Canvas.SetLeft(_selectionRect, _startPoint.X);
            Canvas.SetTop(_selectionRect, _startPoint.Y);
            _selectionRect.Width = 0;
            _selectionRect.Height = 0;

            // 追加: マウスの仮想スクリーン座標(px)を保存
            _startScreenPx = System.Windows.Forms.Cursor.Position;

            // マウスキャプチャを開始（ウィンドウ外にマウスが出ても追跡可能）
            SelectionCanvas.CaptureMouse();
        }

        /// <summary>
        /// マウス移動時の処理。選択矩形のサイズと位置を更新します。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">マウスイベント引数。</param>
        private void SelectionCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // マウスがキャプチャされている（ドラッグ中）場合のみ処理
            if (SelectionCanvas.IsMouseCaptured)
            {
                // 現在のマウス位置を取得
                var p = e.GetPosition(SelectionCanvas);

                // 開始点と現在点から矩形の左上座標と幅・高さを計算
                var x = Math.Min(p.X, _startPoint.X);           // 左端のX座標
                var y = Math.Min(p.Y, _startPoint.Y);           // 上端のY座標
                var w = Math.Abs(p.X - _startPoint.X);          // 幅（絶対値）
                var h = Math.Abs(p.Y - _startPoint.Y);          // 高さ（絶対値）

                // 選択矩形の位置とサイズを更新
                Canvas.SetLeft(_selectionRect, x);
                Canvas.SetTop(_selectionRect, y);
                _selectionRect.Width = w;
                _selectionRect.Height = h;
            }
        }

        /// <summary>
        /// マウス左ボタンが離されたときの処理。選択を完了し、切り抜き処理を実行してウィンドウを閉じます。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">マウスイベント引数。</param>
        private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!SelectionCanvas.IsMouseCaptured) return;
            SelectionCanvas.ReleaseMouseCapture();

            // 終了時のスクリーン座標(px)
            var endScreenPx = System.Windows.Forms.Cursor.Position;

            // 仮想スクリーン原点(左上)のpxを取得
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;

            // 表示位置（画面絶対px）
            int screenLeftPx = Math.Min(_startScreenPx.X, endScreenPx.X);
            int screenTopPx = Math.Min(_startScreenPx.Y, endScreenPx.Y);
            int width = Math.Abs(endScreenPx.X - _startScreenPx.X);
            int height = Math.Abs(endScreenPx.Y - _startScreenPx.Y);

            LogService.LogInfo($"Selection ended at screen(px): {screenLeftPx}, {screenTopPx}, width: {width}, height: {height}");

            // 画像内（ビットマップ相対px）に変換（0,0 = VirtualScreen.Left/Top）
            int leftPxImg = screenLeftPx - vs.Left;
            int topPxImg = screenTopPx - vs.Top;
            var rectBitmapPx = new System.Drawing.Rectangle(leftPxImg, topPxImg, width, height);

            LogService.LogInfo($"Final selection rectangle(img px): {rectBitmapPx.X}, {rectBitmapPx.Y}, {rectBitmapPx.Width}, {rectBitmapPx.Height}");

            if (rectBitmapPx.Width > 0 && rectBitmapPx.Height > 0)
            {
                CropAndPin(rectBitmapPx, screenLeftPx, screenTopPx);
            }

            this.Close();
        }

        // キャンバス座標(DIP)2点 → 仮想スクリーン基準(px)矩形
        private System.Drawing.Rectangle ToPixelRect(System.Windows.Point p1DipCanvas, System.Windows.Point p2DipCanvas)
        {
            // Canvas座標(DIP) → 画面座標(DIP)
            var s1Dip = SelectionCanvas.PointToScreen(p1DipCanvas);
            var s2Dip = SelectionCanvas.PointToScreen(p2DipCanvas);

            // 画面座標(DIP) → 物理px
            var ps = PresentationSource.FromVisual(this);
            var m = ps?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;

            var p1Px = new System.Windows.Point(s1Dip.X * m.M11, s1Dip.Y * m.M22);
            var p2Px = new System.Windows.Point(s2Dip.X * m.M11, s2Dip.Y * m.M22);

            // 仮想スクリーン原点(左上)基準にオフセット
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen; // px

            var leftPx = (int)Math.Round(Math.Min(p1Px.X, p2Px.X) - vs.Left);
            var topPx = (int)Math.Round(Math.Min(p1Px.Y, p2Px.Y) - vs.Top);
            var width = (int)Math.Round(Math.Abs(p2Px.X - p1Px.X));
            var height = (int)Math.Round(Math.Abs(p2Px.Y - p1Px.Y));

            return new System.Drawing.Rectangle(leftPx, topPx, width, height);
        }

        /// <summary>
        /// 指定された矩形領域を切り抜いて、ピン留めウィンドウを作成します（現在はコメントアウト）。
        /// </summary>
        /// <param name="rect">切り抜く矩形領域。</param>
        private void CropAndPin(System.Drawing.Rectangle rect)
        {
            LogService.LogInfo($"CropAndPin開始 - 矩形: X={rect.X}, Y={rect.Y}, Width={rect.Width}, Height={rect.Height}");

            try
            {
                // キャプチャしたビットマップが存在しない場合は何もしない
                if (_capturedBitmap == null)
                {
                    LogService.LogError("CropAndPin: キャプチャされたビットマップがありません");
                    return;
                }

                LogService.LogInfo($"ビットマップサイズ: {_capturedBitmap.Width}x{_capturedBitmap.Height}");

                // 矩形領域をビットマップの範囲内に安全にクリップ
                var originalRect = rect;
                rect.Intersect(new System.Drawing.Rectangle(0, 0, _capturedBitmap.Width, _capturedBitmap.Height));

                if (!originalRect.Equals(rect))
                {
                    LogService.LogInfo($"矩形をクリップしました: {originalRect} → {rect}");
                }

                // クリップ後の矩形が無効な場合は何もしない
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    LogService.LogWarning("CropAndPin: クリップ後の矩形が無効です");
                    return;
                }

                // 指定された矩形領域を切り抜いて新しいビットマップを作成
                using var cropped = _capturedBitmap.Clone(rect, _capturedBitmap.PixelFormat);
                LogService.LogInfo($"画像クロップ完了: {cropped.Width}x{cropped.Height} ピクセル");

                // クリップボードへコピー
                try
                {
                    var clip = BitmapToImageSource(cropped);
                    if (clip.CanFreeze) clip.Freeze();
                    System.Windows.Clipboard.SetImage(clip);
                    LogService.LogInfo("切り抜き画像をクリップボードにコピーしました");
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex, "クリップボードへのコピーに失敗しました");
                }

                // ピン留めウィンドウを表示
                try
                {
                    var src = BitmapToImageSource(cropped);
                    if (src.CanFreeze) src.Freeze();

                    // ここで px → DIP に変換
                    var dpi = VisualTreeHelper.GetDpi(this);
                    var leftDip = rect.X / dpi.DpiScaleX;
                    var topDip = rect.Y / dpi.DpiScaleY;

                    Shuyu.Service.PinnedWindowManager.Create(src, (int)leftDip, (int)topDip);
                    LogService.LogInfo($"ピン留めウィンドウを作成・表示しました:{leftDip}, {topDip}");
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex, "PinnedWindow作成に失敗しました");
                }

            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "CropAndPin処理中にエラーが発生しました");
            }
        }

        // 指定された矩形領域を切り抜いて、ピン留めウィンドウを作成します。
        // rectBitmapPx: キャプチャ画像内のクロップ矩形（ビットマップ相対 px）
        // screenLeftPx/screenTopPx: 画面絶対の表示位置（px）
        private void CropAndPin(System.Drawing.Rectangle rectBitmapPx, int screenLeftPx, int screenTopPx)
        {
            LogService.LogInfo($"CropAndPin開始 - 画像矩形(px): X={rectBitmapPx.X}, Y={rectBitmapPx.Y}, Width={rectBitmapPx.Width}, Height={rectBitmapPx.Height} / 画面位置(px): L={screenLeftPx}, T={screenTopPx}");
            try
            {
                if (_capturedBitmap == null)
                {
                    LogService.LogError("CropAndPin: キャプチャされたビットマップがありません");
                    return;
                }

                LogService.LogInfo($"ビットマップサイズ: {_capturedBitmap.Width}x{_capturedBitmap.Height}");

                // 範囲クリップ
                var originalRect = rectBitmapPx;
                rectBitmapPx.Intersect(new System.Drawing.Rectangle(0, 0, _capturedBitmap.Width, _capturedBitmap.Height));
                if (!originalRect.Equals(rectBitmapPx))
                {
                    LogService.LogInfo($"矩形をクリップしました: {originalRect} → {rectBitmapPx}");
                }
                if (rectBitmapPx.Width <= 0 || rectBitmapPx.Height <= 0)
                {
                    LogService.LogWarning("CropAndPin: クリップ後の矩形が無効です");
                    return;
                }

                // クロップ
                using var cropped = _capturedBitmap.Clone(rectBitmapPx, _capturedBitmap.PixelFormat);
                LogService.LogInfo($"画像クロップ完了: {cropped.Width}x{cropped.Height} ピクセル");

                // クリップボードへコピー（任意）
                try
                {
                    var clip = BitmapToImageSource(cropped);
                    if (clip.CanFreeze) clip.Freeze();
                    System.Windows.Clipboard.SetImage(clip);
                    LogService.LogInfo("切り抜き画像をクリップボードにコピーしました");
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex, "クリップボードへのコピーに失敗しました");
                }

                // ピン留めウィンドウを表示（画面絶対px → DIP 変換して配置）
                try
                {
                    var src = BitmapToImageSource(cropped);
                    if (src.CanFreeze) src.Freeze();

                    var dip = ScreenPxToDipAt(screenLeftPx, screenTopPx);
                    Shuyu.Service.PinnedWindowManager.Create(src, (int)Math.Round(dip.X), (int)Math.Round(dip.Y));
                    LogService.LogInfo($"ピン留めウィンドウを作成・表示しました (DIP): {dip.X:0.##}, {dip.Y:0.##}");
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex, "PinnedWindow作成に失敗しました");
                }
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "CropAndPin処理中にエラーが発生しました");
            }
        }

        /// <summary>
        /// System.Drawing.Bitmap を WPF の BitmapSource に変換します。
        /// </summary>
        /// <param name="bmp">変換する Bitmap オブジェクト。</param>
        /// <returns>変換された BitmapSource。</returns>
        private BitmapSource BitmapToImageSource(System.Drawing.Bitmap bmp)
        {
            LogService.LogDebug("BitmapからBitmapSourceへの変換を開始");

            // ビットマップからHBITMAPハンドルを取得
            var hBitmap = bmp.GetHbitmap();
            try
            {
                // HBITMAPからWPFのBitmapSourceを作成
                var src = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,                    // ソースのHBITMAPハンドル
                    IntPtr.Zero,               // パレットハンドル（使用しない）
                    Int32Rect.Empty,           // ソース矩形（全体を使用）
                    BitmapSizeOptions.FromEmptyOptions()); // サイズオプション（デフォルト）

                LogService.LogDebug("BitmapSource変換完了");
                return src;
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "BitmapからBitmapSourceへの変換中にエラーが発生しました");
                throw;
            }
            finally
            {
                // リソースリークを防ぐためHBITMAPハンドルを解放
                DeleteObject(hBitmap);
            }
        }

        /// <summary>
        /// キー押下イベントの処理。Escape キーでウィンドウを閉じます。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">キーイベント引数。</param>
        private void CaptureOverlayWindow_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            LogService.LogDebug($"キー押下イベント: {e.Key}");

            // Escapeキーが押された場合
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                LogService.LogInfo("Escapeキーが押されました - オーバーレイを閉じます");
                // イベントを処理済みとしてマークし、他のハンドラに伝播させない
                e.Handled = true;
                // オーバーレイウィンドウを閉じる
                this.Close();
            }
        }

        // 画面絶対px座標を、その座標が属するモニターのDPIでDIPに変換
        private (double X, double Y) ScreenPxToDipAt(int pxX, int pxY)
        {
            try
            {
                var hmon = MonitorFromPoint(new POINT { X = pxX, Y = pxY }, MONITOR_DEFAULTTONEAREST);
                if (hmon != IntPtr.Zero)
                {
                    if (GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 && dpiX != 0 && dpiY != 0)
                    {
                        return (pxX * 96.0 / dpiX, pxY * 96.0 / dpiY);
                    }
                }
            }
            catch
            {
                // ignore and fallback
            }

            // フォールバック（このウィンドウのDPI）
            var dpi = VisualTreeHelper.GetDpi(this);
            return (pxX / dpi.DpiScaleX, pxY / dpi.DpiScaleY);
        }

        /// <summary>
        /// GDIオブジェクト（HBITMAP等）を削除するためのWin32 API
        /// </summary>
        /// <param name="hObject">削除するGDIオブジェクトのハンドル</param>
        /// <returns>成功した場合はtrue</returns>
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        // Win32: モニター/DPI取得
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    }
}