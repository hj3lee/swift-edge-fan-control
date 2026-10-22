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
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wparam,IntPtr lparam);
        readonly Dictionary<Control,Metric> metrics=new Dictionary<Control,Metric>();
        readonly List<Control> controls=new List<Control>();
        readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
        readonly System.Windows.Forms.Timer resizeTimer=new System.Windows.Forms.Timer();
        Size baseline;float multiplier=1,previous=-1;bool changing,ready;Action<float> changed;Action afterLayout;
        protected void EnableResponsiveText(Action<float> callback,Action layoutComplete=null) {
            baseline=ClientSize;changed=callback;afterLayout=layoutComplete;Record(this);ready=true;DoubleBuffered=true;
            resizeTimer.Interval=100;resizeTimer.Tick+=delegate {resizeTimer.Stop();ApplyResponsive();};
            Resize+=delegate {Schedule();};LocationChanged+=delegate {Schedule();};
            Shown+=delegate {ApplyResponsive();};ApplyResponsive();
        }
        void Record(Control control) {
            metrics[control]=new Metric {Font=control.Font,Margin=control.Margin,Padding=control.Padding,Minimum=control.MinimumSize};
            controls.Add(control);
            foreach(Control child in control.Controls)Record(child);
        }
        void Schedule() {if(!ready||changing||IsDisposed)return;resizeTimer.Stop();resizeTimer.Start();}
        protected void SetTextMultiplier(float value) {if(Math.Abs(multiplier-value)<.001F)return;multiplier=value;ApplyResponsive();}
        protected void RefreshResponsiveText(){previous=-1;ApplyResponsive();}
        protected Font SizedFont(Font source,float size) {
            size=(float)Math.Round(size,2);
            string key=source.FontFamily.Name+"|"+source.Style+"|"+source.Unit+"|"+size.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Font font;if(!fonts.TryGetValue(key,out font)){font=new Font(source.FontFamily,size,source.Style,source.Unit);fonts.Add(key,font);}return font;
        }
        void TrimFonts() {
            if(fonts.Count<=256)return;
            var active=new HashSet<Font>();foreach(var control in controls)if(!control.IsDisposed)active.Add(control.Font);
            var unused=new List<string>();foreach(var pair in fonts)if(!active.Contains(pair.Value))unused.Add(pair.Key);
            foreach(var key in unused){if(fonts.Count<=128)break;fonts[key].Dispose();fonts.Remove(key);}
        }
        static Padding Scale(Padding p,float value) {return new Padding((int)(p.Left*value),(int)(p.Top*value),(int)(p.Right*value),(int)(p.Bottom*value));}
        void ApplyResponsive() {
            if(!ready||changing||IsDisposed||Disposing||ClientSize.Width<1)return;
            var area=Screen.FromControl(this).WorkingArea;
            float factor=Presentation.TextScale(ClientSize.Width,baseline.Width,area.Width,area.Height,baseline.Height)*multiplier;
            if(Math.Abs(factor-previous)<.015)return;
            previous=factor;changing=true;
            var frozen=new List<Control>();
            foreach(var control in controls)if(!control.IsDisposed){control.SuspendLayout();if(control.IsHandleCreated&&control.Visible){SendMessage(control.Handle,0x000B,IntPtr.Zero,IntPtr.Zero);frozen.Add(control);}}
            try {
                foreach(var pair in metrics) {
                    if(pair.Key.IsDisposed)continue;var m=pair.Value;
                    var font=SizedFont(m.Font,Math.Max(9,m.Font.Size*factor));
                    if(!pair.Key.Font.Equals(font))pair.Key.Font=font;
                    pair.Key.Margin=Scale(m.Margin,factor);pair.Key.Padding=Scale(m.Padding,factor);
                    if(pair.Key!=this)pair.Key.MinimumSize=new Size((int)(m.Minimum.Width*factor),(int)(m.Minimum.Height*factor));
                }
                if(changed!=null)changed(factor);
            } finally {
                try {
                    for(int i=controls.Count-1;i>=0;i--)if(!controls[i].IsDisposed)controls[i].ResumeLayout(true);
                    if(afterLayout!=null)afterLayout();
                } finally {
                    foreach(var control in frozen)if(!control.IsDisposed&&control.IsHandleCreated)SendMessage(control.Handle,0x000B,new IntPtr(1),IntPtr.Zero);
                    changing=false;Invalidate(true);TrimFonts();
                }
            }
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
        protected override void Dispose(bool disposing) {if(disposing){ready=false;resizeTimer.Stop();resizeTimer.Dispose();}base.Dispose(disposing);if(disposing){foreach(var font in fonts.Values)font.Dispose();fonts.Clear();}}
    }
}
