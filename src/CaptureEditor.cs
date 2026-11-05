using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuickZh {
    public sealed class CaptureEditor : Form {
        public readonly CaptureDocument Document;
        internal readonly EditorCanvas Canvas;
        internal readonly Form Toolbar;
        private readonly Settings settings;
        private readonly int mode;
        private readonly bool renderOnly;
        private readonly Action<string,string> changed;
        private readonly Dictionary<string,string> cache;
        private Panel toolbarContent;
        private Button viewButton, stopButton, undoButton, redoButton;
        private readonly Dictionary<DrawTool,Button> toolButtons = new Dictionary<DrawTool,Button>();
        private CancellationTokenSource operation;
        private bool sourceView, closing, toolbarReady;
        public bool SourceView { get { return sourceView; } }
        public CaptureEditor(Bitmap image, Rectangle screenBounds, Settings s, int captureMode, Action again, Action<string,string> resultChanged, Dictionary<string,string> results, bool onlyRender) {
            Document = new CaptureDocument(image); settings = s.Clone(); mode = captureMode; changed = resultChanged; cache = results; renderOnly = onlyRender;
            sourceView = mode != 0;
            Text = "截图翻译与标注 · 快译截图"; Icon = Program.AppIcon; FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.Manual; ShowInTaskbar = false; TopMost = true; KeyPreview = true;
            Padding = new Padding(2); BackColor = Theme.Accent; MinimumSize = new Size(160,64);
            var area = Screen.FromRectangle(screenBounds).WorkingArea;
            Size = new Size(Math.Min(area.Width,Math.Max(160,screenBounds.Width+4)),Math.Min(area.Height,Math.Max(64,screenBounds.Height+4)));
            Location = new Point(Math.Max(area.Left,Math.Min(screenBounds.Left-2,area.Right-Width)),Math.Max(area.Top,Math.Min(screenBounds.Top-2,area.Bottom-Height)));
            Canvas = new EditorCanvas(this) { Dock = DockStyle.Fill, AccessibleName = "截图翻译画布" }; Controls.Add(Canvas);
            Toolbar = new Form { Text = "截图工具栏", Icon = Program.AppIcon, FormBorderStyle = FormBorderStyle.None, AutoScaleMode = AutoScaleMode.None, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, TopMost = true, KeyPreview = true, BackColor = Color.White, Font = new Font("Microsoft YaHei UI",8.5F) };
            Toolbar.FormClosing += (sender,e) => { if (!closing) { e.Cancel = true; BeginInvoke((Action)(()=>Close())); } };
            BuildToolbar(Math.Min(Width,area.Width-16));
            UpdateViewButton();
            Move += (sender,e) => PositionToolbar(); Resize += (sender,e) => { Canvas.Invalidate(); PositionToolbar(); };
            KeyDown += HandleKeys; Toolbar.KeyDown += HandleKeys;
            Shown += async (sender,e) => {
                toolbarReady = true;
                if (!renderOnly) { Toolbar.Show(this); PositionToolbar(); Canvas.Focus(); if (mode != 2) await Process(mode == 0, false); }
            };
            FormClosing += (sender,e) => { closing = true; if (operation != null) operation.Cancel(); Toolbar.Close(); };
            SelectTool(DrawTool.Move);
            SetStatus(mode == 2 ? "选择标注工具后拖动画图；拖动图面可移动，右下角可放大。" : "准备识别文字…",false);
        }
        private Button SmallButton(string text, Action action, bool primary) {
            var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(48,32), Padding = new Padding(5,3,5,3), Margin = new Padding(0,0,5,0), FlatStyle = FlatStyle.Flat, BackColor = primary ? Theme.Accent : Color.White, ForeColor = primary ? Color.White : Theme.Ink, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = Color.FromArgb(222,226,236); b.Click += (sender,e) => action(); return b;
        }
        private ToolTip toolbarHints;
        private IconButton ToolButton(string name,string glyph,Action action) {
            var button=new IconButton(name,glyph);button.Click+=(sender,e)=>action();toolbarHints.SetToolTip(button,name);return button;
        }
        private Control Separator() { return new Panel { Width=1,Height=32,BackColor=Color.FromArgb(65,73,94),Margin=new Padding(6,8,7,0),Tag="separator" }; }
        private void BuildToolbar(int width) {
            Toolbar.BackColor=IconButton.Panel;Toolbar.ClientSize=new Size(width,64);Toolbar.Padding=new Padding(8);
            toolbarHints=new ToolTip { InitialDelay=350,ReshowDelay=80,AutoPopDelay=8000 };Toolbar.Disposed+=(sender,e)=>toolbarHints.Dispose();
            Toolbar.Paint+=(sender,e)=> { using (var pen=new Pen(Color.FromArgb(65,73,94))) e.Graphics.DrawRectangle(pen,0,0,Toolbar.ClientSize.Width-1,Toolbar.ClientSize.Height-1); };
            toolbarContent=new Panel { Dock=DockStyle.Fill,Margin=new Padding(0),BackColor=IconButton.Panel };
            var tools=toolbarContent;var actions=toolbarContent;
            DrawTool[] modes={DrawTool.Move,DrawTool.Arrow,DrawTool.Rectangle,DrawTool.Ellipse,DrawTool.Line,DrawTool.Pen,DrawTool.Text,DrawTool.Number,DrawTool.Mosaic,DrawTool.Protect};
            string[] names={"移动 / 缩放标注","箭头","方框","画圈","直线","画笔","添加文字","连续数字标注","马赛克","保留原图"};
            string[] glyphs={"move","arrow","rectangle","ellipse","line","pen","text","number","mosaic","protect"};
            for (int i=0;i<modes.Length;i++) { DrawTool tool=modes[i];var button=ToolButton(names[i],glyphs[i],()=>SelectTool(tool));tools.Controls.Add(button);toolButtons.Add(tool,button); }
            tools.Controls.Add(Separator());
            var color=ToolButton("标注颜色","color",()=>{});var colorMenu=new ContextMenuStrip { BackColor=IconButton.Panel,ForeColor=Color.White };
            string[] colorNames={"红色","蓝色","绿色","黄色","黑色","白色"};Color[] palette={Color.FromArgb(239,68,68),Color.FromArgb(59,130,246),Color.FromArgb(34,197,94),Color.FromArgb(250,204,21),Color.FromArgb(25,25,25),Color.White};
            for (int i=0;i<palette.Length;i++) { Color ink=palette[i];var item=colorMenu.Items.Add("●  "+colorNames[i]);item.ForeColor=ink;item.Click+=(sender,e)=> { Canvas.InkColor=ink;color.Swatch=ink;color.Invalidate();var selected=Canvas.SelectedMark;if (selected!=null && Document.Marks.Contains(selected)) { var before=selected.Clone();selected.Color=ink;Document.RecordChange(selected,before);MarksChanged(); } }; }
            color.Click+=(sender,e)=>colorMenu.Show(color,new Point(0,color.Height));color.Disposed+=(sender,e)=>colorMenu.Dispose();tools.Controls.Add(color);
            var widths=ToolButton("线条粗细 / 马赛克颗粒","weight",()=>{});widths.ValueText="3";var widthMenu=new ContextMenuStrip { BackColor=IconButton.Panel,ForeColor=Color.White };
            foreach (int value in new [] {2,3,5,8}) { int weight=value;var item=widthMenu.Items.Add(weight+" px");item.Click+=(sender,e)=> { Canvas.InkWidth=weight;widths.ValueText=weight.ToString();widths.Invalidate();var selected=Canvas.SelectedMark;if (selected!=null && selected.Tool!=DrawTool.Text && selected.Tool!=DrawTool.Number && Document.Marks.Contains(selected)) { var before=selected.Clone();selected.Width=Canvas.InkWidth*Document.Original.Width/Math.Max(1,Canvas.Width);Document.RecordChange(selected,before);MarksChanged(); } }; }
            widths.Click+=(sender,e)=>widthMenu.Show(widths,new Point(0,widths.Height));widths.Disposed+=(sender,e)=>widthMenu.Dispose();tools.Controls.Add(widths);
            var fonts=ToolButton("文字字号（默认 36）","fontsize",()=>{});fonts.ValueText="36";fonts.Width=56;var fontMenu=new ContextMenuStrip { BackColor=IconButton.Panel,ForeColor=Color.White };
            Action<float> setFont=value=> { Canvas.TextSize=value;fonts.ValueText=value.ToString("0");fonts.Invalidate();var selected=Canvas.SelectedMark;if (selected!=null && (selected.Tool==DrawTool.Text || selected.Tool==DrawTool.Number) && Document.Marks.Contains(selected)) { var before=selected.Clone();selected.FontSize=Canvas.TextSize*Document.Original.Width/Math.Max(1,Canvas.Width);Document.RecordChange(selected,before);MarksChanged(); } };
            foreach (int value in new [] {12,18,24,28,36,48,64,96}) { int pixels=value;fontMenu.Items.Add(pixels.ToString(),null,(sender,e)=>setFont(pixels)); }
            fontMenu.Items.Add("自定义…",null,(sender,e)=> {
                using (var dialog=new Form { Text="文字字号",Icon=Program.AppIcon,ClientSize=new Size(240,105),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,TopMost=true,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false }) {
                    var input=new NumericUpDown { Minimum=6,Maximum=300,Value=(decimal)Math.Max(6,Math.Min(300,Canvas.TextSize)),Location=new Point(18,16),Width=200 };dialog.Controls.Add(input);
                    var accept=SmallButton("确定",()=>dialog.DialogResult=DialogResult.OK,true);accept.Location=new Point(150,57);dialog.Controls.Add(accept);dialog.AcceptButton=accept;
                    if (dialog.ShowDialog(this)==DialogResult.OK) setFont((float)input.Value);
                }
            });
            fonts.Click+=(sender,e)=>fontMenu.Show(fonts,new Point(0,fonts.Height));fonts.Disposed+=(sender,e)=>fontMenu.Dispose();Canvas.TextSize=36;tools.Controls.Add(fonts);
            undoButton=ToolButton("撤销（Ctrl+Z）","undo",()=> { Document.Undo();MarksChanged(); });redoButton=ToolButton("重做（Ctrl+Y）","redo",()=> { Document.Redo();MarksChanged(); });
            actions.Controls.Add(undoButton);actions.Controls.Add(redoButton);actions.Controls.Add(Separator());
            viewButton=ToolButton("显示原文","original",ToggleView);actions.Controls.Add(viewButton);
            actions.Controls.Add(ToolButton("复制截图","copyimage",CopyImage));
            var copy=ToolButton("复制文字 ▾","copytext",()=>{});var copyMenu=new ContextMenuStrip { BackColor=IconButton.Panel,ForeColor=Color.White };
            copyMenu.Items.Add("复制原文",null,async (sender,e)=>await CopyText(false));copyMenu.Items.Add("复制译文",null,async (sender,e)=>await CopyText(true));
            copy.ContextMenuStrip=copyMenu;copy.Click+=(sender,e)=>copyMenu.Show(copy,new Point(0,copy.Height));copy.Disposed+=(sender,e)=>copyMenu.Dispose();actions.Controls.Add(copy);
            actions.Controls.Add(ToolButton("保存桌面","save",SaveImage));actions.Controls.Add(Separator());
            stopButton=ToolButton("停止","stop",()=> { if (operation!=null) { operation.Cancel();SetStatus("正在停止…",false); } });actions.Controls.Add(stopButton);actions.Controls.Add(ToolButton("关闭","close",()=>Close()));
            Toolbar.Controls.Add(toolbarContent);FitToolbarWidth(width);
            SetBusy(false);UpdateViewButton();MarksChanged();
        }
        internal int ToolbarRows { get; private set; }
        private static bool IsSeparator(Control control) { return (control.Tag as string)=="separator"; }
        internal void FitToolbarWidth(int width) {
            const int gap=5,padding=16;
            int natural=padding;
            foreach (Control control in toolbarContent.Controls) natural+=(IsSeparator(control)?14:control.Width)+gap;
            natural-=gap;
            int actual=Math.Min(Math.Max(80,width),natural),available=actual-padding;
            var rows=new List<List<Control>>();var row=new List<Control>();int used=0;
            foreach (Control control in toolbarContent.Controls) {
                control.Visible=true;
                int itemWidth=IsSeparator(control)?14:control.Width;
                if (IsSeparator(control) && row.Count==0) { control.Visible=false;continue; }
                if (row.Count>0 && used+gap+itemWidth>available) {
                    if (IsSeparator(row[row.Count-1])) { row[row.Count-1].Visible=false;row.RemoveAt(row.Count-1); }
                    if (row.Count>0) rows.Add(row);row=new List<Control>();used=0;
                    if (IsSeparator(control)) { control.Visible=false;continue; }
                }
                used+=(row.Count>0?gap:0)+itemWidth;row.Add(control);
            }
            if (row.Count>0 && IsSeparator(row[row.Count-1])) { row[row.Count-1].Visible=false;row.RemoveAt(row.Count-1); }
            if (row.Count>0) rows.Add(row);
            ToolbarRows=rows.Count;Toolbar.SuspendLayout();toolbarContent.SuspendLayout();
            Toolbar.ClientSize=new Size(actual,padding+Math.Max(1,rows.Count)*48+Math.Max(0,rows.Count-1)*gap);
            for (int i=0;i<rows.Count;i++) {
                int total=0;foreach (var control in rows[i]) total+=(IsSeparator(control)?14:control.Width)+gap;total-=gap;
                int x=(available-total)/2;
                foreach (var control in rows[i]) { if (IsSeparator(control)) { control.Location=new Point(x+6,i*53+8);x+=14+gap; }else { control.Location=new Point(x,i*53);x+=control.Width+gap; } }
            }
            toolbarContent.ResumeLayout(false);Toolbar.ResumeLayout(true);
        }
        internal static Point ToolbarPosition(Rectangle capture,Size size,Rectangle area) {
            int x = Math.Max(area.Left+4,Math.Min(capture.Left+(capture.Width-size.Width)/2,area.Right-size.Width-4));
            int y = capture.Bottom+6;
            if (y+size.Height > area.Bottom) y = capture.Top-size.Height-6;
            if (y < area.Top) y = area.Bottom-size.Height-4;
            return new Point(x,Math.Max(area.Top,y));
        }
        internal void PositionToolbar() {
            if (!toolbarReady || renderOnly || closing) return;
            var area = Screen.FromRectangle(Bounds).WorkingArea;
            int width = Math.Min(Width,area.Width-16);
            FitToolbarWidth(width);
            Toolbar.Location = ToolbarPosition(Bounds,Toolbar.Size,area);
        }
        public static void DragWindow(Form window) { ReleaseCapture(); SendMessage(window.Handle,0xA1,new IntPtr(2),IntPtr.Zero); }
        public static void ResizeWindow(Form window) { ReleaseCapture(); SendMessage(window.Handle,0xA1,new IntPtr(17),IntPtr.Zero); }
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if (m.Msg == 0x84) {
                long value = m.LParam.ToInt64(); var point = PointToClient(new Point((short)(value&65535),(short)((value>>16)&65535)));
                if (point.X >= ClientSize.Width-14 && point.Y >= ClientSize.Height-14) m.Result = new IntPtr(17);
            }
        }
        private async void HandleKeys(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { Document.Undo(); MarksChanged(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { Document.Redo(); MarksChanged(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.R) { e.SuppressKeyPress = true; await Process(true,true); }
            else if (e.Control && e.KeyCode == Keys.C) { e.SuppressKeyPress = true; if (e.Shift) await CopyText(!sourceView); else CopyImage(); }
        }
        internal void SelectTool(DrawTool tool) {
            Canvas.Tool = tool; Canvas.CancelStroke();
            if (tool != DrawTool.Move && tool != DrawTool.Text) Canvas.SelectedMark = null;
            foreach (var b in toolButtons) { ((IconButton)b.Value).Selected=b.Key==tool;b.Value.Invalidate(); }
            Canvas.Cursor = tool == DrawTool.Move ? Cursors.SizeAll : Cursors.Cross;
            if (tool==DrawTool.Protect) SetStatus("拖动框选要保留的图标或标识 · 区域恢复原图，Ctrl+Z 撤销。",false);
        }
        internal void MarksChanged() { if (Canvas.SelectedMark!=null && !Document.Marks.Contains(Canvas.SelectedMark)) Canvas.SelectedMark=null;undoButton.Enabled=Document.HasUndo;redoButton.Enabled=Document.HasRedo;Canvas.Invalidate(); }
        internal void EditText(PointF point,Mark existing) {
            using (var dialog = new Form { Text = existing == null ? "添加文字" : "编辑文字", Icon = Program.AppIcon, Font = Toolbar.Font, ClientSize = new Size(450,245), MinimumSize = new Size(380,260), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false, TopMost = true, BackColor = Color.White }) {
                var input = new TextBox { Multiline = true,AcceptsReturn = true,ScrollBars = ScrollBars.Vertical,Dock = DockStyle.Fill,MaxLength = 2000,Text = existing == null ? "" : existing.Text,Margin = new Padding(12) };
                var root = new TableLayoutPanel { Dock = DockStyle.Fill,Padding = new Padding(12),ColumnCount = 1,RowCount = 3 };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
                root.Controls.Add(new Label { Text = "支持换行 · 添加后拖动移动，四角缩放，双击编辑",Dock = DockStyle.Fill,ForeColor = Theme.Muted,AutoEllipsis = true },0,0); root.Controls.Add(input,0,1);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill,FlowDirection = FlowDirection.RightToLeft,Padding = new Padding(0,8,0,0) };
                var accept = SmallButton("确定",()=> { if (!String.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = DialogResult.OK; },true);
                buttons.Controls.Add(accept); buttons.Controls.Add(SmallButton("取消",()=>dialog.DialogResult = DialogResult.Cancel,false)); root.Controls.Add(buttons,0,2); dialog.Controls.Add(root);
                dialog.KeyPreview = true; dialog.KeyDown += (sender,e)=> { if (e.KeyCode == Keys.Escape) dialog.DialogResult = DialogResult.Cancel; if (e.Control && e.KeyCode == Keys.Enter) accept.PerformClick(); };
                dialog.Shown += (sender,e)=>input.Focus();
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (existing == null) {
                    float ratio = (float)Document.Original.Width/Math.Max(1,Canvas.Width);
                    var mark = new Mark { Tool = DrawTool.Text,Text = input.Text.Trim(),Color = Canvas.InkColor,FontSize = Canvas.TextSize*ratio,TextWidth = Math.Max(60,Document.Original.Width-point.X-12) };
                    mark.Points.Add(point); Document.Add(mark); Canvas.SelectedText = mark;
                } else { var before = existing.Clone(); existing.Text = input.Text.Trim(); Document.RecordChange(existing,before); Canvas.SelectedText = existing; }
                SelectTool(DrawTool.Move); MarksChanged(); SetStatus("文字已添加 · 拖动移动，拖住四角缩放，双击编辑。",false);
            }
        }
        internal async void ToggleView() {
            if (sourceView && String.IsNullOrEmpty(Document.Translation) && operation == null) { await Process(true,false); return; }
            sourceView = !sourceView; Canvas.ScrollOffset = 0; UpdateViewButton(); Canvas.Invalidate();
        }
        private void UpdateViewButton() { viewButton.Text=sourceView?"显示译文":"显示原文";viewButton.AccessibleName=viewButton.Text;((IconButton)viewButton).Glyph=sourceView?"translate":"original";toolbarHints.SetToolTip(viewButton,viewButton.Text);viewButton.Invalidate(); }
        private void SetBusy(bool value) { stopButton.Enabled = value; }
        private void SetStatus(string text,bool error) {
            if (closing || IsDisposed) return;Toolbar.AccessibleDescription=text;
            if (viewButton!=null) { viewButton.AccessibleDescription=text;toolbarHints.SetToolTip(viewButton,viewButton.Text+(error?"\n"+text:""));((IconButton)viewButton).HasError=error;viewButton.Invalidate(); }
        }
        private void NotifyChanged() { if (!closing && changed != null) changed(Document.OriginalText,Document.Translation); }
        internal async Task Process(bool translate, bool force) {
            if (operation != null || closing) return;
            var current = new CancellationTokenSource(); operation = current; SetBusy(true); var watch = Stopwatch.StartNew();
            try {
                if (translate) { sourceView = false; UpdateViewButton(); Canvas.ScrollOffset = 0; }
                if (String.IsNullOrWhiteSpace(Document.OriginalText)) {
                    Document.Placeholder = "正在识别文字…"; SetStatus("正在本地识别文字…",false); Canvas.Invalidate();
                    using (var clone = (Bitmap)Document.Original.Clone()) {
                        OcrResult recognized = await Task.Run(()=>Ocr.RecognizeDetailed(clone,settings.Language,current.Token));
                        current.Token.ThrowIfCancellationRequested(); if (closing) return; Document.OriginalText = recognized.Text; Document.Blocks.Clear(); Document.Blocks.AddRange(recognized.Blocks);
                    }
                    NotifyChanged();
                }
                if (String.IsNullOrWhiteSpace(Document.OriginalText)) throw new InvalidOperationException("未识别到文字，请重新截图或在设置中切换识别语言。");
                if (!translate) { SetStatus("取字完成 · 可复制原文；点击显示译文可翻译。",false); return; }
                settings.Validate(true);
                if (Document.OriginalText.Length > 24000) throw new InvalidOperationException("文字过长，请分段截图翻译。");
                if (Document.Blocks.Count == 0) Document.Blocks.Add(new OcrBlock { Id = 0,Text = Document.OriginalText,Bounds = new RectangleF(12,12,Math.Max(1,Document.Original.Width-24),Math.Max(1,Document.Original.Height-24)),Lines = 2 });
                var input = new List<BlockReply>(); foreach (var block in Document.Blocks) input.Add(new BlockReply {id=block.Id,text=block.Text});
                string cacheKey = Json.Stringify(new [] {settings.ApiUrl,settings.Model,settings.Prompt,Json.Stringify(input)}); string cached;
                if (!force && cache.TryGetValue(cacheKey,out cached)) {
                    ApplyReply(ApiClient.ParseLayout(cached,Document.Blocks.Count)); NotifyChanged(); Canvas.Invalidate(); SetStatus("翻译完成 · 已复用缓存；可切换原文、标注或复制。",false); return;
                }
                Document.Translation = ""; foreach (var block in Document.Blocks) block.Translation = "";
                Document.Placeholder = "正在原位翻译…"; Canvas.Invalidate(); SetStatus("正在翻译，将按原文段落位置逐块替换为中文…",false);
                var progress = new Progress<BlockReply>(block=> {
                    if (closing) return; Document.Blocks[block.id].Translation = block.text; JoinTranslations(); Canvas.Invalidate();
                });
                LayoutReply result = await ApiClient.TranslateLayout(settings,Document.Blocks,(id,text)=>((IProgress<BlockReply>)progress).Report(new BlockReply {id=id,text=text}),current.Token);
                current.Token.ThrowIfCancellationRequested(); if (closing) return;
                ApplyReply(result); string cacheValue = Json.Stringify(result);
                int chars = cacheKey.Length+cacheValue.Length; foreach (var item in cache) chars += item.Key.Length+item.Value.Length;
                if (cache.Count >= 20 || chars > 300000) cache.Clear(); cache[cacheKey] = cacheValue; NotifyChanged();
                Canvas.Invalidate(); SetStatus("翻译完成（"+watch.Elapsed.TotalSeconds.ToString("0.0")+" 秒）· 已在原文位置替换，右下角可拖动放大。",false);
            } catch (OperationCanceledException) { if (!closing) { Document.Placeholder = "已停止，可按 Ctrl+R 重试。"; SetStatus("已停止，已有译文可能不完整；Ctrl+R 重试。",false); } }
            catch (Exception ex) { if (!closing) { Document.Placeholder = ex.Message; SetStatus(ex.Message,true); } }
            finally { if (operation == current) operation = null; if (!closing) { SetBusy(false); Canvas.Invalidate(); } current.Dispose(); }
        }
        private void JoinTranslations() { var texts = new List<string>(); foreach (var block in Document.Blocks) if (!String.IsNullOrWhiteSpace(block.Translation)) texts.Add(block.Translation); Document.Translation = String.Join("\n\n",texts); }
        private void ApplyReply(LayoutReply reply) { foreach (var b in reply.blocks) Document.Blocks[b.id].Translation = b.text; JoinTranslations(); }
        private async Task CopyText(bool translated) {
            if (String.IsNullOrWhiteSpace(Document.OriginalText) && operation == null) await Process(false,false);
            if (translated && String.IsNullOrWhiteSpace(Document.Translation) && operation == null && !closing) await Process(true,false);
            string text = translated ? Document.Translation : Document.OriginalText;
            if (String.IsNullOrWhiteSpace(text)) { SetStatus(translated ? "尚无译文，请等待完成或按 Ctrl+R 重试。" : "原文尚未识别完成，请稍后再试。",true); return; }
            if (closing) return;
            try { Clipboard.SetText(text); SetStatus((translated ? "译文" : "原文")+"已复制。",false); } catch { SetStatus("剪贴板暂被占用，请重试。",true); }
        }
        internal Bitmap ExportView() { return Document.Export(sourceView,Document.Original.Size,0); }
        private void CopyImage() { try { using (var image = ExportView()) Clipboard.SetImage(image); Close(); } catch { SetStatus("图片复制失败，请重试。",true); } }
        private void SaveImage() { try { using (var image = ExportView()) DesktopFiles.Save(image); Close(); } catch (Exception ex) { SetStatus("保存失败："+ex.Message,true); } }
        protected override void Dispose(bool disposing) {
            if (disposing) { closing = true; if (operation != null) operation.Cancel(); Toolbar.Dispose(); Document.Dispose(); }
            base.Dispose(disposing);
            if (disposing) ThreadPool.QueueUserWorkItem(state=>Memory.Trim(true));
        }
    }
    internal sealed class EditorCanvas : Control {
        private readonly CaptureEditor editor;
        private Mark pending,manipulated,before;
        private PointF dragStart;
        private RectangleF originalBounds;
        private int resizeCorner=-1;
        public Mark SelectedMark;
        public Mark SelectedText { get { return SelectedMark!=null && SelectedMark.Tool==DrawTool.Text?SelectedMark:null; } set { SelectedMark=value; } }
        public DrawTool Tool;
        public Color InkColor=Color.FromArgb(239,68,68);
        public float InkWidth=3,TextSize=36,ScrollOffset;
        public EditorCanvas(CaptureEditor owner) { editor=owner;DoubleBuffered=true;SetStyle(ControlStyles.Selectable,true);TabStop=true; }
        private float Ratio { get { return (float)editor.Document.Original.Width/Math.Max(1,Width); } }
        private PointF Position(Point p) { return editor.Document.ToDocument(p,ClientSize); }
        internal PointF[] Handles(Graphics g,Mark mark) {
            var bounds=mark.Bounds(g);bounds.Inflate(3*Ratio,3*Ratio);
            return new [] {new PointF(bounds.Left,bounds.Top),new PointF(bounds.Right,bounds.Top),new PointF(bounds.Right,bounds.Bottom),new PointF(bounds.Left,bounds.Bottom)};
        }
        private int HitHandle(PointF point,Graphics g) {
            if (SelectedMark==null || !editor.Document.Marks.Contains(SelectedMark)) return -1;
            var handles=Handles(g,SelectedMark);float radius=7*Ratio;
            for (int i=0;i<handles.Length;i++) if (Math.Abs(point.X-handles[i].X)<=radius && Math.Abs(point.Y-handles[i].Y)<=radius) return i;
            return -1;
        }
        protected override void OnPaint(PaintEventArgs e) {
            editor.Document.Paint(e.Graphics,ClientSize,editor.SourceView,ScrollOffset,pending);
            if (Tool==DrawTool.Protect) {
                var saved=e.Graphics.Save();e.Graphics.ScaleTransform((float)Width/editor.Document.Original.Width,(float)Height/editor.Document.Original.Height);
                var areas=new List<Mark>(editor.Document.Marks);if (pending!=null) areas.Add(pending);
                using (var pen=new Pen(Color.FromArgb(34,197,94),Ratio) { DashStyle=System.Drawing.Drawing2D.DashStyle.Dash }) foreach (var area in areas) if (area.Tool==DrawTool.Protect && area.Points.Count>=2) { var r=area.Bounds(e.Graphics);e.Graphics.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height); }
                e.Graphics.Restore(saved);
            }
            if (SelectedMark!=null && editor.Document.Marks.Contains(SelectedMark) && (Tool==DrawTool.Move || Tool==DrawTool.Text)) {
                var saved=e.Graphics.Save();e.Graphics.ScaleTransform((float)Width/editor.Document.Original.Width,(float)Height/editor.Document.Original.Height);
                var r=SelectedMark.Bounds(e.Graphics);r.Inflate(3*Ratio,3*Ratio);
                using (var pen=new Pen(Theme.Accent,Ratio) { DashStyle=System.Drawing.Drawing2D.DashStyle.Dash }) e.Graphics.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);
                using (var outline=new Pen(Theme.Accent,Ratio)) foreach (var point in Handles(e.Graphics,SelectedMark)) { float d=7*Ratio;var handle=new RectangleF(point.X-d/2,point.Y-d/2,d,d);e.Graphics.FillRectangle(Brushes.White,handle);e.Graphics.DrawRectangle(outline,handle.X,handle.Y,handle.Width,handle.Height); }
                e.Graphics.Restore(saved);
            }
            using (var pen=new Pen(Color.FromArgb(150,Theme.Accent),1)) { e.Graphics.DrawLine(pen,Width-12,Height-2,Width-2,Height-12);e.Graphics.DrawLine(pen,Width-7,Height-2,Width-2,Height-7); }
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            if (e.Button!=MouseButtons.Left) return;Focus();var location=Position(e.Location);
            if (Tool==DrawTool.Move || Tool==DrawTool.Text) {
                using (var g=CreateGraphics()) {
                    resizeCorner=HitHandle(location,g);
                    if (resizeCorner>=0) { manipulated=SelectedMark;before=manipulated.Clone();originalBounds=before.Bounds(g);dragStart=location;Capture=true;return; }
                    manipulated=Tool==DrawTool.Text?editor.Document.HitText(location,g):editor.Document.HitMark(location,g,5*Ratio);
                    SelectedMark=manipulated;
                    if (manipulated!=null) { before=manipulated.Clone();originalBounds=before.Bounds(g);dragStart=location;Capture=true;Invalidate();return; }
                }
                if (Tool==DrawTool.Text) { editor.EditText(location,null);return; }
            }
            if (e.X>=Width-14 && e.Y>=Height-14) { CaptureEditor.ResizeWindow(editor);return; }
            if (Tool==DrawTool.Move) { Invalidate();CaptureEditor.DragWindow(editor);return; }
            if (Tool==DrawTool.Number) {
                var mark=new Mark { Tool=DrawTool.Number,Color=InkColor,FontSize=20*Ratio,Sequence=editor.Document.NextNumber };mark.Points.Add(location);editor.Document.Add(mark);SelectedMark=mark;editor.MarksChanged();return;
            }
            pending=new Mark { Tool=Tool,Color=InkColor,Width=InkWidth*Ratio };
            pending.Points.Add(location);pending.Points.Add(location);Capture=true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            var point=Position(e.Location);
            if (manipulated!=null) {
                if (resizeCorner>=0) {
                    bool left=resizeCorner==0 || resizeCorner==3,top=resizeCorner==0 || resizeCorner==1;
                    var anchor=new PointF(left?originalBounds.Right:originalBounds.Left,top?originalBounds.Bottom:originalBounds.Top);
                    float width=Math.Max(8,originalBounds.Width+(left?-1:1)*(point.X-dragStart.X));
                    float height=Math.Max(8,originalBounds.Height+(top?-1:1)*(point.Y-dragStart.Y));
                    if (before.Tool==DrawTool.Text || before.Tool==DrawTool.Number) {
                        float scale=Math.Max(width/Math.Max(1,originalBounds.Width),height/Math.Max(1,originalBounds.Height));
                        float maxW=left?anchor.X:editor.Document.Original.Width-anchor.X,maxH=top?anchor.Y:editor.Document.Original.Height-anchor.Y;
                        scale=Math.Max(0.15F,Math.Min(scale,Math.Min(maxW/Math.Max(1,originalBounds.Width),maxH/Math.Max(1,originalBounds.Height))));
                        width=originalBounds.Width*scale;height=originalBounds.Height*scale;
                    }
                    var target=new RectangleF(left?anchor.X-width:anchor.X,top?anchor.Y-height:anchor.Y,width,height);manipulated.ResizeFrom(before,originalBounds,target);
                } else {
                    float dx=Math.Max(-originalBounds.Left,Math.Min(editor.Document.Original.Width-originalBounds.Right,point.X-dragStart.X));
                    float dy=Math.Max(-originalBounds.Top,Math.Min(editor.Document.Original.Height-originalBounds.Bottom,point.Y-dragStart.Y));manipulated.MoveFrom(before,dx,dy);
                }
                Invalidate();return;
            }
            if (pending!=null) { if (Tool==DrawTool.Pen) pending.Points.Add(point);else pending.Points[pending.Points.Count-1]=point;Invalidate();return; }
            if (Tool==DrawTool.Move || Tool==DrawTool.Text) {
                using (var g=CreateGraphics()) { int corner=HitHandle(point,g);Cursor=corner>=0?(corner==0 || corner==2?Cursors.SizeNWSE:Cursors.SizeNESW):editor.Document.HitMark(point,g,4*Ratio)!=null?Cursors.SizeAll:Tool==DrawTool.Move?Cursors.Default:Cursors.IBeam; }
            }
        }
        private static bool Different(Mark a,Mark b) {
            if (a.FontSize!=b.FontSize || a.TextWidth!=b.TextWidth || a.Points.Count!=b.Points.Count) return true;
            for (int i=0;i<a.Points.Count;i++) if (a.Points[i]!=b.Points[i]) return true;return false;
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            if (e.Button!=MouseButtons.Left) return;
            if (manipulated!=null) {
                OnMouseMove(e);var item=manipulated;var previous=before;manipulated=null;before=null;resizeCorner=-1;Capture=false;
                if (Different(item,previous)) editor.Document.RecordChange(item,previous);editor.MarksChanged();return;
            }
            if (pending==null) return;
            OnMouseMove(e);var finished=pending;pending=null;Capture=false;var a=finished.Points[0];var b=finished.Points[finished.Points.Count-1];
            if (Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)>=1 || finished.Points.Count>3) { editor.Document.Add(finished);SelectedMark=finished; }editor.MarksChanged();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) CancelStroke();base.OnMouseCaptureChanged(e); }
        public void CancelStroke() { pending=null;if (manipulated!=null && before!=null) manipulated.Apply(before);manipulated=null;before=null;resizeCorner=-1;Capture=false;Invalidate(); }
        protected override void OnMouseDoubleClick(MouseEventArgs e) {
            if (e.Button!=MouseButtons.Left || (Tool!=DrawTool.Move && Tool!=DrawTool.Text)) return;
            Mark mark;using (var g=CreateGraphics()) mark=editor.Document.HitText(Position(e.Location),g);if (mark==null) return;CancelStroke();editor.EditText(mark.Points[0],mark);
        }
        protected override void OnMouseWheel(MouseEventArgs e) {
            if (editor.SourceView || editor.Document.Blocks.Count>0) return;
            float height;using (var g=CreateGraphics()) using (var font=CaptureDocument.TextFont(g,editor.Document.Translation,ClientSize,out height)) ScrollOffset=Math.Max(0,Math.Min(Math.Max(0,height-Height+28),ScrollOffset-e.Delta/3F));Invalidate();
        }
    }
}
