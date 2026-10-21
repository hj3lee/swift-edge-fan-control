using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace SwiftEdgeFanControl {
    public static class Files {
        public static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SwiftEdgeFanControl");
        public static JavaScriptSerializer Json { get { return new JavaScriptSerializer(); } }
        public static void Atomic(string path,string text) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp=path+".tmp";
            File.WriteAllText(tmp,text);
            if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
        }
        public static Settings LoadSettings() {
            string path=Path.Combine(Root,"settings.json");
            if(!File.Exists(path))return new Settings();
            var s=Json.Deserialize<Settings>(File.ReadAllText(path));s.Validate();return s;
        }
        public static void SaveSettings(Settings s) { s.Validate(); Atomic(Path.Combine(Root,"settings.json"),Json.Serialize(s)); }
    }
    public sealed class AppLog {
        readonly object gate=new object();
        readonly Mutex fileGate=new Mutex(false,@"Global\SwiftEdgeFanControlLog");
        public readonly string DirectoryPath;
        public event Action<string> Added;
        public AppLog(bool demo) {
            DirectoryPath=Path.Combine(Files.Root,demo?"demo-logs":"logs");Directory.CreateDirectory(DirectoryPath);
        }
        public void Write(string kind,string message) {
            string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" ["+kind+"] "+message;
            lock(gate) {
                bool held=false;
                try {
                try {held=fileGate.WaitOne(1000);}catch(AbandonedMutexException){held=true;}
                if(!held)return;
                string path=Path.Combine(DirectoryPath,"fan-"+DateTime.Now.ToString("yyyy-MM-dd")+".log");
                if(File.Exists(path) && new FileInfo(path).Length>4*1024*1024) {
                    string rotated=path+".1"; if(File.Exists(rotated))File.Delete(rotated);File.Move(path,rotated);
                }
                File.AppendAllText(path,line+Environment.NewLine);
                foreach(string old in Directory.GetFiles(DirectoryPath,"fan-*.log*"))
                    if(File.GetLastWriteTimeUtc(old)<DateTime.UtcNow.AddDays(-14))File.Delete(old);
                } finally {if(held)fileGate.ReleaseMutex();}
            }
            var callback=Added;if(callback!=null)callback(line);
        }
    }
    public sealed class Status {
        public int Temperature, Rpm1, Rpm2, Level1, Level2;
        public string Mode="Auto", Message="Starting", Updated="", FullUpdated="";
        public bool Owned;
        public long Attempts, Timeouts;
        public Status Copy() { return (Status)MemberwiseClone(); }
    }
    public sealed class Heartbeat {
        public long Tick { get; set; }
        public int Pid { get; set; }
        public bool Done { get; set; }
    }
    public sealed class Controller : IDisposable {
        readonly object gate=new object();
        readonly IEc ec; readonly AppLog log; readonly bool demo;
        readonly AutoResetEvent wake=new AutoResetEvent(false);
        readonly CurvePolicy policy=new CurvePolicy();
        readonly string heartbeatPath;
        Settings requested=new Settings(); Status status=new Status();
        volatile bool stop; Thread worker; Process watchdog;
        volatile bool owned; int expectedLevel,cycle; long version, appliedVersion=-1;
        public event Action<Settings> ForcedAutomatic;
        public Controller(IEc hardware,AppLog logger,bool simulated) {
            ec=hardware;log=logger;demo=simulated;
            heartbeatPath=Path.Combine(Files.Root,"sessions",Guid.NewGuid().ToString("N")+".json");
        }
        public Status Current { get { lock(gate)return status.Copy(); } }
        public bool IsOwned { get { return owned; } }
        public void Start() { worker=new Thread(Run);worker.IsBackground=true;worker.Start(); }
        public void Request(Settings settings) {
            settings.Validate();
            lock(gate) { requested=Files.Json.Deserialize<Settings>(Files.Json.Serialize(settings));version++; }
            log.Write("MODE","Requested "+settings.Mode+" level="+settings.FixedLevel+" hysteresis="+settings.Hysteresis+"°C down-delay="+settings.DownDelaySeconds+"s");
            log.Write("CURVE",Files.Json.Serialize(settings.Curve));wake.Set();
        }
        void Beat(bool done) {
            Files.Atomic(heartbeatPath,Files.Json.Serialize(new Heartbeat {Tick=Stopwatch.GetTimestamp(),Pid=Process.GetCurrentProcess().Id,Done=done}));
        }
        void EnsureWatchdog() {
            if(demo)return;
            if(watchdog!=null && !watchdog.HasExited)return;
            Beat(false);
            watchdog=Process.Start(new ProcessStartInfo(ApplicationPath(),"--watchdog \""+heartbeatPath+"\"") { UseShellExecute=false,CreateNoWindow=true });
            if(watchdog==null)throw new IOException("Watchdog did not start");
            var wait=Stopwatch.StartNew();
            while(!File.Exists(heartbeatPath+".ready") && wait.ElapsedMilliseconds<4000 && !watchdog.HasExited)Thread.Sleep(50);
            if(watchdog.HasExited || !File.Exists(heartbeatPath+".ready"))throw new IOException("Watchdog initialization failed");
        }
        public static string ApplicationPath() { return Process.GetCurrentProcess().MainModule.FileName; }
        void Restore() {
            if(!owned)return;
            Exception last=null;
            for(int i=0;i<5;i++) {
                try {
                    byte mode=ec.Read(0);
                    if((mode&2)!=0)ec.Write(0,(byte)(mode&~2));
                    if((ec.Read(0)&2)!=0)throw new IOException("Auto readback mismatch");
                    owned=false;expectedLevel=0;log.Write("RESTORE","Manufacturer automatic verified");return;
                } catch(Exception e) { last=e;Thread.Sleep(100); }
            }
            throw new IOException("Automatic restore failed",last);
        }
        void ApplyLevel(int level) {
            if(level==expectedLevel && owned)return;
            EnsureWatchdog();
            byte mode=ec.Read(0);
            // Mark ownership before a write: even an ambiguous partial write must be restored.
            owned=true;
            ec.Write(0,(byte)(mode|2));ec.Write(0x55,(byte)level);ec.Write(0x4f,(byte)level);
            if((ec.Read(0)&2)==0 || ec.Read(0x55)!=level || ec.Read(0x4f)!=level)
                throw new IOException("Requested level readback mismatch");
            expectedLevel=level;cycle=0;log.Write("APPLY","Both fans level "+level+" verified");
        }
        public void ForceAuto(string reason) {
            var s=Files.LoadSettings();s.Mode="Auto";
            if(!demo)Files.SaveSettings(s);
            Request(s);log.Write("FALLBACK",reason);
        }
        void LatchAuto(string reason) {
            lock(gate) { requested.Mode="Auto";version++;status.Message=reason; }
            log.Write("FALLBACK",reason);
            Settings s;lock(gate)s=Files.Json.Deserialize<Settings>(Files.Json.Serialize(requested));
            if(!demo)Files.SaveSettings(s);
            var handler=ForcedAutomatic;if(handler!=null)handler(s);
            Restore();
        }
        void Run() {
            DateTime nextLog=DateTime.MinValue;
            bool ready=false;
            try {
                byte baseline=0;bool haveBaseline=false;
                for(int i=0;i<4;i++) {
                    try {baseline=ec.Read(0);haveBaseline=true;break;}
                    catch(TimeoutException) {Thread.Sleep(150);}
                }
                if(!haveBaseline)throw new IOException("Cannot read EC baseline; no control writes performed");
                if((baseline&2)!=0)throw new IOException("Another manual controller is active; no takeover performed");
                EnsureWatchdog();ready=true;
                while(!stop) {
                    var iteration=Stopwatch.StartNew();
                    try {
                        if(owned && !demo && (watchdog==null || watchdog.HasExited))throw new IOException("Watchdog stopped; automatic fallback");
                        Settings settings;long v;
                        lock(gate) { settings=Files.Json.Deserialize<Settings>(Files.Json.Serialize(requested));v=version; }
                        if(v!=appliedVersion) { policy.Reset();appliedVersion=v; }
                        int temp=ec.Read(0x58);
                        if(temp<10 || temp>110)throw new IOException("Invalid temperature sensor value");
                        Beat(false);
                        if(temp>=90 && settings.Mode!="Auto") { LatchAuto("CPU >=90°C; automatic latched until user action");settings.Mode="Auto"; }
                        int level=policy.Select(settings,temp,DateTime.UtcNow);
                        if(level==0)Restore();else ApplyLevel(level);
                        bool full=(cycle++%3)==0;
                        Status next=Current;next.Temperature=temp;next.Mode=settings.Mode;next.Owned=owned;
                        next.Updated=DateTime.Now.ToString("HH:mm:ss");
                        if(full) {
                            int mode=ec.Read(0), a=ec.Read(0x55), b=ec.Read(0x4f);
                            int rpm1=ec.Read(0x5c)*256+ec.Read(0x5d),rpm2=ec.Read(0x6a)*256+ec.Read(0x6b);
                            if(owned && ((mode&2)==0||a!=expectedLevel||b!=expectedLevel))throw new IOException("External level change detected; automatic fallback");
                            if(owned && (rpm1<1000||rpm2<1000||rpm1>10000||rpm2>10000))throw new IOException("Invalid fan tachometer reading");
                            next.Level1=a;next.Level2=b;next.Rpm1=rpm1;next.Rpm2=rpm2;next.FullUpdated=DateTime.Now.ToString("HH:mm:ss");
                        }
                        var pawn=ec as PawnEc;if(pawn!=null) { next.Attempts=pawn.ReadAttempts;next.Timeouts=pawn.ReadTimeouts; }
                        next.Message=demo?"DEMO · no hardware access":"Sensor read verified";
                        lock(gate)status=next;
                        if(DateTime.UtcNow>=nextLog) {
                            log.Write("STATUS","CPU="+temp+"°C mode="+settings.Mode+" level="+next.Level1+"/"+next.Level2+" RPM="+next.Rpm1+"/"+next.Rpm2+" RPM-read="+next.FullUpdated+" timeouts="+next.Timeouts+"/"+next.Attempts);
                            nextLog=DateTime.UtcNow.AddSeconds(20);
                        }
                    } catch(Exception e) {
                        try { LatchAuto(e.Message); }
                        catch(Exception restoreError) {
                            log.Write("CRITICAL",restoreError.ToString());
                            lock(gate)status.Message="Restore failed; watchdog recovery pending";
                            // Stop refreshing heartbeat; independent process will retry restoration.
                            ready=false;break;
                        }
                        lock(gate) { status.Mode="Auto";status.Owned=false;status.Message=e.Message; }
                    }
                    wake.WaitOne(Math.Max(0,2000-(int)iteration.ElapsedMilliseconds));
                }
            } catch(Exception e) { log.Write("ERROR",e.ToString());lock(gate)status.Message=e.Message; }
            finally {
                try { Restore();if(ready)Beat(true); }
                catch(Exception e) { log.Write("CRITICAL","Exit restore: "+e.Message); }
            }
        }
        public void Dispose() {
            stop=true;wake.Set();
            if(worker!=null && !worker.Join(7000)) {
                log.Write("CRITICAL","Worker exit timeout; leaving independent watchdog armed");return;
            }
            if(watchdog!=null && !watchdog.HasExited)watchdog.WaitForExit(4000);
            ec.Dispose();wake.Dispose();
        }
        public static int Watchdog(string path,string module) {
            var log=new AppLog(false);
            try {
                using(var ec=new PawnEc(module)) {
                    Files.Atomic(path+".ready",Process.GetCurrentProcess().Id.ToString());
                    Heartbeat lastGood=null;long opened=Stopwatch.GetTimestamp();string lastReadError="";
                    while(true) {
                        try {
                            var parsed=Files.Json.Deserialize<Heartbeat>(File.ReadAllText(path));
                            if(parsed!=null && parsed.Tick>0)lastGood=parsed;
                        }catch(Exception e) {lastReadError=e.Message;}
                        Heartbeat hb=lastGood;
                        double age=(Stopwatch.GetTimestamp()-(hb==null?opened:hb.Tick))/(double)Stopwatch.Frequency;
                        bool stale=age>8;
                        if(stale || (hb!=null && hb.Done)) {
                            for(int i=0;i<12;i++) {
                                try {
                                    byte mode=ec.Read(0);
                                    if((mode&2)!=0)ec.Write(0,(byte)(mode&~2));
                                    if((ec.Read(0)&2)!=0)throw new IOException("Auto verification mismatch");
                                    log.Write("WATCHDOG","Automatic verified; "+(stale?"heartbeat stale age="+age.ToString("F2")+" "+lastReadError:"normal exit"));
                                    if(File.Exists(path))File.Delete(path);if(File.Exists(path+".ready"))File.Delete(path+".ready");return 0;
                                }catch(Exception e){ log.Write("WATCHDOG_RETRY",e.Message);Thread.Sleep(150); }
                            }
                            log.Write("CRITICAL","Watchdog could not verify automatic mode");return 2;
                        }
                        Thread.Sleep(500);
                    }
                }
            }catch(Exception e){log.Write("CRITICAL","Watchdog: "+e);return 2;}
        }
    }
}
