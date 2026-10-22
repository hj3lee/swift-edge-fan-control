using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
namespace SwiftEdgeFanControl {
    public static class Shapes {
        public static Color Surface(Control control) {
            for(var parent=control.Parent;parent!=null;parent=parent.Parent)
                if(parent.BackColor.A==255)return parent.BackColor;
            return Color.White;
        }
        public static GraphicsPath Round(RectangleF bounds,float radius) {
            float d=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));var path=new GraphicsPath();
            if(d<1){path.AddRectangle(bounds);return path;}
            path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
    }
    public class RoundedButton : Button {
        bool hover,pressed;
        public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Shapes.Surface(this));}
        protected override void OnPaint(PaintEventArgs e){
            var g=e.Graphics;g.Clear(Shapes.Surface(this));g.SmoothingMode=SmoothingMode.AntiAlias;
            Color color=pressed?FlatAppearance.MouseDownBackColor:hover?FlatAppearance.MouseOverBackColor:BackColor;
            using(var path=Shapes.Round(new RectangleF(.5F,.5F,Math.Max(1,Width-1),Math.Max(1,Height-1)),Math.Max(5,Font.Height*.30F)))
            using(var brush=new SolidBrush(color))using(var pen=new Pen(FlatAppearance.BorderColor)){g.FillPath(brush,path);if(FlatAppearance.BorderSize>0)g.DrawPath(pen,path);}
            Color text=pressed&&color==Ui.Accent?Color.White:ForeColor;
            TextRenderer.DrawText(g,Text,Font,ClientRectangle,Enabled?text:Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            if(Focused&&ShowFocusCues){var r=ClientRectangle;r.Inflate(-5,-5);ControlPaint.DrawFocusRectangle(g,r,ForeColor,color);}
        }
    }
    public class RoundedTable : TableLayoutPanel {
        public RoundedTable(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
        protected override void OnPaint(PaintEventArgs e){OnPaintBackground(e);base.OnPaint(e);}
        protected override void OnPaintBackground(PaintEventArgs e){
            e.Graphics.Clear(Shapes.Surface(this));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Shapes.Round(new RectangleF(0,0,Width-1,Height-1),Math.Max(8,Font.Height*.5F)))
            using(var brush=new SolidBrush(BackColor))e.Graphics.FillPath(brush,path);
        }
    }
}
