using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuickZh {
    internal sealed class IconButton : Button {
        internal string Glyph;
        internal string ValueText="";
        internal bool Selected,HasError;
        internal Color Swatch=Color.FromArgb(239,68,68);
        private bool hovered;
        internal static readonly Color Panel=Color.FromArgb(29,33,45),Muted=Color.FromArgb(162,173,194),Active=Color.FromArgb(112,93,238);
        internal IconButton(string name,string glyph) {
            Text=name;AccessibleName=name;Glyph=glyph;Size=new Size(48,48);MinimumSize=Size;AutoSize=false;Margin=new Padding(0,0,3,0);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Panel;ForeColor=Color.White;Cursor=Cursors.Default;
            SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);
            MouseEnter+=(s,e)=> { hovered=true;Invalidate(); };MouseLeave+=(s,e)=> { hovered=false;Invalidate(); };
        }
        private static GraphicsPath Round(RectangleF r,float radius) {
            var p=new GraphicsPath();float d=radius*2;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Panel);
            using (var path=Round(new RectangleF(1,1,Width-2,Height-2),6)) using (var brush=new SolidBrush(Selected?Active:hovered?Color.FromArgb(49,56,76):Panel)) g.FillPath(brush,path);
            Color color=Enabled?(Selected?Color.White:Color.FromArgb(231,236,247)):Color.FromArgb(84,94,116);
            var state=g.Save();g.TranslateTransform((Width-32)/2F,(Height-32)/2F);g.ScaleTransform(32F/24,32F/24);
            using (var pen=new Pen(color,1.8F)) using (var brush=new SolidBrush(color)) {
                pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
                if (Glyph=="move") { g.DrawLines(pen,new [] {new PointF(5,4),new PointF(5,20),new PointF(9,15),new PointF(13,21),new PointF(16,19),new PointF(12,13),new PointF(19,12),new PointF(5,4)}); }
                else if (Glyph=="arrow") { g.DrawLine(pen,4,20,20,4);g.DrawLines(pen,new [] {new Point(11,4),new Point(20,4),new Point(20,13)}); }
                else if (Glyph=="rectangle") g.DrawRectangle(pen,4,5,16,14);
                else if (Glyph=="ellipse") g.DrawEllipse(pen,3,5,18,14);
                else if (Glyph=="line") g.DrawLine(pen,4,20,20,4);
                else if (Glyph=="pen") { g.DrawBezier(pen,3,18,7,1,10,23,15,8);g.DrawLine(pen,15,8,20,4); }
                else if (Glyph=="text") { g.DrawLine(pen,4,5,20,5);g.DrawLine(pen,12,5,12,20);g.DrawLine(pen,8,20,16,20); }
                else if (Glyph=="number") { g.DrawEllipse(pen,3,3,18,18);using (var font=new Font("Segoe UI",12,FontStyle.Bold,GraphicsUnit.Pixel)) using (var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center }) g.DrawString("1",font,brush,new RectangleF(3,3,18,18),format); }
                else if (Glyph=="mosaic") { for (int y=0;y<3;y++) for (int x=0;x<3;x++) using (var tile=new SolidBrush((x+y)%2==0?color:Color.FromArgb(100,113,140))) g.FillRectangle(tile,4+x*6,4+y*6,5,5); }
                else if (Glyph=="protect") { g.DrawLines(pen,new [] {new Point(12,2),new Point(21,6),new Point(19,16),new Point(12,22),new Point(5,16),new Point(3,6),new Point(12,2)});g.DrawLines(pen,new [] {new Point(7,12),new Point(11,16),new Point(17,9)}); }
                else if (Glyph=="undo" || Glyph=="redo") { if (Glyph=="redo") { g.TranslateTransform(24,0);g.ScaleTransform(-1,1); }g.DrawArc(pen,4,7,16,13,195,235);g.DrawLines(pen,new [] {new Point(3,12),new Point(3,5),new Point(10,5)}); }
                else if (Glyph=="original") { g.DrawBezier(pen,2,12,7,2,17,2,22,12);g.DrawBezier(pen,2,12,7,22,17,22,22,12);g.DrawEllipse(pen,9,9,6,6); }
                else if (Glyph=="translate") { using (var font=new Font("Microsoft YaHei UI",13,FontStyle.Bold,GraphicsUnit.Pixel)) g.DrawString("译",font,brush,3,2);g.DrawLine(pen,14,20,22,20); }
                else if (Glyph=="copyimage") { g.DrawRectangle(pen,7,3,14,14);g.DrawLines(pen,new [] {new Point(3,7),new Point(3,21),new Point(17,21)});g.DrawLines(pen,new [] {new Point(9,14),new Point(13,9),new Point(18,14)}); }
                else if (Glyph=="copytext") { g.DrawRectangle(pen,7,3,14,16);g.DrawLines(pen,new [] {new Point(3,7),new Point(3,22),new Point(16,22)});g.DrawLine(pen,11,8,17,8);g.DrawLine(pen,11,12,17,12);g.DrawLine(pen,11,16,15,16); }
                else if (Glyph=="save") { g.DrawRectangle(pen,4,3,16,18);g.DrawRectangle(pen,8,3,8,6);g.DrawRectangle(pen,8,14,8,7); }
                else if (Glyph=="stop") g.FillRectangle(brush,6,6,12,12);
                else if (Glyph=="close") { g.DrawLine(pen,5,5,19,19);g.DrawLine(pen,19,5,5,19); }
                else if (Glyph=="color") { using (var swatch=new SolidBrush(Swatch)) g.FillEllipse(swatch,4,4,16,16);g.DrawEllipse(pen,3,3,18,18); }
                else if (Glyph=="fontsize" || Glyph=="weight") {
                    if (Glyph=="weight") g.DrawLine(pen,2,17,16,17);
                    using (var font=new Font("Segoe UI",Glyph=="fontsize"?13:11,FontStyle.Regular,GraphicsUnit.Pixel)) using (var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center }) g.DrawString(ValueText,font,brush,new RectangleF(-3,Glyph=="weight"?-2:0,25,Glyph=="weight"?18:24),format);
                    g.FillPolygon(brush,new [] {new Point(20,10),new Point(24,10),new Point(22,13)});
                }
            }
            g.Restore(state);
            if (HasError) { using (var warning=new SolidBrush(Color.FromArgb(248,113,113))) g.FillEllipse(warning,Width-11,4,7,7); }
            if (Focused) using (var pen=new Pen(Color.FromArgb(130,140,170),1) { DashStyle=DashStyle.Dot }) g.DrawRectangle(pen,3,3,Width-7,Height-7);
        }
    }
}
