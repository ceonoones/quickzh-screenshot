using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace QuickZh {
    static class Tests {
        static int passed;
        static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS " + name); }
        static void Throws(Action action, string name) { bool threw = false; try { action(); } catch { threw = true; } Check(threw, name); }
        static string Frame(string text) { return "data: " + Json.Stringify(new { choices = new [] { new { delta = new { content = text } } } }) + "\n\n"; }
        [STAThread]
        static int Main(string[] args) {
            if (Array.IndexOf(args,"--render4")>=0 || Array.IndexOf(args,"--render-toolbar")>=0) { RenderVersionFour();RenderToolbars();return 0; }
            if (Array.IndexOf(args,"--layout-examples")>=0 || Array.IndexOf(args,"--live-examples")>=0) {
                try { ExampleLayouts(Array.IndexOf(args,"--live-examples")>=0);return 0; } catch (Exception ex) { Console.WriteLine(ex);return 2; }
            }
            if (Array.IndexOf(args,"--live-sample")>=0) {
                try {
                    using (var doc=new CaptureDocument(new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本","导航原图.png")))) {
                        var watch=System.Diagnostics.Stopwatch.StartNew();doc.Blocks.AddRange(Ocr.RecognizeDetailed(doc.Original,"eng+rus",CancellationToken.None).Blocks);
                        var reply=ApiClient.TranslateLayout(Settings.Load(),doc.Blocks,null,CancellationToken.None).GetAwaiter().GetResult();
                        foreach (var item in reply.blocks) { doc.Blocks[item.id].Translation=item.text;Console.WriteLine("LIVE LABEL "+doc.Blocks[item.id].Text+" -> "+item.text); }
                        using (var image=doc.Export(false,doc.Original.Size,0)) image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"导航真实翻译.png"));
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"导航翻译.json"),Json.Stringify(reply),Encoding.UTF8);
                        Console.WriteLine("LIVE SAMPLE seconds="+watch.Elapsed.TotalSeconds.ToString("0.00"));
                    }return 0;
                } catch (Exception e) { Console.WriteLine(e.Message);return 2; }
            }
            if (Array.IndexOf(args,"--live-layout") >= 0) {
                try {
                    using (var doc = new CaptureDocument(MakeLayoutImage())) {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        var recognized = Ocr.RecognizeDetailed(doc.Original,"eng",CancellationToken.None); doc.Blocks.AddRange(recognized.Blocks);
                        var reply = ApiClient.TranslateLayout(Settings.Load(),doc.Blocks,null,CancellationToken.None).GetAwaiter().GetResult();
                        foreach (var item in reply.blocks) { doc.Blocks[item.id].Translation = item.text; Console.WriteLine("BLOCK "+item.id+": "+item.text); }
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"预览翻译.json"),Json.Stringify(reply),Encoding.UTF8);
                        using (var bitmap = doc.Export(false,doc.Original.Size,0)) bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"真实原位翻译.png"));
                        Console.WriteLine("LIVE LAYOUT OK blocks="+doc.Blocks.Count+" seconds="+watch.Elapsed.TotalSeconds.ToString("0.00")); return 0;
                    }
                } catch (Exception ex) {Console.WriteLine("LIVE LAYOUT: "+ex.Message);return 2;}
            }
            if (Array.IndexOf(args, "--live") >= 0) {
                try {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    string text = ApiClient.Translate(Settings.Load(), "Let's call it a day and pick this up tomorrow.", null, CancellationToken.None).GetAwaiter().GetResult();
                    Console.WriteLine("LIVE OK " + watch.Elapsed.TotalSeconds.ToString("0.00") + " s: " + text); return 0;
                } catch (Exception ex) { Console.WriteLine("LIVE: " + ex.Message); return 2; }
            }
            Environment.SetEnvironmentVariable("SCREENZH_DATA", Path.Combine(Path.GetTempPath(), "quickzh-tests-" + Guid.NewGuid().ToString("N")));
            if (Array.IndexOf(args,"--ui-sample")>=0 || Array.IndexOf(args,"--render-sample")>=0) {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Program.AppIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                var editor=new CaptureEditor(new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本","导航原图.png")),new Rectangle(80,80,1066,175),new Settings(),0,null,null,new System.Collections.Generic.Dictionary<string,string>(),true);
                Console.WriteLine("UI recognizing");var found=Ocr.RecognizeDetailed(editor.Document.Original,"eng+rus",CancellationToken.None);editor.Document.Blocks.AddRange(found.Blocks);editor.Document.OriginalText=found.Text;Console.WriteLine("UI recognized");
                string[] text={"菜单","托福","我是学习者","我是合作伙伴","支持","博客"};for (int i=0;i<found.Blocks.Count;i++) found.Blocks[i].Translation=text[i];editor.Document.Translation=String.Join("\n",text);
                editor.ShowInTaskbar=true;editor.Toolbar.ShowInTaskbar=true;editor.Shown+=(sender,e)=> { editor.Toolbar.Location=new Point(editor.Left,editor.Bottom+6);editor.Toolbar.Show(editor);Console.WriteLine("UI shown"); };
                if (Array.IndexOf(args,"--render-sample")>=0) {
                    editor.Location=new Point(-20000,-20000);editor.Show();Application.DoEvents();
                    using (var combined=new Bitmap(editor.Width+24,editor.Height+editor.Toolbar.Height+40)) {
                        using (var g=Graphics.FromImage(combined)) g.Clear(Theme.Background);
                        using (var shot=new Bitmap(editor.Width,editor.Height)) { editor.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));using (var g=Graphics.FromImage(combined)) g.DrawImageUnscaled(shot,12,12); }
                        using (var shot=new Bitmap(editor.Toolbar.Width,editor.Toolbar.Height)) { editor.Toolbar.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));using (var g=Graphics.FromImage(combined)) g.DrawImageUnscaled(shot,12,editor.Height+20); }
                        combined.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"新版浮窗预览.png"));
                    }editor.Close();return 0;
                }
                Console.WriteLine("UI running");Application.Run(editor);return 0;
            }
            if (Array.IndexOf(args,"--idle") >= 0) {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Program.AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Program.ShowEvent = new EventWaitHandle(false,EventResetMode.AutoReset);
                var main = new MainForm(new Settings(),true);
                main.Shown += async (sender,e)=> {
                    await Task.Delay(1000); var process = System.Diagnostics.Process.GetCurrentProcess(); process.Refresh(); var cpu = process.TotalProcessorTime; long start = process.WorkingSet64;
                    await Task.Delay(5000); process.Refresh();
                    Console.WriteLine("IDLE working_set_MiB="+(process.WorkingSet64/1048576.0).ToString("0.0")+" private_MiB="+(process.PrivateMemorySize64/1048576.0).ToString("0.0")+" cpu_ms_over_5s="+(process.TotalProcessorTime-cpu).TotalMilliseconds.ToString("0.0"));
                    Console.WriteLine("IDLE OCR_processes="+(System.Diagnostics.Process.GetProcessesByName("tesseract").Length+System.Diagnostics.Process.GetProcessesByName("RapidOCR-json").Length)+" engine_expanded="+Directory.Exists(Paths.Engine));
                    Application.Exit();
                };
                Application.Run(main); return 0;
            }
            if (Array.IndexOf(args, "--render") >= 0) {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Program.AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                Program.ShowEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
                using (var form = new MainForm(new Settings(), false)) {
                    typeof(MainForm).GetField("firstShow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(form, false);
                    form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false;
                    form.Show(); Application.DoEvents(); form.PerformLayout();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "主界面.png")); }
                }
                using (var form = new SettingsForm(new Settings(), (s,b) => null)) {
                    form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false;
                    form.Show(); Application.DoEvents(); form.PerformLayout();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设置界面.png")); }
                    foreach (Control c in form.Controls) { var p = c as Panel; if (p != null && p.AutoScroll) p.AutoScrollPosition = new Point(0, p.DisplayRectangle.Height); }
                    Application.DoEvents();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设置下半页.png")); }
                }
                RenderEditor();
                Console.WriteLine("RENDER OK"); return 0;
            }
            try {
                Check(ApiClient.Endpoint("https://example.com") == "https://example.com/v1/chat/completions", "root endpoint");
                Check(ApiClient.Endpoint("https://example.com/v1/") == "https://example.com/v1/chat/completions", "v1 endpoint");
                Check(ApiClient.Endpoint("https://example.com/api/chat/completions") == "https://example.com/api/chat/completions", "custom endpoint");
                Throws(() => ApiClient.Endpoint("https://example.com?key=123"), "reject key in URL");
                Throws(() => ApiClient.Endpoint("http://example.com"), "reject external plaintext transport");
                Check(HotKey.Parse("Ctrl+Alt+Q").Modifiers == 3, "hotkey modifiers");
                Throws(() => HotKey.Parse("Q"), "reject unmodified hotkey");
                Throws(() => HotKey.Parse("Ctrl+Q+W"), "reject multiple hotkey keys");
                var config = new Settings { ApiKey = "test-local-only" }; config.Save();
                Check(Settings.Load().ApiKey == "test-local-only", "settings persistence");
                config.Prompt = "测试提示词"; config.Save(); Check(Settings.Load().Prompt == "测试提示词", "atomic settings replacement");
                string deltas = "";
                string result = ApiClient.ReadStream(new StringReader(": ping\n\n" + Frame("你好") + Frame("，世界。") + "data: [DONE]\n\n"), x => deltas += x, CancellationToken.None);
                Check(result == "你好，世界。" && result == deltas, "UTF-8 SSE streaming");
                Throws(() => ApiClient.ReadStream(new StringReader(Frame("半截译文")), null, CancellationToken.None), "detect truncated stream");
                Throws(() => ApiClient.ReadStream(new StringReader("data: {\"error\":{\"message\":\"failed\"}}\n\ndata: [DONE]\n\n"), null, CancellationToken.None), "detect API error in stream");
                TestHttp().GetAwaiter().GetResult();
                TestFallback().GetAwaiter().GetResult();
                Ocr.Prepare();
                using (var image = MakeImage("Hello world!\nPlease send me the document tomorrow.", false)) {
                    string text = Ocr.Recognize(image, "eng+rus", CancellationToken.None);
                    Console.WriteLine("English OCR output: " + text);
                    Check(text.IndexOf("Hello world", StringComparison.OrdinalIgnoreCase) >= 0 && text.Contains("tomorrow"), "English OCR");
                }
                using (var image = MakeImage("Привет, друзья!\nДо встречи завтра.", false)) {
                    string text = Ocr.Recognize(image, "eng+rus", CancellationToken.None);
                    Console.WriteLine("Russian OCR output: " + text);
                    Check(text.Contains("Привет") && text.Contains("завтра"), "Russian OCR");
                }
                using (var image = MakeImage("Hello world!\nThis is dark mode text.", true)) {
                    string text = Ocr.Recognize(image, "eng", CancellationToken.None);
                    Check(text.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0, "dark background OCR");
                }
                var cts = new CancellationTokenSource(); cts.Cancel();
                using (var image = MakeImage("Hello world", false)) Throws(() => Ocr.Recognize(image, "eng", cts.Token), "OCR cancellation");
                TestCapture();
                var previousClipboard = Clipboard.GetDataObject();
                try {
                    Clipboard.SetText("原文 / 中文译文 / Привет"); Check(Clipboard.GetText() == "原文 / 中文译文 / Привет", "Unicode text clipboard");
                    using (var image = MakeImage("Clipboard image", false)) {
                        Clipboard.SetImage(image); using (var copied = Clipboard.GetImage()) Check(copied != null && copied.Width == image.Width, "screenshot clipboard");
                        string saved = DesktopFiles.Save(image);
                        try { using (var loaded = Image.FromFile(saved)) Check(loaded.Width == image.Width && loaded.Height == image.Height, "save screenshot to desktop"); }
                        finally { File.Delete(saved); }
                    }
                } finally { if (previousClipboard != null) Clipboard.SetDataObject(previousClipboard, true); }
                TestAnnotations();
                TestEditor();
                TestEditorCancellation();
                TestLayout();
                TestSampleLayout();
                TestFontRecognition();
                TestCompletionActions();
                TestObjectEditing();
                TestExampleTypography();
                Console.WriteLine("ALL " + passed + " CHECKS PASSED"); return 0;
            } catch (Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
        }
        static Mark Shape(DrawTool tool, PointF a, PointF b) {
            var mark = new Mark { Tool = tool, Color = Color.Red, Width = 5 }; mark.Points.Add(a); mark.Points.Add(b); return mark;
        }
        static void TestAnnotations() {
            var image = new Bitmap(500,240); using (var g = Graphics.FromImage(image)) g.Clear(Color.White);
            using (var doc = new CaptureDocument(image)) {
                foreach (var tool in new [] {DrawTool.Arrow,DrawTool.Ellipse,DrawTool.Line,DrawTool.Pen,DrawTool.Rectangle}) {
                    var mark = Shape(tool,new PointF(40,40),new PointF(350,180));
                    if (tool == DrawTool.Pen) mark.Points.Add(new PointF(440,70));
                    doc.Add(mark);
                    using (var output = doc.Export(true,image.Size,0)) {
                        int red = 0;
                        for (int y=0;y<output.Height;y++) for (int x=0;x<output.Width;x++) { var p = output.GetPixel(x,y); if (p.R>200 && p.G<100) red++; }
                        Check(red > 300, "annotation export " + tool);
                    }
                    doc.Undo(); Check(doc.Marks.Count == 0 && doc.HasRedo,"undo " + tool); doc.Redo(); Check(doc.Marks.Count == 1,"redo " + tool); doc.Undo();
                }
                doc.Add(Shape(DrawTool.Line,new PointF(10,100),new PointF(440,100))); Check(!doc.HasRedo,"new stroke replaces redo branch");
                var point = doc.ToDocument(new Point(200,100),new Size(1000,480)); Check(point == new PointF(100,50),"annotation coordinates survive resize");
                Check(image.GetPixel(200,100).ToArgb() == Color.White.ToArgb(),"original pixels remain intact");
                doc.Translation = "今天就到这里吧，明天再继续。";
                using (var result = doc.Export(false,image.Size,0)) Check(result.GetPixel(200,100).R > 200 && result.GetPixel(200,100).G < 100,"translated export retains annotations");
                var text = new Mark { Tool = DrawTool.Text,Text = "重要说明",Color = Color.Blue,FontSize = 22,TextWidth = 240 }; text.Points.Add(new PointF(30,140)); doc.Add(text);
                using (var g = Graphics.FromImage(image)) Check(doc.HitText(new PointF(45,150),g) == text,"text annotation hit testing");
                var before = text.Clone(); text.Points[0] = new PointF(180,180); doc.RecordChange(text,before); doc.Undo(); Check(text.Points[0] == new PointF(30,140),"undo text movement"); doc.Redo(); Check(text.Points[0] == new PointF(180,180),"redo text movement");
                using (var exported = doc.Export(true,image.Size,0)) { int blue=0; for (int y=180;y<230;y++) for (int x=180;x<400;x++) { var p=exported.GetPixel(x,y); if (p.B>150 && p.R<100) blue++; } Check(blue>50,"text is included in exported screenshot"); }
            }
        }
        static void Pump(Task task) {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted) { Application.DoEvents(); Thread.Sleep(5); if (watch.Elapsed.TotalSeconds>15) throw new TimeoutException("editor test timed out"); }
            task.GetAwaiter().GetResult();
        }
        static void TestToolbarLayout(CaptureEditor editor) {
            var panel=(Panel)editor.Toolbar.Controls[0];
            foreach (int width in new [] {1600,620,320,160,900,1600}) {
                editor.FitToolbarWidth(width);Application.DoEvents();
                bool within=true,overlap=false,buttonsVisible=true,large=true,noStatus=true,noRetake=true,centered=true;
                var rows=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<Control>>();
                foreach (Control c in panel.Controls) {
                    noStatus &= !(c is Label);noRetake &= c.Text!="重新截图";
                    if (c is Button) { buttonsVisible &= c.Visible;large &= c.Width>=48 && c.Height==48; }
                    if (!c.Visible) continue;
                    within &= c.Left>=0 && c.Top>=0 && c.Right<=panel.ClientSize.Width && c.Bottom<=panel.ClientSize.Height;
                    foreach (Control other in panel.Controls) if (other!=c && other.Visible) overlap |= c.Bounds.IntersectsWith(other.Bounds);
                    if (c is Button) { if (!rows.ContainsKey(c.Top)) rows[c.Top]=new System.Collections.Generic.List<Control>();rows[c.Top].Add(c); }
                }
                foreach (var row in rows.Values) { int left=int.MaxValue,right=0;foreach (var c in row) { left=Math.Min(left,c.Left);right=Math.Max(right,c.Right); }centered &= Math.Abs(left-(panel.ClientSize.Width-right))<=1; }
                Check(within && !overlap && buttonsVisible,"responsive toolbar fits all buttons at width "+width);
                Check(centered && large,"toolbar rows centered with large click areas at width "+width);
                Check(width>=1600?editor.ToolbarRows==1:editor.ToolbarRows>1,"toolbar chooses single or multiple rows at width "+width);
                Check(noStatus && noRetake,"toolbar has no status text or retake button at width "+width);
            }
            var capture=new Rectangle(300,100,1400,180);var area=new Rectangle(0,0,1920,1080);var size=editor.Toolbar.Size;
            var point=CaptureEditor.ToolbarPosition(capture,size,area);
            Check(point.Y==capture.Bottom+6 && Math.Abs((point.X+size.Width/2)-(capture.Left+capture.Width/2))<=1,"toolbar sits centered under capture");
            point=CaptureEditor.ToolbarPosition(new Rectangle(1700,950,220,100),new Size(220,400),area);
            Check(area.Contains(new Rectangle(point,new Size(220,400))),"toolbar moves above capture near bottom edge");
            editor.FitToolbarWidth(620);
        }
        static void RenderToolbars() {
            using (var output=new Bitmap(1464,1000)) using (var g=Graphics.FromImage(output)) using (var title=new Font("Microsoft YaHei UI",16,FontStyle.Bold)) using (var text=new Font("Microsoft YaHei UI",12)) {
                g.Clear(Color.FromArgb(238,241,248));int y=18;
                foreach (int width in new [] {1400,740,320}) {
                    using (var editor=new CaptureEditor(MakeImage("",false),new Rectangle(30,30,width,100),new Settings(),2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                        editor.Location=new Point(-20000,-20000);editor.Show();editor.Toolbar.Location=new Point(-20000,-20000);editor.Toolbar.Show(editor);editor.FitToolbarWidth(width);Application.DoEvents();
                        g.DrawString(width+" 像素宽 · "+editor.ToolbarRows+" 行工具栏",title,Brushes.MidnightBlue,24,y);y+=38;
                        using (var shot=new SolidBrush(Color.White)) g.FillRectangle(shot,24,y,width,62);
                        g.DrawString("截图区域：工具栏在底部居中，宽度不足自动换行",text,Brushes.DimGray,new RectangleF(36,y+8,width-24,54));y+=68;
                        using (var bar=new Bitmap(editor.Toolbar.Width,editor.Toolbar.Height)) { editor.Toolbar.DrawToBitmap(bar,new Rectangle(Point.Empty,bar.Size));g.DrawImageUnscaled(bar,24+(width-bar.Width)/2,y); }
                        y+=editor.Toolbar.Height+30;editor.Close();
                    }
                }
                output.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"工具栏自适应预览.png"));
            }
        }
        static void TestEditor() {
            var area = new Rectangle(-1280,0,1280,720); var capture = new Rectangle(-400,550,400,160);
            var location = CaptureEditor.ToolbarPosition(capture,new Size(850,160),area);
            Check(area.Contains(new Rectangle(location,new Size(850,160))),"toolbar stays on negative-origin screen");
            using (var editor = new CaptureEditor(MakeImage("Hello world",false),new Rectangle(20,20,900,180),new Settings(),2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                editor.Document.OriginalText = "Hello world"; editor.Document.Translation = "你好，世界。";
                Check(editor.SourceView,"capture-only opens original view"); editor.ToggleView(); Check(!editor.SourceView,"toggle to translation"); editor.ToggleView(); Check(editor.SourceView,"toggle back to original");
                Check(((Button)typeof(CaptureEditor).GetField("viewButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor)).Text=="显示译文","source view button offers translated view");
                editor.Document.Add(Shape(DrawTool.Arrow,new PointF(30,100),new PointF(500,100))); editor.MarksChanged();
                using (var bitmap = editor.ExportView()) Check(bitmap.Size == editor.Document.Original.Size,"export retains original dimensions and excludes floating toolbar");
                Check(editor.Document.Marks.Count == 1,"view toggle preserves marks");
                editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-20000,-20000); editor.Show();
                editor.Toolbar.Location = new Point(-20000,-20000); editor.Toolbar.Show(editor);
                editor.FitToolbarWidth(620); Application.DoEvents();
                TestToolbarLayout(editor);
                editor.SelectTool(DrawTool.Line);
                var flags = System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                typeof(EditorCanvas).GetMethod("OnMouseDown",flags).Invoke(editor.Canvas,new object[] {new MouseEventArgs(MouseButtons.Left,1,40,80,0)});
                typeof(EditorCanvas).GetMethod("OnMouseUp",flags).Invoke(editor.Canvas,new object[] {new MouseEventArgs(MouseButtons.Left,1,400,80,0)});
                Check(editor.Document.Marks.Count == 2,"mouse drag creates annotation");
                var textMark = new Mark { Tool = DrawTool.Text,Text = "可拖动",Color = Color.Blue,FontSize = 22,TextWidth = 250 }; textMark.Points.Add(new PointF(50,30)); editor.Document.Add(textMark); editor.SelectTool(DrawTool.Move);
                typeof(EditorCanvas).GetMethod("OnMouseDown",flags).Invoke(editor.Canvas,new object[] {new MouseEventArgs(MouseButtons.Left,1,60,40,0)});
                typeof(EditorCanvas).GetMethod("OnMouseUp",flags).Invoke(editor.Canvas,new object[] {new MouseEventArgs(MouseButtons.Left,1,150,80,0)});
                Check(Math.Abs(textMark.Points[0].X-140)<2 && Math.Abs(textMark.Points[0].Y-70)<2,"mouse drag moves text independently");
            }
            var finder = new TcpListener(IPAddress.Loopback,0); finder.Start(); int port = ((IPEndPoint)finder.LocalEndpoint).Port; finder.Stop();
            using (var listener = new HttpListener()) {
                string address = "http://127.0.0.1:"+port; listener.Prefixes.Add(address+"/"); listener.Start();
                var server = Task.Run(async ()=> {
                    var ctx = await listener.GetContextAsync();
                    ctx.Response.ContentType = "text/event-stream";
                    var bytes = Encoding.UTF8.GetBytes(Frame("你好，世界。")+"data: [DONE]\n\n");
                    ctx.Response.OutputStream.Write(bytes,0,bytes.Length); ctx.Response.Close();
                });
                using (var editor = new CaptureEditor(MakeImage("Hello world",false),new Rectangle(20,20,900,180),new Settings { ApiUrl = address, ApiKey = "test-local-only" },2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                    editor.Document.Add(Shape(DrawTool.Ellipse,new PointF(100,20),new PointF(400,120)));
                    Pump(editor.Process(true,false));
                    Check(editor.Document.OriginalText.Contains("Hello"),"editor OCR pipeline");
                    Check(editor.Document.Translation == "你好，世界。" && !editor.SourceView,"editor translates inside capture");
                    Check(editor.Document.Marks.Count == 1,"streaming preserves annotations");
                    Pump(server);
                }
            }
        }
        static Bitmap MakeLayoutImage() {
            var image = new Bitmap(900,360);
            using (var g = Graphics.FromImage(image)) using (var font = new Font("Arial",27F,FontStyle.Regular,GraphicsUnit.Pixel)) {
                g.Clear(Color.FromArgb(238,242,247)); g.FillRectangle(Brushes.White,20,20,860,125);
                using (var dark = new SolidBrush(Color.FromArgb(30,41,59))) g.FillRectangle(dark,20,185,860,145);
                g.DrawString("Let's call it a day.",font,Brushes.Black,40,42); g.DrawString("We will continue tomorrow.",font,Brushes.Black,40,88);
                g.DrawString("Please send me the document.",font,Brushes.White,40,206); g.DrawString("I will reply as soon as I can.",font,Brushes.White,40,253);
            }
            return image;
        }
        static bool SameRegion(Bitmap a,Bitmap b,Rectangle area) {
            for (int y=area.Top;y<area.Bottom;y++) for (int x=area.Left;x<area.Right;x++) if (a.GetPixel(x,y).ToArgb()!=b.GetPixel(x,y).ToArgb()) return false;
            return true;
        }
        static readonly System.Collections.Generic.Dictionary<OcrBlock,System.Collections.Generic.List<TranslationLayout.Line>> composed=new System.Collections.Generic.Dictionary<OcrBlock,System.Collections.Generic.List<TranslationLayout.Line>>();
        static bool InTextRegion(OcrBlock block,int x,int y) {
            var bounds=block.Bounds;bounds.Inflate(3,3);if (bounds.Contains(x,y)) return true;
            System.Collections.Generic.List<TranslationLayout.Line> lines;if (!composed.TryGetValue(block,out lines)) { lines=TranslationLayout.Compose(block);composed[block]=lines; }
            foreach (var line in lines) { var drawn=new RectangleF(line.X,line.Y,line.Ink.Width,line.Ink.Height);drawn.Inflate(2,2);if (drawn.Contains(x,y)) return true; }
            return false;
        }
        static void ExampleLayouts(bool live) {
            foreach (string name in new [] {"正文","横幅"}) {
                using (var doc=new CaptureDocument(new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本",name+"原图.png")))) {
                    doc.Blocks.AddRange(Ocr.RecognizeDetailed(doc.Original,"eng",CancellationToken.None).Blocks);
                    Console.WriteLine("EXAMPLE "+name+" blocks="+doc.Blocks.Count);
                    string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本",name+"译文.json");LayoutReply reply=null;
                    if (live) { reply=ApiClient.TranslateLayout(Settings.Load(),doc.Blocks,null,CancellationToken.None).GetAwaiter().GetResult();File.WriteAllText(fixture,Json.Stringify(reply),Encoding.UTF8); }
                    else if (File.Exists(fixture)) reply=ApiClient.ParseLayout(File.ReadAllText(fixture,Encoding.UTF8),doc.Blocks.Count);
                    if (reply!=null) foreach (var block in reply.blocks) doc.Blocks[block.id].Translation=block.text;
                    foreach (var block in doc.Blocks) {
                        if (String.IsNullOrEmpty(block.Translation)) block.Translation=block.Text;
                        Console.WriteLine("REGION "+block.Id+" "+block.Text.Replace("\n"," / ")+" bounds="+block.Bounds+" font="+block.FontPixels.ToString("0.0")+" lines="+block.Lines+" button="+block.CenterText+" layout="+block.LayoutBounds);
                        foreach (var line in TranslationLayout.Compose(block)) Console.WriteLine("  PAINT "+line.Text+" pixels="+line.Pixels.ToString("0.0")+" at="+line.X.ToString("0.0")+","+line.Y.ToString("0.0"));
                    }
                    using (var output=doc.Export(false,doc.Original.Size,0)) output.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name+"新版翻译.png"));
                }
            }
        }
        static void RenderVersionFour() {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Program.AppIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            using (var editor=new CaptureEditor(new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本","正文原图.png")),new Rectangle(30,30,1120,515),new Settings(),0,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                editor.Document.Blocks.AddRange(Ocr.RecognizeDetailed(editor.Document.Original,"eng",CancellationToken.None).Blocks);
                var reply=ApiClient.ParseLayout(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本","正文译文.json"),Encoding.UTF8),editor.Document.Blocks.Count);
                foreach (var b in reply.blocks) editor.Document.Blocks[b.id].Translation=b.text;
                editor.Document.Translation="原位翻译示例";
                var circle=Shape(DrawTool.Ellipse,new PointF(72,74),new PointF(730,132));circle.Color=Color.FromArgb(112,93,238);editor.Document.Add(circle);
                var rectangle=Shape(DrawTool.Rectangle,new PointF(115,244),new PointF(704,375));rectangle.Color=Color.FromArgb(59,130,246);editor.Document.Add(rectangle);
                for (int i=0;i<3;i++) { var number=new Mark { Tool=DrawTool.Number,Sequence=i+1,FontSize=16,Color=Color.FromArgb(239,68,68) };number.Points.Add(new PointF(54,265+i*44));editor.Document.Add(number); }
                var text=new Mark { Tool=DrawTool.Text,Text="拖动四角缩放",FontSize=36,TextWidth=380,Color=Color.FromArgb(112,93,238) };text.Points.Add(new PointF(720,424));editor.Document.Add(text);editor.Canvas.SelectedMark=text;editor.MarksChanged();
                editor.Location=new Point(-20000,-20000);editor.Show();editor.Toolbar.Location=new Point(-20000,-20000);editor.Toolbar.Show(editor);Application.DoEvents();
                using (var output=new Bitmap(editor.Width+32,editor.Height+editor.Toolbar.Height+52)) {
                    using (var g=Graphics.FromImage(output)) g.Clear(Color.FromArgb(238,241,248));
                    using (var canvas=new Bitmap(editor.Width,editor.Height)) { editor.DrawToBitmap(canvas,new Rectangle(Point.Empty,canvas.Size));using (var g=Graphics.FromImage(output)) g.DrawImageUnscaled(canvas,16,16); }
                    using (var bar=new Bitmap(editor.Toolbar.Width,editor.Toolbar.Height)) { editor.Toolbar.DrawToBitmap(bar,new Rectangle(Point.Empty,bar.Size));using (var g=Graphics.FromImage(output)) g.DrawImageUnscaled(bar,16+(editor.Width-editor.Toolbar.Width)/2,editor.Height+28); }
                    output.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"4.1界面预览.png"));
                }
                editor.Close();
            }
        }
        static void TestSampleLayout() {
            string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本","导航原图.png");
            using (var doc=new CaptureDocument(new Bitmap(fixture))) {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var found=Ocr.RecognizeDetailed(doc.Original,"eng+rus",CancellationToken.None);doc.Blocks.AddRange(found.Blocks);
                Console.WriteLine("SAMPLE OCR seconds="+watch.Elapsed.TotalSeconds.ToString("0.00"));
                Check(found.Blocks.Count==6,"sample detects six separate UI labels");
                string[] expected={"MENU","TOEFL","I'M A LEARNER","I'M A PARTNER","SUPPORT","BLOG"};
                string[] translated={"菜单","托福","我是学习者","我是合作伙伴","支持","博客"};
                for (int i=0;i<expected.Length;i++) {
                    var block=found.Blocks[i];Console.WriteLine("SAMPLE "+block.Text+" "+block.Bounds+" bold="+block.Bold+" confidence="+block.Confidence.ToString("0.000"));
                    Check(block.Text==expected[i],"sample OCR exact label "+i);block.Translation=translated[i];
                }
                Check(found.Blocks[1].Bold && !found.Blocks[0].Bold,"sample preserves different text weights");
                Check(Math.Abs(found.Blocks[0].Bounds.Left-64)<3 && found.Blocks[0].Bounds.Height>=17,"sample tight text bounds preserve placement and size");
                using (var output=doc.Export(false,doc.Original.Size,0)) {
                    foreach (var r in new [] {new Rectangle(15,24,36,24),new Rectangle(160,110,7,40),new Rectangle(392,122,24,18),new Rectangle(647,122,23,18),new Rectangle(846,122,25,18),new Rectangle(0,84,1066,5)}) Check(SameRegion(doc.Original,output,r),"sample retains icon/divider pixels "+r);
                    int outside=0,changed=0;
                    for (int y=0;y<output.Height;y++) for (int x=0;x<output.Width;x++) {
                        if (output.GetPixel(x,y).ToArgb()==doc.Original.GetPixel(x,y).ToArgb()) continue;
                        changed++;bool inside=false;foreach (var block in doc.Blocks) inside|=InTextRegion(block,x,y);if (!inside) outside++;
                    }
                    Check(changed>1000 && outside==0,"sample replaces text without altering other image regions");
                    output.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"导航排版验证.png"));
                }
                // A protected region restores arbitrary image content, including anything that
                // happens to look like a letter or number to an OCR model.
                var protect=Shape(DrawTool.Protect,new PointF(0,105),new PointF(200,160));doc.Add(protect);
                using (var output=doc.Export(false,doc.Original.Size,0)) Check(SameRegion(doc.Original,output,new Rectangle(0,105,200,55)),"protection restores exact source pixels");
                doc.Undo();Check(doc.Marks.Count==0,"protection supports undo");doc.Redo();Check(doc.Marks.Count==1,"protection supports redo");
                found.Blocks[1].Translation=found.Blocks[1].Text;
                using (var output=doc.Export(false,doc.Original.Size,0)) Check(SameRegion(doc.Original,output,new Rectangle(12,112,95,35)),"unchanged brand labels retain original pixels");
            }
        }
        static string NormalizeOcr(string text) { return System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(),@"[^\p{L}\p{N}]",""); }
        static int Distance(string a,string b) {
            int[] previous=new int[b.Length+1];for (int j=0;j<=b.Length;j++) previous[j]=j;
            for (int i=1;i<=a.Length;i++) { var row=new int[b.Length+1];row[0]=i;for (int j=1;j<=b.Length;j++) row[j]=Math.Min(Math.Min(row[j-1]+1,previous[j]+1),previous[j-1]+(a[i-1]==b[j-1]?0:1));previous=row; }return previous[b.Length];
        }
        static void TestFontRecognition() {
            string sentence="Small fonts, clear meaning. Number 2026.";int oldErrors=0,newErrors=0;
            string[] names={"Arial","Times New Roman","Georgia","Courier New","Segoe UI"};
            for (int i=0;i<names.Length;i++) using (var image=new Bitmap(900,100)) {
                using (var g=Graphics.FromImage(image)) using (var font=new Font(names[i],i==4?12:20,i==1||i==2?FontStyle.Italic:FontStyle.Regular,GraphicsUnit.Pixel)) { g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.DrawString(sentence,font,Brushes.Black,20,25); }
                string old=Ocr.RecognizeLegacy(image,"eng",CancellationToken.None).Text, current=Ocr.Recognize(image,"eng",CancellationToken.None);
                int before=Distance(NormalizeOcr(sentence),NormalizeOcr(old)),after=Distance(NormalizeOcr(sentence),NormalizeOcr(current));oldErrors+=before;newErrors+=after;
                Console.WriteLine("FONT "+names[i]+" legacy_errors="+before+" neural_errors="+after+" result="+current);
                Check(after<=2,"font recognition "+names[i]);
            }
            Console.WriteLine("FONT TOTAL legacy="+oldErrors+" neural="+newErrors);
            Check(newErrors<=oldErrors,"neural OCR does not regress font sample set");
        }
        static void Mouse(EditorCanvas canvas,string method,int x,int y) {
            typeof(EditorCanvas).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(canvas,new object[] {new MouseEventArgs(MouseButtons.Left,1,x,y,0)});
        }
        static void TestObjectEditing() {
            using (var editor=new CaptureEditor(MakeImage("Annotation example",false),new Rectangle(20,20,900,180),new Settings(),2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                Check(editor.Canvas.TextSize==36 && new Mark().FontSize==36,"default annotation font size is 36");
                foreach (var tool in new [] {DrawTool.Rectangle,DrawTool.Ellipse}) {
                    editor.SelectTool(tool);
                    for (int i=0;i<3;i++) {
                        Mouse(editor.Canvas,"OnMouseDown",40+i*180,30);Mouse(editor.Canvas,"OnMouseUp",150+i*180,110);
                    }
                    Check(editor.Canvas.Tool==tool && editor.Document.Marks.Count==3,"shape tool stays active for three consecutive drawings "+tool);
                    bool allMatch=true;foreach (var mark in editor.Document.Marks) allMatch &= mark.Tool==tool;
                    Check(allMatch,"continuous drawing creates the selected shape type "+tool);
                    var buttons=(System.Collections.Generic.Dictionary<DrawTool,Button>)typeof(CaptureEditor).GetField("toolButtons",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(editor);
                    Check(((IconButton)buttons[tool]).Selected && !((IconButton)buttons[DrawTool.Move]).Selected,"shape toolbar highlight stays selected "+tool);
                    editor.SelectTool(DrawTool.Move);
                    Check(editor.Canvas.Tool==DrawTool.Move && !((IconButton)buttons[tool]).Selected,"explicit tool switch exits continuous drawing "+tool);
                    for (int i=0;i<3;i++) editor.Document.Undo();editor.MarksChanged();
                }
                foreach (var tool in new [] {DrawTool.Rectangle,DrawTool.Ellipse}) {
                    var shape=Shape(tool,new PointF(150,40),new PointF(300,100));editor.Document.Add(shape);editor.SelectTool(DrawTool.Move);
                    Mouse(editor.Canvas,"OnMouseDown",200,65);Mouse(editor.Canvas,"OnMouseUp",250,85);
                    Check(shape.Points[0]==new PointF(200,60) && shape.Points[1]==new PointF(350,120),"drag moves "+tool);
                    editor.Document.Undo();Check(shape.Points[0]==new PointF(150,40),"undo movement "+tool);editor.Document.Redo();Check(shape.Points[0]==new PointF(200,60),"redo movement "+tool);
                    editor.Document.Undo();editor.Document.Undo();editor.MarksChanged();
                }
                for (int corner=0;corner<4;corner++) {
                    var text=new Mark { Tool=DrawTool.Text,Text="可以缩放",FontSize=36,TextWidth=220,Color=Color.Blue };text.Points.Add(new PointF(300,65));editor.Document.Add(text);editor.SelectTool(DrawTool.Move);editor.Canvas.SelectedMark=text;
                    RectangleF before;PointF handle;using (var g=editor.Canvas.CreateGraphics()) { before=text.TextBounds(g);handle=editor.Canvas.Handles(g,text)[corner]; }
                    int dx=corner==0 || corner==3?-25:25,dy=corner==0 || corner==1?-10:10;
                    Mouse(editor.Canvas,"OnMouseDown",(int)Math.Round(handle.X),(int)Math.Round(handle.Y));Mouse(editor.Canvas,"OnMouseUp",(int)Math.Round(handle.X)+dx,(int)Math.Round(handle.Y)+dy);
                    RectangleF after;using (var g=editor.Canvas.CreateGraphics()) after=text.TextBounds(g);
                    Check(text.FontSize>36 && after.Width>before.Width,"text corner resize "+corner);
                    PointF anchorBefore=new PointF(corner==0 || corner==3?before.Right:before.Left,corner==0 || corner==1?before.Bottom:before.Top);
                    PointF anchorAfter=new PointF(corner==0 || corner==3?after.Right:after.Left,corner==0 || corner==1?after.Bottom:after.Top);
                    Check(Math.Abs(anchorBefore.X-anchorAfter.X)<2 && Math.Abs(anchorBefore.Y-anchorAfter.Y)<2,"text opposite corner stays anchored "+corner);
                    editor.Document.Undo();Check(text.FontSize==36 && text.Points[0]==new PointF(300,65),"undo text resize "+corner);editor.Document.Redo();Check(text.FontSize>36,"redo text resize "+corner);
                    editor.Document.Undo();editor.Document.Undo();editor.MarksChanged();
                }
                editor.SelectTool(DrawTool.Number);
                for (int i=1;i<=12;i++) Mouse(editor.Canvas,"OnMouseDown",40+i*55,100);
                Check(editor.Document.Marks.Count==12 && editor.Document.Marks[11].Sequence==12,"number tool creates consecutive 1 through 12");
                editor.Document.Undo();Check(editor.Document.NextNumber==12,"undo number reuses next sequence");editor.Document.Redo();Check(editor.Document.NextNumber==13,"redo number restores next sequence");
                var number=editor.Document.Marks[0];editor.SelectTool(DrawTool.Move);Mouse(editor.Canvas,"OnMouseDown",95,100);Mouse(editor.Canvas,"OnMouseUp",110,120);Check(number.Points[0]==new PointF(110,120),"number markers can move");
                using (var output=editor.ExportView()) { int red=0;for (int y=80;y<140;y++) for (int x=80;x<140;x++) { var c=output.GetPixel(x,y);if (c.R>180 && c.G<150) red++; }Check(red>100,"number markers appear in export"); }
            }
            using (var original=new Bitmap(240,160)) {
                using (var g=Graphics.FromImage(original)) { g.Clear(Color.White);for (int y=0;y<160;y+=4) for (int x=0;x<240;x+=4) g.FillRectangle((x+y)%8==0?Brushes.Black:Brushes.White,x,y,4,4); }
                using (var doc=new CaptureDocument((Bitmap)original.Clone())) {
                    var mosaic=Shape(DrawTool.Mosaic,new PointF(40,32),new PointF(180,132));mosaic.Width=3;doc.Add(mosaic);
                    using (var output=doc.Export(true,original.Size,0)) {
                        Check(output.GetPixel(45,40).ToArgb()==output.GetPixel(50,44).ToArgb(),"mosaic pixels form uniform blocks");
                        Check(!SameRegion(original,output,new Rectangle(40,32,140,100)),"mosaic alters selected area");
                        Check(SameRegion(original,output,new Rectangle(0,0,35,160)) && SameRegion(original,output,new Rectangle(185,0,55,160)),"mosaic leaves outside pixels intact");
                    }
                    doc.Undo();using (var output=doc.Export(true,original.Size,0)) Check(SameRegion(original,output,new Rectangle(Point.Empty,original.Size)),"undo mosaic restores source");doc.Redo();Check(doc.Marks.Count==1,"redo mosaic restores mask");
                    using (var output=doc.Export(true,new Size(480,320),0)) Check(output.Size==new Size(480,320),"mosaic supports preview scaling");
                }
            }
        }
        static void TestExampleTypography() {
            foreach (string name in new [] {"正文","横幅"}) using (var doc=new CaptureDocument(new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本",name+"原图.png")))) {
                doc.Blocks.AddRange(Ocr.RecognizeDetailed(doc.Original,"eng",CancellationToken.None).Blocks);
                var reply=ApiClient.ParseLayout(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"测试样本",name+"译文.json"),Encoding.UTF8),doc.Blocks.Count);
                foreach (var block in reply.blocks) doc.Blocks[block.id].Translation=block.text;
                Check(doc.Blocks.Count==6,"example distinct paragraphs and labels "+name);
                foreach (var block in doc.Blocks) {
                    var lines=TranslationLayout.Compose(block);Check(lines.Count>0 && lines[0].Pixels>=block.FontPixels*0.98F,"example preserves estimated font size "+name+" "+block.Id);
                    if (block.CenterText) { float center=block.LayoutBounds.Left+block.LayoutBounds.Width/2;Check(Math.Abs(lines[0].X+lines[0].Ink.Width/2-center)<1,"button translation centered "+block.Text); }
                }
                Check(doc.Blocks[1+(name=="横幅"?1:0)].Lines==2,"wrapped prose retains two source lines "+name);
                using (var output=doc.Export(false,doc.Original.Size,0)) {
                    if (name=="正文") { Check(SameRegion(doc.Original,output,new Rectangle(94,254,16,111)),"body bullet symbols remain unchanged");Check(SameRegion(doc.Original,output,new Rectangle(669,424,25,22)),"body link arrow remains unchanged"); }
                    else { Check(SameRegion(doc.Original,output,new Rectangle(1250,0,650,680)),"hero illustration remains unchanged");Check(SameRegion(doc.Original,output,new Rectangle(1020,482,26,28)),"hero link arrow remains unchanged");Check(SameRegion(doc.Original,output,new Rectangle(310,461,330,3)),"button border remains unchanged"); }
                }
            }
        }
        static void TestCompletionActions() {
            var previous=Clipboard.GetDataObject();
            try {
                using (var editor=new CaptureEditor(MakeImage("Screenshot",false),new Rectangle(20,20,900,180),new Settings(),2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    typeof(CaptureEditor).GetMethod("CopyImage",flags).Invoke(editor,null);
                    Check(editor.IsDisposed,"copy screenshot closes editor after success");
                    using (var copied=Clipboard.GetImage()) Check(copied!=null && copied.Size==new Size(900,180),"copied screenshot preserves source resolution");
                }
                string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var before=new System.Collections.Generic.HashSet<string>(Directory.GetFiles(desktop,"截图_*.png"));
                using (var editor=new CaptureEditor(MakeImage("Saved screenshot",false),new Rectangle(20,20,900,180),new Settings(),2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                    typeof(CaptureEditor).GetMethod("SaveImage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(editor,null);
                    Check(editor.IsDisposed,"save screenshot closes editor after success");
                }
                int count=0;foreach (var file in Directory.GetFiles(desktop,"截图_*.png")) if (!before.Contains(file)) { count++;File.Delete(file); }Check(count==1,"save action writes one desktop image");
                using (var editor=new CaptureEditor(MakeImage("Toolbar",false),new Rectangle(20,20,900,180),new Settings(),2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                    var actions=(Panel)editor.Toolbar.Controls[0];Button copy=null;bool translate=false;
                    foreach (Control button in actions.Controls) { if (button.Text=="翻译") translate=true;if (button.Text=="复制文字 ▾") copy=(Button)button; }
                    Check(!translate,"toolbar removes redundant translate button");
                    Check(copy!=null && copy.ContextMenuStrip.Items.Count==2 && copy.ContextMenuStrip.Items[0].Text=="复制原文" && copy.ContextMenuStrip.Items[1].Text=="复制译文","copy text menu contains only original and translated text");
                }
            } finally { if (previous!=null) Clipboard.SetDataObject(previous,true); }
        }
        static void TestLayout() {
            var good = ApiClient.ParseLayout("{\"blocks\":[{\"id\":1,\"text\":\"明天继续\"},{\"id\":0,\"text\":\"今天结束\"}]}",2);
            Check(good.blocks[0].id == 0,"layout maps translations by paragraph id");
            Throws(()=>ApiClient.ParseLayout("{\"blocks\":[{\"id\":0,\"text\":\"你好\"}]}",2),"layout rejects missing paragraphs");
            using (var doc = new CaptureDocument(MakeLayoutImage())) {
                var result = Ocr.RecognizeDetailed(doc.Original,"eng",CancellationToken.None); doc.Blocks.AddRange(result.Blocks);
                Check(result.Blocks.Count >= 2 && result.Text.Contains("document"),"OCR retains separate light and dark text positions");
                foreach (var block in doc.Blocks) block.Translation = "这是在原文位置显示的中文译文。";
                using (var translated = doc.Export(false,doc.Original.Size,0)) {
                    int outside=0,inside=0;
                    for (int y=0;y<translated.Height;y+=2) for (int x=0;x<translated.Width;x+=2) {
                        if (translated.GetPixel(x,y).ToArgb() == doc.Original.GetPixel(x,y).ToArgb()) continue;
                        bool inBlock=false; foreach (var b in doc.Blocks) inBlock|=InTextRegion(b,x,y);
                        if (inBlock) inside++; else outside++;
                    }
                    Check(inside>100 && outside==0,"translation changes only original text regions");
                    translated.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"原位翻译验证.png"));
                }
            }
        }
        static void TestEditorCancellation() {
            var finder = new TcpListener(IPAddress.Loopback,0); finder.Start(); int port = ((IPEndPoint)finder.LocalEndpoint).Port; finder.Stop();
            using (var listener = new HttpListener()) {
                string address = "http://127.0.0.1:"+port; listener.Prefixes.Add(address+"/"); listener.Start();
                var received = new TaskCompletionSource<bool>();
                var server = Task.Run(async ()=> {
                    var ctx = await listener.GetContextAsync(); received.SetResult(true);
                    await Task.Delay(250); ctx.Response.Abort();
                });
                using (var editor = new CaptureEditor(MakeImage("Hello",false),new Rectangle(20,20,900,180),new Settings { ApiUrl = address,ApiKey = "test-local-only" },2,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                    editor.Document.OriginalText = "Hello";
                    var task = editor.Process(true,false); Pump(received.Task);
                    editor.Close(); Pump(task); Pump(server);
                    Check(editor.IsDisposed,"closing capture cancels active translation");
                }
            }
        }
        static void RenderEditor() {
            using (var editor = new CaptureEditor(MakeLayoutImage(),new Rectangle(50,50,850,340),new Settings(),0,null,null,new System.Collections.Generic.Dictionary<string,string>(),true)) {
                var recognized = Ocr.RecognizeDetailed(editor.Document.Original,"eng",CancellationToken.None); editor.Document.Blocks.AddRange(recognized.Blocks); editor.Document.OriginalText = recognized.Text;
                string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"预览翻译.json");
                LayoutReply reply = new LayoutReply {blocks=new System.Collections.Generic.List<BlockReply>()};
                if (File.Exists(fixture)) try { reply=ApiClient.ParseLayout(File.ReadAllText(fixture,Encoding.UTF8),recognized.Blocks.Count); } catch (InvalidOperationException) { }
                if (reply.blocks.Count == 0) foreach (var block in recognized.Blocks) reply.blocks.Add(new BlockReply {id=block.Id,text="这是在原文位置显示的中文译文。"});
                foreach (var block in reply.blocks) editor.Document.Blocks[block.id].Translation = block.text;
                editor.Document.Translation = "原位翻译示例";
                editor.Document.Add(Shape(DrawTool.Arrow,new PointF(720,160),new PointF(250,220)));
                editor.Document.Add(Shape(DrawTool.Ellipse,new PointF(30,35),new PointF(360,116)));
                var text = new Mark { Tool=DrawTool.Text,Text="重点：这段文字可以拖动",Color=Color.FromArgb(37,99,235),FontSize=22,TextWidth=350 }; text.Points.Add(new PointF(480,145)); editor.Document.Add(text); editor.Canvas.SelectedText = text;
                editor.MarksChanged(); editor.StartPosition = FormStartPosition.Manual; editor.Location = new Point(-20000,-20000); editor.Show();
                editor.Toolbar.Location = new Point(-20000,-20000); editor.Toolbar.Show(editor); Application.DoEvents();
                using (var combined = new Bitmap(Math.Max(editor.Width,editor.Toolbar.Width)+32,editor.Height+editor.Toolbar.Height+50)) {
                    using (var g = Graphics.FromImage(combined)) g.Clear(Theme.Background);
                    using (var shot = new Bitmap(editor.Width,editor.Height)) { editor.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size)); using (var g = Graphics.FromImage(combined)) g.DrawImageUnscaled(shot,16,16); }
                    using (var bar = new Bitmap(editor.Toolbar.Width,editor.Toolbar.Height)) { editor.Toolbar.DrawToBitmap(bar,new Rectangle(Point.Empty,bar.Size)); using (var g = Graphics.FromImage(combined)) g.DrawImageUnscaled(bar,16,editor.Height+24); }
                    combined.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"截图浮层预览.png"));
                }
            }
        }
        static void TestCapture() {
            var source = new Bitmap(600, 400); using (var g = Graphics.FromImage(source)) { g.Clear(Color.White); g.FillRectangle(Brushes.Blue, 100, 100, 200, 100); }
            using (var overlay = new CaptureOverlay(source, new Rectangle(-600, 0, 600, 400))) {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CaptureOverlay).GetMethod("OnMouseDown", flags).Invoke(overlay, new object[] {new MouseEventArgs(MouseButtons.Left, 1, 300, 200, 0)});
                typeof(CaptureOverlay).GetMethod("OnMouseUp", flags).Invoke(overlay, new object[] {new MouseEventArgs(MouseButtons.Left, 1, 100, 100, 0)});
                using (var image = overlay.Captured) Check(image != null && image.Size == new Size(200, 100) && image.GetPixel(50,50).ToArgb() == Color.Blue.ToArgb(), "reverse selection on negative-origin monitor");
                Check(overlay.CapturedBounds == new Rectangle(-500,100,200,100),"capture retains screen location");
            }
            using (var overlay = new CaptureOverlay(new Bitmap(100, 100), new Rectangle(0,0,100,100))) {
                typeof(CaptureOverlay).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(overlay, new object[] {new KeyEventArgs(Keys.Escape)});
                Check(overlay.Captured == null && overlay.DialogResult == DialogResult.Cancel, "escape cancels screenshot");
            }
        }
        public static Bitmap MakeImage(string text, bool dark) {
            var image = new Bitmap(900, 180);
            using (var g = Graphics.FromImage(image)) using (var font = new Font("Arial", 32F, FontStyle.Regular, GraphicsUnit.Pixel)) {
                g.Clear(dark ? Color.FromArgb(25, 28, 36) : Color.White);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(text, font, dark ? Brushes.White : Brushes.Black, new RectangleF(25, 22, 850, 150));
            }
            return image;
        }
        static async Task TestHttp() {
            await OneServer(200, "text/event-stream", Frame("测试成功") + "data: [DONE]\n\n", false, false);
            await OneServer(200, "application/json", "{\"choices\":[{\"message\":{\"content\":\"普通响应成功\"}}]}", false, false);
            await OneServer(401, "application/json", "{}", true, false);
            await OneServer(502, "text/plain", "Bad Gateway", true, false);
            await OneServer(200, "text/event-stream", "", false, true);
        }
        static async Task TestFallback() {
            var finder = new TcpListener(IPAddress.Loopback, 0); finder.Start(); int port = ((IPEndPoint)finder.LocalEndpoint).Port; finder.Stop();
            using (var listener = new HttpListener()) {
                string address = "http://127.0.0.1:" + port; listener.Prefixes.Add(address + "/"); listener.Start();
                var server = Task.Run(async () => {
                    for (int i = 0; i < 2; i++) {
                        var ctx = await listener.GetContextAsync();
                        using (var reader = new StreamReader(ctx.Request.InputStream)) {
                            var body = Json.Parse<System.Collections.Generic.Dictionary<string,object>>(reader.ReadToEnd());
                            Check((bool)body["stream"] == (i == 0), "fallback request mode " + i);
                        }
                        ctx.Response.StatusCode = i == 0 ? 502 : 200; ctx.Response.ContentType = "application/json";
                        var bytes = Encoding.UTF8.GetBytes(i == 0 ? "{}" : "{\"choices\":[{\"message\":{\"content\":\"兼容请求成功\"}}]}");
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length); ctx.Response.Close();
                    }
                });
                string result = await ApiClient.Translate(new Settings { ApiUrl = address, ApiKey = "test-local-only" }, "test", null, CancellationToken.None);
                Check(result == "兼容请求成功", "automatic streaming fallback"); await server;
            }
        }
        static async Task OneServer(int status, string contentType, string body, bool shouldFail, bool cancel) {
            int port; var portFinder = new TcpListener(IPAddress.Loopback, 0); portFinder.Start(); port = ((IPEndPoint)portFinder.LocalEndpoint).Port; portFinder.Stop();
            using (var listener = new HttpListener()) {
                string address = "http://127.0.0.1:" + port; listener.Prefixes.Add(address + "/"); listener.Start();
                var server = Task.Run(async () => {
                    var ctx = await listener.GetContextAsync();
                    Check(ctx.Request.Headers["Authorization"] == "Bearer test-local-only", "HTTP authorization header");
                    using (var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) {
                        var request = Json.Parse<System.Collections.Generic.Dictionary<string,object>>(r.ReadToEnd());
                        Check(Convert.ToString(request["model"]) == "deepseek-flash", "HTTP configured model");
                    }
                    if (cancel) { await Task.Delay(1000); ctx.Response.Abort(); return; }
                    ctx.Response.StatusCode = status; ctx.Response.ContentType = contentType;
                    byte[] bytes = Encoding.UTF8.GetBytes(body); ctx.Response.OutputStream.Write(bytes, 0, bytes.Length); ctx.Response.Close();
                });
                using (var cancellation = new CancellationTokenSource()) {
                    if (cancel) cancellation.CancelAfter(200);
                    bool failed = false, canceled = false; string result = "";
                    try { result = await ApiClient.Translate(new Settings { ApiUrl = address, ApiKey = "test-local-only", UseStreaming = status != 502 }, "Test", null, cancellation.Token); }
                    catch (OperationCanceledException) { canceled = true; }
                    catch (IOException) { failed = true; }
                    Check(cancel ? canceled : shouldFail ? failed : !failed && result.Length > 0, cancel ? "in-flight cancellation" : "HTTP " + status + " " + contentType);
                }
                await server;
            }
        }
    }
}
