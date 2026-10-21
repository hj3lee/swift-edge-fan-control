using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SwiftEdgeFanControl {
    public static class Ui {
        public static readonly Color Background=Color.FromArgb(19,24,29), Panel=Color.FromArgb(29,36,43),
            Accent=Color.FromArgb(113,224,178), Muted=Color.FromArgb(172,187,196);
        public static float Scale { get { using(var g=Graphics.FromHwnd(IntPtr.Zero))return Math.Max(1,g.DpiX/96F); } }
        public static int S(float value) {return (int)Math.Round(value*Scale);}
        public static Font TextFont(float pixels) {return new Font("Segoe UI",pixels*Scale,FontStyle.Regular,GraphicsUnit.Pixel);}
        public static void Window(Form form,string title,int w,int h) {
            form.Text=title;form.BackColor=Background;form.ForeColor=Color.White;form.Font=TextFont(17);
            form.AutoScaleMode=AutoScaleMode.None;form.StartPosition=FormStartPosition.CenterScreen;
            var screen=Screen.PrimaryScreen.WorkingArea;
            form.ClientSize=new Size(Math.Min(S(w),screen.Width-S(32)),Math.Min(S(h),screen.Height-S(48)));
            form.MinimumSize=new Size(Math.Min(S(460),screen.Width-S(40)),Math.Min(S(340),screen.Height-S(60)));
        }
        public static Label Label(string text,float pixels,Color color) {
            return new Label {Text=text,AutoSize=true,Font=TextFont(pixels),ForeColor=color,Margin=new Padding(0,S(4),0,S(5))};
        }
        public static Button Button(string text,Action action) {
            var b=new Button {Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(S(48),S(40)),
                FlatStyle=FlatStyle.Flat,BackColor=Panel,ForeColor=Color.White,Padding=new Padding(S(10),S(6),S(10),S(6)),
                Margin=new Padding(0,0,S(7),S(7)),Cursor=Cursors.Hand};
            b.FlatAppearance.BorderColor=Color.FromArgb(64,79,87);b.Click+=delegate {action();};return b;
        }
        public static FlowLayoutPanel Flow() {return new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,Margin=Padding.Empty,WrapContents=true};}
        public static TableLayoutPanel Stack() {
            var p=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Margin=Padding.Empty};
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return p;
        }
        public static Settings Copy(Settings s) {return Files.Json.Deserialize<Settings>(Files.Json.Serialize(s));}
    }
    public sealed class MainForm : Form {
        readonly bool demo,startHidden;readonly AppLog log;Controller controller;Settings settings;
        readonly Label temperature=Ui.Label("—°C",36,Ui.Accent), mode=Ui.Label("제조사 자동",20,Color.White),
            rpm=Ui.Label("FAN 1  — RPM     /     FAN 2  — RPM",17,Ui.Muted), detail=Ui.Label("연결 확인 중",14,Ui.Muted);
        readonly TextBox recentLog=new TextBox();readonly CheckBox startup=new CheckBox();readonly Button[] levels=new Button[7];
        readonly NotifyIcon tray=new NotifyIcon();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly Panel logPanel=new Panel();readonly Dictionary<Control,Font> baseFonts=new Dictionary<Control,Font>();
        Button autoButton,curveButton;CurveForm curveWindow;DateTime nextDisplay=DateTime.MinValue;bool loading=true,exiting;
        float textZoom=1F;Size originalClient;
        public MainForm(bool simulated,AppLog logger,string module,bool hidden) {
            demo=simulated;startHidden=hidden;log=logger;settings=demo?new Settings():Files.LoadSettings();
            Ui.Window(this,"Swift Edge Fan Control"+(demo?" · DEMO":""),620,370);Build();originalClient=ClientSize;
            RecordFonts(this);log.Added+=OnLog;
            if(!demo) {var id=DeviceIdentity.Detect();if(!id.Supported)throw new InvalidOperationException("지원되지 않는 하드웨어: "+id);}
            IEc ec=demo?(IEc)new FakeEc():new PawnEc(module);
            var pawn=ec as PawnEc;if(pawn!=null)pawn.Diagnostic=delegate(string x){log.Write("EC_RETRY",x);};
            controller=new Controller(ec,log,demo);
            controller.ForcedAutomatic+=delegate(Settings s){SafeUi(delegate {settings=s;Selection();nextDisplay=DateTime.MinValue;});};
            controller.Request(settings);controller.Start();
            startup.Checked=!demo&&Startup.IsEnabled();startup.AutoCheck=!demo;startup.ForeColor=demo?Ui.Muted:Color.White;loading=false;
            timer.Interval=1000;timer.Tick+=delegate {RefreshStatus();};timer.Start();
            SystemEvents.PowerModeChanged+=PowerChanged;FormClosing+=ClosingApp;
            Shown+=delegate {if(startHidden)Hide();};Selection();RefreshStatus();
        }
        void Build() {
            var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(Ui.S(22))};Controls.Add(scroll);
            var root=Ui.Stack();scroll.Controls.Add(root);
            var header=Ui.Flow();header.Controls.Add(Ui.Label("SWIFT EDGE  /  팬 제어",18,Ui.Accent));
            var zoom=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=Ui.S(90),Margin=new Padding(Ui.S(28),0,0,0)};
            zoom.Items.AddRange(new object[]{"100%","115%","130%","150%"});zoom.SelectedIndex=0;
            zoom.AccessibleName="글자 크기";zoom.SelectedIndexChanged+=delegate {Zoom(new[]{1F,1.15F,1.3F,1.5F}[zoom.SelectedIndex]);};
            header.Controls.Add(Ui.Label("글자",14,Ui.Muted));header.Controls.Add(zoom);root.Controls.Add(header);
            var card=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=2,BackColor=Ui.Panel,
                Padding=new Padding(Ui.S(14)),Margin=new Padding(0,Ui.S(12),0,Ui.S(14))};
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,Ui.S(130)));card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            temperature.Anchor=AnchorStyles.Left;card.Controls.Add(temperature,0,0);card.SetRowSpan(temperature,2);card.Controls.Add(mode,1,0);card.Controls.Add(rpm,1,1);root.Controls.Add(card);
            card.SizeChanged+=delegate {rpm.MaximumSize=new Size(Math.Max(Ui.S(150),card.ClientSize.Width-Ui.S(165)),0);};
            var buttons=Ui.Flow();for(int i=0;i<7;i++) {int n=i+1;levels[i]=Ui.Button(n.ToString(),delegate {Try(delegate{settings.Mode="Fixed";settings.FixedLevel=n;Apply();});});buttons.Controls.Add(levels[i]);}
            autoButton=Ui.Button("제조사 자동",delegate {Try(delegate{settings.Mode="Auto";Apply();});});buttons.Controls.Add(autoButton);root.Controls.Add(buttons);
            var actions=Ui.Flow();curveButton=Ui.Button("팬 커브 설정 ↗",OpenCurve);actions.Controls.Add(curveButton);
            actions.Controls.Add(Ui.Button("로그 보기",ToggleLog));actions.Controls.Add(Ui.Button("종료",delegate {exiting=true;Close();}));root.Controls.Add(actions);
            startup.Text="Windows 로그인 시 실행";startup.AutoSize=true;startup.ForeColor=Color.White;startup.Margin=new Padding(0,Ui.S(7),0,Ui.S(8));
            startup.CheckedChanged+=delegate {if(loading)return;Try(delegate {Startup.SetEnabled(startup.Checked);log.Write("STARTUP",startup.Checked?"Enabled":"Disabled");});loading=true;startup.Checked=Startup.IsEnabled();loading=false;};
            root.Controls.Add(startup);
            logPanel.Dock=DockStyle.Top;logPanel.Height=Ui.S(115);logPanel.Visible=false;logPanel.Margin=new Padding(0,Ui.S(6),0,Ui.S(8));
            var logLinks=Ui.Flow();logLinks.Dock=DockStyle.Top;logLinks.Controls.Add(Ui.Button("전체 로그 폴더",delegate {Process.Start(new ProcessStartInfo(log.DirectoryPath){UseShellExecute=true});}));
            recentLog.Dock=DockStyle.Fill;recentLog.Multiline=true;recentLog.ReadOnly=true;recentLog.ScrollBars=ScrollBars.Vertical;
            recentLog.BackColor=Ui.Panel;recentLog.ForeColor=Ui.Muted;recentLog.BorderStyle=BorderStyle.None;recentLog.Font=Ui.TextFont(14);
            logPanel.Controls.Add(recentLog);logPanel.Controls.Add(logLinks);root.Controls.Add(logPanel);
            detail.MaximumSize=new Size(Ui.S(565),0);root.Controls.Add(detail);
            root.SizeChanged+=delegate {detail.MaximumSize=new Size(Math.Max(Ui.S(180),root.ClientSize.Width),0);};
            var menu=new ContextMenuStrip();menu.Items.Add("열기",null,delegate {Show();WindowState=FormWindowState.Normal;Activate();});
            menu.Items.Add("제조사 자동",null,delegate {Try(delegate{settings.Mode="Auto";Apply();});});
            menu.Items.Add("종료 · 자동 복귀",null,delegate {exiting=true;Close();});
            tray.Icon=SystemIcons.Application;tray.Text="Swift Edge Fan Control";tray.ContextMenuStrip=menu;tray.Visible=true;
            tray.DoubleClick+=delegate {Show();WindowState=FormWindowState.Normal;Activate();};
        }
        void OpenCurve() {
            if(curveWindow!=null&&!curveWindow.IsDisposed) {curveWindow.Activate();return;}
            curveWindow=new CurveForm(settings,delegate(Settings next){settings=next;Apply();});curveWindow.Show(this);
        }
        void ToggleLog() {
            logPanel.Visible=!logPanel.Visible;
            int change=Ui.S(125)* (logPanel.Visible?1:-1);
            Height=Math.Min(Screen.FromControl(this).WorkingArea.Height,Math.Max(MinimumSize.Height,Height+change));
        }
        void RecordFonts(Control parent) {baseFonts[parent]=parent.Font;foreach(Control c in parent.Controls)RecordFonts(c);}
        void Zoom(float value) {
            if(baseFonts.Count==0)return;SuspendLayout();textZoom=value;
            foreach(var pair in baseFonts)pair.Key.Font=new Font(pair.Value.FontFamily,pair.Value.Size*value,pair.Value.Style,pair.Value.Unit);
            logPanel.Height=(int)(Ui.S(115)*value);
            ClientSize=new Size(Math.Min((int)(originalClient.Width*value),Screen.FromControl(this).WorkingArea.Width-Ui.S(24)),
                Math.Min((int)(originalClient.Height*value)+(logPanel.Visible?Ui.S(125):0),Screen.FromControl(this).WorkingArea.Height-Ui.S(40)));
            ResumeLayout(true);
        }
        void Apply() {settings.Validate();if(!demo)Files.SaveSettings(settings);controller.Request(settings);Selection();nextDisplay=DateTime.MinValue;}
        void Selection() {
            for(int i=0;i<7;i++) {bool on=settings.Mode=="Fixed"&&settings.FixedLevel==i+1;levels[i].BackColor=on?Ui.Accent:Ui.Panel;levels[i].ForeColor=on?Ui.Background:Color.White;}
            autoButton.ForeColor=settings.Mode=="Auto"?Ui.Accent:Color.White;curveButton.ForeColor=settings.Mode=="Curve"?Ui.Accent:Color.White;
        }
        static string ModeText(Status s) {return s.Mode=="Fixed"?"고정 · "+s.Level1+"단계":s.Mode=="Curve"?"사용자 커브 · "+s.Level1+"단계":"제조사 자동";}
        void RefreshStatus() {
            if(controller==null)return;var s=controller.Current;
            if(DateTime.UtcNow>=nextDisplay) {
                temperature.Text=s.Updated==""?"—°C":s.Temperature+"°C";mode.Text=ModeText(s);
                rpm.Text=String.Format("FAN 1  {0:N0} RPM     /     FAN 2  {1:N0} RPM",s.Rpm1,s.Rpm2);nextDisplay=DateTime.UtcNow.AddSeconds(20);
            }
            bool fault=s.Message!="Sensor read verified"&&!s.Message.StartsWith("DEMO");
            detail.Text=demo?"가상 센서 · 하드웨어 접근 없음":fault?s.Message:"90°C 이상 자동 복귀  ·  상태 표시 20초 간격";
            detail.ForeColor=fault&&!demo?Color.FromArgb(255,190,120):Ui.Muted;
            string tip=s.Temperature+"°C · "+ModeText(s)+" · "+s.Rpm1+"/"+s.Rpm2+" RPM";tray.Text=tip.Length>63?tip.Substring(0,63):tip;
        }
        void OnLog(string text) {if(text.Contains("[EC_RETRY]"))return;SafeUi(delegate {if(recentLog.Lines.Length>80)recentLog.Clear();recentLog.AppendText(text+Environment.NewLine);});}
        void SafeUi(Action action) {if(IsDisposed||Disposing||!IsHandleCreated)return;if(InvokeRequired){try{BeginInvoke(action);}catch(InvalidOperationException){}}else action();}
        void Try(Action action) {try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"설정 확인",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
        void PowerChanged(object sender,PowerModeChangedEventArgs e) {
            if(e.Mode!=PowerModes.Suspend&&e.Mode!=PowerModes.Resume)return;
            controller.ForceAuto("Power state: "+e.Mode);
            if(e.Mode==PowerModes.Suspend){var wait=Stopwatch.StartNew();while(controller.IsOwned&&wait.ElapsedMilliseconds<5000)Thread.Sleep(25);}
            SafeUi(delegate {settings.Mode="Auto";if(!demo)Files.SaveSettings(settings);Selection();});
        }
        void ClosingApp(object sender,FormClosingEventArgs e) {
            if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();return;}
            timer.Stop();SystemEvents.PowerModeChanged-=PowerChanged;if(curveWindow!=null)curveWindow.Close();controller.Dispose();tray.Visible=false;tray.Dispose();
        }
        public void RenderPreview() {
            temperature.Text="52°C";mode.Text="고정 · 1단계";rpm.Text="FAN 1  3,330 RPM     /     FAN 2  3,540 RPM";
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}
            ToggleLog();
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-log.png"));}
            Zoom(1.3F);
            using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-large.png"));}
            using(var editor=new CurveForm(new Settings(),delegate(Settings s){})) {editor.Show();Application.DoEvents();editor.RenderPreview();}
            exiting=true;Close();
        }
    }
    public sealed class CurveForm : Form {
        readonly DataGridView table=new DataGridView();readonly NumericUpDown hysteresis=new NumericUpDown(),delay=new NumericUpDown();
        readonly CurveChart chart=new CurveChart();readonly Label message=Ui.Label("그래프의 점을 끌거나 표의 값을 수정하세요.",14,Ui.Muted);
        Settings draft;readonly Action<Settings> apply;bool filling;
        public CurveForm(Settings initial,Action<Settings> onApply) {
            draft=Ui.Copy(initial);apply=onApply;Ui.Window(this,"팬 커브 설정",760,680);MinimumSize=new Size(Ui.S(560),Ui.S(500));
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(Ui.S(20))};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,52));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            root.Controls.Add(Ui.Label("온도에 따라 단계 1–7 자동 조절",21,Color.White),0,0);
            chart.Dock=DockStyle.Fill;chart.Margin=new Padding(0,Ui.S(6),0,Ui.S(12));root.Controls.Add(chart,0,1);
            table.Dock=DockStyle.Fill;table.BackgroundColor=Ui.Panel;table.BorderStyle=BorderStyle.None;table.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            table.AllowUserToAddRows=true;table.AllowUserToDeleteRows=true;table.RowHeadersWidth=Ui.S(28);table.RowTemplate.Height=Ui.S(30);
            table.ColumnHeadersHeight=Ui.S(34);table.EnableHeadersVisualStyles=false;table.ColumnHeadersDefaultCellStyle.BackColor=Ui.Panel;
            table.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;table.DefaultCellStyle.BackColor=Ui.Panel;table.DefaultCellStyle.ForeColor=Color.White;
            table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(48,87,74);table.DefaultCellStyle.SelectionForeColor=Color.White;
            table.Columns.Add("temperature","이 온도부터 (°C)");var levels=new DataGridViewComboBoxColumn {Name="level",HeaderText="팬 단계",FlatStyle=FlatStyle.Flat};
            for(int n=1;n<=7;n++)levels.Items.Add(n.ToString());table.Columns.Add(levels);root.Controls.Add(table,0,2);
            var options=Ui.Flow();options.Margin=new Padding(0,Ui.S(12),0,0);
            options.Controls.Add(Ui.Label("히스테리시스 °C",16,Color.White));hysteresis.Minimum=1;hysteresis.Maximum=10;hysteresis.Width=Ui.S(65);options.Controls.Add(hysteresis);
            options.Controls.Add(Ui.Label("감속 대기 초",16,Color.White));delay.Minimum=0;delay.Maximum=120;delay.Width=Ui.S(80);options.Controls.Add(delay);root.Controls.Add(options,0,3);
            root.Controls.Add(message,0,4);var buttons=Ui.Flow();
            buttons.Controls.Add(Ui.Button("저장 · 커브 모드 적용",Save));buttons.Controls.Add(Ui.Button("기본값 · 60°C부터 2단계",delegate {draft=new Settings();Fill();}));
            buttons.Controls.Add(Ui.Button("닫기",delegate {Close();}));root.Controls.Add(buttons,0,5);
            table.CellValueChanged+=delegate {UpdateChart();};table.RowsRemoved+=delegate {UpdateChart();};
            table.CurrentCellDirtyStateChanged+=delegate {if(table.IsCurrentCellDirty)table.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            table.DataError+=delegate(object sender,DataGridViewDataErrorEventArgs e){e.ThrowException=false;message.Text="온도와 단계를 확인하세요.";};
            chart.PointChanged=delegate(int i,int temp,int level){filling=true;table.Rows[i].Cells[0].Value=temp;table.Rows[i].Cells[1].Value=level.ToString();filling=false;UpdateChart();};
            Fill();
        }
        void Fill() {filling=true;table.Rows.Clear();foreach(var p in draft.Curve)table.Rows.Add(p.Temperature,p.Level.ToString());hysteresis.Value=draft.Hysteresis;delay.Value=draft.DownDelaySeconds;filling=false;UpdateChart();}
        Settings Read() {
            var s=Ui.Copy(draft);s.Curve.Clear();
            foreach(DataGridViewRow r in table.Rows){if(r.IsNewRow)continue;if(r.Cells[0].Value==null||r.Cells[1].Value==null)throw new ArgumentException("모든 행에 온도와 단계를 입력하세요.");
                s.Curve.Add(new CurvePoint(Convert.ToInt32(r.Cells[0].Value),Convert.ToInt32(r.Cells[1].Value)));}
            s.Hysteresis=(int)hysteresis.Value;s.DownDelaySeconds=(int)delay.Value;s.Mode="Curve";s.Validate();return s;
        }
        void UpdateChart() {if(filling)return;try {var s=Read();chart.Points=s.Curve;chart.Invalidate();message.Text="가열 시 즉시 상승 · 냉각 시 히스테리시스와 감속 대기 적용";message.ForeColor=Ui.Muted;}
            catch(Exception e){chart.Points=new List<CurvePoint>();chart.Invalidate();message.Text=e.Message;message.ForeColor=Color.FromArgb(255,190,120);}}
        void Save() {try{table.EndEdit();var s=Read();apply(s);draft=s;message.Text="저장하고 사용자 커브 모드를 적용했습니다.";}catch(Exception e){MessageBox.Show(this,e.Message,"커브 확인");}}
        public void RenderPreview() {using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"curve-preview.png"));}}
    }
    public sealed class CurveChart : Control {
        public List<CurvePoint> Points=new List<CurvePoint>();public Action<int,int,int> PointChanged;int drag=-1;
        public CurveChart(){DoubleBuffered=true;BackColor=Ui.Panel;ForeColor=Ui.Muted;Font=Ui.TextFont(14);}
        RectangleF Plot {get{return new RectangleF(Ui.S(38),Ui.S(18),Math.Max(1,Width-Ui.S(64)),Math.Max(1,Height-Ui.S(52)));}}
        PointF Position(CurvePoint p){var r=Plot;return new PointF(r.Left+r.Width*p.Temperature/90F,r.Bottom-r.Height*(p.Level-1)/6F);}
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var r=Plot;
            using(var grid=new Pen(Color.FromArgb(55,67,77)))using(var ink=new SolidBrush(Ui.Muted))using(var line=new Pen(Ui.Accent,Ui.S(2)))using(var dot=new SolidBrush(Ui.Accent)) {
                for(int n=1;n<=7;n++){float y=r.Bottom-r.Height*(n-1)/6F;g.DrawLine(grid,r.Left,y,r.Right,y);g.DrawString(n.ToString(),Font,ink,Ui.S(12),y-Ui.S(8));}
                for(int t=0;t<=90;t+=15){float x=r.Left+r.Width*t/90F;g.DrawLine(grid,x,r.Top,x,r.Bottom);g.DrawString(t+"°",Font,ink,x-Ui.S(10),r.Bottom+Ui.S(8));}
                if(Points.Count==0)return;
                for(int i=0;i<Points.Count;i++){PointF a=Position(Points[i]);float end=i+1<Points.Count?Position(Points[i+1]).X:r.Right;
                    g.DrawLine(line,a.X,a.Y,end,a.Y);if(i+1<Points.Count)g.DrawLine(line,end,a.Y,end,Position(Points[i+1]).Y);
                    float radius=Ui.S(5);g.FillEllipse(dot,a.X-radius,a.Y-radius,radius*2,radius*2);}
            }
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;
            for(int i=0;i<Points.Count;i++){var p=Position(Points[i]);if(Math.Abs(e.X-p.X)<Ui.S(14)&&Math.Abs(e.Y-p.Y)<Ui.S(14)){drag=i;Capture=true;break;}}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(drag<0||drag>=Points.Count)return;var r=Plot;
            int t=(int)Math.Round((e.X-r.Left)/r.Width*90),level=(int)Math.Round(1+(r.Bottom-e.Y)/r.Height*6);
            int low=drag==0?0:Points[drag-1].Temperature+1,high=drag+1<Points.Count?Points[drag+1].Temperature-1:89;
            t=drag==0?0:Math.Max(low,Math.Min(high,t));level=Math.Max(drag==0?1:Points[drag-1].Level,Math.Min(drag+1<Points.Count?Points[drag+1].Level:7,Math.Max(1,Math.Min(7,level))));
            if(PointChanged!=null)PointChanged(drag,t,level);
        }
        protected override void OnMouseUp(MouseEventArgs e){drag=-1;Capture=false;base.OnMouseUp(e);}
        protected override void OnResize(EventArgs e){base.OnResize(e);Invalidate();}
    }
}
