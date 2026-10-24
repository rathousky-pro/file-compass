using System;
using System.Collections.Generic;
namespace FileCompass {
    internal sealed class TransferRateEstimate {
        readonly Queue<Tuple<double,long>> history=new Queue<Tuple<double,long>>();
        internal double? Remaining(long bytes,double activeSeconds,long remaining) {
            if(remaining<=0)return 0;
            if(activeSeconds<3||bytes<=0)return null;
            if(history.Count>0&&(activeSeconds<history.Peek().Item1||bytes<history.Peek().Item2))history.Clear();
            if(history.Count==0)history.Enqueue(Tuple.Create(0.0,0L));
            while(history.Count>1&&activeSeconds-history.Peek().Item1>30)history.Dequeue();
            var start=history.Peek();double rate=bytes/activeSeconds;
            if(activeSeconds-start.Item1>=3)rate=Math.Min(rate,Math.Max(0,bytes-start.Item2)/(activeSeconds-start.Item1));
            if(history.Count==0||activeSeconds-history.ToArray()[history.Count-1].Item1>=1)history.Enqueue(Tuple.Create(activeSeconds,bytes));
            if(rate<=0)return null;
            double seconds=remaining/rate;return seconds>=10?Math.Ceiling(seconds/5)*5:Math.Ceiling(seconds);
        }
    }
}
