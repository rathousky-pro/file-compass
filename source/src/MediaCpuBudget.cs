using System;
namespace FileCompass {
 internal static class MediaCpuBudget {
  // Concurrent files remain a user choice; only internal codec/OpenMP budgets adapt.
  internal static int ImageThreads(int processors,int concurrentFiles,string format){
   int jobs=Math.Max(1,concurrentFiles),budget=Math.Max(1,Math.Max(1,processors)/jobs);
   int cap=jobs>1?2:String.Equals(format,"WebP",StringComparison.OrdinalIgnoreCase)?8:4;
   return Math.Min(cap,budget);
  }
  internal static int AudioThreads(int processors,int concurrentFiles){return Math.Min(4,Math.Max(1,Math.Max(1,processors)/Math.Max(1,concurrentFiles)));}
 }
}
