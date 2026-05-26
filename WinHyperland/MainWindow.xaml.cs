using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WinHyperland
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int HTCLIENT = 1;
        private const int HTTRANSPARENT = -1;

        private SettingsService? _settings;
        private IntPtr _hwnd;
        private DispatcherTimer? _topmostTimer;
        private HyperlandController? _controller;

        public HyperlandController? Controller
        {
            get => _controller;
            set => _controller = value;
        }

        public MainWindow()
        {
            InitializeComponent();
            this.Deactivated += MainWindow_Deactivated;
        }

        public MainWindow(SettingsService settings) : this()
        {
            _settings = settings;
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            if (_controller != null && _settings != null && _settings.ClickOutsideToCollapse)
            {
                if (_controller.CurrentState == HyperlandState.Expanded)
                {
                    _controller.TransitionTo(_controller.MediaActive ? HyperlandState.Compact : HyperlandState.Idle);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            // Set WS_EX_LAYERED (already done by AllowsTransparency), ensure not transparent
            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED;
            exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);

            // Subclass WndProc for hit-testing
            HwndSource source = HwndSource.FromHwnd(_hwnd);
            source.AddHook(WndProc);

            PositionTopCenter();
            SetAlwaysOnTop();

            // Always-on-top heartbeat
            _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _topmostTimer.Tick += (_, _) => SetAlwaysOnTop();
            _topmostTimer.Start();

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => PositionTopCenter();
        }

        private void SetAlwaysOnTop()
        {
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private System.Windows.Forms.Screen GetTargetScreen(SettingsService settings)
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            string targetName = settings.TargetMonitor;

            if (targetName == "Active")
            {
                POINT mousePt;
                GetCursorPos(out mousePt);
                return System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(mousePt.X, mousePt.Y));
            }
            
            if (targetName == "Primary")
            {
                return System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
            }

            foreach (var s in screens)
            {
                if (s.DeviceName == targetName)
                    return s;
            }

            return System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
        }

        private void PositionTopCenter()
        {
            if (_settings != null)
            {
                ApplyPositionSettings(_settings);
                return;
            }

            POINT mousePt;
            GetCursorPos(out mousePt);
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(mousePt.X, mousePt.Y));

            this.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - this.Width) / 2;
            this.Top = screen.WorkingArea.Top;
        }

        public void ApplyPositionSettings(SettingsService settings)
        {
            try
            {
                var screen = GetTargetScreen(settings);
                double left = 0;
                switch (settings.ScreenPosition)
                {
                    case ScreenPosition.TopLeft:
                        left = screen.WorkingArea.Left + 20 + settings.CustomX;
                        break;
                    case ScreenPosition.TopRight:
                        left = screen.WorkingArea.Right - this.Width - 20 - settings.CustomX;
                        break;
                    case ScreenPosition.Custom:
                        left = screen.WorkingArea.Left + settings.CustomX;
                        break;
                    case ScreenPosition.TopCenter:
                    default:
                        left = screen.WorkingArea.Left + (screen.WorkingArea.Width - this.Width) / 2 + settings.CustomX;
                        break;
                }

                // Subtract 12 to compensate for the visual Margin="0,12,0,0" of KeylineBorder,
                // so that when VerticalOffset is 0, the pill visually touches the exact edge of the monitor.
                double top = screen.WorkingArea.Top + settings.PillVerticalOffset - 12;

                System.IO.File.AppendAllText("d:\\Antigravity\\debug_settings.log", 
                    $"ApplyPositionSettings: Position={settings.ScreenPosition}, CustomX={settings.CustomX}, VerticalOffset={settings.PillVerticalOffset}, TargetMonitor={settings.TargetMonitor}, screenDevice={screen.DeviceName}, calculated (Left={left}, Top={top})\n");

                this.Left = left;
                this.Top = top;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("d:\\Antigravity\\debug_settings.log", $"Error in ApplyPositionSettings: {ex}\n");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST && _controller != null)
            {
                int x = (short)(lParam.ToInt64() & 0xFFFF);
                int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                var pillRect = GetPillScreenRect();
                if (pillRect.Contains(new System.Windows.Point(x, y)))
                {
                    handled = true;
                    return new IntPtr(HTCLIENT);
                }

                // Outside pill: click-through
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }
            else if (msg == WM_LBUTTONDOWN && _controller != null)
            {
                int x = (short)(lParam.ToInt64() & 0xFFFF);
                int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

                var pillRect = GetPillScreenRect();
                if (!pillRect.Contains(new System.Windows.Point(x, y)) &&
                    _controller.CurrentState == HyperlandState.Expanded)
                {
                    Dispatcher.BeginInvoke(() =>
                        _controller.TransitionTo(HyperlandState.Compact));
                }
            }

            return IntPtr.Zero;
        }

        
        public Rect GetPillScreenRect()
        {
            try
            {
                var transform = HyperlandPill.TransformToAncestor(this);
                var pillTopLeft = transform.Transform(new System.Windows.Point(0, 0));
                var pillSize = new System.Windows.Size(HyperlandPill.ActualWidth, HyperlandPill.ActualHeight);

                // Convert WPF coordinates to screen (handles DPI)
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    var m = source.CompositionTarget.TransformToDevice;
                    var screenTopLeft = m.Transform(pillTopLeft);
                    var screenSize = new System.Windows.Size(
                        pillSize.Width * m.M11,
                        pillSize.Height * m.M22);

                    return new Rect(
                        this.Left * m.M11 + screenTopLeft.X,
                        this.Top * m.M22 + screenTopLeft.Y,
                        screenSize.Width,
                        screenSize.Height);
                }
            }
            catch { }

            // Fallback
            return new Rect(this.Left, this.Top, this.Width, this.Height);
        }

        // Transport button click handlers
        public event Action? OnPreviousClicked;
        public event Action? OnPlayPauseClicked;
        public event Action? OnNextClicked;

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => OnPreviousClicked?.Invoke();
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e) => OnPlayPauseClicked?.Invoke();
        private void NextButton_Click(object sender, RoutedEventArgs e) => OnNextClicked?.Invoke();
    }
}
