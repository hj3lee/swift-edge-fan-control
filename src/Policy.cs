using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftEdgeFanControl {
    public class CurvePoint {
        public int Temperature { get; set; }
        public int Level { get; set; }
        public CurvePoint() { }
        public CurvePoint(int t, int l) { Temperature = t; Level = l; }
    }
    public class Settings {
        public string Mode { get; set; }
        public string Language { get; set; }
        public int FixedLevel { get; set; }
        public int Hysteresis { get; set; }
        public int DownDelaySeconds { get; set; }
        public List<CurvePoint> Curve { get; set; }
        public Settings() {
            Language="en"; Mode = "Curve"; FixedLevel = 1; Hysteresis = 3; DownDelaySeconds = 10;
            Curve = new List<CurvePoint> { new CurvePoint(0,1), new CurvePoint(60,2),
                new CurvePoint(65,3), new CurvePoint(72,4), new CurvePoint(78,5),
                new CurvePoint(83,6), new CurvePoint(87,7) };
        }
        public bool UpgradeFactoryCurve() {
            int[] previous={0,55,65,72,78,83,87};
            if(Curve==null||Curve.Count!=previous.Length)return false;
            for(int i=0;i<previous.Length;i++)if(Curve[i].Temperature!=previous[i]||Curve[i].Level!=i+1)return false;
            Curve[1].Temperature=60;return true;
        }
        public void Validate() {
            if (Mode != "Auto" && Mode != "Fixed" && Mode != "Curve") throw new ArgumentException("Invalid mode");
            if (FixedLevel < 1 || FixedLevel > 7) throw new ArgumentException("Level must be 1–7");
            if (Hysteresis < 1 || Hysteresis > 10) throw new ArgumentException("Hysteresis must be 1–10°C");
            if (DownDelaySeconds < 0 || DownDelaySeconds > 120) throw new ArgumentException("Down delay must be 0–120 seconds");
            if (Curve == null || Curve.Count < 2 || Curve.Count > 20 || Curve[0].Temperature != 0)
                throw new ArgumentException("Curve must start at 0°C and have 2–20 points");
            for (int i=0; i<Curve.Count; i++) {
                var p=Curve[i];
                if(p.Temperature<0 || p.Temperature>=90 || p.Level<1 || p.Level>7)
                    throw new ArgumentException("Curve temperature 0–89°C; level 1–7");
                if(i>0 && (p.Temperature<=Curve[i-1].Temperature || p.Level<Curve[i-1].Level))
                    throw new ArgumentException("Temperatures must increase; levels must not decrease");
            }
        }
    }
    public static class Presentation {
        public static string Describe(string mode,int level) {
            if(mode=="Fixed")return "팬 커브를 "+level+"단계로 고정합니다.";
            if(mode=="Curve")return "온도에 따라 팬 소음을 조절합니다.";
            return "팬 컨트롤을 중지하고 제조사 기본 세팅을 사용합니다.";
        }
        public static string StatusLine(int temperature,string mode,int level1,int level2,int rpm1,int rpm2) {
            return "CPU="+temperature+"°C mode="+mode+" level="+level1+"/"+level2+" RPM="+rpm1+"/"+rpm2;
        }
        public static float TextScale(int width,int baselineWidth,int screenWidth,int screenHeight,int baselineHeight) {
            float available=Math.Min(screenWidth/(baselineWidth+48F),screenHeight/(baselineHeight+80F));
            return Math.Max(.45F,Math.Min(2F,Math.Min(width/(float)Math.Max(1,baselineWidth),available)));
        }
    }
    public class CurvePolicy {
        int current=0, pending=0;
        DateTime pendingSince;
        public void Reset() { current=0; pending=0; }
        public int Select(Settings settings, int temperature, DateTime now) {
            settings.Validate();
            if (temperature >= 90) return 0;
            if(settings.Mode == "Auto") return 0;
            if(settings.Mode == "Fixed") return settings.FixedLevel;
            int target=settings.Curve.Last(p=>p.Temperature<=temperature).Level;
            if(current==0 || target>=current) { current=target; pending=0; return current; }
            int boundary=settings.Curve.First(p=>p.Level>=current).Temperature;
            if(temperature > boundary-settings.Hysteresis) { pending=0; return current; }
            if(pending!=target) { pending=target; pendingSince=now; }
            if((now-pendingSince).TotalSeconds>=settings.DownDelaySeconds) { current=target; pending=0; }
            return current;
        }
    }
}
