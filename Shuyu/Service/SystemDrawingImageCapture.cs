using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Media.Imaging;

namespace Shuyu.Service
{
    /// <summary>
    /// Default implementation of <see cref="IImageCapture"/> using System.Drawing (GDI+).
    /// </summary>
    public class SystemDrawingImageCapture : IImageCapture
    {
        public void Dispose()
        {
            // nothing to dispose currently
        }

        public Bitmap? CaptureRegionToBitmap(Rectangle region, CancellationToken cancellationToken)
        {
            Bitmap? bitmap = null;
            Graphics? graphics = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                graphics = Graphics.FromImage(bitmap);

                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                cancellationToken.ThrowIfCancellationRequested();

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

        public BitmapSource? ConvertBitmapToBitmapSource(Bitmap bitmap, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bitmapSource = BitmapSource.Create(
                        bitmap.Width,
                        bitmap.Height,
                        96,
                        96,
                        System.Windows.Media.PixelFormats.Bgra32,
                        null,
                        bitmapData.Scan0,
                        bitmapData.Stride * bitmap.Height,
                        bitmapData.Stride);

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
    }
}
