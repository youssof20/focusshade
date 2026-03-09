using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FocusShade;

public static class BlurHelper
{
    /// <summary>
    /// Captures a region of the screen and applies a box blur.
    /// Caller must dispose or not hold the returned bitmap long-term.
    /// </summary>
    public static BitmapSource? CaptureAndBlur(Rect bounds, int blurRadiusPx)
    {
        int w = (int)Math.Ceiling(bounds.Width);
        int h = (int)Math.Ceiling(bounds.Height);
        if (w <= 0 || h <= 0) return null;

        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen((int)bounds.X, (int)bounds.Y, 0, 0, new System.Drawing.Size(w, h));
        }

        if (blurRadiusPx > 0)
            ApplyBoxBlur(bmp, blurRadiusPx);

        return ToBitmapSource(bmp);
    }

    private static void ApplyBoxBlur(Bitmap bmp, int radius)
    {
        if (radius <= 0) return;
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int w = bmp.Width;
            int h = bmp.Height;
            int stride = Math.Abs(data.Stride);
            var src = new byte[stride * h];
            Marshal.Copy(data.Scan0, src, 0, src.Length);
            var dst = new byte[src.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    long sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                    int count = 0;
                    for (int ky = -radius; ky <= radius; ky++)
                    {
                        for (int kx = -radius; kx <= radius; kx++)
                        {
                            int nx = x + kx;
                            int ny = y + ky;
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                            {
                                int i = ny * stride + nx * 4;
                                sumB += src[i];
                                sumG += src[i + 1];
                                sumR += src[i + 2];
                                sumA += src[i + 3];
                                count++;
                            }
                        }
                    }

                    int o = y * stride + x * 4;
                    count = Math.Max(1, count);
                    dst[o] = (byte)(sumB / count);
                    dst[o + 1] = (byte)(sumG / count);
                    dst[o + 2] = (byte)(sumR / count);
                    dst[o + 3] = (byte)(sumA / count);
                }
            }

            Marshal.Copy(dst, 0, data.Scan0, dst.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static BitmapSource ToBitmapSource(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var wb = new WriteableBitmap(bmp.Width, bmp.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            wb.Lock();
            var buffer = new byte[Math.Abs(data.Stride) * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            wb.WritePixels(new Int32Rect(0, 0, bmp.Width, bmp.Height), buffer, data.Stride, 0);
            wb.Unlock();
            wb.Freeze();
            return wb;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
