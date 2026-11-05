using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;

namespace QuickZh {
    public sealed class HotKey {
        public uint Modifiers; public uint Key;
        public static HotKey Parse(string text) {
            var result = new HotKey(); bool hasKey = false;
            foreach (string bit in (text ?? "").Split('+')) {
                string s = bit.Trim();
                if (s.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) result.Modifiers |= 2;
                else if (s.Equals("Alt", StringComparison.OrdinalIgnoreCase)) result.Modifiers |= 1;
                else if (s.Equals("Shift", StringComparison.OrdinalIgnoreCase)) result.Modifiers |= 4;
                else {
                    Keys key;
                    if (hasKey || !Enum.TryParse<Keys>(s, true, out key) || key == Keys.None || key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
                        throw new ArgumentException("快捷键格式无效，例如 Ctrl+Alt+Q。请点击输入框后直接按下组合键。");
                    result.Key = (uint)(key & Keys.KeyCode); hasKey = true;
                }
            }
            if (!hasKey || (result.Modifiers & 3) == 0) throw new ArgumentException("快捷键必须包含 Ctrl 或 Alt，再加一个普通按键。");
            return result;
        }
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr handle, int id);
        public bool Register(IntPtr handle, int id) { return RegisterHotKey(handle, id, Modifiers | 0x4000, Key); }
    }
    public static class Startup {
        private const string RunPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string Name = "QuickZhScreenshot";
        public static bool Enabled { get { using (var k = Registry.CurrentUser.OpenSubKey(RunPath)) return k != null && k.GetValue(Name) != null; } }
        public static void Set(bool enabled) {
            using (var k = Registry.CurrentUser.CreateSubKey(RunPath)) {
                if (enabled) k.SetValue(Name, "\"" + Application.ExecutablePath + "\" --background", RegistryValueKind.String);
                else k.DeleteValue(Name, false);
            }
        }
    }
    public static class DesktopFiles {
        public static string Save(Bitmap image) {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (String.IsNullOrEmpty(desktop)) throw new IOException("无法确定桌面路径。");
            Directory.CreateDirectory(desktop);
            string file = Path.Combine(desktop, "截图_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            using (var fs = new FileStream(file, FileMode.CreateNew)) image.Save(fs, ImageFormat.Png);
            return file;
        }
    }
    public sealed class CaptureOverlay : Form {
        private Bitmap screen; private Point origin; private Rectangle selection; private bool dragging;
        public Bitmap Captured { get; private set; }
        public Rectangle CapturedBounds { get; private set; }
        public CaptureOverlay() : this(CaptureDesktop(), SystemInformation.VirtualScreen) { }
        internal CaptureOverlay(Bitmap frozenDesktop, Rectangle desktopBounds) {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; ShowInTaskbar = false;
            TopMost = true; KeyPreview = true; Cursor = Cursors.Cross; DoubleBuffered = true; AutoScaleMode = AutoScaleMode.None;
            Bounds = desktopBounds;
            screen = frozenDesktop;
            AccessibleName = "截图选区";
        }
        private static Bitmap CaptureDesktop() {
            var bounds = SystemInformation.VirtualScreen;
            var image = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            try { using (var g = Graphics.FromImage(image)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy); return image; }
            catch { image.Dispose(); throw; }
        }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.DrawImageUnscaled(screen, 0, 0);
            using (var shade = new SolidBrush(Color.FromArgb(115, 12, 20, 36))) e.Graphics.FillRectangle(shade, ClientRectangle);
            if (selection.Width > 0 && selection.Height > 0) {
                e.Graphics.DrawImage(screen, selection, selection, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(99, 102, 241), 2)) e.Graphics.DrawRectangle(pen, selection);
                string size = selection.Width + " × " + selection.Height;
                var box = new Rectangle(selection.Left, Math.Max(0, selection.Top - 28), 145, 26);
                using (var b = new SolidBrush(Color.FromArgb(35, 41, 63))) e.Graphics.FillRectangle(b, box);
                TextRenderer.DrawText(e.Graphics, size, Font, box, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }
            var monitor = Screen.FromPoint(Cursor.Position).Bounds;
            var help = new Rectangle(monitor.X - Left + 24, monitor.Y - Top + 24, 530, 44);
            using (var b = new SolidBrush(Color.FromArgb(235, 27, 33, 52))) e.Graphics.FillRectangle(b, help);
            TextRenderer.DrawText(e.Graphics, "拖动鼠标框选文字区域  ·  松开完成  ·  Esc / 右键取消", Font, help, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (e.Button != MouseButtons.Left) return;
            origin = e.Location; selection = Rectangle.Empty; dragging = true; Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            if (!dragging) return;
            var current = new Point(Math.Max(0, Math.Min(ClientSize.Width, e.X)), Math.Max(0, Math.Min(ClientSize.Height, e.Y)));
            selection = Rectangle.FromLTRB(Math.Min(origin.X, current.X), Math.Min(origin.Y, current.Y), Math.Max(origin.X, current.X), Math.Max(origin.Y, current.Y)); Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            if (e.Button != MouseButtons.Left || !dragging) return;
            OnMouseMove(e); dragging = false; Capture = false;
            if (selection.Width < 5 || selection.Height < 5) { DialogResult = DialogResult.Cancel; Close(); return; }
            CapturedBounds = new Rectangle(Left + selection.Left, Top + selection.Top, selection.Width, selection.Height);
            Captured = screen.Clone(selection, PixelFormat.Format32bppArgb); DialogResult = DialogResult.OK; Close();
        }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } base.OnKeyDown(e); }
        protected override void Dispose(bool disposing) { if (disposing && screen != null) { screen.Dispose(); screen = null; } base.Dispose(disposing); }
    }
    public static class Theme {
        public static readonly Color Ink = Color.FromArgb(30, 41, 59);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Accent = Color.FromArgb(79, 70, 229);
        public static readonly Color Background = Color.FromArgb(245, 247, 251);
        public static Button Button(string text, EventHandler action, bool primary) {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(100, 38), Padding = new Padding(12, 4, 12, 4), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 8), BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderSize = primary ? 0 : 1; b.FlatAppearance.BorderColor = Color.FromArgb(218, 224, 234); b.Click += action; return b;
        }
        public static Label Label(string text) { return new Label { Text = text, AutoSize = true, ForeColor = Ink, Margin = new Padding(0, 8, 10, 8) }; }
    }
}
