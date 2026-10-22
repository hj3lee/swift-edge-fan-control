using System;using System.Diagnostics;using System.Reflection;using System.Collections.Generic;using System.Windows.Forms;using System.IO;using SwiftEdgeFanControl;
class ZoomBenchmark {
 static int layouts;static void Observe(Control c){c.Layout+=delegate{layouts++;};foreach(Control child in c.Controls)Observe(child);}
 [STAThread]static void Main(string[] args){try{Run(args);}catch(Exception e){File.WriteAllText(args[0]+".error",e.ToString());Environment.ExitCode=1;}}
 static void Run(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
 var form=new MainForm(true,new AppLog(true),"",false);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);Observe(form);form.Show();Application.DoEvents();
 if(args.Length>1){form.RenderPreview();form.Dispose();return;}
 var zoom=typeof(MainForm).GetMethod("Zoom",BindingFlags.NonPublic|BindingFlags.Instance);
 var toggle=typeof(MainForm).GetMethod("ToggleLog",BindingFlags.NonPublic|BindingFlags.Instance);toggle.Invoke(form,null);Application.DoEvents();
 float[] values={1.15F,1.3F,1.5F,1F};foreach(float v in values){zoom.Invoke(form,new object[]{v});Application.DoEvents();form.Update();}
 var timings=new List<double>();layouts=0;
 for(int i=0;i<20;i++){var sw=Stopwatch.StartNew();zoom.Invoke(form,new object[]{values[i%4]});Application.DoEvents();form.Update();sw.Stop();timings.Add(sw.Elapsed.TotalMilliseconds);}
 timings.Sort();string json="{\"changes\":20,\"median_ms\":"+timings[10].ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+",\"max_ms\":"+timings[19].ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+",\"layout_events\":"+layouts+",\"hardware\":false}";
 File.WriteAllText(args[0],json);typeof(MainForm).GetField("exiting",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,true);form.Close();form.Dispose();}
}
