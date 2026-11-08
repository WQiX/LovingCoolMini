using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LovingCoolMini
{
    public sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly Logger _logger;
        private readonly MainForm _form;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _autoStartItem;
        private readonly SensorReader _sensor;
        private readonly HidDevice _hid;
        private readonly Thread _hidThread;
        private volatile bool _exit;
        private SensorReading _latest;
        private bool _hidConnected;
        private bool _autoStartEnabled;
        private DateTime _lastHeartbeat;
        private DateTime _lastMemoryLog;
        private DateTime _lastTrim;
        private readonly DateTime _startTime;
        private bool _firstReadingLogged;
        private bool _firstWriteLogged;
        private int _lastSentTemp = int.MinValue;
        private bool _balloonShown;

        public TrayApplicationContext(AppConfig config, Logger logger, bool startMinimized)
        {
            _config = config;
            _logger = logger;
            _hidConnected = false;
            _lastHeartbeat = DateTime.MinValue;
            _startTime = DateTime.Now;
            _lastTrim = DateTime.MinValue;

            _form = new MainForm(_config, _logger);
            _form.AutoStartClicked += FormAutoStartClicked;
            _form.HideToTrayClicked += FormHideToTrayClicked;
            _form.OpenLogClicked += FormOpenLogClicked;

            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.BackColor = Theme.Card;
            _menu.ForeColor = Theme.Text;
            _menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
            _menu.Font = Theme.Font(9f);

            ToolStripMenuItem showItem = new ToolStripMenuItem("显示主界面");
            showItem.Font = Theme.Font(9f, FontStyle.Bold);
            showItem.ForeColor = Theme.Text;
            showItem.Click += ShowItemClick;
            _menu.Items.Add(showItem);
            _menu.Items.Add(new ToolStripSeparator());

            _autoStartItem = new ToolStripMenuItem("开机自启");
            _autoStartItem.ForeColor = Theme.Text;
            _autoStartItem.Click += AutoStartItemClick;
            _menu.Items.Add(_autoStartItem);

            ToolStripMenuItem logItem = new ToolStripMenuItem("打开日志文件夹");
            logItem.ForeColor = Theme.Text;
            logItem.Click += LogItemClick;
            _menu.Items.Add(logItem);
            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.ForeColor = Theme.Text;
            exitItem.Click += ExitItemClick;
            _menu.Items.Add(exitItem);

            _tray = new NotifyIcon();
            _tray.Icon = IconFactory.TrayIcon;
            _tray.Text = "LOVINGCOOL Mini";
            _tray.Visible = true;
            _tray.ContextMenuStrip = _menu;
            _tray.DoubleClick += TrayDoubleClick;

            _sensor = new SensorReader(_logger, _config.RefreshMs);
            _sensor.ReadingAvailable += SensorReadingAvailable;
            _sensor.StatusChanged += SensorStatusChanged;

            _hid = new HidDevice(_logger);
            _hid.StatusChanged += HidStatusChanged;

            _hidThread = new Thread(HidLoop);
            _hidThread.IsBackground = true;
            _hidThread.Name = "HidWriter";
            _hidThread.Start();

            ApplyAutoStartOnStartup();
            _sensor.Start();

            // Force the form handle to exist even when starting minimized, so
            // background sensor/HID events can safely marshal to the UI thread.
            IntPtr formHandle = _form.Handle;
            if (!startMinimized)
            {
                _form.Show();
            }
            else
            {
                _form.Hide();
                StartBalloonTimer();
            }
        }

        private void ApplyAutoStartOnStartup()
        {
            try
            {
                bool taskEnabled = AutoStart.IsEnabled(_logger);
                if (_config.AutoStart && !taskEnabled)
                {
                    string error;
                    if (AutoStart.Enable(Application.ExecutablePath, _logger, out error))
                    {
                        taskEnabled = true;
                    }
                    else
                    {
                        _logger.Error("启动时创建自启任务失败: " + error, null);
                        _config.AutoStart = false;
                        _config.Save(_logger);
                        taskEnabled = false;
                    }
                }
                else if (!_config.AutoStart && taskEnabled)
                {
                    string error;
                    if (!AutoStart.Disable(_logger, out error))
                    {
                        _logger.Error("启动时删除自启任务失败: " + error, null);
                    }
                    taskEnabled = false;
                }

                _autoStartEnabled = taskEnabled;
                _autoStartItem.Checked = taskEnabled;
                _form.UpdateAutoStart(taskEnabled);
                _logger.Info("开机自启任务状态: " + taskEnabled);
            }
            catch (Exception ex)
            {
                _logger.Error("初始化开机自启失败", ex);
                _autoStartEnabled = false;
                _autoStartItem.Checked = false;
                _form.UpdateAutoStart(false);
            }
        }

        private void StartBalloonTimer()
        {
            if (_balloonShown) return;
            _balloonShown = true;
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 1200;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                if (!_exit)
                {
                    try
                    {
                        _tray.ShowBalloonTip(3000, "LOVINGCOOL Mini",
                            "程序已在后台运行，关闭窗口不会退出。右键托盘图标可退出。",
                            ToolTipIcon.Info);
                    }
                    catch
                    {
                    }
                }
            };
            timer.Start();
        }

        private void SensorReadingAvailable(object sender, SensorReadingEventArgs e)
        {
            _latest = e.Reading;
            if (!_firstReadingLogged)
            {
                _firstReadingLogged = true;
                _logger.Info("首次读取温度: " + e.Reading.SensorName + " = " + e.Reading.Tctl.ToString("F2") + "°C");
            }
            if ((DateTime.Now - _lastMemoryLog).TotalSeconds >= 300)
            {
                _lastMemoryLog = DateTime.Now;
                try
                {
                    using (Process p = Process.GetCurrentProcess())
                    {
                        _logger.Info("内存: WorkingSet=" + (p.WorkingSet64 / 1024 / 1024) +
                                     "MB, Private=" + (p.PrivateMemorySize64 / 1024 / 1024) + "MB");
                    }
                }
                catch
                {
                }
            }
            if ((_lastTrim == DateTime.MinValue && (DateTime.Now - _startTime).TotalSeconds > 8) ||
                (_lastTrim != DateTime.MinValue && (DateTime.Now - _lastTrim).TotalSeconds > 300))
            {
                TrimWorkingSet();
            }
            _form.UpdateReading(e.Reading);
            _form.UpdateSensorStatus(true, e.Reading.SensorName + " · " + e.Reading.Time.ToString("HH:mm:ss"));
            try
            {
                _tray.Text = Truncate("LOVINGCOOL Mini · CPU " + e.Reading.Tctl.ToString("F1") + "°C", 63);
            }
            catch
            {
            }
        }

        private void SensorStatusChanged(object sender, SensorStatusEventArgs e)
        {
            _form.UpdateSensorStatus(e.Ok, e.Message);
            if (!e.Ok)
            {
                try { _tray.Text = "LOVINGCOOL Mini · 传感器异常"; } catch { }
            }
        }

        private void HidStatusChanged(object sender, HidStatusEventArgs e)
        {
            _hidConnected = e.Connected;
            _form.UpdateHidStatus(e.Connected, e.Message);
            if (e.Connected)
            {
                _lastSentTemp = int.MinValue;
            }
        }

        private void TrimWorkingSet()
        {
            _lastTrim = DateTime.Now;
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (Process p = Process.GetCurrentProcess())
                {
                    long before = p.WorkingSet64;
                    NativeMethods.EmptyWorkingSet(p.Handle);
                    p.Refresh();
                    long after = p.WorkingSet64;
                    _logger.Info("内存整理: " + (before / 1024 / 1024) + "MB -> " + (after / 1024 / 1024) + "MB");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("内存整理失败", ex);
            }
        }

        private void HidLoop()
        {
            while (!_exit)
            {
                try
                {
                    if (!_hid.IsConnected)
                    {
                        if (!_hid.TryConnect())
                        {
                            Thread.Sleep(1500);
                            continue;
                        }
                        _lastSentTemp = int.MinValue;
                    }

                    SensorReading reading = _latest;
                    if (reading == null || (DateTime.Now - reading.Time).TotalSeconds > 5)
                    {
                        Thread.Sleep(300);
                        continue;
                    }

                    int temperature = (int)Math.Round(reading.Tctl, MidpointRounding.AwayFromZero);
                    bool due = temperature != _lastSentTemp ||
                               (DateTime.Now - _lastHeartbeat).TotalSeconds >= _config.HeartbeatSeconds;
                    if (due)
                    {
                        if (_hid.WriteTemperature(temperature))
                        {
                            if (!_firstWriteLogged)
                            {
                                _firstWriteLogged = true;
                                _logger.Info("首次写入面板: " + temperature + "°C");
                            }
                            _lastSentTemp = temperature;
                            _lastHeartbeat = DateTime.Now;
                            _form.UpdateHidStatus(true, "面板已连接 · 显示 " + temperature + "°C");
                        }
                        else
                        {
                            _lastSentTemp = int.MinValue;
                            _form.UpdateHidStatus(false, "写入失败，正在重连...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("HID 写入线程异常", ex);
                    _form.UpdateHidStatus(false, "HID 异常，正在重连...");
                    Thread.Sleep(1000);
                }
                Thread.Sleep(200);
            }
        }

        private void FormAutoStartClicked(object sender, EventArgs e)
        {
            ToggleAutoStart();
        }

        private void FormHideToTrayClicked(object sender, EventArgs e)
        {
            _logger.Info("窗口隐藏到托盘");
            _form.Hide();
            StartBalloonTimer();
        }

        private void FormOpenLogClicked(object sender, EventArgs e)
        {
            OpenLogFolder();
        }

        private void ShowItemClick(object sender, EventArgs e)
        {
            _logger.Info("从托盘显示主窗口");
            _form.ShowFromTray();
        }

        private void TrayDoubleClick(object sender, EventArgs e)
        {
            _logger.Info("双击托盘显示主窗口");
            _form.ShowFromTray();
        }

        private void AutoStartItemClick(object sender, EventArgs e)
        {
            ToggleAutoStart();
        }

        private void LogItemClick(object sender, EventArgs e)
        {
            OpenLogFolder();
        }

        private void ExitItemClick(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private void ToggleAutoStart()
        {
            bool enable = !_autoStartEnabled;
            string error;
            if (enable)
            {
                if (!AutoStart.Enable(Application.ExecutablePath, _logger, out error))
                {
                    MessageBox.Show("设置开机自启失败：\r\n" + error, "LOVINGCOOL Mini",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                if (!AutoStart.Disable(_logger, out error))
                {
                    MessageBox.Show("关闭开机自启失败：\r\n" + error, "LOVINGCOOL Mini",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _autoStartEnabled = enable;
            _config.AutoStart = enable;
            _config.Save(_logger);
            _autoStartItem.Checked = enable;
            _form.UpdateAutoStart(enable);
            try
            {
                _tray.ShowBalloonTip(2000, "LOVINGCOOL Mini",
                    enable ? "已开启开机自启，登录 Windows 后会自动在托盘运行。" : "已关闭开机自启。",
                    ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                Process.Start("explorer.exe", "\"" + _logger.DirectoryPath + "\"");
            }
            catch (Exception ex)
            {
                _logger.Error("打开日志目录失败", ex);
                MessageBox.Show("打开日志目录失败：\r\n" + ex.Message, "LOVINGCOOL Mini",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExitApplication()
        {
            if (_exit) return;
            _exit = true;
            _logger.Info("用户退出程序");
            try { _sensor.Stop(); } catch { }
            try { _sensor.Dispose(); } catch { }
            try { _hid.Dispose(); } catch { }
            try
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            catch
            {
            }
            try
            {
                _form.AllowExit = true;
                _form.Close();
            }
            catch
            {
            }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _exit = true;
                try { _sensor.Dispose(); } catch { }
                try { _hid.Dispose(); } catch { }
                try { _tray.Dispose(); } catch { }
                try { _menu.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private sealed class DarkMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
            public override Color MenuItemSelected { get { return Theme.CardHover; } }
            public override Color MenuItemBorder { get { return Theme.Accent; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
            public override Color MenuBorder { get { return Theme.CardBorder; } }
            public override Color SeparatorDark { get { return Theme.CardBorder; } }
            public override Color SeparatorLight { get { return Theme.CardBorder; } }
        }
    }
}
