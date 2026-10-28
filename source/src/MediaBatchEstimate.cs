using System;using System.Linq;
namespace FileCompass {
 internal sealed class MediaBatchEstimate {
  readonly double[] weights,progress,started,ended;readonly bool[] terminal,successful;readonly int workers;
  internal MediaBatchEstimate(double[] work,int concurrent){weights=work.Select(x=>x>0&&!Double.IsNaN(x)&&!Double.IsInfinity(x)?x:1).ToArray();progress=new double[work.Length];started=Enumerable.Repeat(-1.0,work.Length).ToArray();ended=new double[work.Length];terminal=new bool[work.Length];successful=new bool[work.Length];workers=Math.Max(1,Math.Min(concurrent,work.Length));}
  internal void Start(int index,double seconds){started[index]=seconds;}
  internal void Update(int index,double fraction){if(!terminal[index]&&!Double.IsNaN(fraction))progress[index]=Math.Max(progress[index],Math.Max(0,Math.Min(1,fraction)));}
  internal void Finish(int index,double seconds,bool success){ended[index]=seconds;terminal[index]=true;successful[index]=success;progress[index]=1;}
  internal double Fraction{get{return weights.Length==0?0:weights.Select((w,i)=>w*progress[i]).Sum()/weights.Sum();}}
  internal bool Finishing{get{return started.Length>0&&Enumerable.Range(0,weights.Length).All(i=>terminal[i]||started[i]>=0&&progress[i]>=.99)&&terminal.Any(x=>!x);}}
  internal static string Time(double seconds){var span=TimeSpan.FromSeconds(Math.Min(315360000,Math.Max(0,Math.Ceiling(seconds))));return ((long)span.TotalHours).ToString("D2")+span.ToString(@"\:mm\:ss");}
  internal double? Remaining(double seconds){
   if(weights.Length==0||terminal.All(x=>x))return 0;if(seconds<3)return null;
   double work=0,busy=0;
   for(int i=0;i<weights.Length;i++){if(started[i]<0)continue;double elapsed=(terminal[i]?ended[i]:seconds)-started[i];if(terminal[i]&&successful[i]&&elapsed>.05){work+=weights[i];busy+=elapsed;}else if(!terminal[i]&&elapsed>=2&&progress[i]>=.02){work+=weights[i]*progress[i];busy+=elapsed;}}
   if(work<=0||busy<=0)return null;double rate=work/busy;var slots=new double[workers];int active=0;
   for(int i=0;i<weights.Length;i++)if(started[i]>=0&&!terminal[i]){double elapsed=seconds-started[i],own=elapsed>=2&&progress[i]>=.02?weights[i]*progress[i]/elapsed:rate;slots[active++]=Math.Max(1,weights[i]*(1-progress[i])/Math.Max(.000001,own));}
   for(int i=0;i<weights.Length;i++)if(started[i]<0&&!terminal[i]){int slot=Array.IndexOf(slots,slots.Min());slots[slot]+=weights[i]/rate;}
   return slots.Max();
  }
 }
}
