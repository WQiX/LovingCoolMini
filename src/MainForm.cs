using System;
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
        private readonly Label _tempLabel;
        private readonly Label _unitLabel;
        private readonly Label _sensorLabel;
        private readonly Label _panelLabel;
        private readonly Label _statusLabel;
        private readonly Label _detailLabel;
        private readonly StatusDot _dot;
        private readonly Button _autoStartButton;
        private bool _allowExit;
        private bool _sensorOk;
        private bool _hidConnected;
        private string _sensorMessage;
        private string _hidMessage;
        private DateTime _lastReadingTime;

        public MainForm(AppConfig config, Logger logger)
        {
            _config = config;
            _logger = logger;
            _sensorOk = false;
            _hidConnected = false;
            _sensorMessage = "正在初始化传感器...";
            _hidMessage = "正在连接面板...";

            Text = "LOVINGCOOL Mini";
            ClientSize = new Size(460, 414);
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
            subtitle.Text = "CPU 温度数显控制 · 独立版";
            subtitle.Font = Theme.Font(9f);
            subtitle.ForeColor = Theme.TextSecondary;
            subtitle.AutoSize = true;
            subtitle.BackColor = Color.Transparent;
            subtitle.Location = new Point(24, 50);
            Controls.Add(subtitle);

            Label version = new Label();
            version.Text = "v1.0.0";
            version.Font = Theme.Font(8.5f);
            version.ForeColor = Theme.Muted;
            version.AutoSize = false;
            version.TextAlign = ContentAlignment.MiddleRight;
            version.BackColor = Color.Transparent;
            version.Location = new Point(350, 22);
            version.Size = new Size(88, 22);
            Controls.Add(version);

            CardPanel tempCard = new CardPanel();
            tempCard.Location = new Point(20, 82);
            tempCard.Size = new Size(420, 150);
            Controls.Add(tempCard);

            Label tempCaption = new Label();
            tempCaption.Text = "CPU 温度";
            tempCaption.Font = Theme.Font(9.5f);
            tempCaption.ForeColor = Theme.TextSecondary;
            tempCaption.AutoSize = true;
            tempCaption.BackColor = Color.Transparent;
            tempCaption.Location = new Point(20, 16);
            tempCard.Controls.Add(tempCaption);

            _panelLabel = new Label();
            _panelLabel.Text = "面板 --°C";
            _panelLabel.Font = Theme.Font(9f, FontStyle.Bold);
            _panelLabel.ForeColor = Theme.AccentLight;
            _panelLabel.AutoSize = false;
            _panelLabel.TextAlign = ContentAlignment.MiddleRight;
            _panelLabel.BackColor = Color.Transparent;
            _panelLabel.Location = new Point(290, 13);
            _panelLabel.Size = new Size(110, 22);
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
            _sensorLabel.Text = "正在读取 Core (Tctl/Tdie)...";
            _sensorLabel.Font = Theme.Font(8.5f);
            _sensorLabel.ForeColor = Theme.TextSecondary;
            _sensorLabel.AutoSize = false;
            _sensorLabel.TextAlign = ContentAlignment.MiddleLeft;
            _sensorLabel.BackColor = Color.Transparent;
            _sensorLabel.Location = new Point(20, 120);
            _sensorLabel.Size = new Size(380, 20);
            tempCard.Controls.Add(_sensorLabel);

            CardPanel statusCard = new CardPanel();
            statusCard.Location = new Point(20, 246);
            statusCard.Size = new Size(420, 70);
            Controls.Add(statusCard);

            _dot = new StatusDot();
            _dot.Location = new Point(18, 29);
            statusCard.Controls.Add(_dot);

            _statusLabel = new Label();
            _statusLabel.Text = "正在启动...";
            _statusLabel.Font = Theme.Font(10f, FontStyle.Bold);
            _statusLabel.ForeColor = Theme.Text;
            _statusLabel.AutoSize = false;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.BackColor = Color.Transparent;
            _statusLabel.Location = new Point(40, 13);
            _statusLabel.Size = new Size(360, 22);
            statusCard.Controls.Add(_statusLabel);

            _detailLabel = new Label();
            _detailLabel.Text = "正在初始化传感器...";
            _detailLabel.Font = Theme.Font(8.5f);
            _detailLabel.ForeColor = Theme.TextSecondary;
            _detailLabel.AutoSize = false;
            _detailLabel.TextAlign = ContentAlignment.MiddleLeft;
            _detailLabel.BackColor = Color.Transparent;
            _detailLabel.Location = new Point(40, 37);
            _detailLabel.Size = new Size(360, 20);
            statusCard.Controls.Add(_detailLabel);

            _autoStartButton = new Button();
            _autoStartButton.Location = new Point(20, 330);
            _autoStartButton.Size = new Size(160, 38);
            _autoStartButton.Text = "开机自启：关";
            _autoStartButton.Click += AutoStartButtonClick;
            StyleButton(_autoStartButton, false);
            Controls.Add(_autoStartButton);

            Button hideButton = new Button();
            hideButton.Location = new Point(192, 330);
            hideButton.Size = new Size(130, 38);
            hideButton.Text = "最小化到托盘";
            hideButton.Click += HideButtonClick;
            StyleButton(hideButton, false);
            Controls.Add(hideButton);

            Button logButton = new Button();
            logButton.Location = new Point(334, 330);
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
            footer.Location = new Point(22, 380);
            footer.Size = new Size(420, 18);
            Controls.Add(footer);

            FormClosing += MainFormClosing;
            Shown += MainFormShown;
            UpdateAutoStart(config.AutoStart);
            RefreshStatus();
        }

        public event EventHandler AutoStartClicked;
        public event EventHandler HideToTrayClicked;
        public event EventHandler OpenLogClicked;

        public bool AllowExit
        {
            get { return _allowExit; }
            set { _allowExit = value; }
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

        public void UpdateReading(SensorReading reading)
        {
            if (reading == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<SensorReading>(UpdateReading), reading);
                return;
            }

            _lastReadingTime = reading.Time;
            string format = _config.ShowDecimal ? "F1" : "F0";
            _tempLabel.Text = reading.Tctl.ToString(format);
            _sensorLabel.Text = reading.SensorName + " · " + reading.Time.ToString("HH:mm:ss");
            int panelTemp = (int)Math.Round(reading.Tctl, MidpointRounding.AwayFromZero);
            _panelLabel.Text = "面板 " + panelTemp + "°C";
            if (reading.Tctl >= 85)
            {
                _tempLabel.ForeColor = Theme.Red;
            }
            else if (reading.Tctl >= 70)
            {
                _tempLabel.ForeColor = Theme.Amber;
            }
            else
            {
                _tempLabel.ForeColor = Theme.Text;
            }
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
            // The tray icon is owned by TrayApplicationContext; this method is kept
            // for callers that want to raise the window attention.
            ShowFromTray();
        }

        private void RefreshStatus()
        {
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
                _detailLabel.Text = _hidMessage + (_lastReadingTime == DateTime.MinValue ? "" : " · 最近 " + _lastReadingTime.ToString("HH:mm:ss"));
                return;
            }
            _dot.Color = Theme.Green;
            _statusLabel.Text = "运行中";
            _detailLabel.Text = "面板已连接 · " + _sensorMessage;
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
    }
}
