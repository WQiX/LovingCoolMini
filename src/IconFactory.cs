using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace LovingCoolMini
{
    internal static class IconFactory
    {
        private static readonly object Sync = new object();
        private static Icon _appIcon;
        private static Icon _trayIcon;

        public static Icon AppIcon
        {
            get
            {
                lock (Sync)
                {
                    if (_appIcon == null) _appIcon = CreateIcon(64);
                    return _appIcon;
                }
            }
        }

        public static Icon TrayIcon
        {
            get
            {
                lock (Sync)
                {
                    if (_trayIcon == null) _trayIcon = CreateIcon(32);
                    return _trayIcon;
                }
            }
        }

        private static Icon CreateIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);

                    Rectangle rect = new Rectangle(0, 0, size - 1, size - 1);
                    using (GraphicsPath path = RoundedRect(rect, Math.Max(4, size / 5)))
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        rect,
                        Color.FromArgb(124, 58, 237),
                        Color.FromArgb(192, 132, 252),
                        45f))
                    {
                        g.FillPath(brush, path);
                    }

                    using (Pen border = new Pen(Color.FromArgb(90, 255, 255, 255), Math.Max(1f, size / 32f)))
                    {
                        using (GraphicsPath path = RoundedRect(rect, Math.Max(4, size / 5)))
                        {
                            g.DrawPath(border, path);
                        }
                    }

                    string text = size >= 48 ? "LC" : "C";
                    using (Font font = new Font("Segoe UI", size * (text == "LC" ? 0.31f : 0.40f), FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        RectangleF textRect = new RectangleF(0, size * 0.02f, size, size);
                        g.DrawString(text, font, Brushes.White, textRect, sf);
                    }

                    if (size >= 48)
                    {
                        using (Font font = new Font("Segoe UI", size * 0.14f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (StringFormat sf = new StringFormat())
                        {
                            sf.Alignment = StringAlignment.Center;
                            sf.LineAlignment = StringAlignment.Center;
                            g.DrawString("°C", font, new SolidBrush(Color.FromArgb(235, 255, 255, 255)),
                                new RectangleF(0, size * 0.70f, size, size * 0.22f), sf);
                        }
                    }
                }

                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                    {
                        return (Icon)tmp.Clone();
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(h);
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
