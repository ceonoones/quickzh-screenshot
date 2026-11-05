using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuickZh {
    public sealed class MainForm : Form {
        private Settings settings;
        private NotifyIcon tray;
        private Label status, shortcutLabel;
        private CaptureEditor activeEditor;
        private RegisteredWaitHandle showWait;
        private bool exiting, selecting, disposed, firstShow = true, hotkeyRegistered;
        private int hotkeyId = 0xA11;
        private readonly bool background;
        private readonly Dictionary<string,string> cache = new Dictionary<string,string>();
        public MainForm(Settings s,bool startInTray) {
            settings = s; background = startInTray || s.StartMinimized;
            Text = "快译截图"; Icon = Program.AppIcon; Font = new Font("Microsoft YaHei UI",10F);
            AutoScaleDimensions = new SizeF(96F,96F); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(720,350); MinimumSize = new Size(680,360); StartPosition = FormStartPosition.CenterScreen; BackColor = Theme.Background;
            BuildInterface(); BuildTray();
            Shown += OnFirstShow; Resize += (sender,e)=> { if (WindowState == FormWindowState.Minimized) HideToTray(false); };
            FormClosing += OnClosing;
            showWait = ThreadPool.RegisterWaitForSingleObject(Program.ShowEvent,(state,timedOut)=> {
                if (disposed || IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((Action)(()=> { if (!disposed) ShowMain(); })); } catch (InvalidOperationException) { }
            },null,Timeout.Infinite,false);
            UpdateShortcut();
        }
        private void BuildInterface() {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill,Padding = new Padding(24),ColumnCount = 1,RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,58)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,58)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,58)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); Controls.Add(root);
            var heading = new Label { Text = "快译截图", Font = new Font(Font.FontFamily,25F,FontStyle.Bold), ForeColor = Theme.Ink,AutoSize = true }; root.Controls.Add(heading,0,0);
            root.Controls.Add(new Label { Text = "框选后在原位置翻译、标注和复制。",ForeColor = Theme.Muted,Dock = DockStyle.Fill,TextAlign = ContentAlignment.MiddleLeft },0,1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill,WrapContents = false,Margin = new Padding(0) };
            actions.Controls.Add(Theme.Button("截图翻译",(sender,e)=>BeginCapture(0),true)); actions.Controls.Add(Theme.Button("截图取字",(sender,e)=>BeginCapture(1),false)); actions.Controls.Add(Theme.Button("截图与标注",(sender,e)=>BeginCapture(2),false)); actions.Controls.Add(Theme.Button("设置",(sender,e)=>OpenSettings(),false)); root.Controls.Add(actions,0,2);
            shortcutLabel = new Label { Dock = DockStyle.Fill,ForeColor = Theme.Muted,TextAlign = ContentAlignment.MiddleLeft }; root.Controls.Add(shortcutLabel,0,3);
            status = new Label { Dock = DockStyle.Fill,ForeColor = Theme.Muted,AutoEllipsis = true,TextAlign = ContentAlignment.MiddleLeft }; root.Controls.Add(status,0,4);
        }
        private void BuildTray() {
            var menu = new ContextMenuStrip(); menu.Items.Add("截图翻译",null,(sender,e)=>BeginCapture(0)); menu.Items.Add("截图取字",null,(sender,e)=>BeginCapture(1)); menu.Items.Add("截图与标注",null,(sender,e)=>BeginCapture(2));
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("打开主界面",null,(sender,e)=>ShowMain()); menu.Items.Add("设置",null,(sender,e)=> { ShowMain(); OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出",null,(sender,e)=> { exiting = true; Close(); });
            tray = new NotifyIcon { Icon = Program.AppIcon,Text = "快译截图",Visible = true,ContextMenuStrip = menu }; tray.DoubleClick += (sender,e)=>ShowMain();
        }
        private void OnFirstShow(object sender,EventArgs e) {
            if (!firstShow) return; firstShow = false; hotkeyRegistered = HotKey.Parse(settings.Hotkey).Register(Handle,hotkeyId);
            if (background) HideToTray(false);
            SetStatus(hotkeyRegistered ? "准备就绪 · 关闭窗口后在托盘运行。" : "快捷键已被占用，请在设置中更换。",!hotkeyRegistered);
        }
        public void ShowMain() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void HideToTray(bool notify) { Hide(); Memory.Trim(false); if (notify) tray.ShowBalloonTip(1500,"快译截图正在托盘运行","按 "+settings.Hotkey+" 截图，双击托盘图标打开界面。",ToolTipIcon.Info); }
        protected override void WndProc(ref Message m) { if (m.Msg == 0x0312 && m.WParam.ToInt32() == hotkeyId) { BeginCapture(0); return; } base.WndProc(ref m); }
        private void UpdateShortcut() { shortcutLabel.Text = settings.Hotkey+"  截图翻译    ·    Esc 关闭浮层"; }
        private void SetStatus(string text,bool error) { if (disposed) return; status.Text = text; status.ForeColor = error ? Color.Firebrick : Theme.Muted; }
        private void OpenSettings() {
            if (selecting || OwnedForms.Length > 0) return;
            var editor = activeEditor;
            if (editor != null && !editor.IsDisposed) { editor.Toolbar.Hide(); editor.Hide(); }
            try { using (var dialog = new SettingsForm(settings,ApplySettings)) dialog.ShowDialog(this); }
            finally { if (editor != null && !editor.IsDisposed) { editor.Show(); editor.Toolbar.Show(editor); editor.PositionToolbar(); } }
        }
        private string ApplySettings(Settings next,bool autoStart) {
            bool changed = next.Hotkey != settings.Hotkey || !hotkeyRegistered; int newId = hotkeyId == 0xA11 ? 0xA12 : 0xA11; bool oldStartup = Startup.Enabled;
            if (changed && !HotKey.Parse(next.Hotkey).Register(Handle,newId)) return "该快捷键已被其他程序占用，请更换。";
            try { Startup.Set(autoStart); next.Save(); }
            catch (Exception ex) { if (changed) HotKey.UnregisterHotKey(Handle,newId); try { Startup.Set(oldStartup); } catch { } return "保存失败："+ex.Message; }
            if (changed) { HotKey.UnregisterHotKey(Handle,hotkeyId); hotkeyId = newId; hotkeyRegistered = true; }
            settings = next; cache.Clear(); UpdateShortcut(); SetStatus("设置已保存，新截图使用新设置。",false); return null;
        }
        private async void BeginCapture(int mode) {
            if (selecting || OwnedForms.Length > 0) return;
            if (activeEditor != null && !activeEditor.IsDisposed) activeEditor.Close();
            selecting = true; bool wasVisible = Visible; Hide(); await Task.Delay(160);
            Bitmap captured = null;
            try {
                if (disposed) return; Rectangle bounds;
                using (var overlay = new CaptureOverlay()) { if (overlay.ShowDialog() != DialogResult.OK) { if (wasVisible) ShowMain(); return; } captured = overlay.Captured; bounds = overlay.CapturedBounds; }
                if (settings.AutoCopyImage) Clipboard.SetImage(captured);
                if (settings.AutoSaveImage) DesktopFiles.Save(captured);
                var editor = new CaptureEditor(captured,bounds,settings,mode,()=>BeginCapture(mode),null,cache,false); captured = null; activeEditor = editor;
                editor.FormClosed += (sender,e)=> { if (activeEditor == editor) activeEditor = null; };
                editor.Show(); SetStatus("翻译、标注、复制和保存在截图下方操作。",false);
            } catch (Exception ex) { if (!disposed) { ShowMain(); SetStatus("截图失败："+ex.Message,true); } }
            finally { if (captured != null) captured.Dispose(); selecting = false; }
        }
        private void OnClosing(object sender,FormClosingEventArgs e) {
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideToTray(true); return; }
            if (activeEditor != null && !activeEditor.IsDisposed) activeEditor.Close();
        }
        protected override void Dispose(bool disposing) {
            if (disposing && !disposed) {
                disposed = true; if (showWait != null) showWait.Unregister(null);
                if (activeEditor != null && !activeEditor.IsDisposed) activeEditor.Dispose();
                if (IsHandleCreated) HotKey.UnregisterHotKey(Handle,hotkeyId);
                tray.Visible = false; tray.Dispose(); cache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
