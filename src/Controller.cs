using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace SwiftEdgeFanControl {
    public static class Files {
        public static void StartupTrace(string stage) {
            try {Directory.CreateDirectory(Root);string p=Path.Combine(Root,"startup.log");
                if(File.Exists(p)&&new FileInfo(p).Length>131072)File.WriteAllText(p,"");
                File.AppendAllText(p,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" pid="+Process.GetCurrentProcess().Id+" "+stage+Environment.NewLine);
            } catch { }
        }
        public static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SwiftEdgeFanControl");
        public static JavaScriptSerializer Json { get { return new JavaScriptSerializer(); } }
        static readonly object writeGate=new object();
        public static string ReadShared(string path) {
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(file))return reader.ReadToEnd();
        }
        public static void Atomic(string path,string text) {
            lock(writeGate) {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try {
                    File.WriteAllText(tmp,text);
                    for(int attempt=0;;attempt++) {
                        try {if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);return;}
                        catch(IOException){if(attempt==7)throw;Thread.Sleep(20*(attempt+1));}
                    }
                }finally{if(File.Exists(tmp))File.Delete(tmp);}
            }
        }
        public static Settings LoadSettings() {
            string path=Path.Combine(Root,"settings.json");
            if(!File.Exists(path))return new Settings();
            var s=Json.Deserialize<Settings>(ReadShared(path));s.UpgradeFactoryCurve();s.Validate();return s;
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
    public sealed class Controller : IDisposable {
        readonly object gate=new object();
        readonly IEc ec; readonly AppLog log;
        readonly AutoResetEvent wake=new AutoResetEvent(false);
        readonly CurvePolicy policy=new CurvePolicy();
        Settings requested=new Settings(); Status status=new Status();
        volatile bool stop, owned; Thread worker;
        long version, appliedVersion=-1;
        public const int SampleIntervalMilliseconds=10000;
        public event Action StatusUpdated;
        public Controller(IEc hardware,AppLog logger,bool simulated) {ec=hardware;log=logger;}
        public Status Current {get {lock(gate)return status.Copy();}}
        public bool IsOwned {get {return owned;}}
        public static string ApplicationPath() {return Process.GetCurrentProcess().MainModule.FileName;}
        public void Start() {
            lock(gate) {
                if(stop || (worker!=null&&worker.IsAlive))return;
                worker=new Thread(Run);worker.IsBackground=true;worker.Start();
            }
        }
        public void Request(Settings settings) {
            settings.Validate();
            lock(gate) {
                requested=Files.Json.Deserialize<Settings>(Files.Json.Serialize(settings));version++;
                status.Mode=requested.Mode;
            }
            log.Write("MODE","Requested "+settings.Mode+" level="+settings.FixedLevel);
            log.Write("CURVE",Files.Json.Serialize(settings.Curve));wake.Set();
        }
        void Restore() {
            // Only an explicitly selected Auto request reaches this method.
            byte mode=ec.Read(0);
            if((mode&2)!=0)ec.Write(0,(byte)(mode&~2));
            if((ec.Read(0)&2)!=0)throw new IOException("Auto readback mismatch");
            owned=false;
        }
        void ApplyLevel(int level) {
            // Read hardware every sample, including startup/resume; cached ownership is insufficient.
            byte mode=ec.Read(0);
            int a=ec.Read(0x55),b=ec.Read(0x4f);
            if((mode&2)!=0&&a==level&&b==level) {owned=true;return;}
            owned=true;
            ec.Write(0,(byte)(mode|2));ec.Write(0x55,(byte)level);ec.Write(0x4f,(byte)level);
            if((ec.Read(0)&2)==0||ec.Read(0x55)!=level||ec.Read(0x4f)!=level)
                throw new IOException("Requested level readback mismatch; will retry");
            log.Write("APPLY","Both fans level "+level+" verified");
        }
        void Run() {
            while(!stop) {
                wake.WaitOne(0);bool failed=false;
                try {
                    lock(gate) {
                        Settings settings=requested;
                        if(appliedVersion!=version){policy.Reset();appliedVersion=version;}
                        // Fixed and Auto requests do not depend on a successful temperature read.
                        if(settings.Mode=="Auto")Restore();
                        else if(settings.Mode=="Fixed")ApplyLevel(settings.FixedLevel);
                        int temp=ec.Read(0x58);
                        if(temp<10||temp>110)throw new IOException("Invalid temperature sensor value; retaining requested mode");
                        if(settings.Mode=="Curve")ApplyLevel(policy.Select(settings,temp,DateTime.UtcNow));
                        Status next=status.Copy();next.Mode=settings.Mode;next.Owned=owned;next.Temperature=temp;
                        next.Level1=ec.Read(0x55);next.Level2=ec.Read(0x4f);
                        next.Rpm1=ec.Read(0x5c)*256+ec.Read(0x5d);
                        next.Rpm2=ec.Read(0x6a)*256+ec.Read(0x6b);
                        next.Updated=next.FullUpdated=DateTime.Now.ToString("HH:mm:ss");
                        var pawn=ec as PawnEc;if(pawn!=null){next.Attempts=pawn.ReadAttempts;next.Timeouts=pawn.ReadTimeouts;}
                        next.Message="Sensor read verified";status=next;
                        log.Write("STATUS",Presentation.StatusLine(temp,settings.Mode,next.Level1,next.Level2,next.Rpm1,next.Rpm2));
                    }
                } catch(Exception e) {
                    failed=true;
                    lock(gate){status.Mode=requested.Mode;status.Owned=owned;status.Message="Retry pending: "+e.Message;}
                    log.Write("RETRY",e.Message);
                }
                var updated=StatusUpdated;if(updated!=null)updated();
                // Retry startup and transient errors without requiring another user click.
                wake.WaitOne(failed?1000:SampleIntervalMilliseconds);
            }
        }
        public void Dispose() {
            stop=true;wake.Set();
            if(worker!=null&&!worker.Join(7000)){log.Write("ERROR","Worker exit timeout");return;}
            // Exit leaves hardware state unchanged; select Auto explicitly to release control.
            ec.Dispose();wake.Dispose();
        }
    }
}
