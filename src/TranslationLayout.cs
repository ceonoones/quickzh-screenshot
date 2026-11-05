using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.RegularExpressions;

namespace QuickZh {
    internal static class TranslationLayout {
        internal sealed class Line {
            internal string Text;
            internal RectangleF Ink;
            internal float X,Y,Pixels;
        }
        internal static float EstimateFont(OcrBlock block) {
            string text=block.Text.Trim();
            using (var family=new FontFamily(Regex.IsMatch(text,@"[\u3000-\u9fff]")?"Microsoft YaHei UI":"Arial"))
            using (var path=new GraphicsPath()) using (var format=(StringFormat)StringFormat.GenericTypographic.Clone()) {
                path.AddString(text,family,(int)(block.Bold?FontStyle.Bold:FontStyle.Regular),100,PointF.Empty,format);
                float height=path.GetBounds().Height;
                bool capitals=Regex.IsMatch(text,@"[A-Z]") && !Regex.IsMatch(text,@"[a-z]");
                float sourceHeight=capitals && block.GlyphHeight>0?Math.Min(block.Bounds.Height,block.GlyphHeight*1.13F):block.Bounds.Height/Math.Max(1,block.Lines);
                return Math.Max(7,Math.Min(200,sourceHeight*100/Math.Max(30,height/Math.Max(1,block.Lines))));
            }
        }
        private static int Difference(Color a,Color b) { return Math.Max(Math.Abs(a.R-b.R),Math.Max(Math.Abs(a.G-b.G),Math.Abs(a.B-b.B))); }
        private static int Edge(Bitmap image,int start,int fixedAt,int direction,bool horizontal,Color bg,int maximum) {
            int limit=horizontal?image.Width:image.Height;
            for (int step=0;step<maximum;step++) {
                int value=start+direction*step;if (value<0 || value>=limit) return -1;
                Color color=horizontal?image.GetPixel(value,fixedAt):image.GetPixel(fixedAt,value);
                if (Difference(color,bg)>40) return value;
            }return -1;
        }
        private static void FindButton(Bitmap image,OcrBlock block) {
            var b=block.Bounds;int cx=(int)(b.Left+b.Width/2),cy=(int)(b.Top+b.Height/2);
            int left=Edge(image,(int)b.Left-3,cy,-1,true,block.Background,(int)(block.FontPixels*12));
            int right=Edge(image,(int)b.Right+3,cy,1,true,block.Background,(int)(block.FontPixels*12));
            int top=Edge(image,(int)b.Top-3,cx,-1,false,block.Background,(int)(block.FontPixels*3));
            int bottom=Edge(image,(int)b.Bottom+3,cx,1,false,block.Background,(int)(block.FontPixels*3));
            if (left<0 || right<0 || top<0 || bottom<0) return;
            float height=bottom-top,width=right-left;
            if (height<block.FontPixels*1.3F || height>block.FontPixels*4 || width<b.Width+8 || width>block.FontPixels*38) return;
            // A closed flat area is a button only when its boundary is consistent at the corners.
            int hits=0;
            foreach (int x in new [] {left+2,cx,right-2}) foreach (int y in new [] {top+2,bottom-2})
                if (Difference(image.GetPixel(Math.Max(0,Math.Min(image.Width-1,x)),Math.Max(0,Math.Min(image.Height-1,y))),block.Background)<40) hits++;
            if (hits<5) return;
            block.CenterText=true;block.LayoutBounds=RectangleF.FromLTRB(left+5,top+4,right-5,bottom-4);
        }
        internal static void Prepare(Bitmap image,OcrResult result) {
            for (int i=0;i<result.Blocks.Count-1;i++) {
                var a=result.Blocks[i];var b=result.Blocks[i+1];
                if (Math.Abs(a.Bounds.Top-b.Bounds.Top)<Math.Min(a.Bounds.Height,b.Bounds.Height)*0.25F && b.Bounds.Left>a.Bounds.Left && b.Bounds.Left-a.Bounds.Right<Math.Min(a.Bounds.Height,b.Bounds.Height)*0.6F) {
                    a.Text+=" "+b.Text;a.Bounds=RectangleF.Union(a.Bounds,b.Bounds);a.Ink.AddRange(b.Ink);a.Obstacles.AddRange(b.Obstacles);result.Blocks.RemoveAt(i+1);i--;
                }
            }
            foreach (var b in result.Blocks) {
                b.FontPixels=EstimateFont(b);b.LineAdvance=b.FontPixels*1.25F;
                b.LayoutBounds=b.Bounds;
                FindButton(image,b);
                int cy=Math.Max(0,Math.Min(image.Height-1,(int)(b.Bounds.Top+b.Bounds.Height/2)));
                for (int x=Math.Max(0,(int)(b.Bounds.Left-b.FontPixels*1.3F));x<(int)(b.Bounds.Left-b.FontPixels*0.3F);x++) if (Difference(image.GetPixel(x,cy),b.Background)>80) { b.IsList=true;break; }
            }
            // Join actual wrapped prose. Keep titles, lists and independently placed UI labels separate.
            for (int i=0;i<result.Blocks.Count-1;i++) {
                var a=result.Blocks[i];var b=result.Blocks[i+1];
                bool prose=!a.CenterText && !b.CenterText && !a.IsList && !b.IsList && a.FontPixels<45 && a.Text.Length>38 &&
                    !Regex.IsMatch(a.Text.TrimEnd(),@"[.!?。！？:]$") && !Regex.IsMatch(b.Text,@"^[•●▪\-]") &&
                    Math.Abs(a.Bounds.Left-b.Bounds.Left)<Math.Max(8,a.FontPixels*0.45F) &&
                    b.Bounds.Top>=a.Bounds.Bottom && b.Bounds.Top-a.Bounds.Bottom<a.FontPixels*0.9F &&
                    Math.Abs(a.FontPixels-b.FontPixels)<a.FontPixels*0.36F && Difference(a.Background,b.Background)<35;
                if (!prose) continue;
                a.LineAdvance=(b.Bounds.Top-a.Bounds.Top)/Math.Max(1,a.Lines);
                a.FontPixels=Math.Min(a.FontPixels,b.FontPixels);
                a.Text+="\n"+b.Text;a.Lines+=b.Lines;a.Bounds=RectangleF.Union(a.Bounds,b.Bounds);
                a.LayoutBounds=a.Bounds;a.Ink.AddRange(b.Ink);a.Obstacles.AddRange(b.Obstacles);a.Confidence=Math.Min(a.Confidence,b.Confidence);
                result.Blocks.RemoveAt(i+1);i--;
            }
            for (int i=0;i<result.Blocks.Count;i++) result.Blocks[i].Id=i;
            foreach (var block in result.Blocks) {
                if (block.CenterText) continue;
                float right=Math.Min(image.Width-3,block.Bounds.Right+Math.Min(block.FontPixels*5,block.Bounds.Width*0.22F));
                foreach (var other in result.Blocks) if (other!=block && other.Bounds.Left>block.Bounds.Left && other.Bounds.Top<block.Bounds.Bottom && other.Bounds.Bottom>block.Bounds.Top) right=Math.Min(right,other.Bounds.Left-4);
                foreach (var obstacle in block.Obstacles) if (obstacle.Left>=block.Bounds.Right-2) right=Math.Min(right,obstacle.Left-4);
                // Grow into verified empty pixels rather than reducing the em size immediately.
                // Strong edges, icons and illustration pixels stop the expansion.
                int stop=(int)block.Bounds.Right;
                for (int x=stop+3;x<(int)right;x++) {
                    bool occupied=false;
                    for (int y=Math.Max(0,(int)block.Bounds.Top);y<Math.Min(image.Height,(int)block.Bounds.Bottom);y+=2) if (Difference(image.GetPixel(x,y),block.Background)>80) { occupied=true;break; }
                    if (occupied) break;stop=x;
                }
                block.LayoutBounds=new RectangleF(block.Bounds.Left,block.Bounds.Top,Math.Max(block.Bounds.Width,stop-block.Bounds.Left-3),block.Bounds.Height);
            }
        }
        private static float Width(string text,float pixels,FontFamily family,FontStyle style) {
            if (String.IsNullOrEmpty(text)) return 0;
            using (var path=new GraphicsPath()) using (var format=(StringFormat)StringFormat.GenericTypographic.Clone()) {
                path.AddString(text,family,(int)style,pixels,PointF.Empty,format);return path.GetBounds().Width;
            }
        }
        private static List<string> Wrap(string text,float width,float pixels,FontFamily family,FontStyle style) {
            var result=new List<string>();
            foreach (string paragraph in text.Replace("\r","").Split('\n')) {
                var line=new StringBuilder();
                foreach (char c in paragraph) {
                    if (line.Length>0 && Width(line.ToString()+c,pixels,family,style)>width) {
                        int split=line.ToString().LastIndexOf(' ');
                        if (split>line.Length/2 && c<128 && !Char.IsWhiteSpace(c)) { result.Add(line.ToString(0,split).TrimEnd());string tail=line.ToString(split+1,line.Length-split-1);line.Clear();line.Append(tail); }
                        else { result.Add(line.ToString().TrimEnd());line.Clear(); }
                    }
                    if (line.Length>0 || !Char.IsWhiteSpace(c)) line.Append(c);
                }
                if (line.Length>0) result.Add(line.ToString().TrimEnd());
            }return result;
        }
        internal static List<Line> Compose(OcrBlock block) {
            string cacheKey=block.Translation+"|"+block.Bounds+"|"+block.LayoutBounds+"|"+block.FontPixels+"|"+block.LineAdvance+"|"+block.Bold+"|"+block.CenterText;
            if (block.LayoutCache!=null && block.LayoutCacheKey==cacheKey) return block.LayoutCache;
            var bounds=block.LayoutBounds.IsEmpty?block.Bounds:block.LayoutBounds;
            float pixels=block.FontPixels>0?block.FontPixels:EstimateFont(block);
            float originalPixels=pixels;
            float availableWidth=block.CenterText?bounds.Width:block.Bounds.Width;
            var style=block.Bold?FontStyle.Bold:FontStyle.Regular;
            using (var family=new FontFamily("Microsoft YaHei UI")) {
                List<string> wrapped;
                while (true) {
                    wrapped=Wrap(block.Translation.Trim(),Math.Max(1,availableWidth),pixels,family,style);
                    // Allow the em-size glyphs to exceed Latin cap height, but don't consume the
                    // next paragraph. Wrapping is tried before reducing any font size.
                    float advance=(block.LineAdvance>0?block.LineAdvance:originalPixels*1.25F)*(pixels/originalPixels);
                    float allowed=block.CenterText?bounds.Height:Math.Max(block.Bounds.Height+originalPixels*0.42F,Math.Max(1,block.Lines)*advance);
                    if ((wrapped.Count-1)*advance+pixels*0.9F<=allowed || pixels<=originalPixels*0.72F || pixels<=7) break;
                    if (availableWidth<bounds.Width) { availableWidth=bounds.Width;continue; }
                    pixels-=0.5F;
                }
                var result=new List<Line>();float lineAdvance=(block.LineAdvance>0?block.LineAdvance:originalPixels*1.25F)*(pixels/originalPixels);
                for (int i=0;i<wrapped.Count;i++) {
                    using (var path=new GraphicsPath()) using (var format=(StringFormat)StringFormat.GenericTypographic.Clone()) {
                        path.AddString(wrapped[i],family,(int)style,pixels,PointF.Empty,format);var ink=path.GetBounds();
                        float x=block.CenterText?bounds.Left+(bounds.Width-ink.Width)/2:block.Bounds.Left;
                        float y=block.CenterText?bounds.Top+(bounds.Height-ink.Height)/2:block.Bounds.Top+(block.Bounds.Height/Math.Max(1,block.Lines)-ink.Height)/2+i*lineAdvance;
                        result.Add(new Line { Text=wrapped[i],Pixels=pixels,Ink=ink,X=x,Y=y });
                    }
                }block.LayoutCacheKey=cacheKey;block.LayoutCache=result;return result;
            }
        }
        internal static void Paint(Graphics g,OcrBlock block) {
            using (var family=new FontFamily("Microsoft YaHei UI")) using (var brush=new SolidBrush(block.Foreground)) {
                foreach (var line in Compose(block)) {
                    using (var path=new GraphicsPath()) using (var format=(StringFormat)StringFormat.GenericTypographic.Clone()) {
                        path.AddString(line.Text,family,(int)(block.Bold?FontStyle.Bold:FontStyle.Regular),line.Pixels,PointF.Empty,format);
                        var state=g.Save();g.TranslateTransform(line.X-line.Ink.X,line.Y-line.Ink.Y);g.FillPath(brush,path);g.Restore(state);
                    }
                }
            }
        }
    }
}
