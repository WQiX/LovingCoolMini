using System;
using System.IO;
using System.Text;

namespace LovingCoolMini
{
    public sealed class Logger
    {
        private readonly object _lock = new object();
        private readonly string _path;

        public Logger()
        {
            try
            {
                Directory.CreateDirectory(AppConfig.LogDir);
            }
            catch
            {
            }
            _path = Path.Combine(AppConfig.LogDir, "app.log");
        }

        public string DirectoryPath
        {
            get { return AppConfig.LogDir; }
        }

        public string FilePath
        {
            get { return _path; }
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Error(string message, Exception ex)
        {
            Write("ERROR", message, ex);
        }

        private void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (_lock)
                {
                    RotateIfNeeded();
                    StringBuilder sb = new StringBuilder();
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    sb.Append(" [");
                    sb.Append(level);
                    sb.Append("] ");
                    sb.Append(message);
                    if (ex != null)
                    {
                        sb.AppendLine();
                        sb.Append(ex.ToString());
                    }
                    File.AppendAllText(_path, sb.ToString() + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        private void RotateIfNeeded()
        {
            try
            {
                FileInfo fi = new FileInfo(_path);
                if (!fi.Exists || fi.Length < 1024 * 1024)
                {
                    return;
                }

                string p1 = _path + ".1";
                string p2 = _path + ".2";
                string p3 = _path + ".3";
                if (File.Exists(p3)) File.Delete(p3);
                if (File.Exists(p2)) File.Move(p2, p3);
                if (File.Exists(p1)) File.Move(p1, p2);
                File.Move(_path, p1);
            }
            catch
            {
            }
        }
    }
}
