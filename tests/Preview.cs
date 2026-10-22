using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace SwiftEdgeFanControl {
    public sealed partial class MainForm {
        public void RenderPreview() {
            temperature.Text="56°C";mode.Text=ModeText(new Status {Mode="Curve",Level1=1});rpm.Text="FAN 1  3,369 RPM     /     FAN 2  3,569 RPM";
            recentLog.Text="2026-10-22 08:23:47 [STATUS] CPU=55°C mode=Curve level=1/1 RPM=3369/3599\r\n"+
                "2026-10-22 08:23:57 [STATUS] CPU=55°C mode=Curve level=1/1 RPM=3369/3599\r\n"+
                "2026-10-22 08:24:07 [STATUS] CPU=56°C mode=Curve level=1/1 RPM=3369/3569\r\n"+
                "2026-10-22 08:24:17 [STATUS] CPU=56°C mode=Curve level=1/1 RPM=3369/3569";
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}
            bool corners=true;var cornerColors=new List<string>();
            foreach(var button in levels.Concat(new[]{curveModeButton,autoButton,curveButton})) {
                using(var bitmap=new Bitmap(button.Width,button.Height)){button.DrawToBitmap(bitmap,button.ClientRectangle);Color c=bitmap.GetPixel(0,0);cornerColors.Add(button.Text+":"+c.ToArgb().ToString("X8"));corners&=c.A==255&&c.ToArgb()==Color.White.ToArgb();}
            }
            using(var bitmap=new Bitmap(temperature.Parent.Width,temperature.Parent.Height)){temperature.Parent.DrawToBitmap(bitmap,temperature.Parent.ClientRectangle);Color c=bitmap.GetPixel(0,0);cornerColors.Add("status-card:"+c.ToArgb().ToString("X8"));corners&=c.ToArgb()==Color.White.ToArgb();}
            foreach(string code in L10n.Codes) {
                language.SelectedIndex=Array.IndexOf(L10n.Codes,code);Application.DoEvents();
                temperature.Text="56°C";mode.Text=ModeText(new Status {Mode="Curve",Level1=1});rpm.Text="FAN 1  3,369 RPM     /     FAN 2  3,569 RPM";
                using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-"+code+".png"));}
            }
            language.SelectedIndex=0;Application.DoEvents();
            ToggleLog();
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-log.png"));}
            Zoom(1.3F);
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-large.png"));}
            float wideFont=temperature.Font.Size;
            ClientSize=new Size(Ui.S(480),Ui.S(520));SetTextMultiplier(1F);Application.DoEvents();FitModeButtons();
            bool fourLines=recentLog.GetPositionFromCharIndex(recentLog.GetFirstCharIndexFromLine(3)).Y+recentLog.Font.Height<=recentLog.ClientSize.Height-SystemInformation.HorizontalScrollBarHeight;
            bool temperatureFits=temperature.ClientSize.Width>=TextRenderer.MeasureText(temperature.Text,temperature.Font,Size.Empty,TextFormatFlags.NoPadding).Width;
            Files.Atomic(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-render-checks.json"),Files.Json.Serialize(new {four_status_lines_visible=fourLines,temperature_single_line=temperatureFits,text_shrinks_with_window=temperature.Font.Size<wideFont,opaque_white_button_corners=corners,corner_colors=cornerColors,languages=L10n.Codes,default_language="en"}));
            if(!fourLines||!temperatureFits||temperature.Font.Size>=wideFont||!corners)throw new InvalidOperationException("Responsive UI render validation failed");
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-narrow.png"));}
            using(var editor=new CurveForm(new Settings(),delegate(Settings s){})) {editor.Show();Application.DoEvents();editor.RenderPreview();}
            exiting=true;Close();
        }
    }
    public sealed partial class CurveForm {
        public void RenderPreview() {
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"curve-preview.png"));}
            foreach(string code in L10n.Codes){L10n.Set(code);ApplyLanguage();Application.DoEvents();using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"curve-preview-"+code+".png"));}}
            L10n.Set("en");ApplyLanguage();
        }
    }
    public sealed class FakeEc : IEc {
        public byte Temperature=52; public bool FailReads; public byte Mode,Level1=4,Level2=4;
        public byte Read(byte r) {
            if(FailReads) throw new TimeoutException("Simulated sensor failure");
            if(r==0) return Mode;if(r==0x55)return Level1;if(r==0x4f)return Level2;if(r==0x58)return Temperature;
            int rpm=(r==0x5c||r==0x5d)?(Level1==1?3330:4800):(Level2==1?3540:5100);
            return (byte)((r==0x5c||r==0x6a)?rpm/256:rpm%256);
        }
        public void Write(byte r,byte v) { if(r==0) { Mode=v;if((v&2)==0)Level1=Level2=4; } if(r==0x55)Level1=v;if(r==0x4f)Level2=v; }
        public void Dispose() { }
    }
}
