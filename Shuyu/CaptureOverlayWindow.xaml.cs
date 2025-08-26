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
            
            LogService.LogFormat("ウィンドウサイズ設定: ({0}, {1}, {2}, {3})", 
                this.Left, this.Top, this.Width, this.Height);
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
                LogService.LogFormat("キャプチャ完了: {0}x{1} ピクセル", 
                    _capturedBitmap.Width, _capturedBitmap.Height);
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

            LogService.LogFormat($"Selection started at {_startPoint}");
            
            // 選択矩形を開始点に配置（幅・高さは0で初期化）
            Canvas.SetLeft(_selectionRect, _startPoint.X);
            Canvas.SetTop(_selectionRect, _startPoint.Y);
            _selectionRect.Width = 0;
            _selectionRect.Height = 0;
            
            // マウスキャプチャを開始（ウィンドウ外にマウスが出ても追跡可能）
            CaptureMouse();
        }

        /// <summary>
        /// マウス移動時の処理。選択矩形のサイズと位置を更新します。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">マウスイベント引数。</param>
        private void SelectionCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // マウスがキャプチャされている（ドラッグ中）場合のみ処理
            if (IsMouseCaptured)
            {
                // 現在のマウス位置を取得
                var p = e.GetPosition(SelectionCanvas);
                
                // 開始点と現在点から矩形の左上座標と幅・高さを計算
                var x = Math.Min(p.X, _startPoint.X);           // 左端のX座標
                var y = Math.Min(p.Y, _startPoint.Y);           // 上端のY座標
                var w = Math.Abs(p.X - _startPoint.X);          // 幅（絶対値）
                var h = Math.Abs(p.Y - _startPoint.Y);          // 高さ（絶対値）

                LogService.LogFormat($"Selection rectangle updated: {x}, {y}, {w}, {h}");

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
            // マウスがキャプチャされていない場合は何もしない
            if (!IsMouseCaptured) return;
            
            // マウスキャプチャを解除
            ReleaseMouseCapture();
            
            // 選択終了点を取得
            var end = e.GetPosition(SelectionCanvas);
            
            // 選択領域の矩形を計算（整数ピクセル単位に変換）
            var x = (int)Math.Min(_startPoint.X, end.X);        // 左端X座標
            var y = (int)Math.Min(_startPoint.Y, end.Y);        // 上端Y座標
            var w = (int)Math.Abs(end.X - _startPoint.X);       // 幅
            var h = (int)Math.Abs(end.Y - _startPoint.Y);       // 高さ

            LogService.LogFormat($"Selection ended at {end}");
            LogService.LogFormat($"Final selection rectangle: {x}, {y}, {w}, {h}");

            // 有効な選択範囲がある場合のみ切り抜き処理を実行
            if (w > 0 && h > 0)
            {
                // 注意：PreviewImageはWPFのDIP単位で表示されているが、
                // _capturedBitmapはピクセル単位。DPI差がある環境では座標変換が必要
                // 現在の実装：ウィンドウ左上が仮想スクリーン左上に対応すると仮定
                CropAndPin(new System.Drawing.Rectangle((int)x, (int)y, w, h));
            }
            
            // オーバーレイウィンドウを閉じる
            this.Close();
        }

        /// <summary>
        /// 指定された矩形領域を切り抜いて、ピン留めウィンドウを作成します（現在はコメントアウト）。
        /// </summary>
        /// <param name="rect">切り抜く矩形領域。</param>
        private void CropAndPin(System.Drawing.Rectangle rect)
        {
            LogService.LogFormat("CropAndPin開始 - 矩形: X={0}, Y={1}, Width={2}, Height={3}", 
                rect.X, rect.Y, rect.Width, rect.Height);
            
            try
            {
                // キャプチャしたビットマップが存在しない場合は何もしない
                if (_capturedBitmap == null)
                {
                    LogService.LogError("CropAndPin: キャプチャされたビットマップがありません");
                    return;
                }

                LogService.LogFormat("ビットマップサイズ: {0}x{1}", 
                    _capturedBitmap.Width, _capturedBitmap.Height);

                // 矩形領域をビットマップの範囲内に安全にクリップ
                var originalRect = rect;
                rect.Intersect(new System.Drawing.Rectangle(0, 0, _capturedBitmap.Width, _capturedBitmap.Height));
                
                if (!originalRect.Equals(rect))
                {
                    LogService.LogFormat("矩形をクリップしました: {0} → {1}", originalRect, rect);
                }
                
                // クリップ後の矩形が無効な場合は何もしない
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    LogService.LogWarning("CropAndPin: クリップ後の矩形が無効です");
                    return;
                }
                
                // 指定された矩形領域を切り抜いて新しいビットマップを作成
                var cropped = _capturedBitmap.Clone(rect, _capturedBitmap.PixelFormat);
                LogService.LogFormat("画像クロップ完了: {0}x{1} ピクセル", 
                    cropped.Width, cropped.Height);
                
                // ピン留めウィンドウの作成（現在はコメントアウト）
                //var pinned = new PinnedWindow(cropped);
                //pinned.Show();
                LogService.LogInfo("PinnedWindow作成処理完了（コメントアウト中）");
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

        /// <summary>
        /// GDIオブジェクト（HBITMAP等）を削除するためのWin32 API
        /// </summary>
        /// <param name="hObject">削除するGDIオブジェクトのハンドル</param>
        /// <returns>成功した場合はtrue</returns>
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}