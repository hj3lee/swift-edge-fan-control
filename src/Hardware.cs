using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace SwiftEdgeFanControl {
    public sealed class DeviceIdentity {
        public string Model="", Board="", Bios="";
        public bool Supported { get { return (Model.Equals("SFE16-43",StringComparison.OrdinalIgnoreCase) || Model.Equals("Swift SFE16-43",StringComparison.OrdinalIgnoreCase))
            && Board.Equals("Ray_PEU",StringComparison.OrdinalIgnoreCase)
            && Bios.Equals("V1.71",StringComparison.OrdinalIgnoreCase); } }
        public static DeviceIdentity Detect() {
            var d=new DeviceIdentity();
            using(var s=new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem"))
                foreach(ManagementObject x in s.Get()) { d.Model=Convert.ToString(x["Model"]).Trim(); x.Dispose(); }
            using(var s=new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard"))
                foreach(ManagementObject x in s.Get()) { d.Board=Convert.ToString(x["Product"]).Trim(); x.Dispose(); }
            using(var s=new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
                foreach(ManagementObject x in s.Get()) { d.Bios=Convert.ToString(x["SMBIOSBIOSVersion"]).Trim(); x.Dispose(); }
            return d;
        }
        public override string ToString() { return Model+" / "+Board+" / "+Bios; }
    }
    public interface IEc : IDisposable { byte Read(byte register); void Write(byte register, byte value); }
    public sealed class PawnEc : IEc {
        [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string p, IntPtr f,uint flags);
        [DllImport("kernel32",CharSet=CharSet.Ansi,SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr h,string name);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Open(out IntPtr h);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Load(IntPtr h, byte[] data, UIntPtr size);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Execute(IntPtr h, [MarshalAs(UnmanagedType.LPStr)]string name,
            ulong[] input,UIntPtr count,ulong[] output,UIntPtr capacity,out UIntPtr actual);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Close(IntPtr h);
        IntPtr handle, library;
        Execute execute; Close close;
        Mutex mutex=new Mutex(false,@"Global\Access_EC");
        readonly Random random=new Random();
        public long ReadAttempts, ReadTimeouts;
        public Action<string> Diagnostic;
        public PawnEc(string modulePath) {
            var id=DeviceIdentity.Detect();
            if(!id.Supported) throw new InvalidOperationException("Unsupported hardware: "+id);
            byte[] blob=File.ReadAllBytes(modulePath);
            using(var sha=SHA256.Create()) {
                string hash=BitConverter.ToString(sha.ComputeHash(blob)).Replace("-","");
                if(hash!="C38FD116E7AFF4D1FDB0A494E296BE0A6708E5A22FC72F14587442FB7F8F7906")
                    throw new InvalidOperationException("EC module hash mismatch");
            }
            string dll=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"PawnIO\PawnIOLib.dll");
            library=LoadLibraryEx(dll,IntPtr.Zero,0x1100);
            if(library==IntPtr.Zero) throw new InvalidOperationException("Install official PawnIO first; cannot load PawnIOLib.dll");
            var open=(Open)Marshal.GetDelegateForFunctionPointer(GetProcAddress(library,"pawnio_open"),typeof(Open));
            var load=(Load)Marshal.GetDelegateForFunctionPointer(GetProcAddress(library,"pawnio_load"),typeof(Load));
            execute=(Execute)Marshal.GetDelegateForFunctionPointer(GetProcAddress(library,"pawnio_execute"),typeof(Execute));
            close=(Close)Marshal.GetDelegateForFunctionPointer(GetProcAddress(library,"pawnio_close"),typeof(Close));
            Check(open(out handle));
            try { Check(load(handle,blob,(UIntPtr)blob.Length)); } catch { close(handle); handle=IntPtr.Zero; throw; }
        }
        static void Check(int hr) { if(hr<0) Marshal.ThrowExceptionForHR(hr); }
        ulong Io(string name, params ulong[] input) {
            var output=new ulong[1]; UIntPtr actual;
            int count=name=="ioctl_pio_read"?1:0;
            Check(execute(handle,name,input,(UIntPtr)input.Length,output,(UIntPtr)count,out actual));
            if(count==1 && actual.ToUInt64()!=1) throw new IOException("Unexpected PawnIO output size");
            return output[0];
        }
        byte Port(int p) { return (byte)Io("ioctl_pio_read",(ulong)p); }
        void Port(int p,int v) { Io("ioctl_pio_write",(ulong)p,(ulong)v); }
        void Wait(int mask,int expected,string phase) {
            var sw=Stopwatch.StartNew(); byte status=0;
            while(sw.ElapsedMilliseconds<250) {
                status=Port(0x66); if((status&mask)==expected) return;
                // EC responses are often much faster than the Windows sleep quantum.
                // Poll briefly without yielding before falling back to low-CPU waiting.
                if(sw.ElapsedMilliseconds<2)Thread.SpinWait(32);else Thread.Sleep(1);
            }
            throw new TimeoutException("EC "+phase+" status=0x"+status.ToString("X2"));
        }
        T Locked<T>(Func<T> action) {
            bool held=false;
            try { try { held=mutex.WaitOne(700); } catch(AbandonedMutexException) { held=true; }
                if(!held) throw new TimeoutException("EC mutex busy"); return action();
            } finally { if(held) mutex.ReleaseMutex(); }
        }
        public byte Read(byte register) {
            for(int attempt=0;;attempt++) {
                ReadAttempts++;
                try { return Locked(delegate {
                    Wait(2,0,"read-command"); Port(0x66,0x80);
                    Wait(2,0,"read-address"); Port(0x62,register);
                    Wait(1,1,"read-data"); return Port(0x62);
                }); } catch(TimeoutException e) {
                    ReadTimeouts++; if(Diagnostic!=null) Diagnostic("read_timeout reg="+register.ToString("X2")+" "+e.Message);
                    if(attempt==2) throw;
                    Thread.Sleep(random.Next(50,121)*(attempt+1));
                }
            }
        }
        public void Write(byte register,byte value) {
            Locked(delegate {
                Wait(2,0,"write-command");Port(0x66,0x81);
                Wait(2,0,"write-address");Port(0x62,register);
                Wait(2,0,"write-data");Port(0x62,value);Wait(2,0,"write-complete");return 0;
            });
        }
        public void Dispose() { if(handle!=IntPtr.Zero) { close(handle);handle=IntPtr.Zero; } mutex.Dispose(); }
    }
}
