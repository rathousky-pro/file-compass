using System;
using System.IO;
using System.Linq;
namespace CommanderCZ {
    internal static class QueuePersistenceTests {
        internal static int Run(string root) {
            try {
                using(var queue=new DurableQueue(root)) {
                    var pending=queue.Pending();
                    if(pending.Length!=1||pending[0].Files.Count!=2||pending[0].Files.Count(x=>x.Done)!=1)throw new Exception("Saved plan/receipts did not survive process restart");
                    if(pending[0].Running)throw new Exception("Recovered job incorrectly still running");
                }
                File.WriteAllText(Path.Combine(root,"queue-process-result.txt"),"PASS: separate process restored complete job plan, completed receipts and resumable state");return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"queue-process-result.txt"),"FAIL: "+ex);return 1;}
        }
    }
}
