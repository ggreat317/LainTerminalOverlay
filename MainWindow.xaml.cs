using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace TerminalOverlay
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;

        const uint GW_HWNDPREV = 3;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int GWL_EXSTYLE = -20;

        private nint handle;
        private double originalHeight;
        private double originalWidth;

        public MainWindow()
        {
            SetCurrentProcessExplicitAppUserModelID("TerminalOverlay.HUD.App");
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            InitializeComponent();
            StartClock();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Hide();
            LoadGif();

            handle = new WindowInteropHelper(this).Handle;

            originalHeight = Height;
            originalWidth = Width;


            MediaPlayer player = new MediaPlayer();

            player.Open(new Uri("assets/lain.mp3", UriKind.Relative));
            player.Play();

        }

        private void MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.Opacity = 0.3;
        }
        private void MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.Opacity = 0.9;
        }

        private void cantClickMe()
        {
            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(
                handle,
                GWL_EXSTYLE, 
                exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED
            );
        }

        private void canClickMe()
        {
            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(
                handle,
                GWL_EXSTYLE,
                exStyle | WS_EX_LAYERED
            );
        }

        private void StartClock()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {

            var proc = Process.GetProcessesByName("WindowsTerminal").FirstOrDefault();
            
            if (proc == null) {
                return;
            }

            proc.Refresh();

            // IntPtr terminalHandle = FindWindow(null, "Windows Terminal");
            IntPtr terminalHandle = proc.MainWindowHandle;

            if (terminalHandle == IntPtr.Zero)
            {
                this.Hide();
                return;
            }

            
            if (!GetWindowRect(terminalHandle, out RECT tmp))
            {
                this.Hide();
                return;
            }

            if (IsIconic(terminalHandle))
            {
                this.Hide();
                return;
            }

            if (this.Visibility != Visibility.Visible)
            {
                this.Show();
            }

            IntPtr above = GetWindow(terminalHandle, GW_HWNDPREV);

            SetWindowPos(
                handle,
                above,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
            );

            RECT rect;
            DwmGetWindowAttribute(terminalHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());

            double scale = GetScale(terminalHandle);

            //System.Diagnostics.Debug.WriteLine(
            //    $"Scale: {scale}"
            //);

            Height = originalHeight / scale;
            Width = originalWidth / scale;

            Left = ( rect.Right / scale ) - this.Width;
            Top = ( rect.Bottom / scale ) - this.Height;

        }

        private void LoadGif()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "assets",
                "lain.gif"
            );

            if (!File.Exists(path))
            {
                MessageBox.Show("GIF not found:\n" + path);
                return;
            }

            var gif = new BitmapImage();

            gif.BeginInit();
            gif.UriSource = new Uri(path);
            gif.EndInit();

            ImageBehavior.SetAnimatedSource(OverlayImage, gif);
        }

        private double GetScale(IntPtr hwnd)
        {
            int dpi = GetDpiForWindow(hwnd);
            return dpi / 96.0; // 96 = 100% scaling baseline
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            out RECT pvAttribute,
            int cbAttribute);

        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern int GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

        static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);


        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("shell32.dll")]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

    public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }


    }
}