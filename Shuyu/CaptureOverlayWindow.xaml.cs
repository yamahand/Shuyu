using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
    public partial class CaptureOverlayWindow : Window, IDisposable
    {
        /// <summary>
        /// 非同期画面キャプチャサービス
        /// </summary>
        private readonly AsyncScreenCaptureService _captureService;

        /// <summary>
        /// キャンセレーショントークンソース
        /// </summary>
        private readonly CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// マウス選択の開始点（WPF座標系、DIP単位）
        /// </summary>
        private System.Windows.Point _startPoint;

        /// <summary>
        /// 選択範囲を表示する矩形図形（シアン色の枠線）
        /// </summary>
        private System.Windows.Shapes.Rectangle _selectionRect = null!; // CS8618 回避

        /// <summary>
        /// 開始スクリーン座標(px)
        /// </summary>
        private System.Drawing.Point _startScreenPx;

        /// <summary>
        /// キャプチャ完了フラグ
        /// </summary>
        private bool _captureCompleted;

        /// <summary>
        /// リソース解放フラグ
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// CaptureOverlayWindow の新しいインスタンスを初期化します。
        /// </summary>
        public CaptureOverlayWindow()
        {
            InitializeComponent();

            // サービスとキャンセレーショントークンの初期化
            _captureService = new AsyncScreenCaptureService();
            _cancellationTokenSource = new CancellationTokenSource();

            LogService.LogDebug("CaptureOverlayWindow を初期化しています");

            // デバッグ時はTopmostをfalseにしてDebugLogWindowを前面に保つ
#if DEBUG
            this.Topmost = false;
            LogService.LogInfo("デバッグモード: CaptureOverlayWindowのTopmostをfalseに設定");
#endif

            InitializeUI();
            SetupWindowBounds();
        }

        /// <summary>
        /// UIコンポーネントを初期化します
        /// </summary>
        private void InitializeUI()
        {
            // 選択矩形の見た目を設定（シアン色の枠線、半透明の黒い背景）
            _selectionRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = System.Windows.Media.Brushes.Cyan,        // 枠線色：シアン
                StrokeThickness = 2,                               // 枠線の太さ：2px
                Fill = new System.Windows.Media.SolidColorBrush(   // 塗りつぶし：半透明の黒
                    System.Windows.Media.Color.FromArgb(50, 0, 0, 0)),
                Visibility = Visibility.Hidden // 初期状態では非表示
            };
            // キャンバスに選択矩形を追加
            SelectionCanvas.Children.Add(_selectionRect);

            // フォーカスを確保して Esc キーを受け取れるようにする設定
            this.Focusable = true;
            this.Loaded += OnWindowLoaded;
            this.Closing += OnWindowClosing;
            
            // Escape キー押下イベントを登録
            this.PreviewKeyDown += CaptureOverlayWindow_PreviewKeyDown;
        }

        /// <summary>
        /// ウィンドウサイズと位置を設定します
        /// </summary>
        private void SetupWindowBounds()
        {
            // 座標変換ユーティリティを使用して仮想スクリーン情報を取得
            var virtualScreen = CoordinateTransformation.GetVirtualScreenInfo();
            var currentDpi = CoordinateTransformation.GetCurrentCursorDpi();
            
            // スクリーン座標をDIPに変換
            var (leftDip, topDip) = CoordinateTransformation.ScreenPixelToDip(
                virtualScreen.Left, virtualScreen.Top, currentDpi);
            var (widthDip, heightDip) = CoordinateTransformation.ScreenPixelToDip(
                virtualScreen.Width, virtualScreen.Height, currentDpi);

            this.Left = leftDip;
            this.Top = topDip;
            this.Width = widthDip;
            this.Height = heightDip;

            LogService.LogInfo($"ウィンドウサイズ設定 (DIP): ({leftDip:F1}, {topDip:F1}, {widthDip:F1}, {heightDip:F1})");
        }

        /// <summary>
        /// ウィンドウロード時の処理
        /// </summary>
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // ウィンドウをアクティブ化してキーボードフォーカスを設定
            this.Activate();
            System.Windows.Input.Keyboard.Focus(this);
            LogService.LogDebug("CaptureOverlayWindow がアクティブ化されました");
        }

        /// <summary>
        /// ウィンドウクローズ時の処理
        /// </summary>
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e) // CS8622 対応
        {
            // 非同期処理をキャンセル
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// キャプチャを非同期で開始し、オーバーレイに表示します。
        /// </summary>
        public async void StartCaptureAndShow()
        {
            if (_disposed)
            {
                LogService.LogWarning("オーバーレイウィンドウは既に破棄されています");
                return;
            }

            try
            {
                LogService.LogInfo("非同期画面キャプチャを開始します");
                
                // プログレスレポーターを設定
                var progress = new AsyncScreenCaptureService.ProgressReporter((percentage, message) =>
                {
                    // UIスレッドでプログレスを更新
                    Dispatcher.Invoke(() =>
                    {
                        // プログレスバーやステータスラベルがあれば更新
                        LogService.LogDebug($"キャプチャ進行状況: {percentage}% - {message}");
                    });
                });

                // 非同期でフルスクリーンキャプチャを実行
                var result = await _captureService.CaptureFullScreenAsync(progress, _cancellationTokenSource.Token);
                
                if (result.IsSuccess && result.BitmapSource != null)
                {
                    // UIスレッドで画像を表示
                    Dispatcher.Invoke(() =>
                    {
                        PreviewImage.Source = result.BitmapSource;
                        _captureCompleted = true;
                        LogService.LogInfo($"非同期キャプチャ完了: {result.BitmapSource.PixelWidth}x{result.BitmapSource.PixelHeight}");
                    });
                }
                else
                {
                    LogService.LogError($"非同期画面キャプチャに失敗しました: {result.ErrorMessage}");
                    
                    // エラー時はウィンドウを閉じる
                    Dispatcher.Invoke(() => this.Close());
                }
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "非同期キャプチャエラー");
                Dispatcher.Invoke(() => this.Close());
            }
        }


        /// <summary>
        /// マウス左ボタンが押されたときの処理。選択開始点を記録し、マウスキャプチャを開始します。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">マウスイベント引数。</param>
        private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // キャプチャが完了していない場合は選択を禁止
            if (!_captureCompleted)
            {
                LogService.LogInfo("キャプチャがまだ完了していません");
                return;
            }

            // マウス押下位置を選択開始点として記録（キャンバス座標系）
            _startPoint = e.GetPosition(SelectionCanvas);
            _startScreenPx = System.Windows.Forms.Cursor.Position;

            LogService.LogInfo($"選択開始: Canvas({_startPoint.X:F1}, {_startPoint.Y:F1}) Screen({_startScreenPx.X}, {_startScreenPx.Y})");

            // 選択矩形を表示し、開始点に配置
            _selectionRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(_selectionRect, _startPoint.X);
            Canvas.SetTop(_selectionRect, _startPoint.Y);
            _selectionRect.Width = 0;
            _selectionRect.Height = 0;

            // マウスキャプチャを開始
            SelectionCanvas.CaptureMouse();
        }

        /// <summary>
        /// マウス移動時の処理。選択矩形のサイズと位置を更新します。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">マウスイベント引数。</param>
        private void SelectionCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // マウスがキャプチャされている（ドラッグ中）かつキャプチャ完了している場合のみ処理
            if (SelectionCanvas.IsMouseCaptured && _captureCompleted)
            {
                // 現在のマウス位置を取得
                var currentPoint = e.GetPosition(SelectionCanvas);

                // 開始点と現在点から矩形の左上座標と幅・高さを計算
                var left = Math.Min(currentPoint.X, _startPoint.X);
                var top = Math.Min(currentPoint.Y, _startPoint.Y);
                var width = Math.Abs(currentPoint.X - _startPoint.X);
                var height = Math.Abs(currentPoint.Y - _startPoint.Y);

                // 選択矩形の位置とサイズを更新
                Canvas.SetLeft(_selectionRect, left);
                Canvas.SetTop(_selectionRect, top);
                _selectionRect.Width = width;
                _selectionRect.Height = height;
            }
        }

        /// <summary>
        /// マウス左ボタンが離されたときの処理。選択を完了し、非同期で切り抜き処理を実行してウィンドウを閉じます。
        /// </summary>
        /// <param name="sender">イベント送信者。</param>
        /// <param name="e">マウスイベント引数。</param>
        private async void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!SelectionCanvas.IsMouseCaptured || !_captureCompleted) return;
            
            SelectionCanvas.ReleaseMouseCapture();
            _selectionRect.Visibility = Visibility.Hidden;

            try
            {
                // 終了時のスクリーン座標(px)
                var endScreenPx = System.Windows.Forms.Cursor.Position;

                // 選択範囲を計算（スクリーン座標）
                var screenLeftPx = Math.Min(_startScreenPx.X, endScreenPx.X);
                var screenTopPx = Math.Min(_startScreenPx.Y, endScreenPx.Y);
                var width = Math.Abs(endScreenPx.X - _startScreenPx.X);
                var height = Math.Abs(endScreenPx.Y - _startScreenPx.Y);

                LogService.LogInfo($"選択範囲 (screen px): {screenLeftPx}, {screenTopPx}, {width}x{height}");

                // 範囲が十分大きいかチェック
                if (width < 3 || height < 3)
                {
                    LogService.LogInfo("選択範囲が小さすぎます - キャプチャをキャンセル");
                    this.Close();
                    return;
                }

                // スクリーン座標でのキャプチャ領域と表示位置を作成
                var captureRegion = new System.Drawing.Rectangle(screenLeftPx, screenTopPx, width, height);
                var displayPosition = new System.Drawing.Point(screenLeftPx, screenTopPx);

                // 非同期でクロップとピン留め処理を実行
                await CropAndPinAsync(captureRegion, displayPosition);
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "マウスアップ処理エラー");
            }
            finally
            {
                this.Close();
            }
        }



        /// <summary>
        /// 指定された矩形領域を切り抜いて、ピン留めウィンドウを非同期で作成します。
        /// </summary>
        /// <param name="screenRegion">スクリーン座標でのキャプチャ領域</param>
        /// <param name="displayPosition">表示位置（スクリーン座標）</param>
        private async Task CropAndPinAsync(System.Drawing.Rectangle screenRegion, System.Drawing.Point displayPosition)
        {
            if (_disposed)
                return;

            try
            {
                LogService.LogInfo($"非同期クロップ開始 - 領域: {screenRegion}, 表示位置: {displayPosition}");
                
                // 非同期で指定領域をキャプチャ
                var result = await _captureService.CaptureRegionAsync(screenRegion, cancellationToken: _cancellationTokenSource.Token);
                
                if (!result.IsSuccess || result.BitmapSource == null)
                {
                    LogService.LogError($"クロップ用キャプチャに失敗: {result.ErrorMessage}");
                    return;
                }

                // UIスレッドでクリップボードへコピーとピン留めウィンドウ作成
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // クリップボードへコピー
                        System.Windows.Clipboard.SetImage(result.BitmapSource);
                        LogService.LogInfo("切り抜き画像をクリップボードにコピーしました");

                        // 座標変換してピン留めウィンドウを作成
                        var dipPosition = CoordinateTransformation.ScreenPixelToDip(
                            displayPosition.X, displayPosition.Y);
                        
                        PinnedWindowManager.Create(result.BitmapSource, 
                            (int)Math.Round(dipPosition.X), (int)Math.Round(dipPosition.Y));
                        
                        LogService.LogInfo($"ピン留めウィンドウを作成・表示しました (DIP): {dipPosition.X:F1}, {dipPosition.Y:F1}");
                    }
                    catch (Exception ex)
                    {
                        LogService.LogException(ex, "クリップボードコピーまたはPinnedWindow作成エラー");
                    }
                });
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogService.LogException(ex, "非同期クロップ処理エラー");
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
        /// リソースを解放します
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            try
            {
                // キャンセレーショントークンをキャンセル
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                
                // キャプチャサービスを解放
                _captureService?.Dispose();
                
                LogService.LogDebug("CaptureOverlayWindowリソース解放完了");
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "CaptureOverlayWindowリソース解放エラー");
            }
        }
    }
}