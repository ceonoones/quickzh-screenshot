using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace QuickZh {
    public sealed partial class Ocr {
        private sealed class NeuralUnavailableException : IOException { }
        public sealed class NeuralItem { public string text { get; set; } public float score { get; set; } public int[][] box { get; set; } }
        public sealed class NeuralReply { public int code { get; set; } public List<NeuralItem> data { get; set; } }
        private static OcrResult RecognizeNeural(Bitmap image,string language,CancellationToken token) {
            string temp = Path.Combine(Paths.Root,"ocr-"+Guid.NewGuid().ToString("N")+".png");
            // Keep the detector's coordinate system, including small UI fonts, at sufficient resolution.
            double scale = image.Width < 1800 && image.Height < 1000 ? 2 : 1;
            try {
                using (var input = new Bitmap((int)(image.Width*scale),(int)(image.Height*scale),PixelFormat.Format24bppRgb)) {
                    using (var g = Graphics.FromImage(input)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(image,new Rectangle(0,0,input.Width,input.Height)); }
                    input.Save(temp,ImageFormat.Png);
                }
                string model = "en_v5", keys = "en_v5";
                if (language.StartsWith("rus") || language.StartsWith("ukr")) { model = "eslav_v5"; keys = model; }
                else if (language.StartsWith("chi")) { model = "rec_ch_PP-OCRv4_infer"; keys = "dict_chinese"; }
                else if (language.StartsWith("jpn")) { model = "rec_japan_PP-OCRv3_infer"; keys = "dict_japan"; }
                else if (language.StartsWith("kor")) { model = "rec_korean_PP-OCRv3_infer"; keys = "dict_korean"; }
                else if (language != "eng" && language != "eng+rus") { model = "latin_v5"; keys = model; }
                var items = RunNeural(temp,model,keys,token);
                if (language == "eng+rus") {
                    // The East Slavic recognizer reads English too. Use its Cyrillic lines, while
                    // retaining the English recognizer for Latin typography and UI punctuation.
                    var slavic = RunNeural(temp,"eslav_v5","eslav_v5",token);
                    foreach (var item in slavic) {
                        if (Regex.Matches(item.text ?? "",@"[\u0400-\u052f]").Count < 2) continue;
                        RectangleF box = ItemBounds(item,scale); int match = -1;
                        for (int i=0;i<items.Count;i++) {
                            RectangleF other = ItemBounds(items[i],scale), intersection = RectangleF.Intersect(box,other);
                            if (intersection.Width*intersection.Height > Math.Min(box.Width*box.Height,other.Width*other.Height)*0.5F) { match=i;break; }
                        }
                        if (match < 0) items.Add(item);
                        else if (item.score >= items[match].score-0.12F) items[match]=item;
                    }
                }
                var result = new OcrResult();
                foreach (var item in items) {
                    if (String.IsNullOrWhiteSpace(item.text) || item.score < 0.30F) continue;
                    var bounds = RectangleF.Intersect(ItemBounds(item,scale),new RectangleF(0,0,image.Width,image.Height));
                    if (bounds.Width < 2 || bounds.Height < 3 || !Regex.IsMatch(item.text,@"[\p{L}\p{N}]")) continue;
                    var block = new OcrBlock { Text = item.text.Trim(),Bounds = bounds,Lines = 1,Confidence = item.score };
                    RefineAppearance(image,block); result.Blocks.Add(block);
                }
                // Bucket by visual row, then sort each row horizontally (detector order is arbitrary).
                result.Blocks.Sort((a,b)=>a.Bounds.Top.CompareTo(b.Bounds.Top));
                int start=0;
                while (start<result.Blocks.Count) {
                    int end=start+1; var anchor=result.Blocks[start].Bounds;
                    while (end<result.Blocks.Count && result.Blocks[end].Bounds.Top < anchor.Top+anchor.Height*0.65F) end++;
                    result.Blocks.Sort(start,end-start,Comparer<OcrBlock>.Create((a,b)=>a.Bounds.Left.CompareTo(b.Bounds.Left))); start=end;
                }
                for (int i=0;i<result.Blocks.Count;i++) result.Blocks[i].Id=i;
                return result;
            } finally { try { File.Delete(temp); } catch { } }
        }
        private static RectangleF ItemBounds(NeuralItem item,double scale) {
            if (item.box == null || item.box.Length != 4) throw new IOException("OCR 返回了无效的文字位置。");
            float left=Single.MaxValue,top=Single.MaxValue,right=0,bottom=0;
            foreach (var point in item.box) { if (point.Length!=2) throw new IOException("OCR 位置格式无效。"); left=Math.Min(left,point[0]); top=Math.Min(top,point[1]); right=Math.Max(right,point[0]); bottom=Math.Max(bottom,point[1]); }
            return RectangleF.FromLTRB((float)(left/scale),(float)(top/scale),(float)(right/scale),(float)(bottom/scale));
        }
        private static List<NeuralItem> RunNeural(string image,string model,string keys,CancellationToken token) {
            string root = Path.Combine(Paths.Engine,"neural");
            // Native argv decoding varies with Windows' locale. The working directory can be
            // Unicode, but all arguments remain ASCII, including the relative screenshot path.
            string args = "--models=models --det=ch_PP-OCRv4_det_infer.onnx --rec="+model+".onnx --keys="+keys+".txt --numThread=2 --doAngle=0 --mostAngle=0 --padding=24 --maxSideLen=2880 --ensureAscii=1 --image_path=\"../../"+Path.GetFileName(image)+"\"";
            var psi = new ProcessStartInfo(Path.Combine(root,"RapidOCR-json.exe"),args) { WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
            psi.EnvironmentVariables["OMP_NUM_THREADS"]="2";
            Process started;
            try { started=Process.Start(psi); } catch (System.ComponentModel.Win32Exception) { throw new NeuralUnavailableException(); }
            using (var process = started) {
                var stdout=Task.Run(()=>process.StandardOutput.ReadToEnd()); var stderr=Task.Run(()=>process.StandardError.ReadToEnd()); var watch=Stopwatch.StartNew();
                while (!process.WaitForExit(50)) {
                    if (token.IsCancellationRequested || watch.ElapsedMilliseconds>25000) {
                        try { process.Kill(); process.WaitForExit(2000); } catch { }
                        token.ThrowIfCancellationRequested(); throw new TimeoutException("本地识别超时，请缩小截图区域后重试。");
                    }
                }
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0) throw new NeuralUnavailableException();
                foreach (string line in stdout.Result.Split('\n')) {
                    string clean=line.Trim(); if (!clean.StartsWith("{")) continue;
                    var meta=Json.Parse<Dictionary<string,object>>(clean); int code=Convert.ToInt32(meta["code"],CultureInfo.InvariantCulture);
                    if (code==101) return new List<NeuralItem>();
                    if (code!=100) throw new IOException("本地 OCR 无法读取截图（"+code+"）。");
                    var reply=Json.Parse<NeuralReply>(clean); return reply.data ?? new List<NeuralItem>();
                }
                throw new NeuralUnavailableException();
            }
        }
        private sealed class Component { public Rectangle Bounds, DarkBounds; public int Count; }
        internal static void RefineAppearance(Bitmap image,OcrBlock block) {
            block.Background=SampleBackground(image,block.Bounds);
            var crop=Rectangle.Intersect(Rectangle.Ceiling(block.Bounds),new Rectangle(Point.Empty,image.Size));
            if (crop.Width<1 || crop.Height<1) return;
            int width=crop.Width,height=crop.Height; var ink=new bool[width*height]; var strong=new bool[ink.Length]; var visited=new bool[ink.Length];
            int bestContrast=0; Color foreground=Color.Black;
            for (int y=0;y<height;y++) for (int x=0;x<width;x++) {
                Color c=image.GetPixel(crop.X+x,crop.Y+y);
                int contrast=Math.Max(Math.Abs(c.R-block.Background.R),Math.Max(Math.Abs(c.G-block.Background.G),Math.Abs(c.B-block.Background.B)));
                // Screen fonts often contain whole strokes rendered in light gray by subpixel
                // antialiasing. Include them in the erasure mask to avoid residual vertical lines.
                ink[y*width+x]=contrast>20;
                strong[y*width+x]=contrast>70;
                if (contrast>bestContrast) { bestContrast=contrast;foreground=c; }
            }
            block.Foreground=foreground;
            var components=new List<Component>(); var queue=new Queue<int>();
            for (int at=0;at<ink.Length;at++) {
                if (!ink[at] || visited[at]) continue;
                visited[at]=true;queue.Enqueue(at); int left=width,top=height,right=0,bottom=0,count=0,dark=0,dl=width,dt=height,dr=0,db=0;
                while (queue.Count>0) {
                    int index=queue.Dequeue(),x=index%width,y=index/width;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);count++;if (strong[index]) { dark++;dl=Math.Min(dl,x);dt=Math.Min(dt,y);dr=Math.Max(dr,x);db=Math.Max(db,y); }
                    for (int dy=-1;dy<=1;dy++) for (int dx=-1;dx<=1;dx++) {
                        int xx=x+dx,yy=y+dy;if (xx<0 || xx>=width || yy<0 || yy>=height) continue;
                        int next=yy*width+xx;if (!visited[next] && ink[next]) { visited[next]=true;queue.Enqueue(next); }
                    }
                }
                if (count>=2) components.Add(new Component { Bounds=new Rectangle(left+crop.X,top+crop.Y,right-left+1,bottom-top+1),Count=dark,DarkBounds=dark>0?new Rectangle(dl,dt,dr-dl+1,db-dt+1):Rectangle.Empty });
            }
            var heights=new List<int>();foreach (var c in components) if (c.Bounds.Height>=height*0.4 && c.Bounds.Width<height*5) heights.Add(c.Bounds.Height);
            if (heights.Count==0) return; heights.Sort(); float glyphHeight=heights[heights.Count/2];
            block.GlyphHeight=glyphHeight;
            components.Sort((a,b)=>a.Bounds.Left.CompareTo(b.Bounds.Left));
            RectangleF tight=RectangleF.Empty; int pixels=0,area=0;
            for (int i=0;i<components.Count;i++) {
                var c=components[i]; var r=c.Bounds;
                // Exclude divider rules and isolated, shorter edge icons (e.g. dropdown chevrons).
                bool rule=(r.Height<=2 && r.Width>glyphHeight*2) || (r.Width<=2 && r.Height>glyphHeight*1.8);
                int gap=0;
                if (i==components.Count-1 && i>0) gap=r.Left-components[i-1].Bounds.Right;
                if (i==0 && components.Count>1) gap=components[1].Bounds.Left-r.Right;
                string[] words=block.Text.Split(new [] {' '},StringSplitOptions.RemoveEmptyEntries);
                bool singleton=words.Length>1 && ((i==0 && words[0].Length==1) || (i==components.Count-1 && words[words.Length-1].Length==1));
                bool edge=i==0 || i==components.Count-1;
                bool explicitIcon=i==components.Count-1 && Regex.IsMatch(block.Text,@"[→↗↘←✓✔√]$") && gap>glyphHeight*0.25F;
                bool icon=components.Count>2 && edge && (explicitIcon || (r.Height<glyphHeight*0.65 && gap>Math.Max(5,glyphHeight*0.30)) || (!singleton && gap>Math.Max(8,glyphHeight*0.65)));
                if (rule || icon) { block.Obstacles.Add(r);continue; }
                block.Ink.Add(r);tight=tight.IsEmpty ? r : RectangleF.Union(tight,r);
                if (r.Height>=glyphHeight*0.65 && c.Count>0) { pixels+=c.Count;area+=c.DarkBounds.Width*c.DarkBounds.Height; }
            }
            if (tight.IsEmpty) return;
            string[] lastWords=block.Text.Trim().Split(new [] {' '},StringSplitOptions.RemoveEmptyEntries);
            if (block.Ink.Count>3 && lastWords.Length>0 && lastWords[lastWords.Length-1].Length>2) {
                var sorted=new List<RectangleF>(block.Ink);sorted.Sort((a,b)=>a.Left.CompareTo(b.Left));
                var tail=sorted[sorted.Count-1];int cut=sorted.Count-1;
                while (cut>0 && tail.Left-sorted[cut-1].Right<glyphHeight*0.45F) { cut--;tail=RectangleF.Union(tail,sorted[cut]); }
                if (cut>0 && tail.Width<glyphHeight*1.8F && tail.Left-sorted[cut-1].Right>=glyphHeight*0.45F) {
                    for (int i=cut;i<sorted.Count;i++) { block.Ink.Remove(sorted[i]);block.Obstacles.Add(sorted[i]); }
                    tight=RectangleF.Empty;foreach (var r in block.Ink) tight=tight.IsEmpty?r:RectangleF.Union(tight,r);
                }
            }
            if (block.Obstacles.Count>0) block.Text=Regex.Replace(block.Text,@"\s*[→↗↘←✓✔√]\s*$","");
            block.Bounds=tight; block.Bold=area>0 && (float)pixels/area>0.58F;
        }
    }
}
