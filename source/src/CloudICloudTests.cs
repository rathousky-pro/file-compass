using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
namespace FileCompass {
    internal static class CloudICloudTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();Window dialog=null;string language=L.Language;string packs=AppPaths.Resource("languages");
            Action<bool,string> check=(valid,label)=>{if(!valid)throw new Exception(label);checks.Add(label);};
            try {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));FluentTheme.Ensure();
                check(CloudStore.ProviderName(CloudICloud.Provider)=="iCloud Drive"&&CloudApplicationOAuth.AvailabilityError(CloudICloud.Provider)==null,"iCloud uses its own provider without Google publisher credentials");
                int asked=0;Dictionary<string,string> captured=null;string payload=null;
                var task=CloudICloud.Begin(new CloudAccount{Provider=CloudICloud.Provider},q=>{asked++;check(q.Provider==CloudICloud.Provider&&q.Option.Required,"Apple credential prompt is identified and required");if(q.Option.Name=="password")check(q.Option.IsPassword,"Apple password is marked secret");return Task.FromResult(q.Option.Name=="apple_id"?" person@example.test ":"not-a-real-apple-password");},CancellationToken.None,e=>{captured=e;payload=e["RCLONE_JSON"];return Task.FromResult("{\"State\":\"2fa_do\",\"Option\":{\"Name\":\"config_2fa\"}}");});
                NavigationTests.Wait(task);check(asked==2&&task.Result.Contains("2fa_do"),"Apple credentials lead into the backend two-factor state machine");
                var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(payload);var parameters=(Dictionary<string,object>)json["parameters"];var opt=(Dictionary<string,object>)json["opt"];
                check(Convert.ToString(json["type"])=="iclouddrive"&&Convert.ToString(parameters["service"])=="drive","Only iCloud Drive is configured, never a writable Photos account");
                check(Convert.ToString(parameters["apple_id"])=="person@example.test"&&(bool)opt["nonInteractive"]&&(bool)opt["obscure"],"Backend receives normalized account ID and explicit password obscuring");
                check(captured.Count==0,"Short-lived child environment is cleared after authorization step");
                foreach(bool cancelPassword in new[]{false,true}) {
                    int calls=0,runs=0;bool cancelled=false;
                    try{CloudICloud.Begin(new CloudAccount(),q=>{calls++;return Task.FromResult(cancelPassword&&calls==1?"person@example.test":null);},CancellationToken.None,e=>{runs++;return Task.FromResult("{}");}).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
                    check(cancelled&&runs==0&&calls==(cancelPassword?2:1),"Cancellation at credential prompt never starts backend authentication");
                }
                using(var cancellation=new CancellationTokenSource()){int calls=0;bool cancelled=false;try{CloudICloud.Begin(new CloudAccount(),q=>{calls++;cancellation.Cancel();return Task.FromResult("person@example.test");},cancellation.Token,e=>Task.FromResult("{}")).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}check(cancelled&&calls==1,"Cancellation between credential steps prevents a password prompt");}
                string oldRoot=CloudStore.TestRoot,oldOAuth=CloudApplicationOAuth.TestPath;
                try {
                    CloudStore.TestRoot=Path.Combine(root,"empty-accounts");CloudApplicationOAuth.TestPath=Path.Combine(root,"missing-google.json");
                    var manager=new CloudManagerWindow(null){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};dialog=manager;manager.Show();
                    Exception pickerError=null;bool selected=false;var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
                    timer.Tick+=delegate{timer.Stop();var picker=Application.Current.Windows.Cast<Window>().FirstOrDefault(w=>w!=manager&&w.Title==L.Text("cloud.add"));if(picker==null){pickerError=new Exception("Account picker not shown");manager.Close();return;}try{var providers=AdvancedToolsTests.Visual<ComboBox>(picker).Single();check(providers.Items.Cast<object>().Select(Convert.ToString).Contains("iCloud Drive"),"Account picker offers iCloud Drive alongside existing providers");providers.SelectedItem="iCloud Drive";picker.UpdateLayout();check(AdvancedToolsTests.Visual<Button>(picker).Any(b=>Convert.ToString(b.Content)==L.Text("cloud.icloudSignIn")&&b.IsEnabled),"iCloud uses native sign-in and remains enabled without Google credentials");check(AdvancedToolsTests.Visual<TextBlock>(picker).Any(t=>t.Text==L.Text("cloud.icloudHelp")),"Selecting iCloud replaces browser-only instructions");selected=true;}catch(Exception ex){pickerError=ex;}finally{picker.DialogResult=false;}};
                    timer.Start();try{typeof(CloudManagerWindow).GetMethod("NewAccount",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(manager,null);}finally{timer.Stop();manager.Close();dialog=null;}
                    if(pickerError!=null)throw pickerError;check(selected&&!File.Exists(Path.Combine(CloudStore.Root,"accounts.xml")),"Cancelling account picker leaves cloud accounts untouched");
                }finally{CloudStore.TestRoot=oldRoot;CloudApplicationOAuth.TestPath=oldOAuth;}
                foreach(string lang in new[]{"cs","en","de","es","fr","nl","pl","pt"}) {
                    L.LoadForTests(packs,lang);
                    foreach(string field in new[]{"apple_id","password","config_2fa","config_2fa_sms","config_2fa_phone"}) {
                        var question=new CloudQuestion{Option=new CloudOption{Name=field,Required=true,IsPassword=field=="password"}};CloudICloud.PrepareQuestion(question);
                        var window=new CloudQuestionDialog(null,question){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};dialog=window;window.Show();window.UpdateLayout();
                        check(window.Title=="iCloud Drive"&&!AdvancedToolsTests.Visual<TextBlock>(window).Any(x=>x.IsVisible&&x.Text==L.Text("cloud.icloudHelp")),lang+": iCloud sign-in shows only the current step without the full-flow explanation: "+field);
                        if(field=="apple_id"){var input=AdvancedToolsTests.Visual<TextBox>(window).First();input.Text=" person@example.test ";check(window.ReadAnswer()=="person@example.test",lang+": Apple account input trims surrounding spaces");}
                        if(field=="password"||field=="config_2fa"||field=="config_2fa_sms"){check(AdvancedToolsTests.Visual<PasswordBox>(window).Count()==1&&window.ReadAnswer()==null,lang+": secret step is masked and rejects empty input");}
                        if(lang=="cs")NextStageTests.Draw(window,Path.Combine(root,"icloud-"+field+".png"));window.Close();dialog=null;
                    }
                }
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join("\n",checks)+"\nFAIL: "+ex);return 1;}
            finally{if(dialog!=null)dialog.Close();L.LoadForTests(packs,language);}
        }
    }
}
