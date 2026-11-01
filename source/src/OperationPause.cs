using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
namespace FileCompass {
    // Each gate belongs to one operation token; only that operation's isolated children may be held.
    internal static class OperationPause {
        static readonly ConcurrentDictionary<CancellationToken,Ticket> gates=new ConcurrentDictionary<CancellationToken,Ticket>();
        internal sealed class Ticket:IDisposable {
            internal readonly ManualResetEventSlim Ready=new ManualResetEventSlim(true);readonly CancellationToken token;
            internal bool Paused {get{return !Ready.IsSet;}}
            internal Ticket(CancellationToken value){token=value;}
            internal void Toggle(){if(Paused)Ready.Set();else Ready.Reset();}
            public void Dispose(){Ticket removed;gates.TryRemove(token,out removed);Ready.Set();Ready.Dispose();}
        }
        internal static Ticket Create(CancellationToken token){var result=new Ticket(token);if(!gates.TryAdd(token,result))throw new InvalidOperationException("Duplicate pause token");return result;}
        internal static void Wait(CancellationToken token){Ticket gate;if(gates.TryGetValue(token,out gate))gate.Ready.Wait(token);token.ThrowIfCancellationRequested();}
        internal static bool Check(CancellationToken token){try{Wait(token);return true;}catch(OperationCanceledException){return false;}}
        internal static void HoldChild(Process child,CancellationToken token,Stopwatch clock){
            Ticket gate;if(!gates.TryGetValue(token,out gate)||!gate.Paused)return;
            var handles=new List<IntPtr>();var ids=new HashSet<int>();clock.Stop();
            try{
                // The caller created this process and never acquires locks owned by its worker threads.
                for(int pass=0;pass<4;pass++){child.Refresh();bool added=false;if(child.HasExited)break;foreach(ProcessThread thread in child.Threads){using(thread){if(!ids.Add(thread.Id))continue;IntPtr handle=OpenThread(2,false,(uint)thread.Id);if(handle==IntPtr.Zero)continue;if(SuspendThread(handle)==UInt32.MaxValue){CloseHandle(handle);continue;}handles.Add(handle);added=true;}}if(!added)break;}
                gate.Ready.Wait(token);
            }finally{foreach(var handle in handles){ResumeThread(handle);CloseHandle(handle);}clock.Start();}
        }
        [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenThread(uint access,bool inherit,uint thread);
        [DllImport("kernel32.dll",SetLastError=true)]static extern uint SuspendThread(IntPtr handle);
        [DllImport("kernel32.dll",SetLastError=true)]static extern uint ResumeThread(IntPtr handle);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    }
}
