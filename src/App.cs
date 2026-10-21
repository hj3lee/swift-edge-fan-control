using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SwiftEdgeFanControl {
    public static class Program {
        [STAThread] public static int Main(string[] args) {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            bool demo=Array.IndexOf(args,"--demo")>=0 || Array.IndexOf(args,"--render-demo")>=0;
            string module=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LpcACPIEC.bin");
            if(args.Length==2 && args[0]=="--watchdog")return Controller.Watchdog(args[1],module);
            bool fresh;
            using(var single=new Mutex(true,demo?@"Local\SwiftEdgeFanControlDemo":@"Global\SwiftEdgeFanControlApp",out fresh)) {
                if(!fresh) { MessageBox.Show("Swift Edge Fan Control is already running. Check the tray icon.");return 1; }
                if(!demo && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) {
                    single.ReleaseMutex();
                    try { Process.Start(new ProcessStartInfo(Controller.ApplicationPath(),Array.IndexOf(args,"--tray")>=0?"--tray":"") {UseShellExecute=true,Verb="runas"}); }
                    catch(Exception e) { MessageBox.Show("Administrator access is required: "+e.Message);return 1; }
                    return 0;
                }
                // Never retain the single-instance mutex across UAC process creation.
                try {
                    var log=new AppLog(demo);
                    if(Array.IndexOf(args,"--test-startup")>=0) {
                        if(Startup.IsEnabled())throw new InvalidOperationException("An existing startup task was found; test did not alter it");
                        try {Startup.SetEnabled(true);if(!Startup.IsEnabled())throw new IOException("Task registration failed");}
                        finally {if(Startup.IsEnabled())Startup.SetEnabled(false);}
                        Files.Atomic(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-smoke.json"),"{\"registered\":true,\"removed\":"+(!Startup.IsEnabled()).ToString().ToLowerInvariant()+"}");
                        return 0;
                    }
                    if(Array.IndexOf(args,"--smoke30")>=0 || Array.IndexOf(args,"--smoke-crash")>=0) {
                        var smokeEc=new PawnEc(module);smokeEc.Diagnostic=delegate(string x){log.Write("EC_RETRY",x);};
                        using(var c=new Controller(smokeEc,log,false)) {
                            c.Request(new Settings {Mode="Fixed",FixedLevel=1});c.Start();
                            var rows=new System.Collections.Generic.List<Status>();
                            for(int i=0;i<15;i++){
                                Thread.Sleep(2000);rows.Add(c.Current);
                                if(i==4 && Array.IndexOf(args,"--smoke-crash")>=0) {
                                    if(!c.IsOwned)throw new IOException("Crash test not armed; no manual ownership");
                                    log.Write("TEST","Injected process exit; watchdog must restore automatic");Environment.Exit(99);
                                }
                            }
                            c.Request(new Settings());Thread.Sleep(2500);
                            rows.Add(c.Current);
                            Files.Atomic(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hardware-smoke.json"),Files.Json.Serialize(rows));
                        }
                        return 0;
                    }
                    var form=new MainForm(demo,log,module,Array.IndexOf(args,"--tray")>=0);
                    if(Array.IndexOf(args,"--render-demo")>=0) {
                        form.Show();Application.DoEvents();form.RenderPreview();form.Dispose();return 0;
                    }
                    Application.Run(form);return 0;
                } catch(Exception e) {
                    if(Array.IndexOf(args,"--smoke30")>=0 || Array.IndexOf(args,"--smoke-crash")>=0 || Array.IndexOf(args,"--test-startup")>=0)File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hardware-smoke-error.txt"),e.ToString());
                    else MessageBox.Show(e.Message,"Swift Edge Fan Control",MessageBoxButtons.OK,MessageBoxIcon.Error);
                    return 2;
                }
            }
        }
    }
    public sealed class MainForm : Form {
        readonly bool demo,startHidden;
        readonly AppLog log;Controller controller;Settings settings;
        readonly Label letterbox=new Label(), detail=new Label(), connection=new Label();
        readonly TextBox logView=new TextBox();
        readonly DataGridView curve=new DataGridView();
        readonly NumericUpDown hysteresis=new NumericUpDown(), downDelay=new NumericUpDown();
        readonly CheckBox startup=new CheckBox();
        readonly Button[] levels=new Button[7];
        readonly NotifyIcon tray=new NotifyIcon();
        readonly System.Windows.Forms.Timer uiTimer=new System.Windows.Forms.Timer();
        DateTime nextDisplay=DateTime.MinValue;bool exiting,loading=true;
        readonly Color bg=Color.FromArgb(19,24,29), panel=Color.FromArgb(29,36,43), green=Color.FromArgb(113,224,178), muted=Color.FromArgb(164,181,190);
        public MainForm(bool simulated,AppLog logger,string module,bool hidden) {
            SuspendLayout();
            demo=simulated;startHidden=hidden;log=logger;
            settings=demo?new Settings():Files.LoadSettings();
            Text="Swift Edge Fan Control"+(demo?" · DEMO":"");BackColor=bg;ForeColor=Color.White;
            Font=new Font("Segoe UI",13.33F,FontStyle.Regular,GraphicsUnit.Pixel);ClientSize=new Size(830,760);MinimumSize=new Size(800,760);
            AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.CenterScreen;
            BuildUi();FillCurve();
            AutoScaleMode=AutoScaleMode.None;ResumeLayout(true);
            log.Added+=OnLog;
            if(!demo) {
                var id=DeviceIdentity.Detect();connection.Text=id.ToString()+"  ·  "+(id.Supported?"Experimental hardware profile":"UNSUPPORTED · control disabled");
                if(!id.Supported)throw new InvalidOperationException("지원되지 않는 하드웨어입니다. EC 쓰기를 수행하지 않았습니다.\n"+id);
            }else connection.Text="DEMO  ·  가상 센서 / 하드웨어 접근 없음";
            IEc ec=demo?(IEc)new FakeEc():new PawnEc(module);
            var pawn=ec as PawnEc;if(pawn!=null)pawn.Diagnostic=delegate(string x){log.Write("EC_RETRY",x);};
            controller=new Controller(ec,log,demo);
            controller.ForcedAutomatic+=delegate(Settings s){ SafeUi(delegate { settings=s;UpdateSelection(); }); };
            controller.Request(settings);controller.Start();
            startup.Checked=!demo && Startup.IsEnabled();startup.Enabled=!demo;
            loading=false;
            uiTimer.Interval=1000;uiTimer.Tick+=delegate { RefreshStatus(); };uiTimer.Start();
            SystemEvents.PowerModeChanged+=PowerChanged;
            Shown+=delegate { if(startHidden)Hide(); };
            FormClosing+=HandleClosing;
            log.Write("START",demo?"Simulation only":"Profile SFE16-43 / Ray_PEU / V1.71; thermal fallback >=90°C");
            UpdateSelection();RefreshStatus();
        }
        Label LabelAt(string text,int x,int y,int w,int h,float size,Color color) {
            var l=new Label {Text=text,Left=x,Top=y,Width=w,Height=h,ForeColor=color,Font=new Font("Segoe UI",size*1.333F,FontStyle.Regular,GraphicsUnit.Pixel),AutoEllipsis=true};Controls.Add(l);return l;
        }
        Button ButtonAt(string text,int x,int y,int w,Action action) {
            var b=new Button {Text=text,Left=x,Top=y,Width=w,Height=36,FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=Color.White,Cursor=Cursors.Hand};
            b.FlatAppearance.BorderColor=Color.FromArgb(64,79,87);b.Click+=delegate { TryAction(action); };Controls.Add(b);return b;
        }
        void BuildUi() {
            LabelAt("SWIFT EDGE",24,18,530,25,10,green);
            LabelAt("팬 제어",22,43,400,45,25,Color.White);
            connection.SetBounds(24,94,782,24);connection.ForeColor=muted;Controls.Add(connection);
            letterbox.SetBounds(24,127,782,48);letterbox.BackColor=panel;letterbox.ForeColor=green;
            letterbox.Font=new Font("Segoe UI",14.67F,FontStyle.Bold,GraphicsUnit.Pixel);letterbox.TextAlign=ContentAlignment.MiddleCenter;Controls.Add(letterbox);
            detail.SetBounds(24,180,782,26);detail.ForeColor=muted;Controls.Add(detail);
            LabelAt("고정 단계  ·  양쪽 팬에 함께 적용",24,220,500,24,11,Color.White);
            for(int i=0;i<7;i++) {
                int level=i+1;levels[i]=ButtonAt(level.ToString(),24+i*65,252,56,delegate { settings.Mode="Fixed";settings.FixedLevel=level;Apply(); });
            }
            ButtonAt("제조사 자동",500,252,144,delegate { settings.Mode="Auto";Apply(); });
            ButtonAt("종료 · 자동 복귀",654,252,152,delegate { exiting=true;Close(); });
            LabelAt("단계는 RPM 직접 지정이 아닙니다. 90°C 이상이면 제조사 자동으로 복귀합니다.",24,296,780,24,9,muted);
            LabelAt("사용자 온도 커브",24,332,300,27,12,Color.White);
            curve.SetBounds(24,368,375,208);curve.BackgroundColor=panel;curve.BorderStyle=BorderStyle.None;
            curve.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;curve.AllowUserToAddRows=true;curve.AllowUserToDeleteRows=true;
            curve.RowHeadersWidth=30;curve.GridColor=Color.FromArgb(64,79,87);curve.EnableHeadersVisualStyles=false;
            curve.ColumnHeadersDefaultCellStyle.BackColor=panel;curve.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
            curve.DefaultCellStyle.BackColor=panel;curve.DefaultCellStyle.ForeColor=Color.White;
            curve.DefaultCellStyle.SelectionBackColor=Color.FromArgb(48,87,74);curve.DefaultCellStyle.SelectionForeColor=Color.White;
            curve.Columns.Add("temperature","이 온도부터 (°C)");
            var col=new DataGridViewComboBoxColumn {Name="level",HeaderText="단계",FlatStyle=FlatStyle.Flat};
            for(int n=1;n<=7;n++)col.Items.Add(n.ToString());curve.Columns.Add(col);Controls.Add(curve);
            LabelAt("히스테리시스 (°C)",425,370,240,25,10,Color.White);
            hysteresis.SetBounds(680,368,112,28);hysteresis.Minimum=1;hysteresis.Maximum=10;Controls.Add(hysteresis);
            LabelAt("감속 전 대기 (초)",425,412,240,25,10,Color.White);
            downDelay.SetBounds(680,410,112,28);downDelay.Minimum=0;downDelay.Maximum=120;Controls.Add(downDelay);
            LabelAt("가열 시 즉시 상승 / 냉각 시 여유 온도와 대기 적용\n첫 행은 0°C, 온도와 단계는 오름차순입니다.",425,452,370,52,9,muted);
            ButtonAt("커브 저장 · 적용",425,515,190,ApplyCurve);
            ButtonAt("기본 커브",627,515,165,delegate { settings.Curve=new Settings().Curve;settings.Hysteresis=3;settings.DownDelaySeconds=10;FillCurve(); });
            startup.SetBounds(24,590,380,28);startup.Text="Windows 로그인 시 자동 실행";startup.ForeColor=Color.White;
            startup.CheckedChanged+=delegate {
                if(loading)return;
                TryAction(delegate { Startup.SetEnabled(startup.Checked);log.Write("STARTUP",startup.Checked?"Enabled":"Disabled"); });
                loading=true;startup.Checked=Startup.IsEnabled();loading=false;
            };Controls.Add(startup);
            ButtonAt("로그 폴더",645,585,147,delegate { Process.Start(new ProcessStartInfo(log.DirectoryPath){UseShellExecute=true}); });
            logView.SetBounds(24,636,782,100);logView.Multiline=true;logView.ReadOnly=true;logView.ScrollBars=ScrollBars.Vertical;
            logView.BackColor=panel;logView.ForeColor=muted;logView.BorderStyle=BorderStyle.None;logView.Font=new Font("Consolas",12F,FontStyle.Regular,GraphicsUnit.Pixel);
            logView.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(logView);
            var menu=new ContextMenuStrip();menu.Items.Add("열기",null,delegate {Show();WindowState=FormWindowState.Normal;Activate();});
            menu.Items.Add("제조사 자동",null,delegate {TryAction(delegate{settings.Mode="Auto";Apply();});});
            menu.Items.Add("종료 · 자동 복귀",null,delegate {exiting=true;Close();});
            tray.Icon=SystemIcons.Application;tray.Text="Swift Edge Fan Control";tray.Visible=true;tray.ContextMenuStrip=menu;
            tray.DoubleClick+=delegate {Show();WindowState=FormWindowState.Normal;Activate();};
        }
        void FillCurve() {
            curve.Rows.Clear();foreach(var p in settings.Curve)curve.Rows.Add(p.Temperature,p.Level.ToString());
            hysteresis.Value=settings.Hysteresis;downDelay.Value=settings.DownDelaySeconds;
        }
        void ApplyCurve() {
            curve.EndEdit();var next=Files.Json.Deserialize<Settings>(Files.Json.Serialize(settings));
            next.Curve.Clear();
            foreach(DataGridViewRow row in curve.Rows) {
                if(row.IsNewRow)continue;
                next.Curve.Add(new CurvePoint(Convert.ToInt32(row.Cells[0].Value),Convert.ToInt32(row.Cells[1].Value)));
            }
            next.Mode="Curve";next.Hysteresis=(int)hysteresis.Value;next.DownDelaySeconds=(int)downDelay.Value;
            next.Validate();settings=next;Apply();
        }
        void Apply() {
            settings.Validate();if(!demo)Files.SaveSettings(settings);controller.Request(settings);UpdateSelection();nextDisplay=DateTime.MinValue;
        }
        void UpdateSelection() {
            for(int i=0;i<7;i++) { bool selected=settings.Mode=="Fixed"&&settings.FixedLevel==i+1;
                levels[i].BackColor=selected?green:panel;levels[i].ForeColor=selected?bg:Color.White; }
        }
        static string ModeLabel(string mode) {return mode=="Fixed"?"고정":mode=="Curve"?"사용자 지정":"제조사 디폴트";}
        void RefreshStatus() {
            if(controller==null)return;var s=controller.Current;
            if(DateTime.UtcNow>=nextDisplay) {
                letterbox.Text=String.Format("CPU {0}°C    |    {1}    |    FAN 1  {2:N0} RPM    ·    FAN 2  {3:N0} RPM",s.Temperature,ModeLabel(s.Mode),s.Rpm1,s.Rpm2);
                nextDisplay=DateTime.UtcNow.AddSeconds(20);
            }
            detail.Text=s.Message+"  ·  온도 "+s.Updated+" / RPM "+s.FullUpdated+"  ·  읽기 오류 "+s.Timeouts+" / "+s.Attempts;
            string tip="CPU "+s.Temperature+"°C · "+ModeLabel(s.Mode)+" · "+s.Rpm1+"/"+s.Rpm2+" RPM";
            tray.Text=tip.Length>63?tip.Substring(0,63):tip;
        }
        void OnLog(string text) { SafeUi(delegate {
            if(logView.Lines.Length>300)logView.Clear();logView.AppendText(text+Environment.NewLine);
        }); }
        void SafeUi(Action action) {
            if(IsDisposed||Disposing)return;
            if(!IsHandleCreated)return;
            if(InvokeRequired) { try {BeginInvoke(action);}catch(InvalidOperationException){} }else action();
        }
        void TryAction(Action action) {try {action();}catch(Exception e){MessageBox.Show(this,e.Message,"설정 오류",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
        void PowerChanged(object sender,PowerModeChangedEventArgs e) {
            if(e.Mode==PowerModes.Suspend || e.Mode==PowerModes.Resume) {
                controller.ForceAuto("Power state: "+e.Mode+"; manual resume requires user selection");
                if(e.Mode==PowerModes.Suspend) {
                    var wait=Stopwatch.StartNew();while(controller.IsOwned && wait.ElapsedMilliseconds<5000)Thread.Sleep(25);
                }
                SafeUi(delegate {settings.Mode="Auto";if(!demo)Files.SaveSettings(settings);UpdateSelection();});
            }
        }
        void HandleClosing(object sender,FormClosingEventArgs e) {
            if(!exiting && e.CloseReason==CloseReason.UserClosing) {e.Cancel=true;Hide();return;}
            uiTimer.Stop();SystemEvents.PowerModeChanged-=PowerChanged;
            if(controller!=null)controller.Dispose();tray.Visible=false;tray.Dispose();
        }
        public void RenderPreview() {
            letterbox.Text="CPU 52°C    |    고정 · 1단계    |    FAN 1  3,330 RPM    ·    FAN 2  3,540 RPM";
            detail.Text="DEMO · 하드웨어 접근 없음 · 상태 표시는 20초 간격";
            using(var image=new Bitmap(Width,Height)) { DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png")); }
            exiting=true;Close();
        }
    }
    public static class Startup {
        const string TaskName="SwiftEdgeFanControl";
        static string InstallRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"SwiftEdgeFanControl");}}
        public static bool IsEnabled() { using(var p=Process.Start(new ProcessStartInfo("schtasks.exe","/Query /TN "+TaskName) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) { p.StandardOutput.ReadToEnd();p.StandardError.ReadToEnd();p.WaitForExit();return p.ExitCode==0; } }
        public static void SetEnabled(bool enabled) {
            if(!enabled) { Run("/Delete /TN "+TaskName+" /F");return; }
            Directory.CreateDirectory(InstallRoot);
            foreach(string file in new[]{"SwiftEdgeFanControl.exe","LpcACPIEC.bin"}) {
                string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,file),dest=Path.Combine(InstallRoot,file);
                if(!source.Equals(dest,StringComparison.OrdinalIgnoreCase))File.Copy(source,dest,true);
            }
            string exe=Path.Combine(InstallRoot,"SwiftEdgeFanControl.exe");
            string sid=WindowsIdentity.GetCurrent().User.Value;
            string xml="<?xml version=\"1.0\" encoding=\"UTF-16\"?><Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Triggers><LogonTrigger><UserId>"+sid+"</UserId></LogonTrigger></Triggers><Principals><Principal id=\"Author\"><UserId>"+sid+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals><Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings><Actions Context=\"Author\"><Exec><Command>"+System.Security.SecurityElement.Escape(exe)+"</Command><Arguments>--tray</Arguments></Exec></Actions></Task>";
            string temp=Path.Combine(Files.Root,"startup-task.xml");Directory.CreateDirectory(Files.Root);File.WriteAllText(temp,xml,System.Text.Encoding.Unicode);
            try {Run("/Create /TN "+TaskName+" /XML \""+temp+"\" /F");}finally{File.Delete(temp);}
        }
        static void Run(string args) {
            using(var p=Process.Start(new ProcessStartInfo("schtasks.exe",args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) {
                string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new IOException(output);
            }
        }
    }
}
