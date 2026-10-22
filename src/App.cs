using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace SwiftEdgeFanControl {
    public static class Program {
        [STAThread] public static int Main(string[] args) {
            Files.StartupTrace("Process start "+String.Join(" ",args));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            bool demo=false;
#if DEVELOPMENT
            demo=Array.IndexOf(args,"--demo")>=0 || Array.IndexOf(args,"--render-demo")>=0;
#else
            if(args.Length!=0 && !(args.Length==1 && args[0]=="--tray") && !(args.Length==2 && args[0]=="--watchdog")) {
                MessageBox.Show("Unsupported command-line options.");return 1;
            }
#endif
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
                    Files.StartupTrace("AppLog begin");var log=new AppLog(demo);Files.StartupTrace("AppLog ready");
                    var form=new MainForm(demo,log,module,Array.IndexOf(args,"--tray")>=0);
#if DEVELOPMENT
                    if(Array.IndexOf(args,"--render-demo")>=0) {
                        form.Show();Application.DoEvents();form.RenderPreview();form.Dispose();return 0;
                    }
#endif
                    Files.StartupTrace("UI message loop begin");Application.Run(form);return 0;
                } catch(Exception e) {
                    Files.StartupTrace("Startup error "+e);
                    MessageBox.Show(e.Message,"Swift Edge Fan Control",MessageBoxButtons.OK,MessageBoxIcon.Error);
                    return 2;
                }
            }
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
