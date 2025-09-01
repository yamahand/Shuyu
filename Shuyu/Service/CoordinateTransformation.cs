using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Shuyu.Service
{
    /// <summary>
    /// 座標変換とDPIスケーリングを一元管理するユーティリティクラス
    /// </summary>
    public static class CoordinateTransformation
    {
        // Win32 API定数
        private const int MDT_EFFECTIVE_DPI = 0;
        private const double BASE_DPI = 96.0;

        // Win32 API宣言
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;

            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        /// <summary>
        /// DPI情報を格納する構造体
        /// </summary>
        public readonly struct DpiInfo
        {
            public double DpiX { get; }
            public double DpiY { get; }
            public double ScaleFactorX { get; }
            public double ScaleFactorY { get; }

            public DpiInfo(double dpiX, double dpiY)
            {
                DpiX = dpiX;
                DpiY = dpiY;
                ScaleFactorX = dpiX / BASE_DPI;
                ScaleFactorY = dpiY / BASE_DPI;
            }

            public static DpiInfo Default => new(BASE_DPI, BASE_DPI);
        }

        /// <summary>
        /// 仮想スクリーン情報を格納する構造体
        /// </summary>
        public readonly struct VirtualScreenInfo
        {
            public int Left { get; }
            public int Top { get; }
            public int Width { get; }
            public int Height { get; }
            public Rectangle Bounds => new(Left, Top, Width, Height);

            public VirtualScreenInfo(int left, int top, int width, int height)
            {
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }
        }

        /// <summary>
        /// 指定座標のモニターのDPI情報を取得します
        /// </summary>
        /// <param name="x">スクリーン座標X</param>
        /// <param name="y">スクリーン座標Y</param>
        /// <returns>DPI情報</returns>
        public static DpiInfo GetDpiAtPoint(int x, int y)
        {
            try
            {
                var point = new POINT(x, y);
                var monitor = MonitorFromPoint(point, 2); // MONITOR_DEFAULTTONEAREST

                if (monitor != IntPtr.Zero && 
                    GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    return new DpiInfo(dpiX, dpiY);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"DPI取得エラー: {SecurityHelper.SanitizeLogMessage(ex.Message)}");
            }

            return DpiInfo.Default;
        }

        /// <summary>
        /// WPFウィンドウからDPI情報を取得します
        /// </summary>
        /// <param name="window">WPFウィンドウ</param>
        /// <returns>DPI情報</returns>
        public static DpiInfo GetDpiFromWindow(Window window)
        {
            try
            {
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget != null)
                {
                    var matrix = source.CompositionTarget.TransformToDevice;
                    var dpiX = matrix.M11 * BASE_DPI;
                    var dpiY = matrix.M22 * BASE_DPI;
                    return new DpiInfo(dpiX, dpiY);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"Window DPI取得エラー: {SecurityHelper.SanitizeLogMessage(ex.Message)}");
            }

            return DpiInfo.Default;
        }

        /// <summary>
        /// スクリーン座標をDIP座標に変換します
        /// </summary>
        /// <param name="screenX">スクリーンX座標</param>
        /// <param name="screenY">スクリーンY座標</param>
        /// <param name="dpiInfo">DPI情報（未指定時は指定座標のDPIを自動取得）</param>
        /// <returns>DIP座標</returns>
        public static (double X, double Y) ScreenPixelToDip(int screenX, int screenY, DpiInfo? dpiInfo = null)
        {
            var dpi = dpiInfo ?? GetDpiAtPoint(screenX, screenY);
            return (screenX / dpi.ScaleFactorX, screenY / dpi.ScaleFactorY);
        }

        /// <summary>
        /// DIP座標をスクリーン座標に変換します
        /// </summary>
        /// <param name="dipX">DIP X座標</param>
        /// <param name="dipY">DIP Y座標</param>
        /// <param name="dpiInfo">DPI情報（未指定時はカーソル位置のDPIを使用）</param>
        /// <returns>スクリーン座標</returns>
        public static (int X, int Y) DipToScreenPixel(double dipX, double dipY, DpiInfo? dpiInfo = null)
        {
            var dpi = dpiInfo ?? GetCurrentCursorDpi();
            return ((int)Math.Round(dipX * dpi.ScaleFactorX), (int)Math.Round(dipY * dpi.ScaleFactorY));
        }

        /// <summary>
        /// 現在のカーソル位置のDPI情報を取得します
        /// </summary>
        /// <returns>DPI情報</returns>
        public static DpiInfo GetCurrentCursorDpi()
        {
            if (GetCursorPos(out var cursorPos))
            {
                return GetDpiAtPoint(cursorPos.x, cursorPos.y);
            }
            return DpiInfo.Default;
        }

        /// <summary>
        /// 仮想スクリーン情報を取得します
        /// </summary>
        /// <returns>仮想スクリーン情報</returns>
        public static VirtualScreenInfo GetVirtualScreenInfo()
        {
            return new VirtualScreenInfo(
                System.Windows.Forms.SystemInformation.VirtualScreen.Left,
                System.Windows.Forms.SystemInformation.VirtualScreen.Top,
                System.Windows.Forms.SystemInformation.VirtualScreen.Width,
                System.Windows.Forms.SystemInformation.VirtualScreen.Height
            );
        }

        /// <summary>
        /// Rectangle をスケールします
        /// </summary>
        /// <param name="rect">元のRectangle</param>
        /// <param name="scaleX">X方向のスケール</param>
        /// <param name="scaleY">Y方向のスケール</param>
        /// <returns>スケール後のRectangle</returns>
        public static Rectangle ScaleRectangle(Rectangle rect, double scaleX, double scaleY)
        {
            return new Rectangle(
                (int)Math.Round(rect.X * scaleX),
                (int)Math.Round(rect.Y * scaleY),
                (int)Math.Round(rect.Width * scaleX),
                (int)Math.Round(rect.Height * scaleY)
            );
        }

        /// <summary>
        /// WPF Rect をスケールします
        /// </summary>
        /// <param name="rect">元のRect</param>
        /// <param name="scaleX">X方向のスケール</param>
        /// <param name="scaleY">Y方向のスケール</param>
        /// <returns>スケール後のRect</returns>
        public static Rect ScaleRect(Rect rect, double scaleX, double scaleY)
        {
            return new Rect(
                rect.X * scaleX,
                rect.Y * scaleY,
                rect.Width * scaleX,
                rect.Height * scaleY
            );
        }

        /// <summary>
        /// 指定領域が有効な範囲内にあるかチェックします
        /// </summary>
        /// <param name="rect">チェック対象の領域</param>
        /// <param name="bounds">境界領域</param>
        /// <returns>有効な場合はtrue</returns>
        public static bool IsValidRegion(Rectangle rect, Rectangle bounds)
        {
            return rect.Width > 0 && 
                   rect.Height > 0 && 
                   rect.IntersectsWith(bounds) &&
                   rect.Width <= bounds.Width &&
                   rect.Height <= bounds.Height;
        }

        /// <summary>
        /// 指定領域を境界内に制限します
        /// </summary>
        /// <param name="rect">制限対象の領域</param>
        /// <param name="bounds">境界領域</param>
        /// <returns>制限後の領域</returns>
        public static Rectangle ClampRectangle(Rectangle rect, Rectangle bounds)
        {
            var x = Math.Max(bounds.X, Math.Min(rect.X, bounds.Right - rect.Width));
            var y = Math.Max(bounds.Y, Math.Min(rect.Y, bounds.Bottom - rect.Height));
            var width = Math.Max(1, Math.Min(rect.Width, bounds.Width));
            var height = Math.Max(1, Math.Min(rect.Height, bounds.Height));

            return new Rectangle(x, y, width, height);
        }
    }
}