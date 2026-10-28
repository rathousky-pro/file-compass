using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal sealed partial class DiskAnalyzerWindow {
        string lastScanParameters,lastAnalysisStatus;
        internal bool PendingParameters{get;private set;}
        string AnalyzerParameters(DiskScanOptions options){return String.Join("\n",ScanRoots.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()))+"|"+options.Signature;}
        void InitializeAnalyzerStart(bool autoStart){
            foreach(var input in new[]{ScanRoots,FilterSize,FilterAge,FilterMasks,FilterExclusions})input.TextChanged+=delegate{AnalyzerParametersChanged();};
            FilterCategory.SelectionChanged+=delegate{AnalyzerParametersChanged();};
            status.Text=L.Text("analyzer.readyToStart");
            if(!autoStart)AnalyzerPages.SelectedIndex=1;
        }
        void AnalyzerParametersChanged(){
            if(cancellation!=null||closed)return;
            if(Result==null){PendingParameters=false;status.Text=L.Text("analyzer.readyToStart");return;}
            try{PendingParameters=lastScanParameters!=AnalyzerParameters(ReadOptions());}catch{PendingParameters=true;}
            status.Text=PendingParameters?L.Text("analyzer.pendingParameters"):(lastAnalysisStatus??status.Text);
        }
    }
}
