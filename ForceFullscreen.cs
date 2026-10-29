using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class Win32
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWL32(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] static extern IntPtr GetWL64(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWL32(IntPtr h, int i, int v);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWL64(IntPtr h, int i, IntPtr v);

    public static long GetLong(IntPtr h, int i)
    { return IntPtr.Size == 8 ? GetWL64(h, i).ToInt64() : GetWL32(h, i); }
    public static void SetLong(IntPtr h, int i, long v)
    { if (IntPtr.Size == 8) SetWL64(h, i, new IntPtr(v)); else SetWL32(h, i, (int)v); }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int L, T, R, B; }

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_SYSMENU = 0x00080000,
        WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000;
    public const long WS_EX_DLGMODALFRAME = 0x1, WS_EX_CLIENTEDGE = 0x200, WS_EX_STATICEDGE = 0x20000, WS_EX_WINDOWEDGE = 0x100;
    public const uint SWP_FRAMECHANGED = 0x20, SWP_NOOWNERZORDER = 0x200, SWP_SHOWWINDOW = 0x40, SWP_NOZORDER = 0x4;
    public const int SW_RESTORE = 9;
    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
}

class Saved
{
    public long Style, ExStyle;
    public Win32.RECT Rect;
    public bool WasMaximized;
}

class MainForm : Form
{
    const int HOTKEY_ID = 1;
    const int WM_HOTKEY = 0x0312;

    ListBox list = new ListBox();
    Button btnToggle = new Button(), btnRefresh = new Button();
    Label hint = new Label();
    List<IntPtr> handles = new List<IntPtr>();
    Dictionary<IntPtr, Saved> saved = new Dictionary<IntPtr, Saved>();

    public MainForm()
    {
        Text = "Force Fullscreen";
        Size = new Size(520, 480);
        StartPosition = FormStartPosition.CenterScreen;

        list.Dock = DockStyle.Fill;
        list.HorizontalScrollbar = true;

        btnToggle.Text = "На весь экран / Вернуть";
        btnToggle.Dock = DockStyle.Bottom; btnToggle.Height = 40;
        btnToggle.Click += delegate { if (list.SelectedIndex >= 0) Toggle(handles[list.SelectedIndex]); };

        btnRefresh.Text = "Обновить список";
        btnRefresh.Dock = DockStyle.Bottom; btnRefresh.Height = 32;
        btnRefresh.Click += delegate { Refill(); };

        hint.Text = "Выбери окно игры и нажми кнопку. Или в игре жми Ctrl+Alt+F (работает везде).";
        hint.Dock = DockStyle.Top; hint.Height = 36; hint.Padding = new Padding(6);

        Controls.Add(list);
        Controls.Add(btnRefresh);
        Controls.Add(btnToggle);
        Controls.Add(hint);
        list.BringToFront();

        Load += delegate { Refill(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Ctrl(2) + Alt(1) + F
        if (!Win32.RegisterHotKey(Handle, HOTKEY_ID, 3, (uint)Keys.F))
            hint.Text += "  [Горячая клавиша занята другой программой]";
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Win32.UnregisterHotKey(Handle, HOTKEY_ID);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            IntPtr fg = Win32.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != Handle) Toggle(fg);
        }
        base.WndProc(ref m);
    }

    void Refill()
    {
        list.Items.Clear(); handles.Clear();
        Win32.EnumWindows(delegate (IntPtr h, IntPtr l)
        {
            if (!Win32.IsWindowVisible(h) || h == Handle) return true;
            int len = Win32.GetWindowTextLength(h);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            Win32.GetWindowText(h, sb, sb.Capacity);
            handles.Add(h); list.Items.Add(sb.ToString());
            return true;
        }, IntPtr.Zero);
    }

    void Toggle(IntPtr h)
    {
        Saved s;
        if (saved.TryGetValue(h, out s))
        {
            Win32.SetLong(h, Win32.GWL_STYLE, s.Style);
            Win32.SetLong(h, Win32.GWL_EXSTYLE, s.ExStyle);
            Win32.SetWindowPos(h, Win32.HWND_TOP, s.Rect.L, s.Rect.T, s.Rect.R - s.Rect.L, s.Rect.B - s.Rect.T,
                Win32.SWP_FRAMECHANGED | Win32.SWP_NOOWNERZORDER | Win32.SWP_SHOWWINDOW);
            saved.Remove(h);
            return;
        }

        s = new Saved();
        s.Style = Win32.GetLong(h, Win32.GWL_STYLE);
        s.ExStyle = Win32.GetLong(h, Win32.GWL_EXSTYLE);
        s.WasMaximized = Win32.IsZoomed(h);
        if (s.WasMaximized) Win32.ShowWindow(h, Win32.SW_RESTORE);
        Win32.GetWindowRect(h, out s.Rect);
        saved[h] = s;

        long style = s.Style & ~(Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU |
                                 Win32.WS_MINIMIZEBOX | Win32.WS_MAXIMIZEBOX);
        long ex = s.ExStyle & ~(Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_CLIENTEDGE |
                                Win32.WS_EX_STATICEDGE | Win32.WS_EX_WINDOWEDGE);
        Win32.SetLong(h, Win32.GWL_STYLE, style);
        Win32.SetLong(h, Win32.GWL_EXSTYLE, ex);

        Rectangle b = Screen.FromHandle(h).Bounds;
        Win32.SetWindowPos(h, Win32.HWND_TOP, b.X, b.Y, b.Width, b.Height,
            Win32.SWP_FRAMECHANGED | Win32.SWP_NOOWNERZORDER | Win32.SWP_SHOWWINDOW);
    }

    [STAThread]
    static void Main()
    {
        Win32.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.Run(new MainForm());
    }
}
