using System;
using System.Drawing;

namespace LovingCoolMini
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(11, 7, 16);
        public static readonly Color Card = Color.FromArgb(23, 16, 33);
        public static readonly Color CardHover = Color.FromArgb(32, 22, 44);
        public static readonly Color CardBorder = Color.FromArgb(58, 37, 71);
        public static readonly Color Accent = Color.FromArgb(168, 85, 247);
        public static readonly Color AccentLight = Color.FromArgb(216, 180, 254);
        public static readonly Color AccentDark = Color.FromArgb(124, 58, 237);
        public static readonly Color Text = Color.FromArgb(245, 240, 250);
        public static readonly Color TextSecondary = Color.FromArgb(167, 139, 186);
        public static readonly Color Muted = Color.FromArgb(120, 100, 135);
        public static readonly Color Green = Color.FromArgb(74, 222, 128);
        public static readonly Color Amber = Color.FromArgb(251, 191, 36);
        public static readonly Color Red = Color.FromArgb(248, 113, 113);
        public static readonly Color ButtonDark = Color.FromArgb(30, 20, 41);

        private static string _family;

        public static string FontFamily
        {
            get
            {
                if (_family == null)
                {
                    try
                    {
                        using (Font test = new Font("Microsoft YaHei UI", 9f))
                        {
                            _family = test.Name;
                        }
                    }
                    catch
                    {
                        _family = "Segoe UI";
                    }
                }
                return _family;
            }
        }

        public static Font Font(float size)
        {
            return new Font(FontFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Font(float size, FontStyle style)
        {
            return new Font(FontFamily, size, style, GraphicsUnit.Point);
        }
    }
}
