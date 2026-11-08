using System;
using System.Collections.Generic;
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
        private readonly ToolStripMenuItem _cpuItem;
        private readonly ToolStripMenuItem _gpuItem;
        private readonly SensorReader _sensor;
        private readonly HidDevice _hid;
        private readonly Thread _hidThread;
        private volatile bool _exit;
        private SensorSnapshot _snapshot;
        private bool _hidConnected;
        private bool _autoStartEnabled;
        private DateTime _lastHeartbeat;
        private DateTime _lastMemoryLog;
        private DateTime _lastTrim;
        private readonly DateTime _startTime;
        private bool _firstWriteLogged;
        private bool _steadyMemoryLogged;
        private string _lastWriteSource = string.Empty;
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
            _form.SourceChanged += FormSourceChanged;
            _form.GpuChanged += FormGpuChanged;
            _form.SetSelection(_config.TemperatureSource, _config.GpuId);

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

            _cpuItem = new ToolStripMenuItem("显示 CPU 温度");
            _cpuItem.ForeColor = Theme.Text;
            _cpuItem.Click += CpuItemClick;
            _menu.Items.Add(_cpuItem);

            _gpuItem = new ToolStripMenuItem("显示 GPU 温度");
            _gpuItem.ForeColor = Theme.Text;
            _gpuItem.Click += GpuItemClick;
            _menu.Items.Add(_gpuItem);

            ToolStripMenuItem chooseGpuItem = new ToolStripMenuItem("选择显卡...");
            chooseGpuItem.ForeColor = Theme.Text;
            chooseGpuItem.Click += ShowItemClick;
            _menu.Items.Add(chooseGpuItem);
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
            _sensor.SnapshotAvailable += SensorSnapshotAvailable;
            _sensor.StatusChanged += SensorStatusChanged;

            _hid = new HidDevice(_logger);
            _hid.StatusChanged += HidStatusChanged;

            _hidThread = new Thread(HidLoop);
            _hidThread.IsBackground = true;
            _hidThread.Name = "HidWriter";
            _hidThread.Start();

            ApplyAutoStartOnStartup();
            UpdateSourceChecks();
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

        private void SensorSnapshotAvailable(object sender, SensorSnapshotEventArgs e)
        {
            _snapshot = e.Snapshot;
            EnsureSelectedGpu();

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
            if (!_steadyMemoryLogged && (DateTime.Now - _startTime).TotalSeconds >= 30)
            {
                _steadyMemoryLogged = true;
                try
                {
                    using (Process p = Process.GetCurrentProcess())
                    {
                        _logger.Info("稳定内存: WorkingSet=" + (p.WorkingSet64 / 1024 / 1024) +
                                     "MB, Private=" + (p.PrivateMemorySize64 / 1024 / 1024) + "MB");
                    }
                }
                catch
                {
                }
            }

            _form.UpdateSnapshot(e.Snapshot, _config.GpuId);
            bool anyTemperature = e.Snapshot.CpuAvailable;
            foreach (GpuReading gpu in e.Snapshot.Gpus)
            {
                if (gpu.HasTemperature)
                {
                    anyTemperature = true;
                    break;
                }
            }
            _form.UpdateSensorStatus(anyTemperature,
                anyTemperature ? "传感器已启动" : "暂时读不到温度，正在重试...");
            UpdateSourceChecks();
            UpdateTrayText();
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

        private void EnsureSelectedGpu()
        {
            if (_snapshot == null || _snapshot.Gpus.Count == 0) return;
            GpuReading selected = FindGpu(_config.GpuId);
            if (selected == null)
            {
                selected = ChooseDefaultGpu(_snapshot.Gpus);
            }
            if (selected != null && _config.GpuId != selected.Id)
            {
                _config.GpuId = selected.Id;
                _config.Save(_logger);
                _logger.Info("默认显卡: " + selected.Vendor + " | " + selected.Name + " | id=" + selected.Id +
                             " | sensor=" + selected.SensorName +
                             " | temp=" + (selected.HasTemperature ? selected.Temperature.ToString("F2") : "N/A"));
                _form.SetSelection(_config.TemperatureSource, _config.GpuId);
            }
        }

        private GpuReading FindGpu(string gpuId)
        {
            if (_snapshot == null || string.IsNullOrEmpty(gpuId)) return null;
            foreach (GpuReading gpu in _snapshot.Gpus)
            {
                if (gpu.Id == gpuId) return gpu;
            }
            return null;
        }

        private static GpuReading ChooseDefaultGpu(List<GpuReading> gpus)
        {
            GpuReading best = null;
            foreach (GpuReading gpu in gpus)
            {
                if (!gpu.IsIntegrated && gpu.HasTemperature &&
                    gpu.SensorName != null && gpu.SensorName.ToLowerInvariant().Contains("gpu core"))
                {
                    return gpu;
                }
            }
            foreach (GpuReading gpu in gpus)
            {
                if (!gpu.IsIntegrated && gpu.HasTemperature) return gpu;
            }
            foreach (GpuReading gpu in gpus)
            {
                if (gpu.HasTemperature) return gpu;
            }
            foreach (GpuReading gpu in gpus)
            {
                if (best == null) best = gpu;
            }
            return best;
        }

        private void UpdateSourceChecks()
        {
            bool cpu = _config.TemperatureSource != "GPU";
            _cpuItem.Checked = cpu;
            _gpuItem.Checked = !cpu;
        }

        private void UpdateTrayText()
        {
            try
            {
                if (_config.TemperatureSource == "GPU")
                {
                    GpuReading gpu = FindGpu(_config.GpuId);
                    if (gpu == null || !gpu.HasTemperature)
                    {
                        _tray.Text = "LOVINGCOOL Mini · GPU 温度不可用";
                    }
                    else
                    {
                        _tray.Text = Truncate("LOVINGCOOL Mini · GPU " + gpu.Temperature.ToString("F1") + "°C", 63);
                    }
                }
                else
                {
                    if (_snapshot == null || !_snapshot.CpuAvailable)
                    {
                        _tray.Text = "LOVINGCOOL Mini · CPU 温度不可用";
                    }
                    else
                    {
                        _tray.Text = Truncate("LOVINGCOOL Mini · CPU " + _snapshot.CpuTemperature.ToString("F1") + "°C", 63);
                    }
                }
            }
            catch
            {
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

                    SensorSnapshot snapshot = _snapshot;
                    if (snapshot == null || (DateTime.Now - snapshot.Time).TotalSeconds > 5)
                    {
                        Thread.Sleep(300);
                        continue;
                    }

                    int temperature;
                    string sourceLabel;
                    if (!TryGetSelectedTemperature(snapshot, out temperature, out sourceLabel))
                    {
                        _lastSentTemp = int.MinValue;
                        Thread.Sleep(300);
                        continue;
                    }

                    bool due = temperature != _lastSentTemp ||
                               (DateTime.Now - _lastHeartbeat).TotalSeconds >= _config.HeartbeatSeconds;
                    if (due)
                    {
                        if (_hid.WriteTemperature(temperature))
                        {
                            if (!_firstWriteLogged)
                            {
                                _firstWriteLogged = true;
                                _logger.Info("首次写入面板: " + sourceLabel + " " + temperature + "°C");
                            }
                            if (_lastWriteSource != sourceLabel)
                            {
                                _lastWriteSource = sourceLabel;
                                _logger.Info("面板写入来源切换为 " + sourceLabel + ": " + temperature + "°C");
                            }
                            _lastSentTemp = temperature;
                            _lastHeartbeat = DateTime.Now;
                            _form.UpdateHidStatus(true, "面板已连接 · " + sourceLabel + " " + temperature + "°C");
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

        private bool TryGetSelectedTemperature(SensorSnapshot snapshot, out int temperature, out string sourceLabel)
        {
            if (_config.TemperatureSource == "GPU")
            {
                GpuReading gpu = FindGpu(_config.GpuId);
                if (gpu == null || !gpu.HasTemperature)
                {
                    temperature = 0;
                    sourceLabel = "GPU";
                    return false;
                }
                temperature = (int)Math.Round(gpu.Temperature, MidpointRounding.AwayFromZero);
                sourceLabel = "GPU";
                return true;
            }

            if (!snapshot.CpuAvailable)
            {
                temperature = 0;
                sourceLabel = "CPU";
                return false;
            }
            temperature = (int)Math.Round(snapshot.CpuTemperature, MidpointRounding.AwayFromZero);
            sourceLabel = "CPU";
            return true;
        }

        private void FormSourceChanged(object sender, EventArgs e)
        {
            _config.TemperatureSource = _form.SelectedSource;
            _config.GpuId = _form.SelectedGpuId;
            _config.Save(_logger);
            _logger.Info("温度来源切换: " + _config.TemperatureSource +
                         (_config.TemperatureSource == "GPU" ? " (" + _config.GpuId + ")" : string.Empty));
            _lastSentTemp = int.MinValue;
            _lastHeartbeat = DateTime.MinValue;
            UpdateSourceChecks();
            UpdateTrayText();
        }

        private void FormGpuChanged(object sender, EventArgs e)
        {
            _config.GpuId = _form.SelectedGpuId;
            _config.Save(_logger);
            GpuReading gpu = FindGpu(_config.GpuId);
            _logger.Info("显卡选择: " + (gpu == null ? _config.GpuId : gpu.Name + " | " + gpu.Id));
            _lastSentTemp = int.MinValue;
            _lastHeartbeat = DateTime.MinValue;
            UpdateTrayText();
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

        private void CpuItemClick(object sender, EventArgs e)
        {
            SetSource("CPU");
        }

        private void GpuItemClick(object sender, EventArgs e)
        {
            SetSource("GPU");
        }

        private void SetSource(string source)
        {
            if (_config.TemperatureSource == source) return;
            _config.TemperatureSource = source;
            EnsureSelectedGpu();
            _config.Save(_logger);
            _logger.Info("托盘切换温度来源: " + source);
            _form.SetSelection(_config.TemperatureSource, _config.GpuId);
            _lastSentTemp = int.MinValue;
            _lastHeartbeat = DateTime.MinValue;
            UpdateSourceChecks();
            UpdateTrayText();
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
