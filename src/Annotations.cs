using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace QuickZh {
    public enum DrawTool { Move, Arrow, Ellipse, Line, Pen, Rectangle, Text, Protect, Number, Mosaic }
    public sealed class Mark {
        public DrawTool Tool; public Color Color; public float Width;
        public string Text = ""; public float FontSize = 36, TextWidth = 300; public int Sequence;
        public readonly List<PointF> Points = new List<PointF>();
        public Mark Clone() { var mark = new Mark(); mark.Apply(this); return mark; }
        public void Apply(Mark other) { Tool = other.Tool; Color = other.Color; Width = other.Width; Text = other.Text; FontSize = other.FontSize; TextWidth = other.TextWidth; Sequence=other.Sequence;Points.Clear(); Points.AddRange(other.Points); }
        public RectangleF TextBounds(Graphics g) {
            if (Points.Count == 0) return RectangleF.Empty;
            using (var font = new Font("Microsoft YaHei UI",FontSize,FontStyle.Regular,GraphicsUnit.Pixel)) {
                var size = g.MeasureString(Text,font,(int)Math.Max(1,TextWidth));
                return new RectangleF(Points[0],size);
            }
        }
        public void Draw(Graphics g) {
            if (Tool == DrawTool.Protect || Tool==DrawTool.Mosaic) return;
            if (Tool==DrawTool.Number && Points.Count>0) {
                var bounds=Bounds(g);using (var brush=new SolidBrush(Color)) g.FillEllipse(brush,bounds);
                using (var pen=new Pen(Color.White,Math.Max(1,FontSize/15))) g.DrawEllipse(pen,bounds);
                using (var font=new Font("Microsoft YaHei UI",FontSize,FontStyle.Bold,GraphicsUnit.Pixel)) using (var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center }) g.DrawString(Sequence.ToString(),font,Color.GetBrightness()>0.65F?Brushes.Black:Brushes.White,bounds,format);
                return;
            }
            if (Tool == DrawTool.Text && Points.Count > 0 && !String.IsNullOrWhiteSpace(Text)) {
                using (var font = new Font("Microsoft YaHei UI",FontSize,FontStyle.Regular,GraphicsUnit.Pixel)) using (var brush = new SolidBrush(Color)) {
                    var bounds = TextBounds(g); bounds.Width = TextWidth; bounds.Height += 4;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; g.DrawString(Text,font,brush,bounds);
                }
                return;
            }
            if (Points.Count < 2) return;
            var first = Points[0]; var last = Points[Points.Count - 1];
            using (var pen = new Pen(Color, Width)) {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                var r = RectangleF.FromLTRB(Math.Min(first.X,last.X), Math.Min(first.Y,last.Y), Math.Max(first.X,last.X), Math.Max(first.Y,last.Y));
                if (Tool == DrawTool.Ellipse) { if (r.Width > 0 && r.Height > 0) g.DrawEllipse(pen,r); }
                else if (Tool == DrawTool.Rectangle) g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);
                else if (Tool == DrawTool.Pen) g.DrawLines(pen,Points.ToArray());
                else if (Tool == DrawTool.Arrow) {
                    using (var arrow = new AdjustableArrowCap(4,5,true)) { pen.CustomEndCap = arrow; g.DrawLine(pen,first,last); }
                } else g.DrawLine(pen,first,last);
            }
        }
        public RectangleF Bounds(Graphics g) {
            if (Points.Count==0) return RectangleF.Empty;
            if (Tool==DrawTool.Text) return TextBounds(g);
            if (Tool==DrawTool.Number) {
                float diameter=FontSize*1.8F;
                using (var font=new Font("Microsoft YaHei UI",FontSize,FontStyle.Bold,GraphicsUnit.Pixel)) diameter=Math.Max(diameter,g.MeasureString(Sequence.ToString(),font).Width+FontSize*0.5F);
                return new RectangleF(Points[0].X-diameter/2,Points[0].Y-diameter/2,diameter,diameter);
            }
            float left=Single.MaxValue,top=Single.MaxValue,right=Single.MinValue,bottom=Single.MinValue;
            foreach (var p in Points) { left=Math.Min(left,p.X);top=Math.Min(top,p.Y);right=Math.Max(right,p.X);bottom=Math.Max(bottom,p.Y); }
            return RectangleF.FromLTRB(left,top,right,bottom);
        }
        public bool Hit(PointF point,Graphics g,float tolerance) {
            var bounds=Bounds(g);
            if (Tool==DrawTool.Text || Tool==DrawTool.Number || Tool==DrawTool.Rectangle || Tool==DrawTool.Ellipse || Tool==DrawTool.Mosaic || Tool==DrawTool.Protect) { bounds.Inflate(tolerance,tolerance);return bounds.Contains(point); }
            for (int i=1;i<Points.Count;i++) {
                var a=Points[i-1];var b=Points[i];float dx=b.X-a.X,dy=b.Y-a.Y,denominator=dx*dx+dy*dy;
                float t=denominator<0.001F?0:Math.Max(0,Math.Min(1,((point.X-a.X)*dx+(point.Y-a.Y)*dy)/denominator));
                float x=a.X+t*dx-point.X,y=a.Y+t*dy-point.Y;if (x*x+y*y<=tolerance*tolerance) return true;
            }return false;
        }
        public void MoveFrom(Mark before,float dx,float dy) { Apply(before);for (int i=0;i<Points.Count;i++) Points[i]=new PointF(Points[i].X+dx,Points[i].Y+dy); }
        public void ResizeFrom(Mark before,RectangleF original,RectangleF target) {
            Apply(before);float sx=target.Width/Math.Max(1,original.Width),sy=target.Height/Math.Max(1,original.Height);
            if (Tool==DrawTool.Text) { FontSize=Math.Max(6,before.FontSize*sx);TextWidth=Math.Max(20,before.TextWidth*sx);Points[0]=target.Location; }
            else if (Tool==DrawTool.Number) { FontSize=Math.Max(6,before.FontSize*sx);Points[0]=new PointF(target.Left+target.Width/2,target.Top+target.Height/2); }
            else for (int i=0;i<Points.Count;i++) Points[i]=new PointF(target.Left+(before.Points[i].X-original.Left)*sx,target.Top+(before.Points[i].Y-original.Top)*sy);
        }
    }
    public sealed class CaptureDocument : IDisposable {
        private Bitmap erasedBackground;
        private string erasedKey;
        public readonly Bitmap Original;
        public string OriginalText = "", Translation = "", Placeholder = "正在识别文字…";
        public readonly List<OcrBlock> Blocks = new List<OcrBlock>();
        public readonly List<Mark> Marks = new List<Mark>();
        private sealed class Change { public Mark Item, Before, After; public int Index; public bool Added; }
        private readonly Stack<Change> undo = new Stack<Change>(), redo = new Stack<Change>();
        public bool HasUndo { get { return undo.Count > 0; } }
        public bool HasRedo { get { return redo.Count > 0; } }
        public CaptureDocument(Bitmap original) { Original = original; }
        public void Add(Mark mark) { if (mark.Points.Count < (mark.Tool == DrawTool.Text || mark.Tool==DrawTool.Number ? 1 : 2)) return; undo.Push(new Change { Item = mark, Added = true, Index = Marks.Count }); Marks.Add(mark); redo.Clear(); }
        public int NextNumber { get { int value=0;foreach (var m in Marks) if (m.Tool==DrawTool.Number) value=Math.Max(value,m.Sequence);return value+1; } }
        public Mark HitMark(PointF point,Graphics g,float tolerance) { for (int i=Marks.Count-1;i>=0;i--) if (Marks[i].Hit(point,g,tolerance)) return Marks[i];return null; }
        public void RecordChange(Mark mark,Mark before) { undo.Push(new Change { Item = mark, Before = before, After = mark.Clone() }); redo.Clear(); }
        public void Undo() { if (undo.Count == 0) return; var change = undo.Pop(); if (change.Added) Marks.Remove(change.Item); else change.Item.Apply(change.Before); redo.Push(change); }
        public void Redo() { if (redo.Count == 0) return; var change = redo.Pop(); if (change.Added) Marks.Insert(Math.Min(change.Index,Marks.Count),change.Item); else change.Item.Apply(change.After); undo.Push(change); }
        public Mark HitText(PointF point,Graphics g) {
            for (int i=Marks.Count-1;i>=0;i--) if (Marks[i].Tool == DrawTool.Text) { var bounds = Marks[i].TextBounds(g); bounds.Inflate(4,4); if (bounds.Contains(point)) return Marks[i]; }
            return null;
        }
        public PointF ToDocument(Point position, Size viewport) {
            return new PointF(Math.Max(0, Math.Min(Original.Width, (float)position.X * Original.Width / Math.Max(1,viewport.Width))), Math.Max(0,Math.Min(Original.Height,(float)position.Y * Original.Height / Math.Max(1,viewport.Height))));
        }
        public static Font TextFont(Graphics g, string text, Size size, out float textHeight) {
            float availableW = Math.Max(1,size.Width-24), availableH = Math.Max(1,size.Height-24);
            float pixels = Math.Min(21F, Math.Max(12F,size.Height/4F));
            while (true) {
                var font = new Font("Microsoft YaHei UI",pixels,FontStyle.Regular,GraphicsUnit.Pixel);
                textHeight = g.MeasureString(text,font,(int)availableW).Height;
                if (textHeight <= availableH || pixels <= 12F) return font;
                font.Dispose(); pixels -= 1F;
            }
        }
        public void Paint(Graphics g, Size size, bool sourceView, float scroll, Mark pending) {
            bool mosaic=pending!=null && pending.Tool==DrawTool.Mosaic;
            foreach (var mark in Marks) mosaic|=mark.Tool==DrawTool.Mosaic;
            if (!mosaic) { PaintCore(g,size,sourceView,scroll,pending,null);return; }
            using (var layer=new Bitmap(Math.Max(1,size.Width),Math.Max(1,size.Height),PixelFormat.Format32bppArgb)) {
                using (var drawing=Graphics.FromImage(layer)) PaintCore(drawing,size,sourceView,scroll,pending,layer);
                g.DrawImageUnscaled(layer,0,0);
            }
        }
        private void PaintCore(Graphics g, Size size, bool sourceView, float scroll, Mark pending,Bitmap raster) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (sourceView || Blocks.Count > 0 || String.IsNullOrEmpty(Translation)) {
                Bitmap background=Original;
                if (!sourceView && Blocks.Count>0) {
                    string key=TextErasure.Key(Blocks);
                    if (erasedBackground==null || erasedKey!=key) {
                        if (erasedBackground!=null) erasedBackground.Dispose();
                        erasedBackground=TextErasure.Render(Original,Blocks);erasedKey=key;
                    }
                    background=erasedBackground;
                }
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(background,new Rectangle(Point.Empty,size));
                if (!sourceView && Blocks.Count > 0) {
                    var layoutState = g.Save(); g.ScaleTransform((float)size.Width/Original.Width,(float)size.Height/Original.Height);
                    foreach (var block in Blocks) if (!String.IsNullOrWhiteSpace(block.Translation)) PaintBlock(g,block);
                    g.Restore(layoutState);
                }
            }
            else {
                g.Clear(Color.FromArgb(251,252,255));
                string text = String.IsNullOrEmpty(Translation) ? Placeholder : Translation;
                float height;
                using (var font = TextFont(g,text,size,out height))
                using (var brush = new SolidBrush(String.IsNullOrEmpty(Translation) ? Theme.Muted : Theme.Ink)) {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (var format = new StringFormat(StringFormatFlags.LineLimit)) {
                        g.DrawString(text,font,brush,new RectangleF(12,12-scroll,Math.Max(1,size.Width-24),Math.Max(size.Height+scroll,height+8)),format);
                    }
                }
            }
            var state = g.Save();
            g.ScaleTransform((float)size.Width/Original.Width,(float)size.Height/Original.Height);
            // Restore protected image regions before drawing user annotations. These are never
            // synthesized by the translator and remain identical to the captured bitmap.
            foreach (var mark in Marks) if (mark.Tool == DrawTool.Protect && mark.Points.Count>=2) {
                var a=mark.Points[0];var b=mark.Points[mark.Points.Count-1];
                var region=Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Min(a.X,b.X),(int)Math.Min(a.Y,b.Y),(int)Math.Ceiling(Math.Max(a.X,b.X)),(int)Math.Ceiling(Math.Max(a.Y,b.Y))),new Rectangle(Point.Empty,Original.Size));
                if (!region.IsEmpty) { var saved=g.Save();g.SetClip(region,CombineMode.Intersect);g.DrawImageUnscaled(Original,0,0);g.Restore(saved); }
            }
            foreach (var mark in Marks) DrawMark(g,mark,raster,size); if (pending != null) DrawMark(g,pending,raster,size);
            g.Restore(state);
        }
        private void DrawMark(Graphics g,Mark mark,Bitmap raster,Size size) {
            if (mark.Tool!=DrawTool.Mosaic) { mark.Draw(g);return; }
            if (raster==null || mark.Points.Count<2) return;
            var b=mark.Bounds(g);float sx=(float)size.Width/Original.Width,sy=(float)size.Height/Original.Height;
            var region=Rectangle.Intersect(Rectangle.Ceiling(new RectangleF(b.X*sx,b.Y*sy,b.Width*sx,b.Height*sy)),new Rectangle(Point.Empty,size));
            if (region.Width<1 || region.Height<1) return;
            int cell=Math.Max(3,(int)(Math.Max(12,mark.Width*6)*sx));
            g.Flush(FlushIntention.Sync);var saved=g.Save();g.ResetTransform();g.SmoothingMode=SmoothingMode.None;
            for (int y=region.Top;y<region.Bottom;y+=cell) for (int x=region.Left;x<region.Right;x+=cell) {
                int width=Math.Min(cell,region.Right-x),height=Math.Min(cell,region.Bottom-y),red=0,green=0,blue=0;
                for (int row=0;row<3;row++) for (int column=0;column<3;column++) { var color=raster.GetPixel(x+Math.Min(width-1,(2*column+1)*width/6),y+Math.Min(height-1,(2*row+1)*height/6));red+=color.R;green+=color.G;blue+=color.B; }
                using (var brush=new SolidBrush(Color.FromArgb(red/9,green/9,blue/9))) g.FillRectangle(brush,x,y,width,height);
            }g.Restore(saved);
        }
        private static void PaintBlock(Graphics g,OcrBlock block) {
            if (block.Translation.Trim()==block.Text.Trim()) return;
            var saved=g.Save();foreach (var obstacle in block.Obstacles) { var protectedBounds=obstacle;protectedBounds.Inflate(2,2);g.ExcludeClip(Rectangle.Ceiling(protectedBounds)); }
            TranslationLayout.Paint(g,block);
            g.Restore(saved);
        }
        public Bitmap Export(bool sourceView, Size size, float scroll) {
            var result = new Bitmap(Math.Max(1,size.Width),Math.Max(1,size.Height),PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result)) Paint(g,result.Size,sourceView,scroll,null);
            return result;
        }
        public void Dispose() { if (erasedBackground!=null) erasedBackground.Dispose();Original.Dispose(); }
    }
}
