using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Shuyu.Service
{
    public static class PinnedWindowManager
    {
        private static readonly object _lock = new();
        private static readonly List<Shuyu.PinnedWindow> _windows = new();

        public static void Register(Shuyu.PinnedWindow w)
        {
            lock (_lock) { _windows.Add(w); }
        }

        public static void Unregister(Shuyu.PinnedWindow w)
        {
            lock (_lock) { _windows.Remove(w); }
        }

        public static void Create(BitmapSource image, int left, int top)
        {
            var d = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            if (image.CanFreeze) image.Freeze();
            d.Invoke(() =>
            {
                var w = new Shuyu.PinnedWindow(image, left, top);
                w.Show();
            });
        }

        public static void CloseAll()
        {
            var d = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            Shuyu.PinnedWindow[] arr;
            lock (_lock) { arr = _windows.ToArray(); }
            d.Invoke(() =>
            {
                foreach (var w in arr.ToList())
                    w.Close();
            });
        }

        public static void ShowAll()
        {
            var d = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            Shuyu.PinnedWindow[] arr;
            lock (_lock) { arr = _windows.ToArray(); }
            d.Invoke(() =>
            {
                foreach (var w in arr) { if (!w.IsVisible) w.Show(); w.Activate(); }
            });
        }

        public static void HideAll()
        {
            var d = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            Shuyu.PinnedWindow[] arr;
            lock (_lock) { arr = _windows.ToArray(); }
            d.Invoke(() =>
            {
                foreach (var w in arr) { if (w.IsVisible) w.Hide(); }
            });
        }

        public static void ToggleAllVisibility()
        {
            bool anyVisible;
            lock (_lock) { anyVisible = _windows.Any(w => w.IsVisible); }
            if (anyVisible) HideAll(); else ShowAll();
        }
    }
}
