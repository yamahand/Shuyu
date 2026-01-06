using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Shuyu.Service
{
    /// <summary>
    /// 非同期画面キャプチャサービス
    /// </summary>
    public class AsyncScreenCaptureService : IDisposable
    {
        private bool _disposed = false;
        private readonly IImageCapture _imageCapture;

        /// <summary>
        /// キャプチャ結果を格納する構造体
        /// </summary>
        public readonly struct CaptureResult
        {
            public BitmapSource? BitmapSource { get; }
            public Rectangle CapturedRegion { get; }
            public bool IsSuccess { get; }
            public string? ErrorMessage { get; }

            public CaptureResult(BitmapSource bitmapSource, Rectangle capturedRegion)
            {
                BitmapSource = bitmapSource;
                CapturedRegion = capturedRegion;
                IsSuccess = true;
                ErrorMessage = null;
            }

            public CaptureResult(string errorMessage)
            {
                BitmapSource = null;
                CapturedRegion = Rectangle.Empty;
                IsSuccess = false;
                ErrorMessage = errorMessage;
            }
        }

        /// <summary>
        /// 進行状況を報告するデリゲート
        /// </summary>
        /// <param name="percentage">進行率（0-100）</param>
        /// <param name="message">状況メッセージ</param>
        public delegate void ProgressReporter(int percentage, string message);

        /// <summary>
        /// 指定された領域を非同期でキャプチャします
        /// </summary>
        /// <param name="region">キャプチャ領域</param>
        /// <param name="progress">進行状況レポーター</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>キャプチャ結果</returns>
        public AsyncScreenCaptureService() : this(new SystemDrawingImageCapture()) { }

        public AsyncScreenCaptureService(IImageCapture? imageCapture)
        {
            _imageCapture = imageCapture ?? new SystemDrawingImageCapture();
            try
            {
                LogService.LogInfo($"ImageCapture implementation: {_imageCapture.GetType().FullName}");
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"ImageCapture logging failed: {SecurityHelper.SanitizeLogMessage(ex.Message)}");
            }
        }

        public async Task<CaptureResult> CaptureRegionAsync(
            Rectangle region,
            ProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return new CaptureResult("サービスは既に破棄されています");

            if (region.Width <= 0 || region.Height <= 0)
                return new CaptureResult("無効な領域が指定されました");

            try
            {
                progress?.Invoke(0, "キャプチャ準備中...");

                // 仮想スクリーン情報を取得
                var virtualScreen = CoordinateTransformation.GetVirtualScreenInfo();

                // 領域が有効かチェック
                if (!CoordinateTransformation.IsValidRegion(region, virtualScreen.Bounds))
                {
                    region = CoordinateTransformation.ClampRectangle(region, virtualScreen.Bounds);
                    LogService.LogWarning($"キャプチャ領域を調整しました: {region}");
                }

                progress?.Invoke(25, "スクリーンキャプチャ中...");

                // バックグラウンドスレッドでキャプチャ実行（実装は IImageCapture に委譲）
                var bitmap = await Task.Run(() => _imageCapture.CaptureRegionToBitmap(region, cancellationToken), cancellationToken);

                if (bitmap == null)
                    return new CaptureResult("スクリーンキャプチャに失敗しました");

                progress?.Invoke(75, "画像変換中...");

                // BitmapSourceに変換（IImageCapture 実装に委譲）
                var bitmapSource = await Task.Run(() => _imageCapture.ConvertBitmapToBitmapSource(bitmap, cancellationToken), cancellationToken);

                // 元のBitmapを破棄
                bitmap.Dispose();

                if (bitmapSource == null)
                    return new CaptureResult("画像変換に失敗しました");

                progress?.Invoke(100, "完了");

                LogService.LogInfo($"キャプチャ完了: {region} -> {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}");

                return new CaptureResult(bitmapSource, region);
            }
            catch (OperationCanceledException)
            {
                LogService.LogInfo("キャプチャがキャンセルされました");
                return new CaptureResult("キャプチャがキャンセルされました");
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "非同期キャプチャエラー");
                return new CaptureResult($"キャプチャエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// フル画面を非同期でキャプチャします（最適化のため効率的な実装）
        /// </summary>
        /// <param name="progress">進行状況レポーター</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>キャプチャ結果</returns>
        public async Task<CaptureResult> CaptureFullScreenAsync(
            ProgressReporter? progress = null,
            CancellationToken cancellationToken = default)
        {
            var virtualScreen = CoordinateTransformation.GetVirtualScreenInfo();
            return await CaptureRegionAsync(virtualScreen.Bounds, progress, cancellationToken);
        }

        /// <summary>
        /// 指定領域のスクリーンをキャプチャします（同期処理）
        /// </summary>
        /// <param name="region">キャプチャ領域</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>キャプチャされたBitmap</returns>
        private Bitmap? CaptureScreenRegion(Rectangle region, CancellationToken cancellationToken)
        {
            Bitmap? bitmap = null;
            Graphics? graphics = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Bitmapを作成
                bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                graphics = Graphics.FromImage(bitmap);

                // 高品質設定
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                cancellationToken.ThrowIfCancellationRequested();

                // 画面からコピー
                graphics.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);

                return bitmap;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogService.LogException(ex, "画面キャプチャエラー");
                bitmap?.Dispose();
                return null;
            }
            finally
            {
                graphics?.Dispose();
            }
        }

        /// <summary>
        /// BitmapをBitmapSourceに変換します
        /// </summary>
        /// <param name="bitmap">変換元Bitmap</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <returns>変換されたBitmapSource</returns>
        private BitmapSource? ConvertToBitmapSource(Bitmap bitmap, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Bitmapデータを取得
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // BitmapSourceを作成
                    var bitmapSource = BitmapSource.Create(
                        bitmap.Width,
                        bitmap.Height,
                        96, // DPI X
                        96, // DPI Y
                        System.Windows.Media.PixelFormats.Bgra32,
                        null,
                        bitmapData.Scan0,
                        bitmapData.Stride * bitmap.Height,
                        bitmapData.Stride);

                    // フリーズしてスレッドセーフにする
                    bitmapSource.Freeze();

                    return bitmapSource;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogService.LogException(ex, "BitmapSource変換エラー");
                return null;
            }
        }

        /// <summary>
        /// メモリ使用量を最適化するためのクリーンアップ
        /// </summary>
        public void OptimizeMemory()
        {
            if (_disposed) return;

            try
            {
                // ガベージコレクションを実行
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                LogService.LogDebug("メモリ最適化完了");
            }
            catch (Exception ex)
            {
                LogService.LogException(ex, "メモリ最適化エラー");
            }
        }

        /// <summary>
        /// リソースを解放します
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // 最終クリーンアップ
            OptimizeMemory();

            LogService.LogDebug("AsyncScreenCaptureService破棄完了");
        }
    }
}