#if APP_TESTS
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace FileCompass {
    internal static class CopyErrorFlowTests {
        static void Junction(string link,string target){using(var p=Process.Start(new ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+target+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true})){string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(output);}}
        static CopyResult Run(string source,string target,bool move,Func<CopyFailure,CopyErrorChoice> decide,bool perFile=false){using(var c=new CopyControl())return new CopyEngine(new CopyOptions{Target=target,Move=move,Permissions=perFile},c,x=>new CopyDecision(CopyChoice.Overwrite),null,decide).Run(new[]{source});}
        internal static int Run(string root){
            Directory.CreateDirectory(root);var checks=new List<string>();Window dialog=null;
            Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);checks.Add(text);File.WriteAllLines(Path.Combine(root,"progress.txt"),checks);};
            try{
                System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));L.ChangeLanguage("cs");FluentTheme.Ensure();AppearanceTheme.Apply("Light");
                string external=Directory.CreateDirectory(Path.Combine(root,"external")).FullName;File.WriteAllText(Path.Combine(external,"untouched.txt"),"outside");
                string source=Directory.CreateDirectory(Path.Combine(root,"move-source")).FullName;File.WriteAllText(Path.Combine(source,"normal.txt"),"normal");
                string link1=Path.Combine(source,"link-one"),link2=Path.Combine(source,"link-two");Junction(link1,external);Junction(link2,external);int asked=0;
                var result=Run(source,Path.Combine(root,"move-target"),true,f=>{asked++;check(f.Source==link1||f.Source==link2,"scan prompt identifies the exact junction rather than the enclosing folder");return CopyErrorChoice.SkipAll;});
                check(asked==1&&result.Skipped==2&&result.Copied==1&&!result.Cancelled&&result.Errors.Count==0,"Skip all suppresses further scan-error prompts and continues moving healthy files");
                check(Directory.Exists(link1)&&Directory.Exists(link2)&&!File.Exists(Path.Combine(source,"normal.txt"))&&File.ReadAllText(Path.Combine(root,"move-target","move-source","normal.txt"))=="normal","move preserves skipped junctions in source and does not use whole-folder rename");
                check(File.ReadAllText(Path.Combine(external,"untouched.txt"))=="outside"&&!Directory.Exists(Path.Combine(root,"move-target","move-source","link-one")),"copy never follows a skipped junction or modifies its target");
                check(result.Items.Count(x=>x.State==L.Code("state.error"))==2,"skipped preparation failures remain recorded in operation history");

                string retryLink=Path.Combine(root,"retry-link");Junction(retryLink,external);asked=0;
                result=Run(retryLink,Path.Combine(root,"retry-target"),false,f=>{asked++;Directory.Delete(retryLink);Directory.CreateDirectory(retryLink);File.WriteAllText(Path.Combine(retryLink,"new.txt"),"recovered");return CopyErrorChoice.Retry;});
                check(asked==1&&result.Copied==1&&result.Skipped==0&&File.ReadAllText(Path.Combine(root,"retry-target","retry-link","new.txt"))=="recovered","Retry refreshes source metadata and rebuilds the corrected item");

                string cancelled=Path.Combine(root,"cancel-link");Junction(cancelled,external);asked=0;
                result=Run(cancelled,Path.Combine(root,"cancel-target"),true,f=>{asked++;return CopyErrorChoice.Cancel;});
                check(asked==1&&result.Cancelled&&Directory.Exists(cancelled)&&!Directory.Exists(Path.Combine(root,"cancel-target")),"Cancel during preparation leaves the source and destination untouched");

                string lockedFolder=Directory.CreateDirectory(Path.Combine(root,"locked-source")).FullName;string locked=Path.Combine(lockedFolder,"locked.txt");File.WriteAllText(locked,"locked");File.WriteAllText(Path.Combine(lockedFolder,"free.txt"),"free");asked=0;
                using(var handle=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.None))result=Run(lockedFolder,Path.Combine(root,"locked-target"),true,f=>{asked++;return CopyErrorChoice.Skip;},true);
                check(asked==1&&result.Skipped==1&&result.Copied==1&&File.Exists(locked)&&!File.Exists(Path.Combine(lockedFolder,"free.txt")),"per-file folder move keeps the skipped locked file in source and moves healthy siblings");

                string retryFile=Path.Combine(root,"retry-locked.txt");File.WriteAllText(retryFile,"retry");FileStream held=new FileStream(retryFile,FileMode.Open,FileAccess.Read,FileShare.None);asked=0;
                try{result=Run(retryFile,Path.Combine(root,"retry-locked-target"),false,f=>{asked++;held.Dispose();return CopyErrorChoice.Retry;});}finally{held.Dispose();}
                check(asked==1&&result.Copied==1&&File.ReadAllText(Path.Combine(root,"retry-locked-target","retry-locked.txt"))=="retry","Retry copies a file after its exclusive lock is released");

                string dirSource=Directory.CreateDirectory(Path.Combine(root,"dir-source")).FullName;File.WriteAllText(Path.Combine(dirSource,"file.txt"),"folder");string dirTarget=Directory.CreateDirectory(Path.Combine(root,"dir-target")).FullName;string obstruction=Path.Combine(dirTarget,"dir-source");File.WriteAllText(obstruction,"obstruction");asked=0;
                result=Run(dirSource,dirTarget,false,f=>{asked++;File.Delete(obstruction);return CopyErrorChoice.Retry;});
                check(asked==1&&result.Copied==1&&File.ReadAllText(Path.Combine(obstruction,"file.txt"))=="folder","directory-processing errors offer Retry instead of terminating the entire operation");

                var failure=new CopyFailure{Source=locked,Target=Path.Combine(root,"destination","locked.txt"),Error=new IOException(L.Text("copy.reparsePointRejectedPrefix")+locked)};
                dialog=new CopyErrorDialog(null,failure,moving:true){Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false,ShowInTaskbar=false};dialog.Show();dialog.UpdateLayout();
                var buttons=AdvancedToolsTests.Visual<Button>(dialog).Where(x=>x.IsVisible).ToArray();
                check(new[]{"copy.retryAction","action.skip","conflict.skipAll","action.cancelOperation"}.All(k=>buttons.Any(b=>Convert.ToString(b.Content)==L.Text(k))),"new UX exposes Retry, Skip, Skip all and Cancel directly");
                check(buttons.All(b=>b.TranslatePoint(new Point(b.ActualWidth,0),dialog).X<dialog.ActualWidth),"all error-dialog actions fit inside the window");NextStageTests.Draw(dialog,Path.Combine(root,"move-error.png"));dialog.Close();dialog=null;
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(dialog!=null)dialog.Close();}
        }
    }
}
#endif
