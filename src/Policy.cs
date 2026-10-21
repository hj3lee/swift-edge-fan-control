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
        public int FixedLevel { get; set; }
        public int Hysteresis { get; set; }
        public int DownDelaySeconds { get; set; }
        public List<CurvePoint> Curve { get; set; }
        public Settings() {
            Mode = "Auto"; FixedLevel = 1; Hysteresis = 3; DownDelaySeconds = 10;
            Curve = new List<CurvePoint> { new CurvePoint(0,1), new CurvePoint(55,2),
                new CurvePoint(65,3), new CurvePoint(72,4), new CurvePoint(78,5),
                new CurvePoint(83,6), new CurvePoint(87,7) };
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
