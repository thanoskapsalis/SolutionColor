using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SolutionColor
{
    /// <summary>
    /// Recolors images by replacing hue/saturation with the target color while keeping each pixel's
    /// lightness and alpha, so the shape and shading of the Visual Studio logo stay recognizable.
    /// </summary>
    internal static class ImageTinter
    {
        public static BitmapSource Tint(ImageSource source, Color target, int pixelWidth, int pixelHeight)
        {
            if (source == null || pixelWidth <= 0 || pixelHeight <= 0)
                return null;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(source, new Rect(0, 0, pixelWidth, pixelHeight));
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return Tint(rtb, target);
        }

        public static BitmapSource Tint(BitmapSource source, Color target)
        {
            var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = bgra.PixelWidth;
            int height = bgra.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);

            RgbToHsl(target.R, target.G, target.B, out double th, out double ts, out _);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 0)
                    continue;

                RgbToHsl(pixels[i + 2], pixels[i + 1], pixels[i], out _, out double s, out double l);

                // Leave near-grey pixels (white highlights, dark outlines) alone.
                if (s < 0.12)
                    continue;

                HslToRgb(th, Math.Max(ts, 0.25) * Math.Min(1.0, s * 1.2), l, out byte r, out byte g, out byte b);
                pixels[i] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
            }

            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }

        /// <summary>A filled circle with a light outline, used as taskbar overlay badge.</summary>
        public static ImageSource CreateBadge(Color color)
        {
            const double size = 16;
            var drawing = new GeometryDrawing(
                new SolidColorBrush(color),
                new Pen(Brushes.White, 1.5),
                new EllipseGeometry(new Point(size / 2, size / 2), size / 2 - 1, size / 2 - 1));
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        public static Color ContrastingForeground(Color background)
        {
            double luminance = (0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) + 0.0722 * Linear(background.B));
            return luminance > 0.4 ? Colors.Black : Colors.White;
        }

        private static double Linear(byte c)
        {
            double v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static void RgbToHsl(byte rb, byte gb, byte bb, out double h, out double s, out double l)
        {
            double r = rb / 255.0, g = gb / 255.0, b = bb / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;

            if (max == min)
            {
                h = s = 0;
                return;
            }

            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r)
                h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g)
                h = (b - r) / d + 2;
            else
                h = (r - g) / d + 4;
            h /= 6;
        }

        private static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
        {
            double rf, gf, bf;
            if (s == 0)
            {
                rf = gf = bf = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                rf = HueToRgb(p, q, h + 1.0 / 3);
                gf = HueToRgb(p, q, h);
                bf = HueToRgb(p, q, h - 1.0 / 3);
            }

            r = (byte)Math.Round(rf * 255);
            g = (byte)Math.Round(gf * 255);
            b = (byte)Math.Round(bf * 255);
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
