using System;
using System.Drawing;
using System.Threading;
using System.Windows.Media.Imaging;

namespace Shuyu.Service
{
    /// <summary>
    /// Image capture abstraction to decouple from System.Drawing implementations.
    /// </summary>
    public interface IImageCapture : IDisposable
    {
        /// <summary>
        /// Capture the specified region and return a System.Drawing.Bitmap (or null on failure).
        /// </summary>
        Bitmap? CaptureRegionToBitmap(Rectangle region, CancellationToken cancellationToken);

        /// <summary>
        /// Convert a System.Drawing.Bitmap to a WPF BitmapSource.
        /// </summary>
        BitmapSource? ConvertBitmapToBitmapSource(Bitmap bitmap, CancellationToken cancellationToken);
    }
}
