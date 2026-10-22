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
        public static readonly Color Background=Color.White, Panel=Color.FromArgb(244,246,249),
            Accent=Color.FromArgb(0,87,183), Highlight=Color.FromArgb(255,215,0),
            Ink=Color.FromArgb(30,41,59), OnAccent=Color.White, Muted=Color.FromArgb(86,101,119);
        public const string AppTitle="Acer Swift Edge SFE16-43 · Fan Control";
        public const string Repository="https://github.com/hj3lee/swift-edge-fan-control";
        public static readonly Icon AppIcon=LoadIcon();
        static Icon LoadIcon() {
            using(var stream=typeof(Ui).Assembly.GetManifestResourceStream("AppIcon"))
            using(var icon=new Icon(stream))return (Icon)icon.Clone();
        }
        public static readonly Color BlueTint=Color.FromArgb(235,243,255),Subtle=Color.FromArgb(128,140,157);
        public static float Scale { get { using(var g=Graphics.FromHwnd(IntPtr.Zero))return Math.Max(1,g.DpiX/96F); } }
        public static int S(float value) {return (int)Math.Round(value*Scale);}
        public static Font TextFont(float pixels) {return new Font("Segoe UI",pixels*Scale,FontStyle.Regular,GraphicsUnit.Pixel);}
        public static void Window(Form form,string title,int w,int h) {
            form.Text=title;form.Icon=AppIcon;form.BackColor=Background;form.ForeColor=Ink;form.Font=TextFont(17);
            form.AutoScaleMode=AutoScaleMode.None;form.StartPosition=FormStartPosition.CenterScreen;
            var screen=Screen.PrimaryScreen.WorkingArea;
            form.ClientSize=new Size(Math.Min(S(w),screen.Width-S(32)),Math.Min(S(h),screen.Height-S(48)));
            form.MinimumSize=new Size(Math.Min(S(460),screen.Width-S(40)),Math.Min(S(340),screen.Height-S(60)));
        }
        public static Label Label(string text,float pixels,Color color) {
            var label=new Label {AutoSize=true,Font=TextFont(pixels),ForeColor=color,Margin=new Padding(0,S(4),0,S(5))};L10n.Bind(label,text);return label;
        }
        public static Button Button(string text,Action action) {
            var b=new RoundedButton {Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(S(48),S(36)),
                FlatStyle=FlatStyle.Flat,BackColor=Panel,ForeColor=Ink,Padding=new Padding(S(12),S(7),S(12),S(7)),Font=TextFont(15),
                Margin=new Padding(0,0,S(7),S(7)),Cursor=Cursors.Hand};
            b.FlatAppearance.BorderColor=Color.FromArgb(205,214,225);b.FlatAppearance.MouseOverBackColor=Color.FromArgb(255,246,191);
            b.FlatAppearance.MouseDownBackColor=Accent;b.Click+=delegate {action();};L10n.Bind(b,text);return b;
        }
        public static FlowLayoutPanel Flow() {return new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,Margin=Padding.Empty,WrapContents=true};}
        public static TableLayoutPanel Stack() {
            var p=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Margin=Padding.Empty};
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return p;
        }
        public static Settings Copy(Settings s) {return Files.Json.Deserialize<Settings>(Files.Json.Serialize(s));}
    }
    public sealed partial class MainForm : ResponsiveForm {
        readonly bool demo,startHidden;readonly AppLog log;Controller controller;Settings settings;
        readonly Label description=Ui.Label("",15,Ui.Muted);
        readonly Label temperature=Ui.Label("—°C",43,Ui.Accent), mode=Ui.Label("제조사 기본 모드",21,Ui.Ink),
            rpm=Ui.Label("FAN 1  — RPM     /     FAN 2  — RPM",16,Ui.Muted);
        readonly TextBox recentLog=new TextBox();readonly CheckBox startup=new CheckBox();readonly Button[] levels=new Button[7];
        readonly NotifyIcon tray=new NotifyIcon();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly Panel logPanel=new Panel();readonly TableLayoutPanel modeRow=new TableLayoutPanel();
        FlowLayoutPanel logLinks;ComboBox language,zoom;ContextMenuStrip trayMenu;
        Button autoButton,curveButton,curveModeButton;CurveForm curveWindow;DateTime nextDisplay=DateTime.MinValue;bool loading=true,exiting;
        string displayedMode="",displayedSample="";int displayedLevel=-1;
        float textZoom=1F;
        public MainForm(bool simulated,AppLog logger,string module,bool hidden) {
            Files.StartupTrace("MainForm settings begin");
            demo=simulated;startHidden=hidden;log=logger;settings=demo?new Settings():Files.LoadSettings();
            L10n.Set(settings.Language);
            Files.StartupTrace("MainForm Build begin");Ui.Window(this,Ui.AppTitle+(demo?" · DEMO":""),740,400);Build();Files.StartupTrace("MainForm Build ready");
            EnableResponsiveText(delegate(float factor){modeRow.Height=(int)(Ui.S(46)*factor);},delegate {ResizeLog();FitModeButtons();});log.Added+=OnLog;
#if DEVELOPMENT
            IEc ec=demo?(IEc)new FakeEc():new PawnEc(module);
#else
            Files.StartupTrace("PawnEc begin");IEc ec=new PawnEc(module);Files.StartupTrace("PawnEc ready");
#endif
            var pawn=ec as PawnEc;if(pawn!=null)pawn.Diagnostic=delegate(string x){log.Write("EC_RETRY",x);};
            controller=new Controller(ec,log,demo);
            controller.StatusUpdated+=delegate {SafeUi(RefreshStatus);};
            controller.ForcedAutomatic+=delegate(Settings s){SafeUi(delegate {settings=s;Selection();nextDisplay=DateTime.MinValue;});};
            Files.StartupTrace("Controller request begin");controller.Request(settings);controller.Start();Files.StartupTrace("Controller started");
            Files.StartupTrace("Startup task query begin");startup.Checked=!demo&&Startup.IsEnabled();startup.AutoCheck=!demo;startup.ForeColor=Ui.Muted;loading=false;Files.StartupTrace("Startup task query ready");
            timer.Interval=10000;timer.Tick+=delegate {RefreshStatus();};timer.Start();
            SystemEvents.PowerModeChanged+=PowerChanged;FormClosing+=ClosingApp;
            Shown+=delegate {if(startHidden)Hide();};Selection();RefreshStatus();
        }
        void Build() {
            var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(Ui.S(22))};Controls.Add(scroll);
            var root=Ui.Stack();scroll.Controls.Add(root);
            var header=Ui.Flow();
            zoom=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=Ui.S(90),Margin=new Padding(Ui.S(28),0,0,0)};
            zoom.Items.AddRange(new object[]{L10n.T("자동"),"115%","130%","150%"});zoom.SelectedIndex=0;
            zoom.AccessibleName=L10n.T("글자 크기");zoom.SelectedIndexChanged+=delegate {Zoom(new[]{1F,1.15F,1.3F,1.5F}[zoom.SelectedIndex]);};
            header.Controls.Add(Ui.Label("글자 크기",12,Ui.Subtle));header.Controls.Add(zoom);
            var card=new RoundedTable {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=2,BackColor=Ui.Highlight,
                Padding=new Padding(Ui.S(18)),Margin=new Padding(0,0,0,Ui.S(18))};
            card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            temperature.Anchor=AnchorStyles.Left;temperature.MaximumSize=Size.Empty;temperature.Margin=new Padding(0,0,Ui.S(20),0);card.Controls.Add(temperature,0,0);card.SetRowSpan(temperature,2);card.Controls.Add(mode,1,0);card.Controls.Add(rpm,1,1);root.Controls.Add(card);
            card.SizeChanged+=delegate {rpm.MaximumSize=new Size(Math.Max(Ui.S(150),card.ClientSize.Width-Ui.S(165)),0);};
            modeRow.Dock=DockStyle.Top;modeRow.Height=Ui.S(46);modeRow.ColumnCount=3;modeRow.RowCount=1;modeRow.Margin=new Padding(0,0,0,Ui.S(10));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));
            curveModeButton=Ui.Button("팬 커브 모드",delegate {Try(delegate{settings.Mode="Curve";Apply();});});
            ModeButton(curveModeButton);curveModeButton.Margin=new Padding(0,0,Ui.S(12),0);modeRow.Controls.Add(curveModeButton,0,0);
            var fixedButtons=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=7,RowCount=1,Margin=Padding.Empty};
            for(int i=0;i<7;i++) {int n=i+1;fixedButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F/7));
                levels[i]=Ui.Button(n.ToString(),delegate {Try(delegate{settings.Mode="Fixed";settings.FixedLevel=n;Apply();});});ModeButton(levels[i]);fixedButtons.Controls.Add(levels[i],i,0);}
            modeRow.Controls.Add(fixedButtons,1,0);
            autoButton=Ui.Button("제조사 기본",delegate {Try(delegate{settings.Mode="Auto";Apply();});});ModeButton(autoButton);autoButton.Margin=new Padding(Ui.S(12),0,0,0);modeRow.Controls.Add(autoButton,2,0);root.Controls.Add(modeRow);
            description.MaximumSize=new Size(Ui.S(650),0);description.Margin=new Padding(0,0,0,Ui.S(12));root.Controls.Add(description);
            var actions=Ui.Flow();curveButton=Ui.Button("커브 설정",OpenCurve);curveButton.BackColor=Ui.BlueTint;curveButton.ForeColor=Ui.Accent;curveButton.FlatAppearance.BorderSize=0;curveButton.FlatAppearance.MouseOverBackColor=Color.FromArgb(211,230,255);actions.Controls.Add(curveButton);
            var viewLog=Ui.Button("로그 보기",ToggleLog);viewLog.FlatAppearance.MouseOverBackColor=Color.FromArgb(230,234,240);viewLog.FlatAppearance.MouseDownBackColor=Color.FromArgb(216,222,230);actions.Controls.Add(viewLog);
            var exit=Ui.Button("종료",delegate {exiting=true;Close();});exit.BackColor=Ui.Background;exit.ForeColor=Ui.Subtle;exit.FlatAppearance.BorderSize=0;exit.FlatAppearance.MouseOverBackColor=Ui.Panel;actions.Controls.Add(exit);root.Controls.Add(actions);
            L10n.Bind(startup,"Windows 로그인 시 실행");startup.AutoSize=true;startup.ForeColor=Ui.Ink;startup.Margin=new Padding(0,Ui.S(7),0,Ui.S(8));
            startup.CheckedChanged+=delegate {if(loading)return;Try(delegate {Startup.SetEnabled(startup.Checked);log.Write("STARTUP",startup.Checked?"Enabled":"Disabled");});loading=true;startup.Checked=Startup.IsEnabled();loading=false;};
            startup.Font=Ui.TextFont(13);startup.ForeColor=Ui.Muted;root.Controls.Add(startup);
            zoom.Font=Ui.TextFont(13);zoom.Width=Ui.S(84);zoom.Margin=new Padding(Ui.S(10),0,Ui.S(20),0);
            var github=new LinkLabel {Text="GitHub ↗",AutoSize=true,LinkColor=Ui.Accent,ActiveLinkColor=Ui.Ink,VisitedLinkColor=Ui.Accent,Font=Ui.TextFont(13),Margin=new Padding(0,Ui.S(4),0,Ui.S(4))};
            github.LinkClicked+=delegate {Try(delegate {Process.Start(new ProcessStartInfo(Ui.Repository){UseShellExecute=true});});};
            header.Controls.Add(Ui.Label("언어",12,Ui.Subtle));language=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=Ui.S(126),Font=Ui.TextFont(13),Margin=new Padding(Ui.S(8),0,Ui.S(20),0)};
            language.Items.AddRange(L10n.Names);language.SelectedIndex=Math.Max(0,Array.IndexOf(L10n.Codes,L10n.Language));language.AccessibleName=L10n.T("언어");
            language.SelectedIndexChanged+=delegate {if(language.SelectedIndex<0)return;Try(delegate {SetLanguage(L10n.Codes[language.SelectedIndex]);});};header.Controls.Add(language);
            header.Controls.Add(github);header.Margin=new Padding(0,Ui.S(8),0,Ui.S(8));root.Controls.Add(header);
            logPanel.Dock=DockStyle.Top;logPanel.Height=Ui.S(160);logPanel.Visible=false;logPanel.Margin=new Padding(0,Ui.S(6),0,Ui.S(8));
            logLinks=Ui.Flow();logLinks.Dock=DockStyle.Top;logLinks.Controls.Add(Ui.Button("전체 로그 폴더",delegate {Process.Start(new ProcessStartInfo(log.DirectoryPath){UseShellExecute=true});}));
            recentLog.Dock=DockStyle.Fill;recentLog.Multiline=true;recentLog.ReadOnly=true;recentLog.ScrollBars=ScrollBars.Both;recentLog.WordWrap=false;
            recentLog.BackColor=Ui.Panel;recentLog.ForeColor=Ui.Muted;recentLog.BorderStyle=BorderStyle.None;recentLog.Font=new Font("Consolas",13*Ui.Scale,FontStyle.Regular,GraphicsUnit.Pixel);
            logPanel.Controls.Add(recentLog);logPanel.Controls.Add(logLinks);root.Controls.Add(logPanel);
            root.SizeChanged+=delegate {int width=Math.Max(Ui.S(180),root.ClientSize.Width);description.MaximumSize=new Size(width,0);};
            var menu=new ContextMenuStrip();trayMenu=menu;menu.Items.Add(L10n.T("열기"),null,delegate {Show();WindowState=FormWindowState.Normal;Activate();});
            menu.Items.Add(L10n.T("제조사 자동"),null,delegate {Try(delegate{settings.Mode="Auto";Apply();});});
            menu.Items.Add(L10n.T("종료 · 자동 복귀"),null,delegate {exiting=true;Close();});
            tray.Icon=Ui.AppIcon;tray.Text="SFE16-43 · Fan Control";tray.ContextMenuStrip=menu;tray.Visible=true;
            tray.DoubleClick+=delegate {Show();WindowState=FormWindowState.Normal;Activate();};
        }
        void SetLanguage(string code) {
            settings.Language=code;L10n.Set(code);if(!demo)Files.SaveSettings(settings);
            zoom.Items[0]=L10n.T("자동");zoom.AccessibleName=L10n.T("글자 크기");language.AccessibleName=L10n.T("언어");
            trayMenu.Items[0].Text=L10n.T("열기");trayMenu.Items[1].Text=L10n.T("제조사 자동");trayMenu.Items[2].Text=L10n.T("종료 · 자동 복귀");
            if(curveWindow!=null&&!curveWindow.IsDisposed)curveWindow.ApplyLanguage();
            Selection();nextDisplay=DateTime.MinValue;RefreshStatus();RefreshResponsiveText();
        }
        void OpenCurve() {
            if(curveWindow!=null&&!curveWindow.IsDisposed) {curveWindow.Activate();return;}
            curveWindow=new CurveForm(settings,delegate(Settings next){settings=next;Apply();});StyleSelected(curveButton,true);
            curveWindow.FormClosed+=delegate {if(!curveButton.IsDisposed)StyleSelected(curveButton,false);};curveWindow.Show(this);
        }
        static void ModeButton(Button button) {button.Dock=DockStyle.Fill;button.AutoSize=false;button.MinimumSize=Size.Empty;button.Padding=Padding.Empty;button.Margin=new Padding(1,0,1,0);}
        void FitModeButtons() {
            foreach(var button in levels.Concat(new[]{curveModeButton,autoButton})) {
                if(button==null||button.ClientSize.Width<1)continue;
                int measured=TextRenderer.MeasureText(button.Text,button.Font).Width;
                if(measured>button.ClientSize.Width-6)button.Font=SizedFont(button.Font,Math.Max(10,button.Font.Size*(button.ClientSize.Width-6)/measured));
            }
        }
        void ResizeLog() {
            if(logLinks==null)return;
            int toolbar=logLinks.GetPreferredSize(new Size(Math.Max(1,logPanel.Width),0)).Height;
            recentLog.Height=recentLog.Font.Height*4+SystemInformation.HorizontalScrollBarHeight+Ui.S(10);
            logPanel.Height=toolbar+recentLog.Height;
        }
        void ToggleLog() {
            ResizeLog();logPanel.Visible=!logPanel.Visible;
            int change=(logPanel.Height+Ui.S(14))*(logPanel.Visible?1:-1);
            Height=Math.Min(Screen.FromControl(this).WorkingArea.Height,Math.Max(MinimumSize.Height,Height+change));
        }
        void Zoom(float value) {textZoom=value;SetTextMultiplier(value);}
        void Apply() {settings.Validate();if(!demo)Files.SaveSettings(settings);controller.Request(settings);Selection();nextDisplay=DateTime.MinValue;}
        void Selection() {
            for(int i=0;i<7;i++) {bool on=settings.Mode=="Fixed"&&settings.FixedLevel==i+1;StyleSelected(levels[i],on);}
            foreach(var button in new[]{autoButton,curveModeButton}) {
                bool selected=button==autoButton?settings.Mode=="Auto":settings.Mode=="Curve";
                StyleSelected(button,selected);
            }
            description.Text=L10n.Description(settings.Mode,settings.FixedLevel);
        }
        static void StyleSelected(Button button,bool selected) {
            button.BackColor=selected?Ui.Accent:Ui.BlueTint;button.ForeColor=selected?Ui.OnAccent:Ui.Accent;
            button.FlatAppearance.BorderColor=selected?Ui.Accent:Color.FromArgb(205,214,225);
            button.FlatAppearance.BorderSize=0;
            button.FlatAppearance.MouseOverBackColor=selected?Color.FromArgb(0,70,150):Color.FromArgb(211,230,255);
            button.FlatAppearance.MouseDownBackColor=Ui.Accent;
        }
        static string ModeText(Status s) {return s.Mode=="Fixed"?L10n.T("고정 모드 · {0}단계",s.Level1):s.Mode=="Curve"?L10n.T("팬 커브 모드 · {0}단계",s.Level1):L10n.T("제조사 기본 모드");}
        void RefreshStatus() {
            if(controller==null)return;var s=controller.Current;
            if(DateTime.UtcNow>=nextDisplay||displayedSample!=s.Updated||displayedMode!=s.Mode||displayedLevel!=s.Level1) {
                temperature.Text=s.Updated==""?"—°C":s.Temperature+"°C";mode.Text=ModeText(s);
                rpm.Text=String.Format("FAN 1  {0:N0} RPM     /     FAN 2  {1:N0} RPM",s.Rpm1,s.Rpm2);nextDisplay=DateTime.UtcNow.AddSeconds(10);displayedMode=s.Mode;displayedLevel=s.Level1;displayedSample=s.Updated;
            }
            if(s.Updated==""&&s.Message!="Starting")description.Text=s.Message;
            string tip=s.Temperature+"°C · "+ModeText(s)+" · "+s.Rpm1+"/"+s.Rpm2+" RPM";tray.Text=tip.Length>63?tip.Substring(0,63):tip;
        }
        void OnLog(string text) {if(!text.Contains("[STATUS]"))return;SafeUi(delegate {if(recentLog.Lines.Length>80)recentLog.Clear();recentLog.AppendText(text+Environment.NewLine);});}
        void SafeUi(Action action) {if(IsDisposed||Disposing||!IsHandleCreated)return;if(InvokeRequired){try{BeginInvoke(action);}catch(InvalidOperationException){}}else action();}
        void Try(Action action) {try{action();}catch(Exception e){MessageBox.Show(this,L10n.T(e.Message),L10n.T("설정 확인"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
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

    }
    public sealed partial class CurveForm : ResponsiveForm {
        readonly DataGridView table=new DataGridView();readonly NumericUpDown hysteresis=new NumericUpDown(),delay=new NumericUpDown();
        readonly CurveChart chart=new CurveChart();readonly Label message=Ui.Label("그래프의 점을 끌거나 표의 값을 수정하세요.",14,Ui.Muted);
        Settings draft;readonly Action<Settings> apply;bool filling;
        public CurveForm(Settings initial,Action<Settings> onApply) {
            draft=Ui.Copy(initial);apply=onApply;Ui.Window(this,L10n.T("팬 커브 설정"),760,680);L10n.Bind(this,"팬 커브 설정");MinimumSize=new Size(Ui.S(560),Ui.S(500));
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(Ui.S(20))};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,48));root.RowStyles.Add(new RowStyle(SizeType.Percent,52));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
            root.Controls.Add(Ui.Label("온도에 따라 단계 1–7 자동 조절",21,Ui.Ink),0,0);
            chart.Dock=DockStyle.Fill;chart.Margin=new Padding(0,Ui.S(6),0,Ui.S(12));root.Controls.Add(chart,0,1);
            table.Dock=DockStyle.Fill;table.BackgroundColor=Ui.Panel;table.BorderStyle=BorderStyle.None;table.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
            table.AllowUserToAddRows=true;table.AllowUserToDeleteRows=true;table.RowHeadersWidth=Ui.S(28);table.RowTemplate.Height=Ui.S(30);
            table.ColumnHeadersHeight=Ui.S(34);table.EnableHeadersVisualStyles=false;table.ColumnHeadersDefaultCellStyle.BackColor=Ui.Panel;
            table.ColumnHeadersDefaultCellStyle.ForeColor=Ui.Ink;table.DefaultCellStyle.BackColor=Ui.Background;table.DefaultCellStyle.ForeColor=Ui.Ink;
            table.DefaultCellStyle.SelectionBackColor=Color.FromArgb(221,234,251);table.DefaultCellStyle.SelectionForeColor=Ui.Ink;
            table.GridColor=Color.FromArgb(215,222,232);
            table.Columns.Add("temperature",L10n.T("이 온도부터 (°C)"));var levels=new DataGridViewComboBoxColumn {Name="level",HeaderText=L10n.T("팬 단계"),FlatStyle=FlatStyle.Flat};
            for(int n=1;n<=7;n++)levels.Items.Add(n.ToString());table.Columns.Add(levels);root.Controls.Add(table,0,2);
            var options=Ui.Flow();options.Margin=new Padding(0,Ui.S(12),0,0);
            options.Controls.Add(Ui.Label("히스테리시스 °C",16,Ui.Ink));hysteresis.Minimum=1;hysteresis.Maximum=10;hysteresis.Width=Ui.S(65);options.Controls.Add(hysteresis);
            options.Controls.Add(Ui.Label("감속 대기 초",16,Ui.Ink));delay.Minimum=0;delay.Maximum=120;delay.Width=Ui.S(80);options.Controls.Add(delay);root.Controls.Add(options,0,3);
            root.Controls.Add(message,0,4);var buttons=Ui.Flow();
            buttons.Controls.Add(Ui.Button("저장 · 커브 모드 적용",Save));buttons.Controls.Add(Ui.Button("기본값 · 60°C부터 2단계",delegate {draft=new Settings();Fill();}));
            buttons.Controls.Add(Ui.Button("닫기",delegate {Close();}));root.Controls.Add(buttons,0,5);
            table.CellValueChanged+=delegate {UpdateChart();};table.RowsRemoved+=delegate {UpdateChart();};
            table.CurrentCellDirtyStateChanged+=delegate {if(table.IsCurrentCellDirty)table.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            table.DataError+=delegate(object sender,DataGridViewDataErrorEventArgs e){e.ThrowException=false;message.Text=L10n.T("온도와 단계를 확인하세요.");};
            chart.PointChanged=delegate(int i,int temp,int level){filling=true;table.Rows[i].Cells[0].Value=temp;table.Rows[i].Cells[1].Value=level.ToString();filling=false;UpdateChart();};
            Fill();
            EnableResponsiveText(delegate(float factor){table.RowTemplate.Height=Math.Max(24,(int)(Ui.S(30)*factor));foreach(DataGridViewRow row in table.Rows)row.Height=table.RowTemplate.Height;
                table.ColumnHeadersHeight=Math.Max(28,(int)(Ui.S(34)*factor));chart.Invalidate();});
        }
        public void ApplyLanguage(){Text=L10n.T("팬 커브 설정");table.Columns[0].HeaderText=L10n.T("이 온도부터 (°C)");table.Columns[1].HeaderText=L10n.T("팬 단계");UpdateChart();RefreshResponsiveText();}
        void Fill() {filling=true;table.Rows.Clear();foreach(var p in draft.Curve)table.Rows.Add(p.Temperature,p.Level.ToString());hysteresis.Value=draft.Hysteresis;delay.Value=draft.DownDelaySeconds;filling=false;UpdateChart();}
        Settings Read() {
            var s=Ui.Copy(draft);s.Curve.Clear();
            foreach(DataGridViewRow r in table.Rows){if(r.IsNewRow)continue;if(r.Cells[0].Value==null||r.Cells[1].Value==null)throw new ArgumentException("모든 행에 온도와 단계를 입력하세요.");
                int temperature,level;if(!Int32.TryParse(Convert.ToString(r.Cells[0].Value),out temperature)||!Int32.TryParse(Convert.ToString(r.Cells[1].Value),out level))throw new ArgumentException("온도와 단계를 확인하세요.");
                s.Curve.Add(new CurvePoint(temperature,level));}
            s.Hysteresis=(int)hysteresis.Value;s.DownDelaySeconds=(int)delay.Value;s.Mode="Curve";s.Language=L10n.Language;s.Validate();return s;
        }
        void UpdateChart() {if(filling)return;try {var s=Read();chart.Points=s.Curve;chart.Invalidate();message.Text=L10n.T("가열 시 즉시 상승 · 냉각 시 히스테리시스와 감속 대기 적용");message.ForeColor=Ui.Muted;}
            catch(Exception e){chart.Points=new List<CurvePoint>();chart.Invalidate();message.Text=L10n.T(e.Message);message.ForeColor=Color.FromArgb(154,91,0);}}
        void Save() {try{table.EndEdit();var s=Read();apply(s);draft=s;message.Text=L10n.T("저장하고 사용자 커브 모드를 적용했습니다.");}catch(Exception e){MessageBox.Show(this,L10n.T(e.Message),L10n.T("커브 확인"));}}

    }
    public sealed class CurveChart : Control {
        public List<CurvePoint> Points=new List<CurvePoint>();public Action<int,int,int> PointChanged;int drag=-1;
        public CurveChart(){DoubleBuffered=true;BackColor=Ui.Panel;ForeColor=Ui.Muted;Font=Ui.TextFont(14);}
        RectangleF Plot {get{return new RectangleF(Ui.S(38),Ui.S(18),Math.Max(1,Width-Ui.S(64)),Math.Max(1,Height-Ui.S(52)));}}
        PointF Position(CurvePoint p){var r=Plot;return new PointF(r.Left+r.Width*p.Temperature/90F,r.Bottom-r.Height*(p.Level-1)/6F);}
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var r=Plot;
            using(var grid=new Pen(Color.FromArgb(214,222,233)))using(var ink=new SolidBrush(Ui.Muted))using(var line=new Pen(Ui.Accent,Ui.S(2)))using(var dot=new SolidBrush(Ui.Highlight)) {
                for(int n=1;n<=7;n++){float y=r.Bottom-r.Height*(n-1)/6F;g.DrawLine(grid,r.Left,y,r.Right,y);g.DrawString(n.ToString(),Font,ink,Ui.S(12),y-Ui.S(8));}
                for(int t=0;t<=90;t+=15){float x=r.Left+r.Width*t/90F;g.DrawLine(grid,x,r.Top,x,r.Bottom);g.DrawString(t+"°",Font,ink,x-Ui.S(10),r.Bottom+Ui.S(8));}
                if(Points.Count==0)return;
                for(int i=0;i<Points.Count;i++){PointF a=Position(Points[i]);float end=i+1<Points.Count?Position(Points[i+1]).X:r.Right;
                    g.DrawLine(line,a.X,a.Y,end,a.Y);if(i+1<Points.Count)g.DrawLine(line,end,a.Y,end,Position(Points[i+1]).Y);
                    float radius=Ui.S(5);g.FillEllipse(dot,a.X-radius,a.Y-radius,radius*2,radius*2);g.DrawEllipse(line,a.X-radius,a.Y-radius,radius*2,radius*2);}
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
