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
        private static bool Boundary(Bitmap image,RectangleF box,Color background,bool vertical,bool end) {
            int hits=0,interior=0;
            for (int i=1;i<=7;i++) {
                int x=(int)(vertical?(end?box.Right:box.Left):box.Left+box.Width*i/8);
                int y=(int)(vertical?box.Top+box.Height*i/8:(end?box.Bottom:box.Top));
                int insideX=x+(vertical?(end?-3:3):0),insideY=y+(vertical?0:(end?-3:3));
                if (x<0 || x>=image.Width || y<0 || y>=image.Height || insideX<0 || insideX>=image.Width || insideY<0 || insideY>=image.Height) continue;
                if (Difference(image.GetPixel(x,y),background)>32) hits++;
                if (Difference(image.GetPixel(insideX,insideY),background)<40) interior++;
            }
            return hits>=5 && interior>=5;
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
            var box=RectangleF.FromLTRB(left,top,right,bottom);
            // Other glyphs and image edges must not masquerade as a closed rectangular control.
            if (!Boundary(image,box,block.Background,true,false) || !Boundary(image,box,block.Background,true,true) ||
                !Boundary(image,box,block.Background,false,false) || !Boundary(image,box,block.Background,false,true)) return;
            if (Math.Abs((left+right)/2F-(b.Left+b.Width/2))>block.FontPixels*0.85F || Math.Abs((top+bottom)/2F-(b.Top+b.Height/2))>block.FontPixels*0.65F) return;
            block.CenterText=true;block.LayoutBounds=RectangleF.FromLTRB(left+5,top+4,right-5,bottom-4);
        }
        private static bool ListMarker(Bitmap image,OcrBlock block) {
            if (Regex.IsMatch(block.Text,@"^\s*[•●▪\-]\s")) return true;
            float em=block.FontPixels;
            var r=Rectangle.Intersect(Rectangle.FromLTRB((int)(block.Bounds.Left-em*1.8F),(int)(block.Bounds.Top+block.Bounds.Height/2-em*0.4F),(int)(block.Bounds.Left-em*0.15F),(int)(block.Bounds.Top+block.Bounds.Height/2+em*0.4F)),new Rectangle(Point.Empty,image.Size));
            if (r.Width<2 || r.Height<2) return false;
            var visited=new bool[r.Width*r.Height];var queue=new Queue<Point>();
            for (int y=0;y<r.Height;y++) for (int x=0;x<r.Width;x++) {
                int at=y*r.Width+x;if (visited[at]) continue;visited[at]=true;
                if (Difference(image.GetPixel(r.X+x,r.Y+y),block.Background)<75) continue;
                queue.Enqueue(new Point(x,y));int l=x,t=y,right=x,bottom=y,count=0;
                while (queue.Count>0) {
                    var p=queue.Dequeue();count++;l=Math.Min(l,p.X);t=Math.Min(t,p.Y);right=Math.Max(right,p.X);bottom=Math.Max(bottom,p.Y);
                    foreach (var q in new [] {new Point(p.X-1,p.Y),new Point(p.X+1,p.Y),new Point(p.X,p.Y-1),new Point(p.X,p.Y+1)}) {
                        if (q.X<0 || q.X>=r.Width || q.Y<0 || q.Y>=r.Height) continue;int index=q.Y*r.Width+q.X;
                        if (visited[index]) continue;visited[index]=true;
                        if (Difference(image.GetPixel(r.X+q.X,r.Y+q.Y),block.Background)>=75) queue.Enqueue(q);
                    }
                }
                float w=right-l+1,h=bottom-t+1;
                if (count>=3 && l>0 && t>0 && right<r.Width-1 && bottom<r.Height-1 && w<=em*0.55F && h<=em*0.55F && w/h>0.4F && w/h<2.5F && Math.Abs(r.Top+(t+bottom)/2F-(block.Bounds.Top+block.Bounds.Height/2))<em*0.22F) return true;
            }
            return false;
        }
        private static bool SameRow(OcrBlock a,OcrBlock b) {
            return b.Bounds.Left>a.Bounds.Left && b.Bounds.Left>=a.Bounds.Right-Math.Min(a.FontPixels,b.FontPixels)*0.55F && b.Bounds.Left-a.Bounds.Right<Math.Min(a.FontPixels,b.FontPixels)*0.75F &&
                Math.Abs((a.Bounds.Top+a.Bounds.Height/2)-(b.Bounds.Top+b.Bounds.Height/2))<Math.Max(a.Bounds.Height,b.Bounds.Height)*0.28F &&
                Math.Abs(a.FontPixels-b.FontPixels)<Math.Max(a.FontPixels,b.FontPixels)*0.26F && Difference(a.Background,b.Background)<45 && Difference(a.Foreground,b.Foreground)<65;
        }
        private static void Join(OcrBlock a,OcrBlock b,bool wrapped) {
            if (wrapped) {
                float pitch=(b.Bounds.Top-a.Bounds.Top)/Math.Max(1,a.Lines);
                a.LineAdvance=a.Lines>1?(a.LineAdvance*(a.Lines-1)+pitch)/a.Lines:pitch;
                a.Lines+=b.Lines;
            } else a.FirstLineHeight=Math.Max(a.FirstLineHeight,b.FirstLineHeight);
            a.FontPixels=wrapped?Math.Min(a.FontPixels,b.FontPixels):(a.FontPixels+b.FontPixels)/2;
            string added=b.Text;
            if (!wrapped && a.Bounds.Right>b.Bounds.Left) {
                for (int n=Math.Min(a.Text.Length,b.Text.Length);n>0;n--) if ((n==b.Text.Length || Char.IsWhiteSpace(b.Text[n])) && a.Text.EndsWith(b.Text.Substring(0,n),StringComparison.OrdinalIgnoreCase)) { added=b.Text.Substring(n).TrimStart();break; }
            }
            a.Text+=(wrapped?"\n":" ")+added;a.Bounds=RectangleF.Union(a.Bounds,b.Bounds);
            a.Ink.AddRange(b.Ink);a.Obstacles.AddRange(b.Obstacles);a.Confidence=Math.Min(a.Confidence,b.Confidence);
        }
        private static bool Wrapped(OcrBlock a,OcrBlock b,out bool centered) {
            centered=false;float em=Math.Min(a.FontPixels,b.FontPixels);
            if (a.CenterText || b.CenterText || a.IsList || b.IsList || Math.Abs(a.FontPixels-b.FontPixels)>em*0.34F ||
                Difference(a.Background,b.Background)>40 || Difference(a.Foreground,b.Foreground)>65 || b.Bounds.Top<a.Bounds.Bottom-em*0.1F || b.Bounds.Top-a.Bounds.Bottom>em*0.62F) return false;
            bool left=Math.Abs(a.Bounds.Left-b.Bounds.Left)<Math.Max(7,em*0.55F);
            centered=!left && Math.Abs((a.Bounds.Left+a.Bounds.Width/2)-(b.Bounds.Left+b.Bounds.Width/2))<em*0.45F;
            if (!left && !centered) return false;
            if (RectangleF.Intersect(new RectangleF(a.Bounds.Left,0,a.Bounds.Width,1),new RectangleF(b.Bounds.Left,0,b.Bounds.Width,1)).Width<Math.Min(a.Bounds.Width,b.Bounds.Width)*0.6F) return false;
            return a.Lines>1 || a.Text.Length>=28 || (a.Text.Length>=16 && Regex.Matches(a.Text,@"\S+").Count>=3) || (em>=28 && a.Text.Contains(" ") && b.Text.Length>=4);
        }
        internal static void Prepare(Bitmap image,OcrResult result) {
            foreach (var b in result.Blocks) { b.Lines=Math.Max(1,b.Lines);b.FirstLineHeight=b.Bounds.Height/b.Lines;b.FontPixels=EstimateFont(b);b.LineAdvance=b.FontPixels*1.25F;b.LayoutBounds=b.Bounds; }
            // Compare optical row centers and em sizes, rather than tops of ascenders / descenders.
            for (int i=0;i<result.Blocks.Count-1;i++) {
                var a=result.Blocks[i];var b=result.Blocks[i+1];if (!SameRow(a,b)) continue;
                Join(a,b,false);result.Blocks.RemoveAt(i+1);i--;
            }
            foreach (var b in result.Blocks) { FindButton(image,b);b.IsList=!b.CenterText && ListMarker(image,b); }
            // Search within each column; intervening text in another column is not a paragraph boundary.
            for (int i=0;i<result.Blocks.Count;i++) {
                var a=result.Blocks[i];int candidate=-1;float next=Single.MaxValue;bool alignment=false;
                for (int j=i+1;j<result.Blocks.Count;j++) {
                    var b=result.Blocks[j];bool centered;
                    if (b.Bounds.Top<next && Wrapped(a,b,out centered)) { candidate=j;next=b.Bounds.Top;alignment=centered; }
                }
                if (candidate<0) continue;
                Join(a,result.Blocks[candidate],true);a.CenteredParagraph|=alignment;result.Blocks.RemoveAt(candidate);i--;
            }
            result.Blocks.Sort((a,b)=> { int row=a.Bounds.Top.CompareTo(b.Bounds.Top);return row==0?a.Bounds.Left.CompareTo(b.Bounds.Left):row; });
            int rowStart=0;
            while (rowStart<result.Blocks.Count) {
                int rowEnd=rowStart+1;var anchor=result.Blocks[rowStart].Bounds;
                while (rowEnd<result.Blocks.Count && result.Blocks[rowEnd].Bounds.Top<anchor.Top+anchor.Height*0.65F) rowEnd++;
                result.Blocks.Sort(rowStart,rowEnd-rowStart,Comparer<OcrBlock>.Create((a,b)=>a.Bounds.Left.CompareTo(b.Bounds.Left)));rowStart=rowEnd;
            }
            for (int i=0;i<result.Blocks.Count;i++) result.Blocks[i].Id=i;
            foreach (var block in result.Blocks) {
                if (block.CenterText) continue;
                float right=Math.Min(image.Width-1,block.Bounds.Right+Math.Min(block.FontPixels*5,block.Bounds.Width*0.22F));
                foreach (var other in result.Blocks) if (other!=block && other.Bounds.Left>block.Bounds.Left && other.Bounds.Top<block.Bounds.Bottom && other.Bounds.Bottom>block.Bounds.Top) right=Math.Min(right,other.Bounds.Left-4);
                foreach (var obstacle in block.Obstacles) if (obstacle.Left>=block.Bounds.Right-2) right=Math.Min(right,obstacle.Left-4);
                int stop=(int)block.Bounds.Right;
                for (int x=stop+3;x<(int)right;x++) {
                    bool occupied=false;
                    for (int y=Math.Max(0,(int)block.Bounds.Top);y<Math.Min(image.Height,(int)block.Bounds.Bottom);y+=2) if (Difference(image.GetPixel(x,y),block.Background)>80) { occupied=true;break; }
                    if (occupied) break;stop=x;
                }
                float width=Math.Max(block.Bounds.Width,stop-block.Bounds.Left-3);
                float bottom=Math.Min(image.Height-1,block.Bounds.Bottom+block.FontPixels*0.4F);
                foreach (var other in result.Blocks) if (other!=block && other.Bounds.Top>=block.Bounds.Bottom && other.Bounds.Left<block.Bounds.Left+width && other.Bounds.Right>block.Bounds.Left) bottom=Math.Min(bottom,other.Bounds.Top-2);
                block.LayoutBounds=new RectangleF(block.Bounds.Left,block.Bounds.Top,width,Math.Max(block.Bounds.Height,bottom-block.Bounds.Top));
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
                        if (split>0 && c<128 && !Char.IsWhiteSpace(c)) { result.Add(line.ToString(0,split).TrimEnd());string tail=line.ToString(split+1,line.Length-split-1);line.Clear();line.Append(tail); }
                        else if ("，。！？；：、）》】」』％%.,!?;:".IndexOf(c)>=0 && line.Length>1) { char carry=line[line.Length-1];line.Length--;result.Add(line.ToString().TrimEnd());line.Clear();line.Append(carry); }
                        else { result.Add(line.ToString().TrimEnd());line.Clear(); }
                    }
                    if (line.Length>0 || !Char.IsWhiteSpace(c)) line.Append(c);
                }
                if (line.Length>0) result.Add(line.ToString().TrimEnd());
            }return result;
        }
        private static List<Line> Measure(OcrBlock block,float width,float pixels,float original,FontFamily family,FontStyle style,out float totalHeight) {
            var result=new List<Line>();float tallest=0;
            var wrapped=Wrap(block.Translation.Trim(),Math.Max(1,width),pixels,family,style);
            if (block.Lines>=2 && block.Text.Length<80 && pixels>=28 && !block.Translation.Contains("\n") && wrapped.Count>1) {
                float best=Single.MaxValue;var balanced=wrapped;
                for (int step=0;step<=8;step++) {
                    var candidate=Wrap(block.Translation.Trim(),Math.Max(1,width*(1-step*0.055F)),pixels,family,style);
                    if (candidate.Count!=wrapped.Count) break;
                    float shortest=Single.MaxValue,longest=0;
                    foreach (var line in candidate) { float measure=Width(line,pixels,family,style);shortest=Math.Min(shortest,measure);longest=Math.Max(longest,measure); }
                    if (longest-shortest<best) { best=longest-shortest;balanced=candidate; }
                }
                wrapped=balanced;
            }
            foreach (string text in wrapped) {
                using (var path=new GraphicsPath()) using (var format=(StringFormat)StringFormat.GenericTypographic.Clone()) {
                    path.AddString(text,family,(int)style,pixels,PointF.Empty,format);var ink=path.GetBounds();
                    result.Add(new Line { Text=text,Pixels=pixels,Ink=ink });tallest=Math.Max(tallest,ink.Height);
                }
            }
            float advance=Math.Max((block.LineAdvance>0?block.LineAdvance:original*1.25F)*pixels/original,tallest+Math.Max(0.8F,pixels*0.08F));
            for (int i=0;i<result.Count;i++) result[i].Y=i*advance;
            totalHeight=result.Count==0?0:(result.Count-1)*advance+tallest;return result;
        }
        internal static List<Line> Compose(OcrBlock block) {
            string cacheKey=block.Translation+"|"+block.Bounds+"|"+block.LayoutBounds+"|"+block.FontPixels+"|"+block.LineAdvance+"|"+block.Bold+"|"+block.CenterText+"|"+block.CenteredParagraph;
            if (block.LayoutCache!=null && block.LayoutCacheKey==cacheKey) return block.LayoutCache;
            var bounds=block.LayoutBounds.IsEmpty?block.Bounds:block.LayoutBounds;
            float original=block.FontPixels>0?block.FontPixels:EstimateFont(block),width=block.CenterText?bounds.Width:Math.Min(bounds.Width,block.Bounds.Width),height;
            var style=block.Bold?FontStyle.Bold:FontStyle.Regular;
            using (var family=new FontFamily("Microsoft YaHei UI")) {
                var lines=Measure(block,width,original,original,family,style,out height);
                if (height>bounds.Height && width<bounds.Width) { width=bounds.Width;lines=Measure(block,width,original,original,family,style,out height); }
                if (height>bounds.Height) {
                    float lo=Math.Min(5,original),hi=original;
                    lines=Measure(block,width,lo,original,family,style,out height);
                    for (int step=0;step<12;step++) {
                        float mid=(lo+hi)/2;float measured;var trial=Measure(block,width,mid,original,family,style,out measured);
                        if (measured<=bounds.Height) { lo=mid;lines=trial;height=measured; } else hi=mid;
                    }
                }
                float offset=block.CenterText?Math.Max(0,(bounds.Height-height)/2):Math.Min(Math.Max(0,(block.FirstLineHeight-(lines.Count>0?lines[0].Ink.Height:0))/2),Math.Max(0,bounds.Height-height));
                foreach (var line in lines) {
                    line.X=block.CenterText || block.CenteredParagraph?bounds.Left+(bounds.Width-line.Ink.Width)/2:bounds.Left;
                    line.Y+=bounds.Top+offset;
                }
                block.LayoutCacheKey=cacheKey;block.LayoutCache=lines;return lines;
            }
        }
        internal static void Paint(Graphics g,OcrBlock block) {
            var clip=g.Save();g.SetClip(block.LayoutBounds.IsEmpty?block.Bounds:block.LayoutBounds,CombineMode.Intersect);
            using (var family=new FontFamily("Microsoft YaHei UI")) using (var brush=new SolidBrush(block.Foreground)) {
                foreach (var line in Compose(block)) {
                    using (var path=new GraphicsPath()) using (var format=(StringFormat)StringFormat.GenericTypographic.Clone()) {
                        path.AddString(line.Text,family,(int)(block.Bold?FontStyle.Bold:FontStyle.Regular),line.Pixels,PointF.Empty,format);
                        var state=g.Save();g.TranslateTransform(line.X-line.Ink.X,line.Y-line.Ink.Y);g.FillPath(brush,path);g.Restore(state);
                    }
                }
            }
            g.Restore(clip);
        }
    }
}
