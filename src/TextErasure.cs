using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickZh {
    // Interpolate only the selected glyph masks from nearby original pixels. This is a local
    // approximation, not image generation; pixels outside those masks are copied unchanged.
    internal static class TextErasure {
        internal static bool Changed(OcrBlock block) { return !String.IsNullOrWhiteSpace(block.Translation) && block.Translation.Trim()!=block.Text.Trim(); }
        internal static string Key(List<OcrBlock> blocks) {
            var key=new StringBuilder();foreach (var b in blocks) if (Changed(b)) key.Append(b.Id).Append(':').Append(b.Bounds).Append(':').Append(b.Ink.Count).Append('|');return key.ToString();
        }
        private static void Mark(bool[] mask,int width,int height,RectangleF area,bool value) {
            var r=Rectangle.Intersect(Rectangle.Ceiling(area),new Rectangle(0,0,width,height));
            for (int y=r.Top;y<r.Bottom;y++) for (int x=r.Left;x<r.Right;x++) mask[y*width+x]=value;
        }
        internal static Bitmap Render(Bitmap original,List<OcrBlock> blocks) {
            int w=original.Width,h=original.Height;
            var output=new Bitmap(w,h,PixelFormat.Format32bppArgb);
            using (var g=Graphics.FromImage(output)) g.DrawImageUnscaled(original,0,0);
            var data=output.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try {
                var source=new byte[w*h*4];
                for (int y=0;y<h;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),source,y*w*4,w*4);
                var result=(byte[])source.Clone();var mask=new bool[w*h];var horizontal=new int[w*h];
                foreach (var block in blocks) if (Changed(block)) {
                    if (block.Ink.Count==0) { var r=block.Bounds;r.Inflate(1,1);Mark(mask,w,h,r,true); }
                    else foreach (var ink in block.Ink) { var r=ink;r.Inflate(2,2);Mark(mask,w,h,r,true); }
                }
                foreach (var block in blocks) foreach (var obstacle in block.Obstacles) { var r=obstacle;r.Inflate(2,2);Mark(mask,w,h,r,false); }
                for (int y=0;y<h;y++) for (int x=0;x<w;x++) {
                    if (!mask[y*w+x]) continue;int start=x;while (x<w && mask[y*w+x]) x++;int end=x;
                    int left=Math.Max(0,start-1),right=Math.Min(w-1,end),span=Math.Max(1,right-left);
                    for (int xx=start;xx<end;xx++) {
                        int index=y*w+xx,at=index*4;horizontal[index]=span;
                        for (int c=0;c<3;c++) result[at+c]=(byte)((source[(y*w+left)*4+c]*(right-xx)+source[(y*w+right)*4+c]*(xx-left))/span);
                    }
                }
                for (int x=0;x<w;x++) for (int y=0;y<h;y++) {
                    if (!mask[y*w+x]) continue;int start=y;while (y<h && mask[y*w+x]) y++;int end=y;
                    int top=Math.Max(0,start-1),bottom=Math.Min(h-1,end),span=Math.Max(1,bottom-top);
                    for (int yy=start;yy<end;yy++) {
                        int index=yy*w+x,at=index*4;float horizontalSpan=Math.Max(1,horizontal[index]);
                        float weight=horizontalSpan*horizontalSpan/(horizontalSpan*horizontalSpan+span*span);
                        for (int c=0;c<3;c++) {
                            float vertical=(source[(top*w+x)*4+c]*(bottom-yy)+source[(bottom*w+x)*4+c]*(yy-top))/(float)span;
                            result[at+c]=(byte)Math.Max(0,Math.Min(255,Math.Round(result[at+c]*(1-weight)+vertical*weight)));
                        }
                    }
                }
                for (int y=0;y<h;y++) Marshal.Copy(result,y*w*4,IntPtr.Add(data.Scan0,y*data.Stride),w*4);
            } finally { output.UnlockBits(data); }
            return output;
        }
    }
}
