using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media.Imaging;

namespace Shuyu.Service
{
    /// <summary>
    /// IImageCapture implementation using Win32 BitBlt to capture the screen.
    /// Returns a System.Drawing.Bitmap created from HBITMAP via Image.FromHbitmap.
    /// </summary>
    public class BitBltImageCapture : IImageCapture
    {
        private const int SRCCOPY = 0x00CC0020;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        public void Dispose()
        {
            // nothing to dispose at object level
        }

        public Bitmap? CaptureRegionToBitmap(Rectangle region, CancellationToken cancellationToken)
        {
            IntPtr hScreenDC = IntPtr.Zero;
            IntPtr hMemDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                hScreenDC = GetDC(IntPtr.Zero);
                if (hScreenDC == IntPtr.Zero)
                {
                    LogService.LogError($"GetDC failed. Error={Marshal.GetLastWin32Error()}");
                    return null;
                }

                hMemDC = CreateCompatibleDC(hScreenDC);
                if (hMemDC == IntPtr.Zero)
                {
                    LogService.LogError($"CreateCompatibleDC failed. Error={Marshal.GetLastWin32Error()}");
                    return null;
                }

                hBitmap = CreateCompatibleBitmap(hScreenDC, region.Width, region.Height);
                if (hBitmap == IntPtr.Zero)
                {
                    LogService.LogError($"CreateCompatibleBitmap failed. Error={Marshal.GetLastWin32Error()}");
                    return null;
                }

                hOld = SelectObject(hMemDC, hBitmap);

                bool success = BitBlt(hMemDC, 0, 0, region.Width, region.Height, hScreenDC, region.X, region.Y, SRCCOPY);
                if (!success)
                {
                    LogService.LogError($"BitBlt failed. Error={Marshal.GetLastWin32Error()}");
                    return null;
                }

                // Create managed Bitmap from HBITMAP
                var bmp = Image.FromHbitmap(hBitmap);

                return bmp as Bitmap;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogService.LogException(ex, "BitBlt capture error");
                return null;
            }
            finally
            {
                if (hOld != IntPtr.Zero && hMemDC != IntPtr.Zero) SelectObject(hMemDC, hOld);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (hMemDC != IntPtr.Zero) DeleteDC(hMemDC);
                if (hScreenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hScreenDC);
            }
        }

        public BitmapSource? ConvertBitmapToBitmapSource(Bitmap bitmap, CancellationToken cancellationToken)
        {
            // Reuse SystemDrawing conversion logic (same as SystemDrawingImageCapture)
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

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
                LogService.LogException(ex, "BitmapSource conversion error");
                return null;
            }
        }
    }
}
