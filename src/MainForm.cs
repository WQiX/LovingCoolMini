using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LovingCoolMini
{
    internal sealed class CardPanel : Panel
    {
        public Color BorderColor { get; set; }
        public int CornerRadius { get; set; }

        public CardPanel()
        {
            BorderColor = Theme.CardBorder;
            CornerRadius = 12;
            BackColor = Theme.Card;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(rect, CornerRadius))
            using (SolidBrush brush = new SolidBrush(BackColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
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

    internal sealed class StatusDot : Control
    {
        private Color _color;

        public StatusDot()
        {
            _color = Theme.Amber;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(12, 12);
        }

        public Color Color
        {
            get { return _color; }
            set { _color = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(_color))
            {
                e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            }
            using (Pen pen = new Pen(Color.FromArgb(80, 255, 255, 255)))
            {
                e.Graphics.DrawEllipse(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    public sealed class MainForm : Form
    {
        private readonly AppConfig _config;
        private readonly Logger _logger;
        private readonly Label _captionLabel;
        private readonly Label _tempLabel;
        private readonly Label _unitLabel;
        private readonly Label _sensorLabel;
        private readonly Label _panelLabel;
        private readonly Label _statusLabel;
        private readonly Label _detailLabel;
        private readonly Label _gpuLabel;
        private readonly StatusDot _dot;
        private readonly Button _autoStartButton;
        private readonly ComboBox _sourceCombo;
        private readonly ComboBox _gpuCombo;
        private bool _allowExit;
        private bool _sensorOk;
        private bool _hidConnected;
        private bool _updatingCombos;
        private string _sensorMessage;
        private string _hidMessage;
        private string _selectedSource;
        private string _selectedGpuId;
        private string _gpuSignature;
        private string _selectionProblem;
        private double _selectedTemperature;
        private SensorSnapshot _snapshot;
        private DateTime _lastReadingTime;

        public MainForm(AppConfig config, Logger logger)
        {
            _config = config;
            _logger = logger;
            _sensorOk = false;
            _hidConnected = false;
            _sensorMessage = "正在初始化传感器...";
            _hidMessage = "正在连接面板...";
            _selectedSource = config.TemperatureSource == "GPU" ? "GPU" : "CPU";
            _selectedGpuId = config.GpuId ?? string.Empty;
            _gpuSignature = string.Empty;
            _updatingCombos = true;

            Text = "LOVINGCOOL Mini";
            ClientSize = new Size(480, 460);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Font(9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            DoubleBuffered = true;
            Icon = IconFactory.AppIcon;

            Label title = new Label();
            title.Text = "LOVINGCOOL Mini";
            title.Font = Theme.Font(17f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.AutoSize = true;
            title.BackColor = Color.Transparent;
            title.Location = new Point(22, 18);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "CPU / GPU 温度数显控制 · 独立版";
            subtitle.Font = Theme.Font(9f);
            subtitle.ForeColor = Theme.TextSecondary;
            subtitle.AutoSize = true;
            subtitle.BackColor = Color.Transparent;
            subtitle.Location = new Point(24, 50);
            Controls.Add(subtitle);

            Label version = new Label();
            version.Text = "v1.1.0";
            version.Font = Theme.Font(8.5f);
            version.ForeColor = Theme.Muted;
            version.AutoSize = false;
            version.TextAlign = ContentAlignment.MiddleRight;
            version.BackColor = Color.Transparent;
            version.Location = new Point(370, 22);
            version.Size = new Size(88, 22);
            Controls.Add(version);

            CardPanel tempCard = new CardPanel();
            tempCard.Location = new Point(20, 82);
            tempCard.Size = new Size(440, 190);
            Controls.Add(tempCard);

            _captionLabel = new Label();
            _captionLabel.Text = "CPU 温度";
            _captionLabel.Font = Theme.Font(9.5f);
            _captionLabel.ForeColor = Theme.TextSecondary;
            _captionLabel.AutoSize = true;
            _captionLabel.BackColor = Color.Transparent;
            _captionLabel.Location = new Point(20, 14);
            tempCard.Controls.Add(_captionLabel);

            Label sourceCaption = new Label();
            sourceCaption.Text = "显示";
            sourceCaption.Font = Theme.Font(8.5f);
            sourceCaption.ForeColor = Theme.TextSecondary;
            sourceCaption.AutoSize = false;
            sourceCaption.TextAlign = ContentAlignment.MiddleRight;
            sourceCaption.BackColor = Color.Transparent;
            sourceCaption.Location = new Point(250, 12);
            sourceCaption.Size = new Size(42, 24);
            tempCard.Controls.Add(sourceCaption);

            _sourceCombo = new ComboBox();
            _sourceCombo.Location = new Point(298, 10);
            _sourceCombo.Size = new Size(118, 26);
            _sourceCombo.Items.Add("CPU");
            _sourceCombo.Items.Add("GPU");
            _sourceCombo.SelectedIndex = _selectedSource == "GPU" ? 1 : 0;
            StyleCombo(_sourceCombo);
            _sourceCombo.SelectedIndexChanged += SourceComboChanged;
            tempCard.Controls.Add(_sourceCombo);

            _panelLabel = new Label();
            _panelLabel.Text = "面板 --°C";
            _panelLabel.Font = Theme.Font(9f, FontStyle.Bold);
            _panelLabel.ForeColor = Theme.AccentLight;
            _panelLabel.AutoSize = false;
            _panelLabel.TextAlign = ContentAlignment.MiddleRight;
            _panelLabel.BackColor = Color.Transparent;
            _panelLabel.Location = new Point(300, 44);
            _panelLabel.Size = new Size(116, 22);
            tempCard.Controls.Add(_panelLabel);

            _tempLabel = new Label();
            _tempLabel.Text = "--.-";
            _tempLabel.Font = Theme.Font(50f, FontStyle.Bold);
            _tempLabel.ForeColor = Theme.Text;
            _tempLabel.AutoSize = false;
            _tempLabel.TextAlign = ContentAlignment.MiddleLeft;
            _tempLabel.BackColor = Color.Transparent;
            _tempLabel.Location = new Point(14, 38);
            _tempLabel.Size = new Size(230, 82);
            tempCard.Controls.Add(_tempLabel);

            _unitLabel = new Label();
            _unitLabel.Text = "°C";
            _unitLabel.Font = Theme.Font(18f, FontStyle.Bold);
            _unitLabel.ForeColor = Theme.AccentLight;
            _unitLabel.AutoSize = false;
            _unitLabel.TextAlign = ContentAlignment.MiddleLeft;
            _unitLabel.BackColor = Color.Transparent;
            _unitLabel.Location = new Point(218, 68);
            _unitLabel.Size = new Size(54, 36);
            tempCard.Controls.Add(_unitLabel);

            _sensorLabel = new Label();
            _sensorLabel.Text = "正在读取传感器...";
            _sensorLabel.Font = Theme.Font(8.5f);
            _sensorLabel.ForeColor = Theme.TextSecondary;
            _sensorLabel.AutoSize = false;
            _sensorLabel.TextAlign = ContentAlignment.MiddleLeft;
            _sensorLabel.BackColor = Color.Transparent;
            _sensorLabel.Location = new Point(20, 120);
            _sensorLabel.Size = new Size(400, 20);
            tempCard.Controls.Add(_sensorLabel);

            _gpuLabel = new Label();
            _gpuLabel.Text = "显卡";
            _gpuLabel.Font = Theme.Font(8.5f);
            _gpuLabel.ForeColor = Theme.TextSecondary;
            _gpuLabel.AutoSize = false;
            _gpuLabel.TextAlign = ContentAlignment.MiddleLeft;
            _gpuLabel.BackColor = Color.Transparent;
            _gpuLabel.Location = new Point(20, 154);
            _gpuLabel.Size = new Size(40, 24);
            _gpuLabel.Visible = false;
            tempCard.Controls.Add(_gpuLabel);

            _gpuCombo = new ComboBox();
            _gpuCombo.Location = new Point(62, 150);
            _gpuCombo.Size = new Size(354, 26);
            _gpuCombo.Items.Add(new GpuChoice(string.Empty, "正在检测显卡..."));
            _gpuCombo.SelectedIndex = 0;
            _gpuCombo.Enabled = false;
            StyleCombo(_gpuCombo);
            _gpuCombo.SelectedIndexChanged += GpuComboChanged;
            _gpuCombo.Visible = false;
            tempCard.Controls.Add(_gpuCombo);

            CardPanel statusCard = new CardPanel();
            statusCard.Location = new Point(20, 286);
            statusCard.Size = new Size(440, 74);
            Controls.Add(statusCard);

            _dot = new StatusDot();
            _dot.Location = new Point(18, 31);
            statusCard.Controls.Add(_dot);

            _statusLabel = new Label();
            _statusLabel.Text = "正在启动...";
            _statusLabel.Font = Theme.Font(10f, FontStyle.Bold);
            _statusLabel.ForeColor = Theme.Text;
            _statusLabel.AutoSize = false;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.BackColor = Color.Transparent;
            _statusLabel.Location = new Point(40, 14);
            _statusLabel.Size = new Size(380, 22);
            statusCard.Controls.Add(_statusLabel);

            _detailLabel = new Label();
            _detailLabel.Text = "正在初始化传感器...";
            _detailLabel.Font = Theme.Font(8.5f);
            _detailLabel.ForeColor = Theme.TextSecondary;
            _detailLabel.AutoSize = false;
            _detailLabel.TextAlign = ContentAlignment.MiddleLeft;
            _detailLabel.BackColor = Color.Transparent;
            _detailLabel.Location = new Point(40, 40);
            _detailLabel.Size = new Size(380, 20);
            statusCard.Controls.Add(_detailLabel);

            _autoStartButton = new Button();
            _autoStartButton.Location = new Point(20, 374);
            _autoStartButton.Size = new Size(160, 38);
            _autoStartButton.Text = "开机自启：关";
            _autoStartButton.Click += AutoStartButtonClick;
            StyleButton(_autoStartButton, false);
            Controls.Add(_autoStartButton);

            Button hideButton = new Button();
            hideButton.Location = new Point(192, 374);
            hideButton.Size = new Size(130, 38);
            hideButton.Text = "最小化到托盘";
            hideButton.Click += HideButtonClick;
            StyleButton(hideButton, false);
            Controls.Add(hideButton);

            Button logButton = new Button();
            logButton.Location = new Point(334, 374);
            logButton.Size = new Size(106, 38);
            logButton.Text = "打开日志";
            logButton.Click += LogButtonClick;
            StyleButton(logButton, false);
            Controls.Add(logButton);

            Label footer = new Label();
            footer.Text = "关闭窗口不会退出，程序会继续在托盘刷新面板。";
            footer.Font = Theme.Font(8f);
            footer.ForeColor = Theme.Muted;
            footer.AutoSize = false;
            footer.TextAlign = ContentAlignment.MiddleLeft;
            footer.BackColor = Color.Transparent;
            footer.Location = new Point(22, 424);
            footer.Size = new Size(440, 18);
            Controls.Add(footer);

            _updatingCombos = false;
            FormClosing += MainFormClosing;
            Shown += MainFormShown;
            UpdateAutoStart(config.AutoStart);
            UpdateGpuVisibility();
            RefreshStatus();
        }

        public event EventHandler AutoStartClicked;
        public event EventHandler HideToTrayClicked;
        public event EventHandler OpenLogClicked;
        public event EventHandler SourceChanged;
        public event EventHandler GpuChanged;

        public bool AllowExit
        {
            get { return _allowExit; }
            set { _allowExit = value; }
        }

        public string SelectedSource
        {
            get { return _selectedSource; }
        }

        public string SelectedGpuId
        {
            get { return _selectedGpuId; }
        }

        public void ShowFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Activate();
            BringToFront();
        }

        public void SetSelection(string source, string gpuId)
        {
            _updatingCombos = true;
            _selectedSource = source == "GPU" ? "GPU" : "CPU";
            _selectedGpuId = gpuId ?? string.Empty;
            _sourceCombo.SelectedIndex = _selectedSource == "GPU" ? 1 : 0;
            SelectGpuItem(_selectedGpuId);
            _updatingCombos = false;
            UpdateGpuVisibility();
            UpdateSelectionDisplay();
            RefreshStatus();
        }

        public void UpdateSnapshot(SensorSnapshot snapshot, string selectedGpuId)
        {
            if (snapshot == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<SensorSnapshot, string>(UpdateSnapshot), snapshot, selectedGpuId);
                return;
            }

            _snapshot = snapshot;
            _lastReadingTime = snapshot.Time;
            if (!string.IsNullOrEmpty(selectedGpuId))
            {
                _selectedGpuId = selectedGpuId;
            }
            RebuildGpuCombo(snapshot.Gpus);
            UpdateSelectionDisplay();
            RefreshStatus();
        }

        public void UpdateHidStatus(bool connected, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool, string>(UpdateHidStatus), connected, message);
                return;
            }
            _hidConnected = connected;
            _hidMessage = message;
            RefreshStatus();
        }

        public void UpdateSensorStatus(bool ok, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool, string>(UpdateSensorStatus), ok, message);
                return;
            }
            _sensorOk = ok;
            _sensorMessage = message;
            RefreshStatus();
        }

        public void UpdateAutoStart(bool enabled)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(UpdateAutoStart), enabled);
                return;
            }
            _autoStartButton.Text = enabled ? "开机自启：开" : "开机自启：关";
            StyleButton(_autoStartButton, enabled);
        }

        public void ShowBalloon(string title, string text)
        {
            ShowFromTray();
        }

        private void RebuildGpuCombo(List<GpuReading> gpus)
        {
            if (gpus == null) gpus = new List<GpuReading>();
            string signature = string.Empty;
            foreach (GpuReading gpu in gpus)
            {
                signature += gpu.Id + "|" + gpu.Name + "|" + gpu.IsIntegrated + "|" + gpu.HasTemperature + ";";
            }
            if (signature == _gpuSignature) return;
            _gpuSignature = signature;

            _updatingCombos = true;
            _gpuCombo.Items.Clear();
            int selectedIndex = -1;
            for (int i = 0; i < gpus.Count; i++)
            {
                GpuReading gpu = gpus[i];
                string text = gpu.Name + (gpu.IsIntegrated ? "（核显）" : string.Empty) +
                              (gpu.HasTemperature ? string.Empty : "（无温度）");
                _gpuCombo.Items.Add(new GpuChoice(gpu.Id, text));
                if (gpu.Id == _selectedGpuId)
                {
                    selectedIndex = i;
                }
            }

            if (gpus.Count == 0)
            {
                _gpuCombo.Items.Add(new GpuChoice(string.Empty, "未检测到显卡"));
                _gpuCombo.Enabled = false;
                _gpuCombo.SelectedIndex = 0;
                _selectedGpuId = string.Empty;
            }
            else
            {
                _gpuCombo.Enabled = true;
                if (selectedIndex < 0)
                {
                    selectedIndex = 0;
                    _selectedGpuId = gpus[0].Id;
                }
                _gpuCombo.SelectedIndex = selectedIndex;
            }
            _updatingCombos = false;
            UpdateGpuVisibility();
        }

        private void SelectGpuItem(string gpuId)
        {
            if (_gpuCombo.Items.Count == 0) return;
            for (int i = 0; i < _gpuCombo.Items.Count; i++)
            {
                GpuChoice choice = _gpuCombo.Items[i] as GpuChoice;
                if (choice != null && choice.Id == gpuId)
                {
                    _gpuCombo.SelectedIndex = i;
                    return;
                }
            }
            _gpuCombo.SelectedIndex = 0;
        }

        private void UpdateSelectionDisplay()
        {
            _selectionProblem = null;
            _selectedTemperature = 0;

            if (_snapshot == null)
            {
                _captionLabel.Text = _selectedSource == "GPU" ? "GPU 温度" : "CPU 温度";
                _tempLabel.Text = "--.-";
                _tempLabel.ForeColor = Theme.Text;
                _panelLabel.Text = "面板 --°C";
                _sensorLabel.Text = "正在读取传感器...";
                _selectionProblem = "正在初始化温度传感器...";
                return;
            }

            if (_selectedSource == "GPU")
            {
                _captionLabel.Text = "GPU 温度";
                GpuReading gpu = FindGpu(_selectedGpuId);
                if (gpu == null)
                {
                    _tempLabel.Text = "--.-";
                    _tempLabel.ForeColor = Theme.Text;
                    _panelLabel.Text = "面板 --°C";
                    _sensorLabel.Text = "未检测到显卡温度";
                    _selectionProblem = "未检测到可用的显卡。请确认显卡驱动已安装，或切换回 CPU 温度。";
                    return;
                }

                if (!gpu.HasTemperature)
                {
                    _tempLabel.Text = "--.-";
                    _tempLabel.ForeColor = Theme.Text;
                    _panelLabel.Text = "面板 --°C";
                    _sensorLabel.Text = gpu.Name + " · 没有可用的温度传感器";
                    _selectionProblem = "已检测到 " + gpu.Name +
                                        "，但驱动没有提供温度传感器。请更新显卡驱动，或切换回 CPU 温度。";
                    return;
                }

                _selectedTemperature = gpu.Temperature;
                _tempLabel.Text = gpu.Temperature.ToString(_config.ShowDecimal ? "F1" : "F0");
                _tempLabel.ForeColor = TemperatureColor(gpu.Temperature);
                _sensorLabel.Text = gpu.Name + " · " + gpu.SensorName + " · " + _snapshot.Time.ToString("HH:mm:ss");
                _panelLabel.Text = "面板 " + ((int)Math.Round(gpu.Temperature, MidpointRounding.AwayFromZero)) + "°C";
                return;
            }

            _captionLabel.Text = "CPU 温度";
            if (!_snapshot.CpuAvailable)
            {
                _tempLabel.Text = "--.-";
                _tempLabel.ForeColor = Theme.Text;
                _panelLabel.Text = "面板 --°C";
                _sensorLabel.Text = "CPU 温度不可用";
                _selectionProblem = "未读到 CPU 温度。请确认程序以管理员身份运行，或切换温度来源。";
                return;
            }

            _selectedTemperature = _snapshot.CpuTemperature;
            _tempLabel.Text = _snapshot.CpuTemperature.ToString(_config.ShowDecimal ? "F1" : "F0");
            _tempLabel.ForeColor = TemperatureColor(_snapshot.CpuTemperature);
            _sensorLabel.Text = _snapshot.CpuSensorName + " · " + _snapshot.Time.ToString("HH:mm:ss");
            _panelLabel.Text = "面板 " + ((int)Math.Round(_snapshot.CpuTemperature, MidpointRounding.AwayFromZero)) + "°C";
        }

        private GpuReading FindGpu(string gpuId)
        {
            if (_snapshot == null) return null;
            foreach (GpuReading gpu in _snapshot.Gpus)
            {
                if (gpu.Id == gpuId) return gpu;
            }
            return _snapshot.Gpus.Count > 0 ? _snapshot.Gpus[0] : null;
        }

        private static Color TemperatureColor(double temperature)
        {
            if (temperature >= 85) return Theme.Red;
            if (temperature >= 70) return Theme.Amber;
            return Theme.Text;
        }

        private void RefreshStatus()
        {
            if (_selectionProblem != null)
            {
                _dot.Color = Theme.Red;
                _statusLabel.Text = "温度不可用";
                _detailLabel.Text = _selectionProblem;
                return;
            }
            if (!_sensorOk)
            {
                _dot.Color = Theme.Red;
                _statusLabel.Text = "传感器异常";
                _detailLabel.Text = _sensorMessage;
                return;
            }
            if (!_hidConnected)
            {
                _dot.Color = Theme.Amber;
                _statusLabel.Text = "等待面板";
                _detailLabel.Text = _hidMessage +
                    (_lastReadingTime == DateTime.MinValue ? "" : " · 最近 " + _lastReadingTime.ToString("HH:mm:ss"));
                return;
            }
            _dot.Color = Theme.Green;
            _statusLabel.Text = "运行中";
            _detailLabel.Text = "面板已连接 · " + _captionLabel.Text + " " +
                _selectedTemperature.ToString(_config.ShowDecimal ? "F1" : "F0") + "°C";
        }

        private void UpdateGpuVisibility()
        {
            bool visible = _selectedSource == "GPU";
            _gpuLabel.Visible = visible;
            _gpuCombo.Visible = visible;
        }

        private void SourceComboChanged(object sender, EventArgs e)
        {
            if (_updatingCombos) return;
            _selectedSource = _sourceCombo.SelectedIndex == 1 ? "GPU" : "CPU";
            UpdateGpuVisibility();
            UpdateSelectionDisplay();
            RefreshStatus();
            EventHandler handler = SourceChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void GpuComboChanged(object sender, EventArgs e)
        {
            if (_updatingCombos) return;
            GpuChoice choice = _gpuCombo.SelectedItem as GpuChoice;
            if (choice == null) return;
            _selectedGpuId = choice.Id;
            UpdateSelectionDisplay();
            RefreshStatus();
            EventHandler handler = GpuChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void StyleCombo(ComboBox combo)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Theme.ButtonDark;
            combo.ForeColor = Theme.Text;
            combo.Font = Theme.Font(9f);
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 24;
            combo.DrawItem += ComboDrawItem;
        }

        private void ComboDrawItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo == null) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush background = new SolidBrush(selected ? Theme.Accent : Theme.ButtonDark))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }
            if (e.Index >= 0)
            {
                string text = combo.Items[e.Index].ToString();
                TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void StyleButton(Button button, bool accent)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = accent ? 0 : 1;
            button.FlatAppearance.BorderColor = Theme.CardBorder;
            button.BackColor = accent ? Theme.Accent : Theme.ButtonDark;
            button.ForeColor = accent ? Color.White : Theme.Text;
            button.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(139, 92, 246) : Theme.CardHover;
            button.FlatAppearance.MouseDownBackColor = accent ? Theme.AccentDark : Color.FromArgb(24, 15, 34);
            button.Font = Theme.Font(9f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
        }

        private void AutoStartButtonClick(object sender, EventArgs e)
        {
            EventHandler handler = AutoStartClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void HideButtonClick(object sender, EventArgs e)
        {
            Hide();
            EventHandler handler = HideToTrayClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void LogButtonClick(object sender, EventArgs e)
        {
            EventHandler handler = OpenLogClicked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowExit) return;
            if (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing)
            {
                return;
            }
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                EventHandler handler = HideToTrayClicked;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private void MainFormShown(object sender, EventArgs e)
        {
            NativeMethods.UseImmersiveDarkMode(Handle, true);
            _logger.Info("主窗口已显示");
        }

        private sealed class GpuChoice
        {
            public readonly string Id;
            public readonly string Text;

            public GpuChoice(string id, string text)
            {
                Id = id;
                Text = text;
            }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
