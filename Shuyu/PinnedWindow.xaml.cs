using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Shuyu.Service;

namespace Shuyu
{
    public partial class PinnedWindow : Window
    {
        private readonly BitmapSource _image;

        public PinnedWindow(BitmapSource image, int left, int top)
        {
            InitializeComponent();

            _image = image;
            PinnedImage.Source = _image;

            // 画像ピクセルサイズに合わせる（DPIを無視）
            this.Width = _image.PixelWidth;
            this.Height = _image.PixelHeight;
            this.Left = left;
            this.Top = top;

            // 右クリックメニュー
            this.ContextMenu = BuildContextMenu();

            // ドラッグ移動
            this.MouseLeftButtonDown += (_, __) =>
            {
                try { this.DragMove(); } catch { }
                this.Activate();
            };

            // Escで閉じる（アクティブ時）
            this.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape && this.IsActive)
                {
                    this.Close();
                }
            };

            // ロード時にハイライトをフェードアウトして場所を知らせる
            this.Loaded += (_, __) =>
            {
                PinnedWindowManager.Register(this);
                try
                {
                    var fade = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(900),
                        BeginTime = TimeSpan.FromMilliseconds(1000),
                        FillBehavior = FillBehavior.Stop
                    };
                    fade.Completed += (_, ____) =>
                    {
                        // 完全に透明にしつつ、ヒットテストにもかからないようにする
                        var b = (System.Windows.Controls.Border) this.FindName("HighlightBorder");
                        if (b != null)
                        {
                            b.Opacity = 0.0;
                            b.IsHitTestVisible = false;
                        }
                    };

                    var border = (System.Windows.Controls.Border)this.FindName("HighlightBorder");
                    if (border != null)
                    {
                        border.BeginAnimation(OpacityProperty, fade);
                    }
                }
                catch { }
            };

            // 生成時の座標をログ出力（DIP と 物理px）
            LogPosition("作成時");

            this.Closed += (_, __) => PinnedWindowManager.Unregister(this);
        }

        private ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(CreateItem("保存 (PNG)", () => SaveWithDialog("png")));
            menu.Items.Add(CreateItem("保存 (JPEG)", () => SaveWithDialog("jpg")));
            menu.Items.Add(CreateItem("保存 (BMP)", () => SaveWithDialog("bmp")));
            menu.Items.Add(CreateItem("保存 (DDS)", () => SaveWithDialog("dds")));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateItem("閉じる", () => this.Close()));
            return menu;
        }

        private MenuItem CreateItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, __) => action();
            return item;
        }

        private void SaveWithDialog(string ext)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "画像を保存",
                FileName = $"pinned_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}",
                Filter = ext switch
                {
                    "png" => "PNG 画像 (*.png)|*.png",
                    "jpg" => "JPEG 画像 (*.jpg;*.jpeg)|*.jpg;*.jpeg",
                    "bmp" => "Bitmap 画像 (*.bmp)|*.bmp",
                    "dds" => "DDS 画像 (*.dds)|*.dds",
                    _ => "すべてのファイル (*.*)|*.*"
                },
                DefaultExt = ext
            };

            if (dlg.ShowDialog(this) == true)
            {
                // セキュリティ検証を追加
                if (!SecurityHelper.IsValidFilePath(dlg.FileName))
                {
                    LogService.LogWarning($"無効なファイルパスが指定されました: {SecurityHelper.SanitizeLogMessage(dlg.FileName)}");
                    System.Windows.MessageBox.Show(this, "指定されたファイルパスは無効です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    SaveImage(dlg.FileName);
                    LogService.LogInfo($"画像を保存しました: {SecurityHelper.SanitizeLogMessage(dlg.FileName)}");
                }
                catch (Exception ex)
                {
                    LogService.LogException(ex, "画像保存エラー");
                    System.Windows.MessageBox.Show(this, $"保存に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveImage(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
            switch (ext)
            {
                case ".png":
                    {
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(_image));
                        enc.Save(fs);
                        break;
                    }
                case ".jpg":
                case ".jpeg":
                    {
                        var enc = new JpegBitmapEncoder { QualityLevel = 90 };
                        enc.Frames.Add(BitmapFrame.Create(_image));
                        enc.Save(fs);
                        break;
                    }
                case ".bmp":
                    {
                        var enc = new BmpBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(_image));
                        enc.Save(fs);
                        break;
                    }
                case ".dds":
                    {
                        SaveAsDdsUncompressedBgra32(_image, fs);
                        break;
                    }
                default:
                    throw new NotSupportedException($"未対応の拡張子です: {ext}");
            }
        }

        private static void SaveAsDdsUncompressedBgra32(BitmapSource src, Stream stream)
        {
            // BGRA32に変換
            var fmt = src.Format;
            BitmapSource image = src;
            if (fmt != System.Windows.Media.PixelFormats.Bgra32)
            {
                var fcb = new System.Windows.Media.Imaging.FormatConvertedBitmap();
                fcb.BeginInit();
                fcb.Source = src;
                fcb.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
                fcb.EndInit();
                image = fcb;
            }

            int w = image.PixelWidth;
            int h = image.PixelHeight;
            int bpp = 32;
            int stride = (w * bpp + 7) / 8;
            var pixels = new byte[stride * h];
            image.CopyPixels(pixels, stride, 0);

            using var bw = new BinaryWriter(stream);

            // 'DDS '
            bw.Write(0x20534444u);

            // DDS_HEADER
            bw.Write(124u); // dwSize
            const uint DDSD_CAPS = 0x1;
            const uint DDSD_HEIGHT = 0x2;
            const uint DDSD_WIDTH = 0x4;
            const uint DDSD_PITCH = 0x8;
            const uint DDSD_PIXELFORMAT = 0x1000;
            uint flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PITCH | DDSD_PIXELFORMAT;
            bw.Write(flags);        // dwFlags
            bw.Write((uint)h);      // dwHeight
            bw.Write((uint)w);      // dwWidth
            bw.Write((uint)stride); // dwPitchOrLinearSize
            bw.Write(0u);           // dwDepth
            bw.Write(0u);           // dwMipMapCount
            for (int i = 0; i < 11; i++) bw.Write(0u); // dwReserved1[11]

            // DDS_PIXELFORMAT
            bw.Write(32u); // size
            const uint DDPF_RGB = 0x40;
            const uint DDPF_ALPHAPIXELS = 0x1;
            bw.Write(DDPF_RGB | DDPF_ALPHAPIXELS); // flags
            bw.Write(0u);                          // fourCC
            bw.Write(32u);                         // RGBBitCount
            bw.Write(0x00FF0000u);                 // R mask
            bw.Write(0x0000FF00u);                 // G mask
            bw.Write(0x000000FFu);                 // B mask
            bw.Write(0xFF000000u);                 // A mask

            // caps
            const uint DDSCAPS_TEXTURE = 0x1000;
            bw.Write(DDSCAPS_TEXTURE); // dwCaps
            bw.Write(0u);              // dwCaps2
            bw.Write(0u);              // dwCaps3
            bw.Write(0u);              // dwCaps4
            bw.Write(0u);              // dwReserved2

            // ピクセルデータ
            bw.Write(pixels);
        }

        // DIP/px の座標/サイズをログ出力
        private void LogPosition(string prefix)
        {
            // DIP
            var dip = $"Left={Left:0.##}, Top={Top:0.##}, Size={Width}x{Height} (DIP)";

            // 物理px（DPIスケール適用）
            int pxL = (int)Math.Round(Left);
            int pxT = (int)Math.Round(Top);
            int pxW = (int)Math.Round(Width);
            int pxH = (int)Math.Round(Height);

            var src = PresentationSource.FromVisual(this);
            var ct = src?.CompositionTarget;
            if (ct != null)
            {
                var m = ct.TransformToDevice;
                pxL = (int)Math.Round(Left * m.M11);
                pxT = (int)Math.Round(Top * m.M22);
                pxW = (int)Math.Round(Width * m.M11);
                pxH = (int)Math.Round(Height * m.M22);
            }

            var px = $"Left={pxL}, Top={pxT}, Size={pxW}x{pxH} (px)";
            LogService.LogInfo($"PinnedWindow {prefix}: {dip} / {px}");
        }
    }
}
