using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Xm5ControlUi
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                Log("Starting");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => ReportException(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportException(e.ExceptionObject);
                Application.Run(new MainForm());
                Log("Exited");
            }
            catch (Exception ex)
            {
                ReportException(ex);
            }
        }

        private static void ReportException(object exception)
        {
            string text = exception == null ? "Unknown error" : exception.ToString();
            Log(text);
            MessageBox.Show(text, "Sony Headphones Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void Log(string message)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xm5ui-startup.log");
                File.AppendAllText(path, DateTime.Now.ToString("s") + " " + message + Environment.NewLine + Environment.NewLine);
            }
            catch
            {
            }
        }
    }

    internal sealed class DeviceProfile
    {
        private readonly string[] aliases;

        public string DisplayName { get; private set; }
        public string NameFilter { get; private set; }
        public string AssetPath { get; private set; }

        public DeviceProfile(string displayName, string nameFilter, string assetPath, params string[] aliases)
        {
            DisplayName = displayName;
            NameFilter = nameFilter;
            AssetPath = assetPath;
            this.aliases = aliases ?? new string[0];
        }

        public bool MatchesText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.IndexOf(NameFilter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (int i = 0; i < aliases.Length; i++)
            {
                if (text.IndexOf(aliases[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal sealed class DeviceDetection
    {
        public DeviceProfile Profile { get; private set; }
        public bool Connected { get; private set; }
        public bool Remembered { get; private set; }
        public int Score { get; private set; }

        public DeviceDetection(DeviceProfile profile, bool connected, bool remembered, int score)
        {
            Profile = profile;
            Connected = connected;
            Remembered = remembered;
            Score = score;
        }
    }

    internal sealed class ShortcutAction
    {
        public string Id { get; private set; }
        public string Label { get; private set; }

        public ShortcutAction(string id, string label)
        {
            Id = id;
            Label = label;
        }
    }

    internal sealed class ShortcutBinding
    {
        public bool Ctrl { get; private set; }
        public bool Alt { get; private set; }
        public bool Shift { get; private set; }
        public bool Win { get; private set; }
        public Keys Key { get; private set; }

        public ShortcutBinding(bool ctrl, bool alt, bool shift, bool win, Keys key)
        {
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
            Win = win;
            Key = key;
        }

        public bool IsAssigned
        {
            get { return Key != Keys.None; }
        }

        public bool IsGlobalSafe
        {
            get { return IsAssigned && (Ctrl || Alt || Shift || Win || IsFunctionKey(Key)); }
        }

        public uint Modifiers
        {
            get
            {
                uint modifiers = 0x4000; // MOD_NOREPEAT
                if (Alt) modifiers |= 0x0001;
                if (Ctrl) modifiers |= 0x0002;
                if (Shift) modifiers |= 0x0004;
                if (Win) modifiers |= 0x0008;
                return modifiers;
            }
        }

        public string Signature
        {
            get { return ToConfigString().ToUpperInvariant(); }
        }

        public static ShortcutBinding None()
        {
            return new ShortcutBinding(false, false, false, false, Keys.None);
        }

        public ShortcutBinding Clone()
        {
            return new ShortcutBinding(Ctrl, Alt, Shift, Win, Key);
        }

        public override string ToString()
        {
            if (!IsAssigned) return "None";
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(new KeysConverter().ConvertToString(Key));
            return string.Join(" + ", parts.ToArray());
        }

        public string ToConfigString()
        {
            if (!IsAssigned) return "None";
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(Key.ToString());
            return string.Join("+", parts.ToArray());
        }

        public static ShortcutBinding Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return None();
            }

            bool ctrl = false;
            bool alt = false;
            bool shift = false;
            bool win = false;
            Keys key = Keys.None;
            string[] parts = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl = true;
                }
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    alt = true;
                }
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    shift = true;
                }
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                {
                    win = true;
                }
                else
                {
                    Keys parsed;
                    if (Enum.TryParse(part, true, out parsed)) key = parsed;
                    else
                    {
                        try
                        {
                            object converted = new KeysConverter().ConvertFromString(part);
                            if (converted is Keys) key = (Keys)converted;
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return key == Keys.None ? None() : new ShortcutBinding(ctrl, alt, shift, win, key);
        }

        public static bool IsFunctionKey(Keys key)
        {
            return key >= Keys.F1 && key <= Keys.F24;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Color page = Color.FromArgb(18, 18, 18);
        private readonly Color card = Color.FromArgb(31, 31, 31);
        private readonly Color cardSoft = Color.FromArgb(43, 43, 43);
        private readonly Color line = Color.FromArgb(58, 58, 58);
        private readonly Color ink = Color.FromArgb(245, 245, 245);
        private readonly Color subdued = Color.FromArgb(169, 169, 169);
        private readonly Color blue = Color.FromArgb(0x10, 0x9d, 0xf5);
        private readonly Color bluePressed = Color.FromArgb(0x0c, 0x86, 0xd4);
        private readonly Color green = Color.FromArgb(0, 166, 125);
        private readonly Color red = Color.FromArgb(255, 88, 88);
        private readonly Color amber = Color.FromArgb(214, 150, 45);

        private const int CardInset = 36;
        private const int HeroModeOpticalOffset = 0;
        private const int AutoDetectIntervalMs = 4000;
        private const int AutoStateRefreshIntervalMs = 15000;
        private const int LiveEqDebounceMs = 260;
        private const string StateBatchCommand = "batch \"22 00;66 17;D6 D1;D6 D2;52 00;56 00;5A 00;E6 01;E6 00;F6 02;F6 01;26 05\" --timeout 1800";

        private readonly string backendPath;
        private readonly DeviceProfile[] profiles;
        private readonly ShortcutAction[] shortcutActions;
        private readonly Dictionary<string, ShortcutBinding> shortcutBindings = new Dictionary<string, ShortcutBinding>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, ShortcutAction> registeredShortcuts = new Dictionary<int, ShortcutAction>();
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly Icon windowIcon;
        private readonly Icon notificationIcon;
        private readonly SemaphoreSlim commandGate = new SemaphoreSlim(1, 1);
        private readonly System.Windows.Forms.Timer autoDetectTimer;
        private readonly System.Windows.Forms.Timer liveEqTimer;

        private DeviceProfile currentProfile;
        private DeviceProfile lastStateRefreshProfile;
        private DateTime lastStateRefreshAt = DateTime.MinValue;
        private bool exiting;
        private bool minimizeToTray = true;
        private bool hasShownOnce;
        private bool trayNotifications = true;
        private bool trayCleanupStarted;
        private bool commandBusy;
        private bool autoDetectRunning;
        private bool lastStateRefreshConnected;

        private Label titleLabel;
        private Label statusLabel;
        private Label connectionLabel;
        private Label batteryLabel;
        private Label multipointLabel;
        private Label lastActionLabel;
        private Label bigModeLabel;
        private Label bigDetailLabel;
        private Label levelLabel;
        private Label equalizerLabel;
        private Label eqCardSummaryLabel;
        private Label dseeLabel;
        private Label connectionQualityLabel;
        private Label speakToChatLabel;
        private Label wearPauseLabel;
        private Label touchPanelLabel;
        private Label autoPowerLabel;
        private PillButton multipointOnButton;
        private PillButton multipointOffButton;
        private PillButton soundQualityButton;
        private PillButton stableButton;
        private PillButton dseeAutoButton;
        private PillButton dseeOffButton;
        private PillButton speakOnButton;
        private PillButton speakOffButton;
        private PillButton pauseOnButton;
        private PillButton pauseOffButton;
        private PillButton touchOnButton;
        private PillButton touchOffButton;
        private PillButton autoPowerRemovedButton;
        private PillButton autoPowerDisableButton;
        private PillButton configureShortcutsButton;
        private SliderControl levelSlider;
        private ChoiceDropdown ambientKindBox;
        private PillButton ancButton;
        private PillButton ambientButton;
        private PillButton offButton;
        private ChoiceDropdown eqPresetBox;
        private SliderControl[] eqSliders;
        private Label[] eqValueLabels;
        private EqCurveControl eqCurve;
        private PictureBox heroImageBox;
        private int currentEqPreset = 0xA0;
        private bool updatingEqUi;
        private bool liveEqSendRunning;
        private bool liveEqSendPending;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int WmHotKey = 0x0312;
        private const int ShortcutHotkeyBaseId = 0x5300;

        public MainForm()
        {
            backendPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "c", "xm5ctl.exe"));
            profiles = new[]
            {
                new DeviceProfile("WH-1000XM5", "WH-1000XM5", @"assets\wh1000xm5.png", "LE_WH-1000XM5", "WH1000XM5"),
                new DeviceProfile("WF-1000XM5", "WF-1000XM5", @"assets\wf1000xm5.png", "LE_WF-1000XM5", "WF1000XM5"),
                new DeviceProfile("WH-1000XM6", "WH-1000XM6", @"assets\wh1000xm6.png", "LE_WH-1000XM6", "WH1000XM6"),
                new DeviceProfile("WF-1000XM6", "WF-1000XM6", @"assets\wf1000xm6.png", "LE_WF-1000XM6", "WF1000XM6")
            };
            currentProfile = profiles[0];
            shortcutActions = CreateShortcutActions();
            LoadShortcutBindings();

            Text = AppTitle();
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 900);
            Size = new Size(1380, 980);
            BackColor = page;
            ForeColor = ink;
            Font = new Font("Segoe UI", 10f);
            windowIcon = CreateTrayIcon();
            notificationIcon = CreateTrayIcon();
            Icon = windowIcon;
            DoubleBuffered = true;

            BuildUi();

            trayMenu = BuildTrayMenu();
            trayIcon = new NotifyIcon
            {
                Text = TrayTitle(),
                Icon = notificationIcon,
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowWindow();

            autoDetectTimer = new System.Windows.Forms.Timer { Interval = AutoDetectIntervalMs };
            autoDetectTimer.Tick += async (s, e) => await RunUiTaskAsync(AutoDetectTickAsync);
            liveEqTimer = new System.Windows.Forms.Timer { Interval = LiveEqDebounceMs };
            liveEqTimer.Tick += async (s, e) => await RunUiTaskAsync(async () =>
            {
                if (IsClosing) return;
                liveEqTimer.Stop();
                await FlushLiveEqualizerAsync();
            });

            Resize += (s, e) =>
            {
                if (!IsClosing && hasShownOnce && WindowState == FormWindowState.Minimized && minimizeToTray)
                {
                    Hide();
                    Notify("Still running in the tray.");
                }
            };
            Activated += async (s, e) =>
            {
                if (hasShownOnce) await RunUiTaskAsync(AutoDetectTickAsync);
            };
            FormClosing += OnFormClosing;
            Shown += async (s, e) =>
            {
                hasShownOnce = true;
                await RunUiTaskAsync(RefreshAllAsync);
                if (!IsClosing) autoDetectTimer.Start();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryApplyWindows11Chrome();
            RegisterShortcutHotkeys();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (!RecreatingHandle)
            {
                UnregisterShortcutHotkeys();
            }
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotKey)
            {
                ShortcutAction action;
                if (registeredShortcuts.TryGetValue(m.WParam.ToInt32(), out action))
                {
                    QueueShortcutAction(action.Id);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private bool IsClosing
        {
            get { return exiting || trayCleanupStarted || IsDisposed || Disposing; }
        }

        private bool CanUpdateUi
        {
            get { return !IsClosing && IsHandleCreated; }
        }

        private async Task RunUiTaskAsync(Func<Task> action)
        {
            if (action == null || IsClosing) return;
            try
            {
                await action();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
                if (!IsClosing) SetStatus("Operation failed", red);
            }
            catch
            {
                if (!IsClosing) SetStatus("Operation failed", red);
            }
        }

        private void PostToUi(Action action)
        {
            if (action == null || !CanUpdateUi) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsClosing) action();
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22),
                BackColor = page,
                ColumnCount = 2,
                RowCount = 2
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = new Panel { Dock = DockStyle.Fill, BackColor = page };
            root.SetColumnSpan(header, 2);
            root.Controls.Add(header, 0, 0);

            titleLabel = new Label
            {
                Text = currentProfile.DisplayName,
                ForeColor = ink,
                Font = new Font("Segoe UI Semibold", 23f),
                AutoSize = true,
                Location = new Point(2, 1)
            };
            header.Controls.Add(titleLabel);

            statusLabel = new Label
            {
                Text = File.Exists(backendPath) ? "Ready" : "Backend not found",
                ForeColor = File.Exists(backendPath) ? subdued : red,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                Location = new Point(4, 43)
            };
            header.Controls.Add(statusLabel);

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = page,
                RowCount = 3,
                ColumnCount = 1,
                Margin = new Padding(0, 0, 8, 0)
            };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 194));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(left, 0, 1);

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = page,
                RowCount = 1,
                ColumnCount = 1,
                Margin = new Padding(8, 0, 0, 0)
            };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(right, 1, 1);

            var hero = CreateCard();
            var controls = CreateCard();
            var equalizer = CreateCard();
            var settingsHub = CreateCard();

            left.Controls.Add(hero, 0, 0);
            left.Controls.Add(controls, 0, 1);
            left.Controls.Add(equalizer, 0, 2);
            right.Controls.Add(settingsHub, 0, 0);

            BuildHero(hero);
            BuildControls(controls);
            BuildEqualizer(equalizer);
            BuildSettingsHub(settingsHub);
        }

        private void BuildHero(CardPanel parent)
        {
            AddEyebrow(parent, "Current mode", 18);
            bigModeLabel = new Label
            {
                Text = "Unknown",
                ForeColor = ink,
                Font = new Font("Segoe UI Semibold", 29f),
                AutoSize = false,
                Location = new Point(CardInset - HeroModeOpticalOffset, 42),
                Size = new Size(420, 66),
                AutoEllipsis = true,
                UseCompatibleTextRendering = false
            };
            parent.Controls.Add(bigModeLabel);

            bigDetailLabel = new Label
            {
                Text = "Connect a supported headset to read its state",
                ForeColor = subdued,
                Font = new Font("Segoe UI", 10.5f),
                AutoSize = false,
                Location = new Point(CardInset, 112),
                Size = new Size(520, 28),
                AutoEllipsis = true
            };
            parent.Controls.Add(bigDetailLabel);

            lastActionLabel = new Label
            {
                Text = "No changes this session",
                ForeColor = subdued,
                Font = new Font("Segoe UI", 10f),
                AutoSize = false,
                Size = new Size(520, 28),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(CardInset, 142),
                AutoEllipsis = true
            };
            lastActionLabel.Height = 22;
            parent.Controls.Add(lastActionLabel);

            heroImageBox = new PictureBox
            {
                BackColor = card,
                Image = LoadImageAsset(currentProfile.AssetPath),
                Location = new Point(520, 23),
                Size = new Size(160, 120),
                SizeMode = PictureBoxSizeMode.Zoom,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            parent.Controls.Add(heroImageBox);

            Action layoutHero = () =>
            {
                bool showImage = heroImageBox.Image != null && parent.Width > 560;
                heroImageBox.Visible = showImage;
                if (showImage)
                {
                    int imageSize = Math.Min(160, Math.Max(124, parent.Height - 28));
                    heroImageBox.Size = new Size(imageSize, imageSize);
                    heroImageBox.Location = new Point(parent.Width - heroImageBox.Width - CardInset, Math.Max(10, (parent.Height - heroImageBox.Height) / 2));
                }
                int textRight = showImage ? heroImageBox.Left - 24 : parent.Width - CardInset;
                int textWidth = Math.Max(260, textRight - CardInset);
                bigModeLabel.Location = new Point(CardInset - HeroModeOpticalOffset, bigModeLabel.Top);
                bigModeLabel.Width = textWidth;
                bigDetailLabel.Width = textWidth;
                lastActionLabel.Width = textWidth;
                lastActionLabel.Location = new Point(CardInset, 142);
            };
            parent.Resize += (s, e) => layoutHero();
            layoutHero();
        }

        private void BuildControls(CardPanel parent)
        {
            AddTitle(parent, "Noise Control", 20);

            ancButton = new PillButton("Noise cancelling", blue, bluePressed);
            ambientButton = new PillButton("Ambient sound", blue, bluePressed);
            offButton = new PillButton("Off", Color.FromArgb(65, 70, 78), Color.FromArgb(77, 83, 92));

            PlaceRow(parent, 68, 10, 42, ancButton, ambientButton, offButton);
            ancButton.Click += async (s, e) => await SetAncAsync();
            ambientButton.Click += async (s, e) => await SetAmbientAsync(levelSlider.Value, IsVoiceAmbientSelected());
            offButton.Click += async (s, e) => await SetOffAsync();

            AddDivider(parent, 140);
            var ambientLabel = new Label
            {
                Text = "Ambient sound",
                ForeColor = Color.FromArgb(190, 198, 207),
                Font = new Font("Segoe UI Semibold", 9.2f),
                AutoSize = true
            };
            parent.Controls.Add(ambientLabel);

            levelLabel = new Label
            {
                Text = "Level 12",
                ForeColor = Color.FromArgb(190, 198, 207),
                Font = new Font("Segoe UI Semibold", 10f),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            parent.Controls.Add(levelLabel);

            ambientKindBox = new ChoiceDropdown
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = cardSoft,
                ForeColor = ink,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(CardInset, 188),
                Width = 160
            };
            ambientKindBox.Items.AddRange(new object[] { "Normal", "Voice" });
            ambientKindBox.SelectedIndex = 0;
            parent.Controls.Add(ambientKindBox);

            levelSlider = new SliderControl
            {
                Minimum = 0,
                Maximum = 20,
                Value = 12,
                TickFrequency = 2,
                SmallChange = 1,
                LargeChange = 2,
                BackColor = card,
                TrackColor = Color.FromArgb(78, 84, 94),
                FillColor = blue,
                ThumbColor = blue,
                TickColor = Color.FromArgb(91, 98, 108),
                Location = new Point(CardInset, 224),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            parent.Controls.Add(levelSlider);

            var apply = new PillButton("Apply", blue, bluePressed);
            apply.Size = new Size(108, 34);
            apply.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            apply.Click += async (s, e) => await SetAmbientAsync(levelSlider.Value, IsVoiceAmbientSelected());
            parent.Controls.Add(apply);

            Action layoutAmbientControls = () =>
            {
                int sliderHeight = 34;
                int sliderVisibleBottom = sliderHeight / 2 + 12;
                int sliderTop = Math.Max(212, parent.Height - 22 - sliderVisibleBottom);
                int inputTop = sliderTop - 36;
                int labelTop = inputTop - 28;
                int right = parent.Width - CardInset;
                ambientLabel.Location = new Point(CardInset, labelTop);
                levelLabel.Location = new Point(right - levelLabel.Width, labelTop);
                apply.Location = new Point(right - apply.Width, inputTop);

                int dropdownRight = apply.Left - 18;
                ambientKindBox.Location = new Point(CardInset, inputTop);
                ambientKindBox.Width = Math.Max(150, Math.Min(220, dropdownRight - CardInset));

                levelSlider.Location = new Point(CardInset - 12, sliderTop);
                levelSlider.Size = new Size(Math.Max(220, parent.Width - (CardInset * 2) + 24), sliderHeight);
            };
            parent.Resize += (s, e) => layoutAmbientControls();
            layoutAmbientControls();
            levelSlider.ValueChanged += (s, e) =>
            {
                levelLabel.Text = "Level " + levelSlider.Value;
                layoutAmbientControls();
            };
        }

        private void BuildEqualizer(CardPanel parent)
        {
            AddTitle(parent, "Equalizer", 18);

            eqCardSummaryLabel = new Label
            {
                Text = "Waiting",
                ForeColor = subdued,
                Font = new Font("Segoe UI", 10f),
                AutoEllipsis = true,
                Location = new Point(CardInset, 46),
                Size = new Size(320, 22)
            };
            parent.Controls.Add(eqCardSummaryLabel);

            eqCurve = new EqCurveControl
            {
                BackColor = card,
                ForeColor = ink,
                LineColor = blue,
                BassColor = blue,
                GridColor = Color.FromArgb(76, 76, 76),
                MutedColor = subdued,
                Location = new Point(CardInset, 70),
                Size = new Size(520, 112),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            parent.Controls.Add(eqCurve);

            eqPresetBox = new ChoiceDropdown
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = cardSoft,
                ForeColor = ink,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(CardInset, 184),
                Width = 188
            };
            eqPresetBox.Items.AddRange(new object[]
            {
                new EqPresetChoice("Off", 0x00),
                new EqPresetChoice("Bright", 0x10),
                new EqPresetChoice("Excited", 0x11),
                new EqPresetChoice("Mellow", 0x12),
                new EqPresetChoice("Relaxed", 0x13),
                new EqPresetChoice("Vocal", 0x14),
                new EqPresetChoice("Treble", 0x15),
                new EqPresetChoice("Bass", 0x16),
                new EqPresetChoice("Speech", 0x17),
                new EqPresetChoice("Manual", 0xA0),
                new EqPresetChoice("User 1", 0xA1),
                new EqPresetChoice("User 2", 0xA2)
            });
            eqPresetBox.SelectedIndex = 9;
            eqPresetBox.SelectedIndexChanged += (s, e) =>
            {
                if (updatingEqUi) return;
                var choice = eqPresetBox.SelectedItem as EqPresetChoice;
                if (choice == null) return;
                currentEqPreset = choice.Value;
                int[] values = CurrentEqValues();
                if (eqCurve != null) eqCurve.SetState(currentEqPreset, values);
                if (eqCardSummaryLabel != null) eqCardSummaryLabel.Text = FormatEqSummary(currentEqPreset, values);
                lastActionLabel.Text = "Equalizer preset updating";
                ScheduleLiveEqualizerApply();
            };
            parent.Controls.Add(eqPresetBox);

            var flat = new PillButton("Flat", Color.FromArgb(65, 70, 78), Color.FromArgb(77, 83, 92));
            flat.Click += async (s, e) => await SetEqualizerFlatAsync();
            parent.Controls.Add(flat);

            eqSliders = new SliderControl[6];
            eqValueLabels = new Label[6];
            var eqNameLabels = new Label[6];
            string[] names = { "Clear Bass", "400", "1k", "2.5k", "6.3k", "16k" };
            for (int i = 0; i < eqSliders.Length; i++)
            {
                var name = new Label
                {
                    Text = names[i],
                    ForeColor = i == 0 ? blue : subdued,
                    Font = new Font("Segoe UI Semibold", 8.8f),
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                parent.Controls.Add(name);
                eqNameLabels[i] = name;

                var value = new Label
                {
                    Text = "0",
                    ForeColor = ink,
                    Font = new Font("Segoe UI Semibold", 10f),
                    TextAlign = ContentAlignment.MiddleRight
                };
                parent.Controls.Add(value);
                eqValueLabels[i] = value;

                var slider = new SliderControl
                {
                    Minimum = 0,
                    Maximum = 20,
                    Value = 10,
                    TickFrequency = 5,
                    SmallChange = 1,
                    LargeChange = 2,
                    BackColor = card,
                    TrackColor = Color.FromArgb(78, 84, 94),
                    FillColor = blue,
                    ThumbColor = blue,
                    TickColor = Color.FromArgb(91, 98, 108),
                    CenteredFill = true
                };
                slider.ValueChanged += (s, e) =>
                {
                    var changed = (SliderControl)s;
                    int index = Array.IndexOf(eqSliders, changed);
                    if (!updatingEqUi)
                    {
                        currentEqPreset = 0xA0;
                        SelectEqPreset(0xA0);
                    }
                    if (index >= 0) eqValueLabels[index].Text = FormatEqValue(changed.Value);
                    int[] values = CurrentEqValues();
                    if (eqCurve != null) eqCurve.SetState(currentEqPreset, values);
                    if (eqCardSummaryLabel != null) eqCardSummaryLabel.Text = FormatEqSummary(currentEqPreset, values);
                    if (!updatingEqUi)
                    {
                        lastActionLabel.Text = "Equalizer updating";
                        ScheduleLiveEqualizerApply();
                    }
                };
                parent.Controls.Add(slider);
                eqSliders[i] = slider;
            }

            Action layout = () =>
            {
                flat.Size = new Size(74, 34);
                flat.Location = new Point(parent.Width - flat.Width - CardInset, 184);
                eqCardSummaryLabel.Width = Math.Max(260, parent.Width - (CardInset * 2));
                eqCurve.Width = Math.Max(360, parent.Width - (CardInset * 2));
                eqCurve.Height = 112;
                eqPresetBox.Location = new Point(CardInset, 184);
                eqPresetBox.Width = Math.Max(150, Math.Min(220, flat.Left - CardInset - 12));

                int top = 222;
                int rowHeight = 33;
                int gap = 18;
                int valueWidth = 34;
                int availableWidth = Math.Max(420, parent.Width - (CardInset * 2) - gap);
                int columnWidth = Math.Max(236, availableWidth / 2);
                for (int i = 0; i < eqSliders.Length; i++)
                {
                    int column = i < 3 ? 0 : 1;
                    int row = i % 3;
                    int x = CardInset + column * (columnWidth + gap);
                    int y = top + row * rowHeight;
                    int sliderLeft = x + 86;
                    int sliderWidth = Math.Max(120, columnWidth - 126);

                    eqNameLabels[i].Location = new Point(x, y + 4);
                    eqNameLabels[i].Size = new Size(82, 22);
                    eqSliders[i].Location = new Point(sliderLeft - 12, y - 1);
                    eqSliders[i].Size = new Size(sliderWidth + 24, 30);
                    eqValueLabels[i].Location = new Point(sliderLeft + sliderWidth + 6, y + 4);
                    eqValueLabels[i].Size = new Size(valueWidth, 22);
                }
            };
            parent.Resize += (s, e) => layout();
            layout();
        }

        private void BuildSettingsHub(CardPanel parent)
        {
            AddTitle(parent, "Settings", 24);
            configureShortcutsButton = NewOptionButton("Shortcuts");
            configureShortcutsButton.Click += (s, e) => ConfigureShortcuts();
            parent.Controls.Add(configureShortcutsButton);
            UpdateShortcutsLabel();
            Action placeShortcutsButton = () =>
            {
                configureShortcutsButton.Size = new Size(configureShortcutsButton.Text.Length > 11 ? 126 : 108, 32);
                configureShortcutsButton.Location = new Point(parent.Width - configureShortcutsButton.Width - CardInset, 21);
            };
            parent.Resize += (s, e) => placeShortcutsButton();
            placeShortcutsButton();

            AddEyebrow(parent, "Device", 62);
            connectionLabel = AddActionRow(parent, "Connection", "Waiting", 84);
            batteryLabel = AddActionRow(parent, "Battery", "Waiting", 128);
            multipointOnButton = NewOptionButton("On");
            multipointOffButton = NewOptionButton("Off");
            multipointLabel = AddActionRow(parent, "Connect to 2 devices", "Waiting", 172, multipointOnButton, multipointOffButton);
            multipointOnButton.Click += async (s, e) => await SetMultipointAsync(true);
            multipointOffButton.Click += async (s, e) => await SetMultipointAsync(false);

            soundQualityButton = NewOptionButton("Sound quality");
            stableButton = NewOptionButton("Stable");
            connectionQualityLabel = AddActionRow(parent, "Bluetooth connection quality", "Waiting", 222, soundQualityButton, stableButton);
            soundQualityButton.Click += async (s, e) => await SetConnectionQualityAsync(true);
            stableButton.Click += async (s, e) => await SetConnectionQualityAsync(false);

            AddDivider(parent, 278);
            AddEyebrow(parent, "Sound", 300);
            equalizerLabel = AddActionRow(parent, "Equalizer", "Waiting", 322);

            dseeAutoButton = NewOptionButton("Auto");
            dseeOffButton = NewOptionButton("Off");
            dseeLabel = AddActionRow(parent, "DSEE Extreme", "Waiting", 370, dseeAutoButton, dseeOffButton);
            dseeAutoButton.Click += async (s, e) => await SetDseeAsync(true);
            dseeOffButton.Click += async (s, e) => await SetDseeAsync(false);

            AddDivider(parent, 426);
            AddEyebrow(parent, "Controls", 448);
            speakOnButton = NewOptionButton("On");
            speakOffButton = NewOptionButton("Off");
            speakToChatLabel = AddActionRow(parent, "Speak-to-Chat", "Waiting", 470, speakOnButton, speakOffButton);
            speakOnButton.Click += async (s, e) => await SetSpeakToChatAsync(true);
            speakOffButton.Click += async (s, e) => await SetSpeakToChatAsync(false);

            pauseOnButton = NewOptionButton("On");
            pauseOffButton = NewOptionButton("Off");
            wearPauseLabel = AddActionRow(parent, "Pause when headphones are removed", "Waiting", 518, pauseOnButton, pauseOffButton);
            pauseOnButton.Click += async (s, e) => await SetWearPauseAsync(true);
            pauseOffButton.Click += async (s, e) => await SetWearPauseAsync(false);

            touchOnButton = NewOptionButton("On");
            touchOffButton = NewOptionButton("Off");
            touchPanelLabel = AddActionRow(parent, "Touch sensor control panel", "Waiting", 566, touchOnButton, touchOffButton);
            touchOnButton.Click += async (s, e) => await SetTouchPanelAsync(true);
            touchOffButton.Click += async (s, e) => await SetTouchPanelAsync(false);

            AddDivider(parent, 622);
            AddEyebrow(parent, "Power", 644);
            autoPowerRemovedButton = NewOptionButton("When Removed");
            autoPowerDisableButton = NewOptionButton("Disable");
            autoPowerLabel = AddActionRow(parent, "Automatic power off", "Waiting", 666, autoPowerRemovedButton, autoPowerDisableButton);
            autoPowerRemovedButton.Click += async (s, e) => await SetAutoPowerRemovedAsync();
            autoPowerDisableButton.Click += async (s, e) => await SetAutoPowerDisabledAsync();

            SetModeButtonState("Ambient");
            SetMultipointState(null);
            SetConnectionQualityState(null);
            SetDseeState(null);
            SetSpeakToChatState(null);
            SetWearPauseState(null);
            SetTouchPanelState(null);
            SetAutoPowerState(null);
        }

        private PillButton NewOptionButton(string caption)
        {
            var button = new PillButton(caption, InactiveButtonColor(), PressedColor(InactiveButtonColor()));
            return button;
        }

        private void SetModeButtonState(string mode)
        {
            bool anc = string.Equals(mode, "ANC", StringComparison.OrdinalIgnoreCase);
            bool ambient = string.Equals(mode, "Ambient", StringComparison.OrdinalIgnoreCase);
            bool off = string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase);
            SetButtonSelected(ancButton, anc, blue);
            SetButtonSelected(ambientButton, ambient, blue);
            SetButtonSelected(offButton, off, blue);
        }

        private void SetMultipointState(bool? enabled)
        {
            if (multipointLabel != null) multipointLabel.Text = enabled.HasValue ? (enabled.Value ? "On" : "Off") : "Waiting";
            SetOptionPair(multipointOnButton, multipointOffButton, enabled, blue, blue);
        }

        private void SetConnectionQualityState(bool? prioritizeSound)
        {
            if (connectionQualityLabel != null) connectionQualityLabel.Text = prioritizeSound.HasValue ? (prioritizeSound.Value ? "Prioritize Sound Quality" : "Prioritize Stable Connection") : "Waiting";
            SetOptionPair(soundQualityButton, stableButton, prioritizeSound, blue, blue);
        }

        private void SetDseeState(bool? auto)
        {
            if (dseeLabel != null) dseeLabel.Text = auto.HasValue ? (auto.Value ? "Auto" : "Off") : "Waiting";
            SetOptionPair(dseeAutoButton, dseeOffButton, auto, blue, blue);
        }

        private void SetSpeakToChatState(bool? enabled)
        {
            if (speakToChatLabel != null) speakToChatLabel.Text = enabled.HasValue ? (enabled.Value ? "On" : "Off") : "Waiting";
            SetOptionPair(speakOnButton, speakOffButton, enabled, blue, blue);
        }

        private void SetWearPauseState(bool? enabled)
        {
            if (wearPauseLabel != null) wearPauseLabel.Text = enabled.HasValue ? (enabled.Value ? "On" : "Off") : "Waiting";
            SetOptionPair(pauseOnButton, pauseOffButton, enabled, blue, blue);
        }

        private void SetTouchPanelState(bool? enabled)
        {
            if (touchPanelLabel != null) touchPanelLabel.Text = enabled.HasValue ? (enabled.Value ? "On" : "Off") : "Waiting";
            SetOptionPair(touchOnButton, touchOffButton, enabled, blue, blue);
        }

        private void SetAutoPowerState(string state)
        {
            bool removed = string.Equals(state, "removed", StringComparison.OrdinalIgnoreCase);
            bool disabled = string.Equals(state, "disabled", StringComparison.OrdinalIgnoreCase);
            if (autoPowerLabel != null)
            {
                if (removed) autoPowerLabel.Text = "Off when headphones are removed";
                else if (disabled) autoPowerLabel.Text = "Disabled";
                else autoPowerLabel.Text = "Waiting";
            }
            SetButtonSelected(autoPowerRemovedButton, removed, blue);
            SetButtonSelected(autoPowerDisableButton, disabled, blue);
        }

        private void SetAutoPowerCustomState(string text)
        {
            if (autoPowerLabel != null) autoPowerLabel.Text = text;
            SetButtonSelected(autoPowerRemovedButton, false, blue);
            SetButtonSelected(autoPowerDisableButton, false, blue);
        }

        private void SetOptionPair(PillButton first, PillButton second, bool? firstSelected, Color firstColor, Color secondColor)
        {
            if (!firstSelected.HasValue)
            {
                SetButtonSelected(first, false, firstColor);
                SetButtonSelected(second, false, secondColor);
                return;
            }
            SetButtonSelected(first, firstSelected.Value, firstColor);
            SetButtonSelected(second, !firstSelected.Value, secondColor);
        }

        private void SetButtonSelected(PillButton button, bool selected, Color selectedColor)
        {
            if (button == null) return;
            Color fill = selected ? selectedColor : InactiveButtonColor();
            button.SetPalette(fill, PressedColor(fill));
        }

        private static Color InactiveButtonColor()
        {
            return Color.FromArgb(61, 67, 76);
        }

        private static Color PressedColor(Color color)
        {
            return BlendColor(color, Color.Black, 0.18f);
        }

        private static Color BlendColor(Color a, Color b, float amount)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        private ContextMenuStrip BuildTrayMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open window", null, (s, e) => ShowWindow());
            menu.Items.Add(new ToolStripSeparator());
            AddTrayAction(menu.Items, "Noise cancelling", SetAncAsync);
            AddTrayAction(menu.Items, "Ambient sound: 12", () => SetAmbientAsync(12));
            AddTrayAction(menu.Items, "Ambient sound: 20", () => SetAmbientAsync(20));
            AddTrayAction(menu.Items, "Noise control off", SetOffAsync);
            menu.Items.Add(new ToolStripSeparator());
            AddTrayAction(menu.Items, "Reset equalizer to flat", SetEqualizerFlatAsync);
            AddTrayAction(menu.Items, "Prioritize sound quality", () => SetConnectionQualityAsync(true));
            AddTrayAction(menu.Items, "Prioritize stable connection", () => SetConnectionQualityAsync(false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Configure shortcuts...", null, (s, e) => ConfigureShortcuts());

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) =>
            {
                PostToUi(() =>
                {
                    exiting = true;
                    Close();
                });
            });
            return menu;
        }

        private ToolStripMenuItem AddTrayAction(ToolStripItemCollection items, string text, Func<Task> action)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += async (s, e) => await RunUiTaskAsync(action);
            items.Add(item);
            return item;
        }

        private string AppTitle()
        {
            return currentProfile.DisplayName + " Control";
        }

        private string TrayTitle()
        {
            string title = AppTitle();
            return title.Length <= 63 ? title : title.Substring(0, 63);
        }

        private string WithDevice(string args)
        {
            return args + " --name \"" + currentProfile.NameFilter.Replace("\"", "") + "\"";
        }

        private static ShortcutAction[] CreateShortcutActions()
        {
            return new[]
            {
                new ShortcutAction("open", "Open window"),
                new ShortcutAction("anc", "Noise cancelling"),
                new ShortcutAction("ambient12", "Ambient sound: 12"),
                new ShortcutAction("ambient20", "Ambient sound: 20"),
                new ShortcutAction("off", "Noise control off"),
                new ShortcutAction("eqflat", "Reset equalizer to flat"),
                new ShortcutAction("soundquality", "Prioritize sound quality"),
                new ShortcutAction("stable", "Prioritize stable connection"),
                new ShortcutAction("dseeauto", "DSEE Extreme: Auto"),
                new ShortcutAction("dseeoff", "DSEE Extreme: Off")
            };
        }

        private static string ShortcutConfigPath()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SonyHeadphonesControl");
            return Path.Combine(root, "shortcuts.txt");
        }

        private void LoadShortcutBindings()
        {
            shortcutBindings.Clear();
            for (int i = 0; i < shortcutActions.Length; i++)
            {
                shortcutBindings[shortcutActions[i].Id] = ShortcutBinding.None();
            }

            string path = ShortcutConfigPath();
            if (!File.Exists(path)) return;

            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string lineText = lines[i];
                    if (string.IsNullOrWhiteSpace(lineText)) continue;
                    int equals = lineText.IndexOf('=');
                    if (equals <= 0) continue;
                    string id = lineText.Substring(0, equals).Trim();
                    string value = lineText.Substring(equals + 1).Trim();
                    if (shortcutBindings.ContainsKey(id))
                    {
                        shortcutBindings[id] = ShortcutBinding.Parse(value);
                    }
                }
            }
            catch
            {
            }
        }

        private void SaveShortcutBindings()
        {
            try
            {
                string path = ShortcutConfigPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var lines = new List<string>();
                for (int i = 0; i < shortcutActions.Length; i++)
                {
                    ShortcutBinding binding = ShortcutBinding.None();
                    shortcutBindings.TryGetValue(shortcutActions[i].Id, out binding);
                    lines.Add(shortcutActions[i].Id + "=" + (binding == null ? "None" : binding.ToConfigString()));
                }
                File.WriteAllLines(path, lines.ToArray());
            }
            catch
            {
                SetStatus("Could not save shortcuts", red);
            }
        }

        private void ConfigureShortcuts()
        {
            if (IsClosing) return;
            if (!Visible || WindowState == FormWindowState.Minimized) ShowWindow();
            using (var dialog = new ShortcutDialog(shortcutActions, shortcutBindings, page, card, cardSoft, line, ink, subdued, blue, bluePressed))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                shortcutBindings.Clear();
                Dictionary<string, ShortcutBinding> next = dialog.Bindings;
                for (int i = 0; i < shortcutActions.Length; i++)
                {
                    ShortcutBinding binding;
                    if (!next.TryGetValue(shortcutActions[i].Id, out binding) || binding == null)
                    {
                        binding = ShortcutBinding.None();
                    }
                    shortcutBindings[shortcutActions[i].Id] = binding.Clone();
                }
            }

            SaveShortcutBindings();
            RegisterShortcutHotkeys();
            UpdateShortcutsLabel();
        }

        private int AssignedShortcutCount()
        {
            int assigned = 0;
            for (int i = 0; i < shortcutActions.Length; i++)
            {
                ShortcutBinding binding;
                if (shortcutBindings.TryGetValue(shortcutActions[i].Id, out binding) && binding != null && binding.IsAssigned)
                {
                    assigned++;
                }
            }
            return assigned;
        }

        private void UpdateShortcutsLabel()
        {
            if (configureShortcutsButton == null || configureShortcutsButton.IsDisposed) return;
            int assigned = AssignedShortcutCount();
            configureShortcutsButton.Text = assigned == 0 ? "Shortcuts" : "Shortcuts (" + assigned + ")";
            if (configureShortcutsButton.Parent != null)
            {
                configureShortcutsButton.Size = new Size(configureShortcutsButton.Text.Length > 11 ? 126 : 108, 32);
                configureShortcutsButton.Location = new Point(configureShortcutsButton.Parent.Width - configureShortcutsButton.Width - CardInset, 21);
            }
        }

        private void RegisterShortcutHotkeys()
        {
            if (!IsHandleCreated) return;
            UnregisterShortcutHotkeys();

            var signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int failed = 0;
            for (int i = 0; i < shortcutActions.Length; i++)
            {
                ShortcutBinding binding;
                if (!shortcutBindings.TryGetValue(shortcutActions[i].Id, out binding) || binding == null || !binding.IsGlobalSafe) continue;
                if (!signatures.Add(binding.Signature))
                {
                    failed++;
                    continue;
                }

                int id = ShortcutHotkeyBaseId + i;
                if (RegisterHotKey(Handle, id, binding.Modifiers, (uint)binding.Key))
                {
                    registeredShortcuts[id] = shortcutActions[i];
                }
                else
                {
                    failed++;
                }
            }

            UpdateShortcutsLabel();
            if (failed > 0) SetStatus(failed == 1 ? "1 shortcut unavailable" : failed + " shortcuts unavailable", amber);
        }

        private void UnregisterShortcutHotkeys()
        {
            if (!IsHandleCreated || registeredShortcuts.Count == 0)
            {
                registeredShortcuts.Clear();
                return;
            }

            var ids = new List<int>(registeredShortcuts.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                UnregisterHotKey(Handle, ids[i]);
            }
            registeredShortcuts.Clear();
        }

        private void QueueShortcutAction(string actionId)
        {
            PostToUi(async () => await RunUiTaskAsync(() => ExecuteShortcutActionAsync(actionId)));
        }

        private async Task ExecuteShortcutActionAsync(string actionId)
        {
            if (IsClosing) return;
            if (string.Equals(actionId, "open", StringComparison.OrdinalIgnoreCase))
            {
                ShowWindow();
            }
            else if (string.Equals(actionId, "anc", StringComparison.OrdinalIgnoreCase))
            {
                await SetAncAsync();
            }
            else if (string.Equals(actionId, "ambient12", StringComparison.OrdinalIgnoreCase))
            {
                await SetAmbientAsync(12);
            }
            else if (string.Equals(actionId, "ambient20", StringComparison.OrdinalIgnoreCase))
            {
                await SetAmbientAsync(20);
            }
            else if (string.Equals(actionId, "off", StringComparison.OrdinalIgnoreCase))
            {
                await SetOffAsync();
            }
            else if (string.Equals(actionId, "eqflat", StringComparison.OrdinalIgnoreCase))
            {
                await SetEqualizerFlatAsync();
            }
            else if (string.Equals(actionId, "soundquality", StringComparison.OrdinalIgnoreCase))
            {
                await SetConnectionQualityAsync(true);
            }
            else if (string.Equals(actionId, "stable", StringComparison.OrdinalIgnoreCase))
            {
                await SetConnectionQualityAsync(false);
            }
            else if (string.Equals(actionId, "dseeauto", StringComparison.OrdinalIgnoreCase))
            {
                await SetDseeAsync(true);
            }
            else if (string.Equals(actionId, "dseeoff", StringComparison.OrdinalIgnoreCase))
            {
                await SetDseeAsync(false);
            }
        }

        private bool ApplyProfile(DeviceProfile profile)
        {
            if (profile == null) return false;
            bool changed = !ReferenceEquals(currentProfile, profile);
            currentProfile = profile;
            if (IsClosing) return changed;

            Text = AppTitle();
            if (titleLabel != null) titleLabel.Text = currentProfile.DisplayName;
            if (trayIcon != null && !trayCleanupStarted) trayIcon.Text = TrayTitle();
            if (heroImageBox != null && !heroImageBox.IsDisposed && (changed || heroImageBox.Image == null))
            {
                Image previous = heroImageBox.Image;
                heroImageBox.Image = LoadImageAsset(currentProfile.AssetPath);
                if (previous != null) previous.Dispose();
                heroImageBox.Invalidate();
            }

            return changed;
        }

        private async Task AutoDetectTickAsync()
        {
            if (IsClosing || commandBusy || autoDetectRunning) return;
            autoDetectRunning = true;
            try
            {
                var detection = await DetectProfileAsync(true);
                if (IsClosing) return;
                if (detection == null)
                {
                    lastStateRefreshConnected = false;
                    return;
                }

                if (!detection.Connected)
                {
                    lastStateRefreshConnected = false;
                    return;
                }

                bool stateRefreshExpired = DateTime.UtcNow - lastStateRefreshAt >= TimeSpan.FromMilliseconds(AutoStateRefreshIntervalMs);
                if (!ReferenceEquals(lastStateRefreshProfile, detection.Profile) || !lastStateRefreshConnected || stateRefreshExpired)
                {
                    await RefreshCurrentStateQuietAsync(detection);
                }
            }
            catch
            {
                if (!IsClosing) SetStatus("Auto detect failed", amber);
            }
            finally
            {
                autoDetectRunning = false;
            }
        }

        private async Task<DeviceDetection> DetectProfileAsync(bool quiet)
        {
            var output = quiet ? await RunBackendQuietAsync("scan") : await RunBackendAsync("scan", "Detecting device");
            if (IsClosing) return null;
            if (output == null) return null;

            var detection = FindDetectedProfile(output);
            if (detection == null)
            {
                SetStatus("No supported device", amber);
                if (connectionLabel != null) connectionLabel.Text = "No supported device";
                return null;
            }

            bool changed = ApplyProfile(detection.Profile);
            if (connectionLabel != null) connectionLabel.Text = detection.Connected ? "Connected" : "Not connected";
            if (lastActionLabel != null && changed) lastActionLabel.Text = "Detected " + detection.Profile.DisplayName;
            SetStatus(detection.Connected ? "Connected" : "Not connected", detection.Connected ? green : amber);
            return detection;
        }

        private DeviceDetection FindDetectedProfile(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return null;

            DeviceDetection best = null;
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string lineText = lines[i];
                for (int j = 0; j < profiles.Length; j++)
                {
                    DeviceProfile profile = profiles[j];
                    if (!profile.MatchesText(lineText)) continue;

                    bool connected = Regex.IsMatch(lineText, @"^\s*yes\s+", RegexOptions.IgnoreCase);
                    bool remembered = Regex.IsMatch(lineText, @"^\s*(yes|no)\s+(yes|no)\s+yes\s+", RegexOptions.IgnoreCase);
                    int score = (connected ? 100 : 0) + (remembered ? 10 : 0) + (ReferenceEquals(profile, currentProfile) ? 1 : 0);
                    if (best == null || score > best.Score)
                    {
                        best = new DeviceDetection(profile, connected, remembered, score);
                    }
                }
            }
            return best;
        }

        private async Task RefreshAllAsync()
        {
            var detection = await DetectProfileAsync(false);
            if (IsClosing) return;
            if (detection == null) return;
            var output = await RunBackendAsync(WithDevice(StateBatchCommand), "Updating");
            if (IsClosing) return;
            if (string.IsNullOrWhiteSpace(output)) return;
            if (!output.Contains("Could not open"))
            {
                connectionLabel.Text = "Connected";
                MarkStateRefreshed(detection);
            }
            ParseBattery(output);
            ParseMode(output);
            ParseExtraSettings(output);
        }

        private async Task RefreshCurrentStateQuietAsync(DeviceDetection detection)
        {
            var output = await RunBackendQuietAsync(WithDevice(StateBatchCommand));
            if (IsClosing) return;
            if (string.IsNullOrWhiteSpace(output)) return;
            if (output.Contains("Could not open"))
            {
                lastStateRefreshConnected = false;
                return;
            }

            if (connectionLabel != null) connectionLabel.Text = "Connected";
            ParseBattery(output);
            ParseMode(output);
            ParseExtraSettings(output);
            MarkStateRefreshed(detection);
            SetStatus("Connected", green);
        }

        private void MarkStateRefreshed(DeviceDetection detection)
        {
            lastStateRefreshProfile = detection != null ? detection.Profile : currentProfile;
            lastStateRefreshConnected = true;
            lastStateRefreshAt = DateTime.UtcNow;
        }

        private void ParseBattery(string output)
        {
            var match = Regex.Match(output, @"battery:\s*(.+)");
            if (match.Success && batteryLabel != null) batteryLabel.Text = match.Groups[1].Value.Trim();
        }

        private void ParseExtraSettings(string output)
        {
            var touchPanel = Regex.Match(output, @"payload:\s*D7\s+D1\s+00\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (touchPanel.Success && touchPanelLabel != null)
            {
                SetTouchPanelState(HexByte(touchPanel.Groups[1].Value) == 0);
            }

            var multipoint = Regex.Match(output, @"payload:\s*D7\s+D2\s+00\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (multipoint.Success && multipointLabel != null)
            {
                SetMultipointState(HexByte(multipoint.Groups[1].Value) == 0);
            }

            var eqParam = Regex.Match(output, @"payload:\s*57\s+00\s+([0-9A-F]{2})\s+06\s+([0-9A-F]{2})\s+([0-9A-F]{2})\s+([0-9A-F]{2})\s+([0-9A-F]{2})\s+([0-9A-F]{2})\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (eqParam.Success)
            {
                int preset = HexByte(eqParam.Groups[1].Value);
                int[] values = new int[6];
                for (int i = 0; i < values.Length; i++) values[i] = HexByte(eqParam.Groups[i + 2].Value);
                UpdateEqualizerUi(preset, values);
            }
            else
            {
                var equalizer = Regex.Match(output, @"payload:\s*53\s+00\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
                if (equalizer.Success && equalizerLabel != null)
                {
                    int state = HexByte(equalizer.Groups[1].Value);
                    string text = state == 0 ? "Enabled, waiting for bands" : "Off";
                    equalizerLabel.Text = text;
                    if (eqCardSummaryLabel != null) eqCardSummaryLabel.Text = text;
                }
            }

            var dsee = Regex.Match(output, @"payload:\s*E7\s+01\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (dsee.Success && dseeLabel != null)
            {
                SetDseeState(HexByte(dsee.Groups[1].Value) == 1);
            }

            var connection = Regex.Match(output, @"payload:\s*E7\s+00\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (connection.Success && connectionQualityLabel != null)
            {
                SetConnectionQualityState(HexByte(connection.Groups[1].Value) == 0);
            }

            var speak = Regex.Match(output, @"payload:\s*F7\s+02\s+([0-9A-F]{2})\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (speak.Success && speakToChatLabel != null)
            {
                SetSpeakToChatState(HexByte(speak.Groups[1].Value) == 0);
            }

            var wear = Regex.Match(output, @"payload:\s*F7\s+01\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (wear.Success && wearPauseLabel != null)
            {
                SetWearPauseState(HexByte(wear.Groups[1].Value) == 0);
            }

            var power = Regex.Match(output, @"payload:\s*27\s+05\s+([0-9A-F]{2})\s+([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (power.Success && autoPowerLabel != null)
            {
                int mode = HexByte(power.Groups[1].Value);
                int delay = HexByte(power.Groups[2].Value);
                if (mode == 0x10) SetAutoPowerState("removed");
                else if (mode == 0x11) SetAutoPowerState("disabled");
                else SetAutoPowerCustomState(FormatPowerDelay(mode == 0 ? delay : mode));
            }
        }

        private async Task SetAncAsync()
        {
            bigModeLabel.Text = "ANC";
            bigDetailLabel.Text = "Noise cancelling active";
            SetModeButtonState("ANC");
            lastActionLabel.Text = "ANC selected";
            await RunBackendAsync(WithDevice("anc --timeout 1200"), "Setting ANC");
        }

        private async Task SetAmbientAsync(int level, bool voice = false)
        {
            int clamped = Math.Max(levelSlider.Minimum, Math.Min(levelSlider.Maximum, level));
            levelSlider.Value = clamped;
            ambientKindBox.SelectedIndex = voice ? 1 : 0;
            string ambientByte = voice ? "01" : "00";
            string payload = "68 17 01 01 01 " + ambientByte + " " + clamped.ToString("X2");
            string kind = voice ? "Voice" : "Normal";
            bigModeLabel.Text = "Ambient";
            bigDetailLabel.Text = kind + " ambient level " + clamped;
            SetModeButtonState("Ambient");
            lastActionLabel.Text = (voice ? "Voice ambient " : "Ambient ") + clamped;
            await RunBackendAsync(WithDevice("raw \"" + payload + "\" --ack-only --timeout 1200"), "Setting ambient");
        }

        private async Task SetOffAsync()
        {
            bigModeLabel.Text = "Off";
            bigDetailLabel.Text = "Noise control disabled";
            SetModeButtonState("Off");
            lastActionLabel.Text = "Noise control off";
            await RunBackendAsync(WithDevice("off --timeout 1200"), "Turning off");
        }

        private async Task SetDseeAsync(bool enabled)
        {
            SetDseeState(enabled);
            lastActionLabel.Text = "DSEE Extreme " + (enabled ? "Auto" : "Off");
            await RunBackendAsync(WithDevice("raw \"E8 01 " + (enabled ? "01" : "00") + "\" --ack-only --timeout 1200"), "Setting DSEE");
        }

        private async Task SendEqualizerCurrentAsync(bool quiet)
        {
            if (eqSliders == null || eqSliders.Length != 6) return;
            var choice = eqPresetBox != null ? eqPresetBox.SelectedItem as EqPresetChoice : null;
            int preset = choice != null ? choice.Value : currentEqPreset;
            int[] values = new int[6];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Math.Max(0, Math.Min(20, eqSliders[i].Value));
            }

            currentEqPreset = preset;
            if (equalizerLabel != null) equalizerLabel.Text = FormatEqSummary(preset, values);
            if (eqCardSummaryLabel != null) eqCardSummaryLabel.Text = FormatEqSummary(preset, values);
            if (eqCurve != null) eqCurve.SetState(preset, values);
            lastActionLabel.Text = quiet ? "Equalizer updating" : "Equalizer applied";

            string payload = FormatEqualizerPayload(preset, values);
            if (quiet)
            {
                var output = await RunBackendQuietAsync(WithDevice("raw \"" + payload + "\" --ack-only --timeout 1200"));
                if (output == null)
                {
                    liveEqSendPending = true;
                    lastActionLabel.Text = "Equalizer waiting";
                    return;
                }
                if (output.Contains("Could not open"))
                {
                    SetStatus("Device not reachable", red);
                    lastActionLabel.Text = "Equalizer not sent";
                    return;
                }
                lastActionLabel.Text = "Equalizer updated";
                return;
            }

            await RunBackendAsync(WithDevice("raw \"" + payload + "\" --ack-only --timeout 1200"), "Setting equalizer");
        }

        private static string FormatEqualizerPayload(int preset, int[] values)
        {
            return string.Format(
                "58 00 {0:X2} 06 {1:X2} {2:X2} {3:X2} {4:X2} {5:X2} {6:X2}",
                preset,
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5]);
        }

        private void ScheduleLiveEqualizerApply()
        {
            if (updatingEqUi || exiting || trayCleanupStarted || liveEqTimer == null) return;
            liveEqSendPending = true;
            liveEqTimer.Stop();
            liveEqTimer.Start();
        }

        private void CancelLiveEqualizerApply()
        {
            liveEqSendPending = false;
            if (liveEqTimer != null) liveEqTimer.Stop();
        }

        private async Task FlushLiveEqualizerAsync()
        {
            if (updatingEqUi || exiting || trayCleanupStarted) return;
            if (liveEqSendRunning)
            {
                liveEqSendPending = true;
                return;
            }

            liveEqSendRunning = true;
            liveEqSendPending = false;
            try
            {
                await SendEqualizerCurrentAsync(true);
            }
            finally
            {
                liveEqSendRunning = false;
                if (liveEqSendPending && liveEqTimer != null && !exiting && !trayCleanupStarted)
                {
                    liveEqTimer.Stop();
                    liveEqTimer.Start();
                }
            }
        }

        private async Task SetEqualizerFlatAsync()
        {
            CancelLiveEqualizerApply();
            if (eqSliders != null) ResetEqSliders();
            if (equalizerLabel != null) equalizerLabel.Text = "Manual / Flat";
            if (eqCardSummaryLabel != null) eqCardSummaryLabel.Text = "Manual / Flat";
            lastActionLabel.Text = "Equalizer set to flat";
            await RunBackendAsync(WithDevice("raw \"58 00 A0 06 0A 0A 0A 0A 0A 0A\" --ack-only --timeout 1200"), "Setting equalizer");
        }

        private void ResetEqSliders()
        {
            if (eqSliders == null) return;
            updatingEqUi = true;
            try
            {
                currentEqPreset = 0xA0;
                SelectEqPreset(0xA0);
                for (int i = 0; i < eqSliders.Length; i++)
                {
                    eqSliders[i].Value = 10;
                    if (eqValueLabels != null && eqValueLabels[i] != null) eqValueLabels[i].Text = FormatEqValue(10);
                }
                if (equalizerLabel != null) equalizerLabel.Text = "Manual / Flat";
                if (eqCardSummaryLabel != null) eqCardSummaryLabel.Text = "Manual / Flat";
                if (eqCurve != null) eqCurve.SetState(0xA0, new int[] { 10, 10, 10, 10, 10, 10 });
                lastActionLabel.Text = "Equalizer flattened";
            }
            finally
            {
                updatingEqUi = false;
            }
        }

        private void UpdateEqualizerUi(int preset, int[] values)
        {
            currentEqPreset = preset;
            if (equalizerLabel != null) equalizerLabel.Text = FormatEqSummary(preset, values);
            if (eqCardSummaryLabel != null) eqCardSummaryLabel.Text = FormatEqSummary(preset, values);
            if (eqCurve != null) eqCurve.SetState(preset, values);
            if (eqSliders == null || values == null || values.Length < 6) return;

            updatingEqUi = true;
            try
            {
                SelectEqPreset(preset);
                for (int i = 0; i < 6; i++)
                {
                    int value = Math.Max(0, Math.Min(20, values[i]));
                    eqSliders[i].Value = value;
                    if (eqValueLabels != null && eqValueLabels[i] != null) eqValueLabels[i].Text = FormatEqValue(value);
                }
            }
            finally
            {
                updatingEqUi = false;
            }
        }

        private void SelectEqPreset(int preset)
        {
            if (eqPresetBox == null) return;
            for (int i = 0; i < eqPresetBox.Items.Count; i++)
            {
                var choice = eqPresetBox.Items[i] as EqPresetChoice;
                if (choice != null && choice.Value == preset)
                {
                    eqPresetBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private int[] CurrentEqValues()
        {
            int[] values = new int[6];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = eqSliders != null && i < eqSliders.Length && eqSliders[i] != null ? eqSliders[i].Value : 10;
            }
            return values;
        }

        private async Task SetConnectionQualityAsync(bool prioritizeSound)
        {
            SetConnectionQualityState(prioritizeSound);
            lastActionLabel.Text = prioritizeSound ? "Prioritizing sound quality" : "Prioritizing stable connection";
            await RunBackendAsync(WithDevice("raw \"E8 00 " + (prioritizeSound ? "00" : "01") + "\" --ack-only --timeout 1200"), "Setting connection quality");
        }

        private async Task SetSpeakToChatAsync(bool enabled)
        {
            string value = enabled ? "00" : "01";
            SetSpeakToChatState(enabled);
            lastActionLabel.Text = "Speak-to-Chat " + (enabled ? "on" : "off");
            await RunBackendAsync(WithDevice("raw \"F8 02 " + value + " " + value + "\" --ack-only --timeout 1200"), "Setting Speak-to-Chat");
        }

        private async Task SetWearPauseAsync(bool enabled)
        {
            SetWearPauseState(enabled);
            lastActionLabel.Text = "Pause when removed " + (enabled ? "on" : "off");
            await RunBackendAsync(WithDevice("raw \"F8 01 " + (enabled ? "00" : "01") + "\" --ack-only --timeout 1200"), "Setting wearing sensor");
        }

        private async Task SetTouchPanelAsync(bool enabled)
        {
            SetTouchPanelState(enabled);
            lastActionLabel.Text = "Touch panel " + (enabled ? "on" : "off");
            await RunBackendAsync(WithDevice("raw \"D8 D1 00 " + (enabled ? "00" : "01") + "\" --ack-only --timeout 1200"), "Setting touch panel");
        }

        private async Task SetMultipointAsync(bool enabled)
        {
            SetMultipointState(enabled);
            lastActionLabel.Text = "Multipoint " + (enabled ? "on" : "off");
            await RunBackendAsync(WithDevice("raw \"D8 D2 00 " + (enabled ? "00" : "01") + "\" --ack-only --timeout 1200"), "Setting multipoint");
        }

        private async Task SetAutoPowerRemovedAsync()
        {
            SetAutoPowerState("removed");
            lastActionLabel.Text = "Auto power off when removed";
            await RunBackendAsync(WithDevice("raw \"28 05 10 00\" --ack-only --timeout 1200"), "Setting auto power");
        }

        private async Task SetAutoPowerDisabledAsync()
        {
            SetAutoPowerState("disabled");
            lastActionLabel.Text = "Automatic power off disabled";
            await RunBackendAsync(WithDevice("raw \"28 05 11 00\" --ack-only --timeout 1200"), "Setting auto power");
        }

        private void ParseMode(string output)
        {
            var match = Regex.Match(output, @"ncasm:\s*changed;\s*master\s*(\w+);\s*mode\s*(\w+);\s*ambient\s*(\w+)=(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) return;

            string master = Capitalize(match.Groups[1].Value);
            string mode = match.Groups[2].Value.Equals("anc", StringComparison.OrdinalIgnoreCase) ? "ANC" : Capitalize(match.Groups[2].Value);
            string ambient = Capitalize(match.Groups[3].Value);
            string level = match.Groups[4].Value;

            bigModeLabel.Text = master == "Off" ? "Off" : mode;
            bigDetailLabel.Text = master == "Off" ? "Noise control disabled" : ambient + " ambient level " + level;
            SetModeButtonState(master == "Off" ? "Off" : mode);

            int parsed;
            if (int.TryParse(level, out parsed) && parsed >= levelSlider.Minimum && parsed <= levelSlider.Maximum)
            {
                levelSlider.Value = parsed;
            }
            ambientKindBox.SelectedIndex = ambient.Equals("Voice", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private async Task<string> RunBackendAsync(string args, string busyText)
        {
            if (IsClosing) return "";
            if (!File.Exists(backendPath))
            {
                SetStatus("Backend not found", red);
                return "";
            }

            await commandGate.WaitAsync();
            if (IsClosing)
            {
                commandGate.Release();
                return "";
            }

            SetBusy(true, busyText);
            try
            {
                var output = await Task.Run(() => RunBackendProcess(args));
                if (IsClosing) return output;

                if (output.Contains("Could not open")) SetStatus("Device not reachable", red);
                else SetStatus("Ready", subdued);
                return output;
            }
            catch
            {
                SetStatus("Command failed", red);
                return "";
            }
            finally
            {
                if (!IsClosing) SetBusy(false, "Ready");
                commandGate.Release();
            }
        }

        private async Task<string> RunBackendQuietAsync(string args)
        {
            if (IsClosing) return "";
            if (!File.Exists(backendPath)) return "";
            if (!commandGate.Wait(0)) return null;
            try
            {
                var output = await Task.Run(() => RunBackendProcess(args));
                return IsClosing ? "" : output;
            }
            catch
            {
                return "";
            }
            finally
            {
                commandGate.Release();
            }
        }

        private string RunBackendProcess(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = backendPath,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(backendPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(psi))
            {
                if (process == null) return "";
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return stdout + stderr;
            }
        }

        private void SetBusy(bool value, string label)
        {
            commandBusy = value;
            SetStatus(value ? label + "..." : label, value ? blue : subdued);
        }

        private void SetStatus(string value, Color color)
        {
            if (statusLabel == null || statusLabel.IsDisposed) return;
            statusLabel.Text = value;
            statusLabel.ForeColor = color;
        }

        private bool IsVoiceAmbientSelected()
        {
            return ambientKindBox != null && ambientKindBox.SelectedIndex == 1;
        }

        private void ShowWindow()
        {
            if (IsClosing) return;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void TryApplyWindows11Chrome()
        {
            try
            {
                int enabled = 1;
                DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
                DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));

                int rounded = 2;
                DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));

                int caption = ColorTranslator.ToWin32(page);
                int captionText = ColorTranslator.ToWin32(ink);
                int border = ColorTranslator.ToWin32(page);
                DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
                DwmSetWindowAttribute(Handle, 36, ref captionText, sizeof(int));
                DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
            }
            catch
            {
            }
        }

        private void Notify(string message)
        {
            if (!trayNotifications || trayIcon == null || trayCleanupStarted) return;
            try
            {
                trayIcon.ShowBalloonTip(1000, AppTitle(), message, ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!exiting && minimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                Notify("Quick controls are available in the tray.");
                return;
            }
            if (trayCleanupStarted) return;
            exiting = true;
            trayCleanupStarted = true;
            trayNotifications = false;
            UnregisterShortcutHotkeys();
            if (autoDetectTimer != null)
            {
                autoDetectTimer.Stop();
                autoDetectTimer.Dispose();
            }
            if (liveEqTimer != null)
            {
                liveEqTimer.Stop();
                liveEqTimer.Dispose();
            }
            if (trayMenu != null && trayMenu.Visible)
            {
                trayMenu.Close(ToolStripDropDownCloseReason.CloseCalled);
            }
            if (trayIcon != null)
            {
                trayIcon.ContextMenuStrip = null;
                trayIcon.Icon = null;
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            if (heroImageBox != null && heroImageBox.Image != null)
            {
                Image image = heroImageBox.Image;
                heroImageBox.Image = null;
                image.Dispose();
            }
            Icon = null;
            notificationIcon.Dispose();
            windowIcon.Dispose();
        }

        private CardPanel CreateCard()
        {
            return new CardPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(7),
                Radius = 8,
                BackColor = card,
                BorderColor = line
            };
        }

        private void AddTitle(Control parent, string value, int top)
        {
            parent.Controls.Add(new Label
            {
                Text = value,
                ForeColor = ink,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true,
                Location = new Point(CardInset, top)
            });
        }

        private void AddEyebrow(Control parent, string value, int top)
        {
            parent.Controls.Add(new Label
            {
                Text = value,
                ForeColor = Color.FromArgb(190, 198, 207),
                Font = new Font("Segoe UI Semibold", 9.2f),
                AutoSize = true,
                Location = new Point(CardInset, top)
            });
        }

        private Label AddActionRow(Control parent, string caption, string value, int top, params PillButton[] buttons)
        {
            var captionLabel = new Label
            {
                Text = caption,
                ForeColor = ink,
                Font = new Font("Segoe UI Semibold", 10f),
                AutoEllipsis = true,
                Location = new Point(CardInset, top),
                Size = new Size(220, 20)
            };
            parent.Controls.Add(captionLabel);

            var valueLabel = new Label
            {
                Text = value,
                ForeColor = subdued,
                Font = new Font("Segoe UI", 9.2f),
                AutoEllipsis = true,
                Location = new Point(CardInset, top + 22),
                Size = new Size(220, 20)
            };
            parent.Controls.Add(valueLabel);

            foreach (var button in buttons) parent.Controls.Add(button);
            Action layout = () =>
            {
                int buttonArea = buttons.Length == 0 ? 0 : 226;
                int textWidth = Math.Max(140, parent.Width - buttonArea - (CardInset * 2 + 10));
                captionLabel.Width = textWidth;
                valueLabel.Width = textWidth;

                int x = parent.Width - CardInset;
                for (int i = buttons.Length - 1; i >= 0; i--)
                {
                    int width = buttons[i].Text.Length > 8 ? 116 : 74;
                    x -= width;
                    buttons[i].Location = new Point(x, top + 8);
                    buttons[i].Size = new Size(width, 32);
                    x -= 8;
                }
            };
            parent.Resize += (s, e) => layout();
            layout();
            return valueLabel;
        }

        private void AddDivider(Control parent, int top)
        {
            var divider = new Panel
            {
                BackColor = line,
                Height = 1,
                Location = new Point(CardInset, top),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            parent.Controls.Add(divider);
            Action layout = () => divider.Width = Math.Max(80, parent.Width - (CardInset * 2));
            parent.Resize += (s, e) => layout();
            layout();
        }

        private void PlaceRow(Control parent, int top, int gap, int height, params PillButton[] buttons)
        {
            Action layout = () =>
            {
                int width = Math.Max(92, (parent.Width - (CardInset * 2) - (buttons.Length - 1) * gap) / buttons.Length);
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].Location = new Point(CardInset + i * (width + gap), top);
                    buttons[i].Size = new Size(width, height);
                }
            };
            parent.Resize += (s, e) => layout();
            foreach (var button in buttons) parent.Controls.Add(button);
            layout();
        }

        private Image LoadImageAsset(string relativePath)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
                if (!File.Exists(path)) return null;
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                using (var source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
            catch
            {
                return null;
            }
        }

        private Icon CreateTrayIcon()
        {
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            using (var bgBrush = new SolidBrush(Color.FromArgb(22, 27, 34)))
            using (var earBrush = new SolidBrush(blue))
            using (var bandPen = new Pen(blue, 3f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(bgBrush, 0, 0, 31, 31);
                g.DrawArc(bandPen, 8, 6, 16, 18, 205, 130);
                FillRound(g, earBrush, new Rectangle(7, 15, 7, 10), 3);
                FillRound(g, earBrush, new Rectangle(18, 15, 7, 10), 3);
            }
            IntPtr handle = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(handle).Clone();
            DestroyIcon(handle);
            bitmap.Dispose();
            return icon;
        }

        private static void FillRound(Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            using (var path = RoundedPath(bounds, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
        }

        private static int HexByte(string value)
        {
            return Convert.ToInt32(value, 16);
        }

        private static string FormatPowerDelay(int value)
        {
            switch (value)
            {
                case 0:
                    return "5 minutes";
                case 1:
                    return "30 minutes";
                case 2:
                    return "60 minutes";
                case 3:
                    return "180 minutes";
                case 4:
                    return "15 minutes";
                case 0x11:
                    return "Disabled";
                default:
                    return "Unknown";
            }
        }

        private static string FormatEqValue(int value)
        {
            int centered = value - 10;
            if (centered > 0) return "+" + centered;
            return centered.ToString();
        }

        private static string FormatEqSummary(int preset, int[] values)
        {
            string presetName = FormatEqPreset(preset);
            if (values == null || values.Length < 6) return presetName;
            return presetName + " / Clear Bass " + FormatEqValue(values[0]);
        }

        private static string FormatEqPreset(int value)
        {
            switch (value)
            {
                case 0x00:
                    return "Off";
                case 0x10:
                    return "Bright";
                case 0x11:
                    return "Excited";
                case 0x12:
                    return "Mellow";
                case 0x13:
                    return "Relaxed";
                case 0x14:
                    return "Vocal";
                case 0x15:
                    return "Treble";
                case 0x16:
                    return "Bass";
                case 0x17:
                    return "Speech";
                case 0xA0:
                    return "Manual";
                case 0xA1:
                    return "User 1";
                case 0xA2:
                    return "User 2";
                default:
                    return "0x" + value.ToString("X2");
            }
        }

        private static string FormatGeneralBoolean(int value)
        {
            switch (value)
            {
                case 0:
                    return "On";
                case 1:
                    return "Off";
                default:
                    return "0x" + value.ToString("X2");
            }
        }
    }

    internal sealed class EqPresetChoice
    {
        public string Text { get; private set; }
        public int Value { get; private set; }

        public EqPresetChoice(string text, int value)
        {
            Text = text;
            Value = value;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    internal sealed class ShortcutDialog : Form
    {
        private readonly ShortcutAction[] actions;
        private readonly Dictionary<string, HotkeyCaptureBox> boxes = new Dictionary<string, HotkeyCaptureBox>(StringComparer.OrdinalIgnoreCase);
        private readonly Color page;
        private readonly Color card;
        private readonly Color cardSoft;
        private readonly Color line;
        private readonly Color ink;
        private readonly Color subdued;
        private readonly Color blue;
        private readonly Color bluePressed;

        public Dictionary<string, ShortcutBinding> Bindings { get; private set; }

        public ShortcutDialog(ShortcutAction[] actions, Dictionary<string, ShortcutBinding> current, Color page, Color card, Color cardSoft, Color line, Color ink, Color subdued, Color blue, Color bluePressed)
        {
            this.actions = actions;
            this.page = page;
            this.card = card;
            this.cardSoft = cardSoft;
            this.line = line;
            this.ink = ink;
            this.subdued = subdued;
            this.blue = blue;
            this.bluePressed = bluePressed;
            Bindings = new Dictionary<string, ShortcutBinding>(StringComparer.OrdinalIgnoreCase);

            Text = "Keyboard shortcuts";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 600);
            BackColor = page;
            ForeColor = ink;
            Font = new Font("Segoe UI", 10f);
            Build(current);
        }

        private void Build(Dictionary<string, ShortcutBinding> current)
        {
            var title = new Label
            {
                Text = "Keyboard shortcuts",
                ForeColor = ink,
                Font = new Font("Segoe UI Semibold", 17f),
                AutoSize = true,
                Location = new Point(24, 22)
            };
            Controls.Add(title);

            var panel = new Panel
            {
                Location = new Point(24, 68),
                Size = new Size(592, 450),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AutoScroll = true,
                BackColor = card
            };
            Controls.Add(panel);

            for (int i = 0; i < actions.Length; i++)
            {
                ShortcutAction action = actions[i];
                int y = i * 44 + 12;
                var label = new Label
                {
                    Text = action.Label,
                    ForeColor = ink,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    Location = new Point(18, y + 7),
                    Size = new Size(250, 24),
                    AutoEllipsis = true
                };
                panel.Controls.Add(label);

                ShortcutBinding binding;
                if (!current.TryGetValue(action.Id, out binding) || binding == null) binding = ShortcutBinding.None();
                var box = new HotkeyCaptureBox(cardSoft, line, ink, subdued)
                {
                    Binding = binding.Clone(),
                    Location = new Point(278, y + 2),
                    Size = new Size(194, 30)
                };
                panel.Controls.Add(box);
                boxes[action.Id] = box;

                var clear = new PillButton("Clear", Color.FromArgb(61, 67, 76), Color.FromArgb(50, 55, 63))
                {
                    Location = new Point(486, y + 1),
                    Size = new Size(70, 31)
                };
                clear.Click += (s, e) => box.Binding = ShortcutBinding.None();
                panel.Controls.Add(clear);
            }

            var save = new PillButton("Save", blue, bluePressed)
            {
                Size = new Size(96, 34),
                Location = new Point(ClientSize.Width - 222, ClientSize.Height - 54),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            save.Click += (s, e) => SaveAndClose();
            Controls.Add(save);

            var cancel = new PillButton("Cancel", Color.FromArgb(61, 67, 76), Color.FromArgb(50, 55, 63))
            {
                Size = new Size(96, 34),
                Location = new Point(ClientSize.Width - 116, ClientSize.Height - 54),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            cancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancel);
        }

        private void SaveAndClose()
        {
            var signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var next = new Dictionary<string, ShortcutBinding>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < actions.Length; i++)
            {
                HotkeyCaptureBox box = boxes[actions[i].Id];
                ShortcutBinding binding = box.Binding == null ? ShortcutBinding.None() : box.Binding.Clone();
                if (binding.IsAssigned && !binding.IsGlobalSafe)
                {
                    MessageBox.Show("Use a modifier key, or use an F-key.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    box.Focus();
                    return;
                }
                if (binding.IsAssigned && !signatures.Add(binding.Signature))
                {
                    MessageBox.Show("That shortcut is assigned more than once.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    box.Focus();
                    return;
                }
                next[actions[i].Id] = binding;
            }

            Bindings = next;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class HotkeyCaptureBox : TextBox
    {
        private ShortcutBinding binding;
        private readonly Color emptyColor;
        private readonly Color textColor;

        public ShortcutBinding Binding
        {
            get { return binding; }
            set
            {
                binding = value == null ? ShortcutBinding.None() : value;
                UpdateText();
            }
        }

        public HotkeyCaptureBox(Color background, Color border, Color ink, Color subdued)
        {
            binding = ShortcutBinding.None();
            emptyColor = subdued;
            textColor = ink;
            BackColor = background;
            ForeColor = subdued;
            BorderStyle = BorderStyle.FixedSingle;
            ReadOnly = true;
            ShortcutsEnabled = false;
            Font = new Font("Segoe UI", 9.5f);
            TextAlign = HorizontalAlignment.Center;
            TabStop = true;
            UpdateText();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Tab) return base.ProcessCmdKey(ref msg, keyData);
            if (key == Keys.Back || key == Keys.Delete || key == Keys.Escape)
            {
                Binding = ShortcutBinding.None();
                return true;
            }
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
            {
                return true;
            }

            bool ctrl = (keyData & Keys.Control) == Keys.Control;
            bool alt = (keyData & Keys.Alt) == Keys.Alt;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;
            if (!ctrl && !alt && !shift && !ShortcutBinding.IsFunctionKey(key))
            {
                System.Media.SystemSounds.Beep.Play();
                return true;
            }

            Binding = new ShortcutBinding(ctrl, alt, shift, false, key);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            base.OnKeyDown(e);
        }

        private void UpdateText()
        {
            Text = binding == null ? "None" : binding.ToString();
            ForeColor = binding != null && binding.IsAssigned ? textColor : emptyColor;
        }
    }

    internal sealed class ChoiceDropdown : Control
    {
        private readonly List<object> items = new List<object>();
        private int selectedIndex = -1;
        private bool hover;
        private bool down;

        public event EventHandler SelectedIndexChanged;

        public ChoiceItemCollection Items { get; private set; }
        public ComboBoxStyle DropDownStyle { get; set; }
        public FlatStyle FlatStyle { get; set; }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                int next = value < -1 ? -1 : value;
                if (next >= items.Count) next = items.Count - 1;
                if (selectedIndex == next) return;
                selectedIndex = next;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null; }
        }

        public ChoiceDropdown()
        {
            Items = new ChoiceItemCollection(this);
            Size = new Size(170, 30);
            Font = new Font("Segoe UI", 9.3f);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        internal void AddItem(object item)
        {
            items.Add(item);
            if (selectedIndex == -1) selectedIndex = 0;
            Invalidate();
        }

        internal object GetItem(int index)
        {
            return items[index];
        }

        internal int Count
        {
            get { return items.Count; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                down = true;
                Focus();
                Invalidate();
                ShowMenu();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            down = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                ShowMenu();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && selectedIndex < items.Count - 1)
            {
                SelectedIndex++;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up && selectedIndex > 0)
            {
                SelectedIndex--;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Enabled ? (down ? Blend(BackColor, Color.Black, 0.12f) : (hover ? Blend(BackColor, Color.White, 0.05f) : BackColor)) : Color.FromArgb(48, 51, 57);
            Color border = Focused ? Color.FromArgb(16, 157, 245) : Color.FromArgb(82, 88, 98);
            using (var path = RoundedPath(bounds, 6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            Rectangle textBounds = new Rectangle(12, 1, Math.Max(10, Width - 38), Height - 2);
            string text = SelectedItem == null ? "" : SelectedItem.ToString();
            TextRenderer.DrawText(e.Graphics, text, Font, textBounds, Enabled ? ForeColor : Color.FromArgb(135, 140, 148), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            Point[] arrow =
            {
                new Point(Width - 21, Height / 2 - 2),
                new Point(Width - 11, Height / 2 - 2),
                new Point(Width - 16, Height / 2 + 4)
            };
            using (var brush = new SolidBrush(Enabled ? Color.FromArgb(210, 216, 224) : Color.FromArgb(120, 126, 134)))
            {
                e.Graphics.FillPolygon(brush, arrow);
            }
        }

        private void ShowMenu()
        {
            if (items.Count == 0 || IsDisposed || !IsHandleCreated) return;
            var menu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(36, 38, 42),
                ForeColor = Color.White,
                ShowImageMargin = false,
                Padding = new Padding(4)
            };
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(items[i].ToString())
                {
                    Checked = index == selectedIndex,
                    BackColor = Color.FromArgb(36, 38, 42),
                    ForeColor = Color.White
                };
                item.Click += (s, e) => SelectedIndex = index;
                menu.Items.Add(item);
            }
            menu.Closed += (s, e) =>
            {
                down = false;
                if (!IsDisposed) Invalidate();
                if (!IsDisposed && IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!menu.IsDisposed) menu.Dispose();
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            };
            try
            {
                menu.Show(this, new Point(0, Height + 3));
            }
            catch
            {
                menu.Dispose();
            }
        }

        private static Color Blend(Color a, Color b, float amount)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            if (radius == 0)
            {
                var rectangle = new GraphicsPath();
                rectangle.AddRectangle(bounds);
                rectangle.CloseFigure();
                return rectangle;
            }
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal sealed class ChoiceItemCollection
        {
            private readonly ChoiceDropdown owner;

            public ChoiceItemCollection(ChoiceDropdown owner)
            {
                this.owner = owner;
            }

            public int Count
            {
                get { return owner.Count; }
            }

            public object this[int index]
            {
                get { return owner.GetItem(index); }
            }

            public void AddRange(object[] values)
            {
                foreach (object value in values) owner.AddItem(value);
            }
        }
    }

    internal sealed class SliderControl : Control
    {
        private int minimum;
        private int maximum = 100;
        private int value;
        private bool dragging;

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                if (maximum < minimum) maximum = minimum;
                Value = this.value;
                Invalidate();
            }
        }

        public int Maximum
        {
            get { return maximum; }
            set
            {
                maximum = Math.Max(minimum, value);
                Value = this.value;
                Invalidate();
            }
        }

        public int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(minimum, Math.Min(maximum, value));
                if (this.value == next) return;
                this.value = next;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public int TickFrequency { get; set; }
        public int SmallChange { get; set; }
        public int LargeChange { get; set; }
        public bool CenteredFill { get; set; }
        public Color TrackColor { get; set; }
        public Color FillColor { get; set; }
        public Color ThumbColor { get; set; }
        public Color TickColor { get; set; }

        public SliderControl()
        {
            Size = new Size(220, 34);
            TickFrequency = 5;
            SmallChange = 1;
            LargeChange = 2;
            TrackColor = Color.FromArgb(78, 84, 94);
            FillColor = Color.FromArgb(16, 157, 245);
            ThumbColor = Color.FromArgb(16, 157, 245);
            TickColor = Color.FromArgb(91, 98, 108);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Enabled)
            {
                dragging = true;
                Capture = true;
                Focus();
                SetValueFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetValueFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            Capture = false;
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
            {
                Value -= Math.Max(1, SmallChange);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                Value += Math.Max(1, SmallChange);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                Value -= Math.Max(1, LargeChange);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                Value += Math.Max(1, LargeChange);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(12, Height / 2 - 3, Math.Max(8, Width - 24), 6);
            float ratio = maximum == minimum ? 0f : (Value - minimum) / (float)(maximum - minimum);
            int thumbX = track.Left + (int)Math.Round(track.Width * ratio);
            int fillStart = CenteredFill ? track.Left + track.Width / 2 : track.Left;
            int fillLeft = Math.Min(fillStart, thumbX);
            int fillRight = Math.Max(fillStart, thumbX);

            using (var trackBrush = new SolidBrush(Enabled ? TrackColor : Color.FromArgb(54, 58, 65)))
            using (var fillBrush = new SolidBrush(Enabled ? FillColor : Color.FromArgb(85, 90, 98)))
            using (var thumbBrush = new SolidBrush(Enabled ? ThumbColor : Color.FromArgb(95, 101, 110)))
            using (var thumbPen = new Pen(Color.FromArgb(26, 28, 31), 2f))
            {
                FillRound(e.Graphics, trackBrush, track, 3);
                if (fillRight > fillLeft)
                {
                    FillRound(e.Graphics, fillBrush, new Rectangle(fillLeft, track.Top, fillRight - fillLeft, track.Height), 3);
                }

                if (TickFrequency > 0 && maximum > minimum)
                {
                    using (var tickPen = new Pen(TickColor, 1f))
                    {
                        for (int tick = minimum; tick <= maximum; tick += TickFrequency)
                        {
                            float tickRatio = (tick - minimum) / (float)(maximum - minimum);
                            int x = track.Left + (int)Math.Round(track.Width * tickRatio);
                            e.Graphics.DrawLine(tickPen, x, track.Bottom + 6, x, track.Bottom + 9);
                        }
                    }
                }

                Rectangle thumb = new Rectangle(thumbX - 7, Height / 2 - 9, 14, 18);
                FillRound(e.Graphics, thumbBrush, thumb, 6);
                using (var path = RoundedPath(thumb, 6))
                {
                    e.Graphics.DrawPath(thumbPen, path);
                }
            }
        }

        private void SetValueFromX(int x)
        {
            int left = 12;
            int width = Math.Max(1, Width - 24);
            float ratio = (Math.Max(left, Math.Min(left + width, x)) - left) / (float)width;
            Value = minimum + (int)Math.Round((maximum - minimum) * ratio);
        }

        private static void FillRound(Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            using (var path = RoundedPath(bounds, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            if (radius == 0)
            {
                var rectangle = new GraphicsPath();
                rectangle.AddRectangle(bounds);
                rectangle.CloseFigure();
                return rectangle;
            }
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class DeviceGlyphControl : Control
    {
        public Color BandColor { get; set; }
        public Color AccentColor { get; set; }
        public Color SoftColor { get; set; }
        public Color MutedColor { get; set; }

        public DeviceGlyphControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BandColor = Color.FromArgb(16, 157, 245);
            AccentColor = Color.FromArgb(16, 157, 245);
            SoftColor = Color.FromArgb(58, 64, 72);
            MutedColor = Color.FromArgb(169, 169, 169);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = ClientRectangle;
            bounds.Inflate(-4, -4);
            if (bounds.Width < 80 || bounds.Height < 70) return;

            int centerX = bounds.Left + bounds.Width / 2;
            int top = bounds.Top + 8;
            int cupW = Math.Max(24, bounds.Width / 4);
            int cupH = Math.Max(42, bounds.Height / 2);
            int cupY = bounds.Bottom - cupH - 10;
            var leftCup = new Rectangle(bounds.Left + 14, cupY, cupW, cupH);
            var rightCup = new Rectangle(bounds.Right - cupW - 14, cupY, cupW, cupH);
            var arcRect = new Rectangle(bounds.Left + 24, top, bounds.Width - 48, bounds.Height - 22);

            using (var shadow = new SolidBrush(Color.FromArgb(55, Color.Black)))
            using (var soft = new SolidBrush(SoftColor))
            using (var glow = new SolidBrush(Color.FromArgb(42, AccentColor)))
            using (var band = new Pen(BandColor, 5.2f))
            using (var innerBand = new Pen(Color.FromArgb(120, AccentColor), 2.1f))
            using (var cupEdge = new Pen(Color.FromArgb(95, 104, 116), 1.2f))
            using (var pad = new SolidBrush(Color.FromArgb(66, BandColor)))
            {
                e.Graphics.FillEllipse(glow, centerX - bounds.Width / 3, top + 10, bounds.Width * 2 / 3, bounds.Height - 12);
                e.Graphics.DrawArc(band, arcRect, 205, 130);
                e.Graphics.DrawArc(innerBand, new Rectangle(arcRect.Left + 12, arcRect.Top + 12, arcRect.Width - 24, arcRect.Height - 24), 210, 120);

                FillRound(e.Graphics, shadow, new Rectangle(leftCup.Left + 2, leftCup.Top + 3, leftCup.Width, leftCup.Height), 10);
                FillRound(e.Graphics, shadow, new Rectangle(rightCup.Left + 2, rightCup.Top + 3, rightCup.Width, rightCup.Height), 10);
                FillRound(e.Graphics, soft, leftCup, 10);
                FillRound(e.Graphics, soft, rightCup, 10);
                using (var leftPath = RoundedPath(leftCup, 10))
                using (var rightPath = RoundedPath(rightCup, 10))
                {
                    e.Graphics.DrawPath(cupEdge, leftPath);
                    e.Graphics.DrawPath(cupEdge, rightPath);
                }

                FillRound(e.Graphics, pad, new Rectangle(leftCup.Left + 6, leftCup.Top + 8, leftCup.Width - 12, leftCup.Height - 16), 7);
                FillRound(e.Graphics, pad, new Rectangle(rightCup.Left + 6, rightCup.Top + 8, rightCup.Width - 12, rightCup.Height - 16), 7);
            }
        }

        private static void FillRound(Graphics g, Brush brush, Rectangle bounds, int radius)
        {
            using (var path = RoundedPath(bounds, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            if (radius == 0)
            {
                var rectangle = new GraphicsPath();
                rectangle.AddRectangle(bounds);
                rectangle.CloseFigure();
                return rectangle;
            }
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class EqCurveControl : Control
    {
        private int preset = 0xA0;
        private int[] values = new int[] { 10, 10, 10, 10, 10, 10 };

        public Color LineColor { get; set; }
        public Color BassColor { get; set; }
        public Color GridColor { get; set; }
        public Color MutedColor { get; set; }

        public EqCurveControl()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            LineColor = Color.FromArgb(16, 157, 245);
            BassColor = Color.FromArgb(16, 157, 245);
            GridColor = Color.FromArgb(76, 76, 76);
            MutedColor = Color.FromArgb(169, 169, 169);
            Font = new Font("Segoe UI", 8.6f);
        }

        public void SetState(int presetValue, int[] bandValues)
        {
            preset = presetValue;
            int[] next = new int[6];
            for (int i = 0; i < next.Length; i++)
            {
                int value = bandValues != null && i < bandValues.Length ? bandValues[i] : 10;
                next[i] = Math.Max(0, Math.Min(20, value));
            }
            values = next;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle plot = new Rectangle(58, 26, Math.Max(80, Width - 92), Math.Max(56, Height - 62));
            int top = plot.Top;
            int mid = plot.Top + plot.Height / 2;
            int bottom = plot.Bottom;

            using (var gridPen = new Pen(GridColor, 1f))
            using (var zeroPen = new Pen(Color.FromArgb(135, 135, 135), 1.4f))
            using (var textBrush = new SolidBrush(MutedColor))
            using (var linePen = new Pen(LineColor, 2.8f))
            using (var fillBrush = new SolidBrush(Color.FromArgb(36, LineColor)))
            using (var pointBrush = new SolidBrush(LineColor))
            {
                e.Graphics.DrawLine(gridPen, plot.Left, top, plot.Right, top);
                e.Graphics.DrawLine(zeroPen, plot.Left, mid, plot.Right, mid);
                e.Graphics.DrawLine(gridPen, plot.Left, bottom, plot.Right, bottom);

                TextRenderer.DrawText(e.Graphics, "+10", Font, new Rectangle(4, top - 8, 48, 18), MutedColor, TextFormatFlags.Right | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, "0", Font, new Rectangle(4, mid - 8, 48, 18), MutedColor, TextFormatFlags.Right | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, "-10", Font, new Rectangle(4, bottom - 18, 48, 18), MutedColor, TextFormatFlags.Right | TextFormatFlags.NoPadding);

                string[] labels = { "400", "1k", "2.5k", "6.3k", "16k" };
                PointF[] points = new PointF[5];
                for (int i = 0; i < points.Length; i++)
                {
                    float x = points.Length == 1 ? plot.Left : plot.Left + (plot.Width * i / (float)(points.Length - 1));
                    float y = bottom - (plot.Height * values[i + 1] / 20f);
                    points[i] = new PointF(x, y);
                    e.Graphics.FillEllipse(pointBrush, x - 4, y - 4, 8, 8);
                    TextRenderer.DrawText(e.Graphics, labels[i], Font, new Rectangle((int)x - 28, bottom + 2, 56, 18), MutedColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                }

                if (points.Length > 1)
                {
                    using (var fillPath = new GraphicsPath())
                    {
                        fillPath.AddLines(points);
                        fillPath.AddLine(points[points.Length - 1].X, mid, points[0].X, mid);
                        fillPath.CloseFigure();
                        e.Graphics.FillPath(fillBrush, fillPath);
                    }
                    e.Graphics.DrawLines(linePen, points);
                }

                string presetName = FormatEqPreset(preset);
                TextRenderer.DrawText(e.Graphics, presetName, new Font("Segoe UI Semibold", 9f), new Rectangle(plot.Left, 2, plot.Width, 22), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static string FormatEqValue(int value)
        {
            int centered = value - 10;
            return centered > 0 ? "+" + centered : centered.ToString();
        }

        private static string FormatEqPreset(int value)
        {
            switch (value)
            {
                case 0x00: return "Off";
                case 0x10: return "Bright";
                case 0x11: return "Excited";
                case 0x12: return "Mellow";
                case 0x13: return "Relaxed";
                case 0x14: return "Vocal";
                case 0x15: return "Treble";
                case 0x16: return "Bass";
                case 0x17: return "Speech";
                case 0xA0: return "Manual";
                case 0xA1: return "User 1";
                case 0xA2: return "User 2";
                default: return "0x" + value.ToString("X2");
            }
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class CardPanel : Panel
    {
        public int Radius { get; set; }
        public Color BorderColor { get; set; }

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using (var brush = new SolidBrush(BackColor))
            using (var pen = new Pen(BorderColor))
            using (var path = RoundedPath(bounds, Radius))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRoundedRegion();
            Invalidate();
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            UpdateRoundedRegion();
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            Rectangle bounds = new Rectangle(0, 0, Width, Height);
            using (var path = RoundedPath(bounds, Radius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            if (radius == 0)
            {
                var rectangle = new GraphicsPath();
                rectangle.AddRectangle(bounds);
                rectangle.CloseFigure();
                return rectangle;
            }
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class PillButton : Button
    {
        private Color normal;
        private Color pressed;
        private bool hover;
        private bool down;

        public PillButton(string caption, Color normal, Color pressed)
        {
            Text = caption;
            this.normal = normal;
            this.pressed = pressed;
            ForeColor = TextColorFor(normal);
            Font = new Font("Segoe UI Semibold", 9.2f);
            FlatStyle = FlatStyle.Flat;
            BackColor = Color.Transparent;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        }

        public void SetPalette(Color normalColor, Color pressedColor)
        {
            normal = normalColor;
            pressed = pressedColor;
            ForeColor = TextColorFor(normalColor);
            Invalidate();
        }

        public static Color TextColorFor(Color background)
        {
            double luminance = RelativeLuminance(background);
            return luminance > 0.26 ? Color.FromArgb(5, 16, 24) : Color.FromArgb(238, 242, 246);
        }

        private static double RelativeLuminance(Color color)
        {
            double r = LinearChannel(color.R / 255.0);
            double g = LinearChannel(color.G / 255.0);
            double b = LinearChannel(color.B / 255.0);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double LinearChannel(double value)
        {
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            down = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            down = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(Parent != null ? Parent.BackColor : Color.Transparent);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            Color fill = Enabled ? (down ? pressed : (hover ? Blend(normal, Color.White, 0.08f) : normal)) : Color.FromArgb(50, 50, 50);
            Color border = Enabled ? Blend(fill, Color.Black, 0.06f) : Color.FromArgb(68, 68, 68);
            using (var path = RoundedPath(bounds, 6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                pevent.Graphics.FillPath(brush, path);
                pevent.Graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(pevent.Graphics, Text, Font, bounds, Enabled ? ForeColor : Color.FromArgb(145, 145, 145), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private static Color Blend(Color a, Color b, float amount)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

}
