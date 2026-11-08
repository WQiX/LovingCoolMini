using System;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace LovingCoolMini
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool startMinimized = false;
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
                    {
                        startMinimized = true;
                    }
                }
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\LovingCoolMini.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    NativeMethods.ShowExistingWindow();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Logger logger = new Logger();
                AppConfig config = AppConfig.Load(logger);

                logger.Info("LOVINGCOOL Mini 启动，版本 1.1.0，管理员=" + IsAdministrator() +
                            "，参数=" + string.Join(" ", args ?? new string[0]));

                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    logger.Error("未处理的 UI 异常", e.Exception);
                    MessageBox.Show("程序遇到错误：\r\n" + e.Exception.Message +
                                    "\r\n\r\n详细信息已写入日志。", "LOVINGCOOL Mini",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    logger.Error("未处理的异常", ex);
                };

                if (!IsAdministrator())
                {
                    MessageBox.Show("本程序需要管理员权限才能读取 CPU 温度。\r\n请右键程序，选择“以管理员身份运行”。",
                        "LOVINGCOOL Mini", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                TrayApplicationContext context = new TrayApplicationContext(config, logger, startMinimized);
                Application.Run(context);
                GC.KeepAlive(mutex);
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
