using System;
using System.Threading;
using SwiftEdgeFanControl;

class Tests {
    static int count;
    static void Assert(bool ok,string name) {if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static void Reject(Action action,string name) {bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Assert(rejected,name);}
    static void Until(Func<bool> predicate,string name) {
        var end=DateTime.UtcNow.AddSeconds(15);
        while(DateTime.UtcNow<end) {if(predicate()){Assert(true,name);return;}Thread.Sleep(40);}
        throw new Exception("Timed out: "+name);
    }
    static int Main() {
        try {
            var s=new Settings();var p=new CurvePolicy();var t=DateTime.UtcNow;
            Assert(s.Language=="en","English is the default UI language");
            Assert(L10n.Codes.Length==6&&L10n.CatalogComplete,"All six language catalogs have complete entries");
            foreach(string code in L10n.Codes){L10n.Set(code);Assert(L10n.Description("Fixed",3).Contains("3")&&L10n.T("팬 커브 모드")!="","Localized mode and description: "+code);}
            L10n.Set("missing");Assert(L10n.Language=="en","Unknown language falls back to English");
            s.Language="de";Assert(Files.Json.Deserialize<Settings>(Files.Json.Serialize(s)).Language=="de","Language preference survives settings serialization");
            Assert(s.Mode=="Curve","New settings default to fan curve mode");s.Mode="Auto";
            Assert(p.Select(s,50,t)==0,"Manufacturer auto");
            Assert(Presentation.Describe("Fixed",1)=="팬 커브를 1단계로 고정합니다.","Fixed-mode description");
            Assert(Presentation.Describe("Curve",1)=="온도에 따라 팬 소음을 조절합니다.","Curve-mode description");
            Assert(Presentation.Describe("Auto",1).Contains("제조사 기본 세팅"),"Off explains manufacturer automatic mode");
            Assert(Presentation.StatusLine(55,"Fixed",1,1,3369,3599)=="CPU=55°C mode=Fixed level=1/1 RPM=3369/3599","Concise status format");
            Assert(Presentation.TextScale(700,700,1920,1080,420)>Presentation.TextScale(460,700,1920,1080,420),"Text adapts to narrower window");
            Assert(Presentation.TextScale(700,700,800,600,420)<=Presentation.TextScale(700,700,1920,1080,420),"Text respects available screen size");
            s.Mode="Fixed";
            for(int level=1;level<=7;level++) {s.FixedLevel=level;Assert(p.Select(s,50,t)==level,"Fixed level "+level);}
            Assert(p.Select(s,90,t)==0,"90 C overrides fixed mode");
            var defaults=new Settings {Mode="Curve"};var boundary=new CurvePolicy();
            Assert(boundary.Select(defaults,59,t)==1,"Default curve below 60 C stays level 1");
            Assert(boundary.Select(defaults,60,t)==2,"Default curve at 60 C enters level 2");
            defaults.Curve[1].Temperature=55;
            Assert(defaults.UpgradeFactoryCurve()&&defaults.Curve[1].Temperature==60,"Migrate unchanged old factory curve");
            defaults.Curve[1].Temperature=58;
            Assert(!defaults.UpgradeFactoryCurve()&&defaults.Curve[1].Temperature==58,"Preserve customized curve");
            s.Mode="Curve";p.Reset();Assert(p.Select(s,65,t)==3,"Curve rises immediately");
            Assert(p.Select(s,64,t)==3,"Hysteresis holds near threshold");
            Assert(p.Select(s,62,t)==3,"Cooling starts down timer");
            Assert(p.Select(s,62,t.AddSeconds(9))==3,"Down timer holds");
            Assert(p.Select(s,62,t.AddSeconds(10))==2,"Cooling lowers after delay");
            Assert(p.Select(s,88,t.AddSeconds(11))==7,"Fast heating jumps levels");
            Assert(p.Select(s,90,t.AddSeconds(12))==0,"90 C overrides curve");
            s.FixedLevel=8;Reject(s.Validate,"Reject out-of-range level");s.FixedLevel=1;
            s.Curve[1].Temperature=0;Reject(s.Validate,"Reject duplicate temperature");s=new Settings();
            s.Curve[2].Level=1;Reject(s.Validate,"Reject decreasing curve levels");
            var fake=new TestEc();var log=new AppLog(true);
            using(var c=new Controller(fake,log,true)) {
                var set=new Settings {Mode="Fixed",FixedLevel=1};c.Request(set);c.Start();
                Until(()=>fake.Mode==2 && fake.Level==1,"Engine verifies manual level");
                Until(()=>c.Current.FullUpdated!="","Initial full sensor sample published");
                int reads=fake.TemperatureReads;Thread.Sleep(2200);
                Assert(fake.TemperatureReads==reads,"Idle controller does not poll EC every two seconds");
                fake.Temp=90;
                Until(()=>fake.Mode==0 && c.Current.Mode=="Auto","Thermal fallback restores automatic");
                fake.Temp=50;Thread.Sleep(2200);
                Assert(fake.Mode==0,"Cooling does not silently rearm manual mode");
                c.Request(set);Until(()=>fake.Mode==2,"Explicit user action rearms manual");
                fake.Level=4;
                Until(()=>fake.Mode==0 && c.Current.Mode=="Auto","External level change falls back");
                c.Request(set);Until(()=>fake.Mode==2,"Manual rearmed for sensor failure test");
                fake.BadTemp=true;
                Until(()=>fake.Mode==0 && c.Current.Mode=="Auto","Sensor failure restores automatic");
            }
            Console.WriteLine(count+" checks passed. No real hardware was opened.");return 0;
        } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
    class TestEc : IEc {
        public volatile byte Mode,Level=4,Temp=50;public volatile bool BadTemp;
        public int TemperatureReads;
        public byte Read(byte r) {
            if(r==0)return Mode;if(r==0x55||r==0x4f)return Level;
            if(r==0x58) {Interlocked.Increment(ref TemperatureReads);if(BadTemp)throw new TimeoutException("Injected temperature failure");return Temp;}
            return (byte)((r==0x5c||r==0x6a)?13:100);
        }
        public void Write(byte r,byte v) {if(r==0)Mode=v;if(r==0x55||r==0x4f)Level=v;}
        public void Dispose(){}
    }
}
