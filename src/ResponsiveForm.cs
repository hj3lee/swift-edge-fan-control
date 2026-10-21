using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SwiftEdgeFanControl {
    public class ResponsiveForm : Form {
        sealed class Metric {
            public Font Font;public Padding Margin,Padding;public Size Minimum;
        }
        [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
        readonly Dictionary<Control,Metric> metrics=new Dictionary<Control,Metric>();
        readonly System.Windows.Forms.Timer resizeTimer=new System.Windows.Forms.Timer();
        Size baseline;float multiplier=1,previous=-1;bool changing,ready;Action<float> changed;
        protected void EnableResponsiveText(Action<float> callback) {
            baseline=ClientSize;changed=callback;Record(this);ready=true;
            resizeTimer.Interval=100;resizeTimer.Tick+=delegate {resizeTimer.Stop();ApplyResponsive();};
            Resize+=delegate {Schedule();};LocationChanged+=delegate {Schedule();};
            Shown+=delegate {ApplyResponsive();};ApplyResponsive();
        }
        void Record(Control control) {
            metrics[control]=new Metric {Font=control.Font,Margin=control.Margin,Padding=control.Padding,Minimum=control.MinimumSize};
            foreach(Control child in control.Controls)Record(child);
        }
        void Schedule() {if(!ready||changing||IsDisposed)return;resizeTimer.Stop();resizeTimer.Start();}
        protected void SetTextMultiplier(float value) {multiplier=value;previous=-1;ApplyResponsive();}
        static Padding Scale(Padding p,float value) {return new Padding((int)(p.Left*value),(int)(p.Top*value),(int)(p.Right*value),(int)(p.Bottom*value));}
        void ApplyResponsive() {
            if(!ready||changing||ClientSize.Width<1)return;
            var area=Screen.FromControl(this).WorkingArea;
            float factor=Presentation.TextScale(ClientSize.Width,baseline.Width,area.Width,area.Height,baseline.Height)*multiplier;
            if(Math.Abs(factor-previous)<.015)return;
            previous=factor;changing=true;SuspendLayout();
            try {
                foreach(var pair in metrics) {
                    if(pair.Key.IsDisposed)continue;var m=pair.Value;
                    pair.Key.Font=new Font(m.Font.FontFamily,Math.Max(9,m.Font.Size*factor),m.Font.Style,m.Font.Unit);
                    pair.Key.Margin=Scale(m.Margin,factor);pair.Key.Padding=Scale(m.Padding,factor);
                    if(pair.Key!=this)pair.Key.MinimumSize=new Size((int)(m.Minimum.Width*factor),(int)(m.Minimum.Height*factor));
                }
                if(changed!=null)changed(factor);
            } finally {ResumeLayout(true);changing=false;}
        }
        protected override void WndProc(ref Message message) {
            const int DpiChanged=0x02E0,DisplayChanged=0x007E;
            if(message.Msg==DpiChanged && message.LParam!=IntPtr.Zero) {
                var rect=(NativeRect)Marshal.PtrToStructure(message.LParam,typeof(NativeRect));
                float dpi=(message.WParam.ToInt64()&0xffff)/96F;
                MinimumSize=new Size((int)(460*dpi),(int)(340*dpi));
                Bounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);previous=-1;
            }
            base.WndProc(ref message);
            if(message.Msg==DpiChanged||message.Msg==DisplayChanged)Schedule();
        }
        protected override void OnFormClosed(FormClosedEventArgs e) {resizeTimer.Stop();resizeTimer.Dispose();base.OnFormClosed(e);}
    }
}
