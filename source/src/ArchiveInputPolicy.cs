using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal enum ArchiveInputChoice { Stop,Skip,Retry }
    internal sealed class ArchiveInputFailure {internal string Path;internal Exception Error;}
    internal sealed class ArchiveAllInputsSkippedException:IOException {internal ArchiveAllInputsSkippedException():base(L.Text("archive.allInputsSkipped")){} }
    internal sealed class ArchiveInputPolicy {
        internal Func<ArchiveInputFailure,ArchiveInputChoice> Decide;
        internal readonly HashSet<string> Skipped=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static bool Retry(string path,ArchivePackOptions options,CancellationToken token,Exception failure){
            var policy=options.Inputs;if(policy==null||policy.Decide==null)throw failure;
            var choice=policy.Decide(new ArchiveInputFailure{Path=path,Error=failure});token.ThrowIfCancellationRequested();
            if(choice==ArchiveInputChoice.Retry)return true;
            if(choice==ArchiveInputChoice.Stop)throw new OperationCanceledException();
            policy.Skipped.Add(System.IO.Path.GetFullPath(path));return false;
        }
        internal static FileStream Open(string path,ArchivePackOptions options,CancellationToken token){
            var policy=options.Inputs;if(policy!=null&&policy.Skipped.Contains(System.IO.Path.GetFullPath(path)))return null;
            while(true){
                token.ThrowIfCancellationRequested();Exception failure;
                try{return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);}
                catch(IOException ex){failure=ex;}catch(UnauthorizedAccessException ex){failure=ex;}
                if(!Retry(path,options,token,failure))return null;
            }
        }
        internal static string Hash(string path,ArchivePackOptions options,CancellationToken token){
            while(true){
                IOException failure;
                using(var input=Open(path,options,token)){
                    if(input==null)return null;
                    try{return FileParts.Hash(input,token);}catch(IOException ex){failure=ex;}
                }
                if(!Retry(path,options,token,failure))return null;
            }
        }
        internal static void Copy(string path,string target,ArchivePackOptions options,CancellationToken token,ArchiveBudget budget,ArchivePreparationReporter preparation){
            while(true){
                IOException failure=null;long copied=0;
                using(var input=Open(path,options,token)){
                    if(input==null)return;
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));budget.Item();preparation.Visit(path);
                    using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                        var buffer=new byte[1048576];
                        while(true){
                            int count;try{count=input.Read(buffer,0,buffer.Length);}catch(IOException ex){failure=ex;break;}
                            if(count==0)break;token.ThrowIfCancellationRequested();budget.Write(count,target,true);output.Write(buffer,0,count);preparation.Copied(count);copied+=count;
                        }
                    }
                }
                if(failure==null){File.SetLastWriteTimeUtc(target,File.GetLastWriteTimeUtc(path));return;}
                // Only source-read errors are interactive; output/budget failures must stop publication.
                File.Delete(target);preparation.Discard(copied);
                if(!Retry(path,options,token,failure))return;
            }
        }
    }
    internal sealed class ArchiveInputDialog:Window {
        internal ArchiveInputChoice Choice=ArchiveInputChoice.Stop;
        internal ArchiveInputDialog(Window owner,ArchiveInputFailure failure){
            CopyUi.Style(this,owner,L.Text("archive.inputUnavailable"),640);
            var layout=new OperationDialogLayout(this,L.Text("archive.inputUnavailable"),L.Text("archive.inputUnavailableHelp"));
            var group=OperationDialogLayout.Group(layout.Body,L.Text("details.file"));
            group.Children.Add(new TextBlock{Text=failure.Path,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
            group.Children.Add(new TextBlock{Text=failure.Error.Message,TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted});
            var stop=CopyUi.Button(L.Text("action.abort"),delegate{Choice=ArchiveInputChoice.Stop;DialogResult=false;});stop.IsCancel=true;layout.Actions.Children.Add(stop);
            layout.Actions.Children.Add(CopyUi.Button(L.Text("action.retry"),delegate{Choice=ArchiveInputChoice.Retry;DialogResult=true;}));
            var skip=CopyUi.Button(L.Text("action.skip"),delegate{Choice=ArchiveInputChoice.Skip;DialogResult=true;});layout.Actions.Children.Add(skip);
        }
    }
}
