using System;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
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
        private WinEventDelegate _proc;
        private IntPtr _hook;

        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint WINEVENT_OUTOFCONTEXT = 0;
        public const int OBJID_WINDOW = 0;
        const uint GW_HWNDPREV = 3;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int GWL_EXSTYLE = -20;

        private nint myHandle;

        private double originalHeight;
        private double originalWidth;

        private nint parentHandle = IntPtr.Zero;
        private bool dragging = false;
        private double myDragStartTop = 0;
        private double myDragStartLeft = 0;
        private double mouseStartTop = 0;
        private double mouseStartLeft = 0;
        private bool initDrag = false;
        private Cursor grab = new Cursor("Assets/grab.cur");
        private Cursor grabbing = new Cursor("Assets/grabbing.cur");

        public MainWindow()
        {
            SetCurrentProcessExplicitAppUserModelID("TerminalOverlay.HUD.App");
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            InitializeComponent();
            StartClock();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadGif();

            myHandle = new WindowInteropHelper(this).Handle;

            originalHeight = Height;
            originalWidth = Width;


            MediaPlayer player = new MediaPlayer();

            player.Open(new Uri("assets/lain.mp3", UriKind.Relative));
            player.Play();

            _proc = WinEventProc;

            // calls winEventProc on any window moving
            _hook = SetWinEventHook(
                EVENT_OBJECT_LOCATIONCHANGE,
                EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                _proc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            this.Hide();
        }


        // checks if window moving is terminal recorded and moves overlay if so
        private void WinEventProc(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
            )
        {

            if (hwnd != parentHandle)
            {
                return;
            }

            moveWindow();
        }

        // to cut off the listener
        protected override void OnClosed(EventArgs e)
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
            }

            base.OnClosed(e);
        }

        private void MouseDown(object sender, MouseButtonEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine(
            //    $"Mouse Down ( Activation ), Dragging: {dragging}"
            //);
            dragging = true;
            Cursor = grabbing;
            initDrag = true;
            Mouse.Capture((IInputElement)sender);
        }

        private void muPrevAdapter(object sender, MouseButtonEventArgs e)
        {
//            System.Diagnostics.Debug.WriteLine(
//$"Mouse Up, Dragging: {dragging}"
//);
            MouseUp();
        }

        private void muLostAdapter(object sender, System.Windows.Input.MouseEventArgs e)
        {
//            System.Diagnostics.Debug.WriteLine(
//    $"Lost Mouse, Dragging: {dragging}"
//);
            MouseUp();
        }

        private void MouseUp()
        {
            //System.Diagnostics.Debug.WriteLine(
            //    $"Mouse Up, Dragging: {dragging}"
            //);
            if (dragging)
            {
                Cursor = grab;
                dragging = false;
                initDrag = false;
            }
            Mouse.Capture(null);
            moveWindow();
        }

        private void MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine(
            //    $"Mouse Enter, Dragging: ${dragging}"
            //);
            Cursor = grab;
            this.Opacity = 0.3;
        }

        private void MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine(
            //    $"Mouse Leave, Dragging: ${dragging}"
            //);
            Cursor = Cursors.Arrow;
            this.Opacity = 0.9;
        }

        private void MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine(
            //    $"Mouse Move, Dragging: {dragging}"
            //);
            if (!dragging)
            {
                return;
            }

            GetCursorPos(out POINT mousePoint);
            if (initDrag)
            {
                mouseStartLeft = mousePoint.X;
                mouseStartTop = mousePoint.Y;
                myDragStartLeft = Left;
                myDragStartTop = Top;
                initDrag = false;
            }

            double scale = GetScale(myHandle);
            Top = myDragStartTop + ( mousePoint.Y - mouseStartTop ) / scale;
            Left = myDragStartLeft + ( mousePoint.X - mouseStartLeft ) / scale;

            //System.Diagnostics.Debug.WriteLine(
            //    $"Mouse Move, Top: {Top}, Left: {Left}, mydst: {myDragStartTop}," +
            //    $"mydsl: {myDragStartLeft}, modst: {mouseStartTop}," +
            //    $"modsl: {mouseStartLeft} "
            //);
        }

        private void cantClickMe()
        {
            int exStyle = GetWindowLong(myHandle, GWL_EXSTYLE);
            SetWindowLong(
                myHandle,
                GWL_EXSTYLE, 
                exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED
            );
        }

        private void canClickMe()
        {
            int exStyle = GetWindowLong(myHandle, GWL_EXSTYLE);
            SetWindowLong(
                myHandle,
                GWL_EXSTYLE,
                exStyle | WS_EX_LAYERED
            );
        }

        private void StartClock()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200);
            _timer.Tick += mwAdapter;
            _timer.Start();
        }

        private void mwAdapter(object sender, EventArgs e)
        {
            moveWindow();
        }

        private void moveWindow()
        {
            if (dragging)
            {
                return;
            }

            var proc = Process.GetProcessesByName("WindowsTerminal").FirstOrDefault();
            
            if (proc == null) {
                this.Hide();
                return;
            }

            proc.Refresh();

            // IntPtr terminalHandle = FindWindow(null, "Windows Terminal");
            parentHandle = proc.MainWindowHandle;

            if (parentHandle == IntPtr.Zero)
            {
                this.Hide();
                return;
            }
            
            if (!GetWindowRect(parentHandle, out RECT tmp))
            {
                this.Hide();
                return;
            }

            if (IsIconic(parentHandle))
            {
                this.Hide();
                return;
            }

            if (this.Visibility != Visibility.Visible)
            {
                this.Show();
            }

            IntPtr above = GetWindow(parentHandle, GW_HWNDPREV);

            SetWindowPos(
                parentHandle,
                above,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
            );

            RECT rect;
            DwmGetWindowAttribute(parentHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());

            double scale = GetScale(parentHandle);

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

        public delegate void WinEventDelegate(
             IntPtr hWinEventHook,
             uint eventType,
             IntPtr hwnd,
             int idObject,
             int idChild,
             uint dwEventThread,
             uint dwmsEventTime
        );

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags
        );

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

    public struct POINT
    {
        public int X;
        public int Y;
    }

    }
}