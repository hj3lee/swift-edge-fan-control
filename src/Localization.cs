using System;using System.Collections.Generic;using System.Windows.Forms;
namespace SwiftEdgeFanControl {
    public static class L10n {
        public static readonly string[] Codes={"en","ko","ja","zh","es","de"};
        public static readonly string[] Names={"English","한국어","日本語","简体中文","Español","Deutsch"};
        static string language="en";
        static readonly Dictionary<Control,string> bindings=new Dictionary<Control,string>();
        // Each row: English, Korean, Japanese, Simplified Chinese, Spanish, German.
        static readonly Dictionary<string,string[]> words=new Dictionary<string,string[]> {
            {"팬 커브 모드",new[]{"Fan curve mode","팬 커브 모드","ファンカーブ","风扇曲线","Curva del ventilador","Lüfterkurve"}},
            {"제조사 기본",new[]{"System default","제조사 기본","メーカー標準","系统默认","Predeterminado","Systemstandard"}},
            {"제조사 자동",new[]{"System default","제조사 자동","メーカー標準","系统默认","Predeterminado","Systemstandard"}},
            {"제조사 기본 모드",new[]{"System default mode","제조사 기본 모드","メーカー標準モード","系统默认模式","Modo predeterminado","Systemstandardmodus"}},
            {"고정 모드 · {0}단계",new[]{"Fixed mode · Level {0}","고정 모드 · {0}단계","固定モード · レベル {0}","固定模式 · {0} 档","Modo fijo · Nivel {0}","Fester Modus · Stufe {0}"}},
            {"팬 커브 모드 · {0}단계",new[]{"Fan curve mode · Level {0}","팬 커브 모드 · {0}단계","ファンカーブ · レベル {0}","风扇曲线 · {0} 档","Curva del ventilador · Nivel {0}","Lüfterkurve · Stufe {0}"}},
            {"고정 설명",new[]{"Both fans stay at level {0}.","양쪽 팬을 {0}단계로 고정합니다.","両方のファンをレベル {0} に固定します。","将两个风扇固定在 {0} 档。","Ambos ventiladores permanecen en el nivel {0}.","Beide Lüfter bleiben auf Stufe {0}."}},
            {"커브 설명",new[]{"Adjust fan noise to CPU temperature.","온도에 따라 팬 소음을 조절합니다.","CPU 温度に合わせてファンを調整します。","根据 CPU 温度调节风扇噪音。","Ajusta el ruido según la temperatura de la CPU.","Passt das Lüftergeräusch an die CPU-Temperatur an."}},
            {"자동 설명",new[]{"Release fan control to the system firmware.","팬 컨트롤을 중지하고 제조사 기본 세팅을 사용합니다.","ファン制御をメーカー標準に戻します。","将风扇控制交还给系统固件。","Devuelve el control al firmware del sistema.","Gibt die Lüftersteuerung an die Systemfirmware zurück."}},
            {"커브 설정",new[]{"Curve settings","커브 설정","カーブ設定","曲线设置","Ajustar curva","Kurveneinstellungen"}},
            {"로그 보기",new[]{"View log","로그 보기","ログを表示","查看日志","Ver registro","Protokoll anzeigen"}},
            {"종료",new[]{"Exit","종료","終了","退出","Salir","Beenden"}},
            {"열기",new[]{"Open","열기","開く","打开","Abrir","Öffnen"}},
            {"종료 · 자동 복귀",new[]{"Exit · restore default","종료 · 자동 복귀","終了 · 標準に戻す","退出并恢复默认","Salir y restaurar","Beenden · Standard wiederherstellen"}},
            {"Windows 로그인 시 실행",new[]{"Run at Windows sign-in","Windows 로그인 시 실행","Windows ログイン時に起動","登录 Windows 时运行","Iniciar con Windows","Bei Windows-Anmeldung starten"}},
            {"글자 크기",new[]{"Text size","글자 크기","文字サイズ","文字大小","Tamaño del texto","Textgröße"}},
            {"언어",new[]{"Language","언어","言語","语言","Idioma","Sprache"}},
            {"자동",new[]{"Auto","자동","自動","自动","Auto","Auto"}},
            {"전체 로그 폴더",new[]{"Open log folder","전체 로그 폴더","ログフォルダー","日志文件夹","Carpeta de registros","Protokollordner"}},
            {"설정 확인",new[]{"Check settings","설정 확인","設定の確認","检查设置","Revisar ajustes","Einstellungen prüfen"}},
            {"연결 확인 중",new[]{"Connecting…","연결 확인 중","接続中…","正在连接…","Conectando…","Verbindung wird hergestellt…"}},
            {"팬 커브 설정",new[]{"Fan curve settings","팬 커브 설정","ファンカーブ設定","风扇曲线设置","Ajustes de curva","Lüfterkurven-Einstellungen"}},
            {"온도에 따라 단계 1–7 자동 조절",new[]{"Set levels 1–7 by temperature","온도에 따라 단계 1–7 자동 조절","温度に応じてレベル 1–7 を調整","按温度设置 1–7 档","Niveles 1–7 según la temperatura","Stufen 1–7 nach Temperatur festlegen"}},
            {"이 온도부터 (°C)",new[]{"From temperature (°C)","이 온도부터 (°C)","開始温度 (°C)","起始温度 (°C)","Desde temperatura (°C)","Ab Temperatur (°C)"}},
            {"팬 단계",new[]{"Fan level","팬 단계","ファンレベル","风扇档位","Nivel del ventilador","Lüfterstufe"}},
            {"히스테리시스 °C",new[]{"Hysteresis °C","히스테리시스 °C","ヒステリシス °C","滞回温差 °C","Histéresis °C","Hysterese °C"}},
            {"감속 대기 초",new[]{"Cool-down delay (s)","감속 대기 초","減速待機 (秒)","降速延迟（秒）","Espera al enfriar (s)","Absenkverzögerung (s)"}},
            {"저장 · 커브 모드 적용",new[]{"Save & use curve","저장 · 커브 모드 적용","保存して適用","保存并应用曲线","Guardar y aplicar","Speichern & Kurve anwenden"}},
            {"기본값 · 60°C부터 2단계",new[]{"Reset · level 2 at 60°C","기본값 · 60°C부터 2단계","初期値 · 60°C からレベル 2","重置 · 60°C 起为 2 档","Restablecer · nivel 2 a 60°C","Standard · Stufe 2 ab 60°C"}},
            {"닫기",new[]{"Close","닫기","閉じる","关闭","Cerrar","Schließen"}},
            {"그래프의 점을 끌거나 표의 값을 수정하세요.",new[]{"Drag graph points or edit the table.","그래프의 점을 끌거나 표의 값을 수정하세요.","グラフの点をドラッグするか、表を編集します。","拖动图表上的点或编辑表格。","Arrastra los puntos o edita la tabla.","Punkte ziehen oder die Tabelle bearbeiten."}},
            {"온도와 단계를 확인하세요.",new[]{"Check the temperature and level.","온도와 단계를 확인하세요.","温度とレベルを確認してください。","请检查温度和档位。","Revisa la temperatura y el nivel.","Temperatur und Stufe prüfen."}},
            {"모든 행에 온도와 단계를 입력하세요.",new[]{"Enter a temperature and level in every row.","모든 행에 온도와 단계를 입력하세요.","すべての行に温度とレベルを入力してください。","请在每一行输入温度和档位。","Introduce temperatura y nivel en cada fila.","In jeder Zeile Temperatur und Stufe eingeben."}},
            {"가열 시 즉시 상승 · 냉각 시 히스테리시스와 감속 대기 적용",new[]{"Raise on warming; use hysteresis and delay on cooling.","가열 시 즉시 상승 · 냉각 시 히스테리시스와 감속 대기 적용","温度上昇で増速、冷却時はヒステリシスと待機を適用。","升温时提速，降温时应用滞回和延迟。","Sube al calentarse; histéresis y espera al enfriarse.","Bei Erwärmung erhöhen; beim Abkühlen Hysterese und Verzögerung."}},
            {"저장하고 사용자 커브 모드를 적용했습니다.",new[]{"Saved and applied fan curve mode.","저장하고 사용자 커브 모드를 적용했습니다.","保存してファンカーブを適用しました。","已保存并应用风扇曲线。","Curva guardada y aplicada.","Lüfterkurve gespeichert und angewendet."}},
            {"커브 확인",new[]{"Check fan curve","커브 확인","カーブの確認","检查风扇曲线","Revisar curva","Lüfterkurve prüfen"}},
            {"Invalid mode",new[]{"Invalid mode","유효하지 않은 모드입니다.","無効なモードです。","模式无效。","Modo no válido.","Ungültiger Modus."}},
            {"Level must be 1–7",new[]{"Level must be 1–7","단계는 1–7이어야 합니다.","レベルは 1–7 です。","档位必须为 1–7。","El nivel debe ser 1–7.","Die Stufe muss 1–7 sein."}},
            {"Hysteresis must be 1–10°C",new[]{"Hysteresis must be 1–10°C","히스테리시스는 1–10°C여야 합니다.","ヒステリシスは 1–10°C です。","滞回温差必须为 1–10°C。","La histéresis debe ser 1–10°C.","Die Hysterese muss 1–10°C betragen."}},
            {"Down delay must be 0–120 seconds",new[]{"Down delay must be 0–120 seconds","감속 대기는 0–120초여야 합니다.","減速待機は 0–120 秒です。","降速延迟必须为 0–120 秒。","La espera debe ser 0–120 segundos.","Die Verzögerung muss 0–120 Sekunden betragen."}},
            {"Curve must start at 0°C and have 2–20 points",new[]{"Curve must start at 0°C and have 2–20 points","커브는 0°C에서 시작하고 점이 2–20개여야 합니다.","カーブは 0°C から始まり、点は 2–20 個です。","曲线必须从 0°C 开始，包含 2–20 个点。","La curva debe empezar en 0°C y tener 2–20 puntos.","Die Kurve muss bei 0°C beginnen und 2–20 Punkte haben."}},
            {"Curve temperature 0–89°C; level 1–7",new[]{"Curve temperature 0–89°C; level 1–7","커브 온도는 0–89°C, 단계는 1–7입니다.","温度は 0–89°C、レベルは 1–7 です。","曲线温度为 0–89°C，档位为 1–7。","Temperatura 0–89°C; nivel 1–7.","Kurventemperatur 0–89°C; Stufe 1–7."}},
            {"Temperatures must increase; levels must not decrease",new[]{"Temperatures must increase; levels must not decrease","온도는 증가해야 하며 단계는 감소할 수 없습니다.","温度は昇順、レベルは減少不可です。","温度必须递增，档位不能降低。","La temperatura debe subir y los niveles no deben bajar.","Temperaturen müssen steigen; Stufen dürfen nicht sinken."}}
        };
        public static string Language {get{return language;}}
        public static bool CatalogComplete {get {foreach(var row in words.Values){if(row.Length!=Codes.Length)return false;foreach(var text in row)if(String.IsNullOrWhiteSpace(text))return false;}return true;}}
        public static string T(string source,params object[] args) {
            string[] row;int index=Array.IndexOf(Codes,language);
            string text=words.TryGetValue(source,out row)?row[Math.Max(0,index)]:source;
            return args.Length==0?text:String.Format(text,args);
        }
        public static void Bind(Control control,string source) {
            bindings[control]=source;control.Text=T(source);
            control.Disposed+=delegate {bindings.Remove(control);};
        }
        public static void Set(string code) {
            language=Array.IndexOf(Codes,code)>=0?code:"en";
            foreach(var pair in new List<KeyValuePair<Control,string>>(bindings))if(!pair.Key.IsDisposed)pair.Key.Text=T(pair.Value);
        }
        public static string Description(string mode,int level) {return mode=="Fixed"?T("고정 설명",level):mode=="Curve"?T("커브 설명"):T("자동 설명");}
    }
}
