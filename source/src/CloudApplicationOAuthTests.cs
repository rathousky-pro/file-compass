using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ {
    internal static class CloudApplicationOAuthTests {
        internal static int Run(string root) {
            Directory.CreateDirectory(root);var checks=new List<string>();string oldPath=CloudApplicationOAuth.TestPath,oldRoot=CloudStore.TestRoot;Window window=null;
            Action<bool,string> check=(valid,text)=>{if(!valid)throw new Exception(text);checks.Add(text);};
            try {
                FluentTheme.Ensure();CloudApplicationOAuth.TestPath=Path.Combine(root,"publisher-google.json");CloudStore.TestRoot=Path.Combine(root,"accounts");
                check(CloudApplicationOAuth.AvailabilityError("drive")!=null,"Missing publisher registration is reported without asking the user for IDs");
                check(CloudApplicationOAuth.AvailabilityError("onedrive")==null&&CloudApplicationOAuth.AvailabilityError("dropbox")==null,"OneDrive and Dropbox do not require a Google application");
                var account=new CloudAccount{Provider="drive",Name="Google test"};bool rejected=false;
                try{CloudStore.Authorize(account,q=>Task.FromResult(""),CancellationToken.None).GetAwaiter().GetResult();}catch(IOException){rejected=true;}
                check(rejected&&!Directory.Exists(CloudStore.AccountPath(account)),"Missing publisher credentials fails before creating account data");
                string client="12345-filecompasstest.apps.googleusercontent.com",secret="test-publisher-secret";
                var json=new JavaScriptSerializer();File.WriteAllText(CloudApplicationOAuth.TestPath,json.Serialize(new{web=new{client_id=client,client_secret=secret}}));
                check(CloudApplicationOAuth.AvailabilityError("drive")!=null,"Web application credentials are rejected; desktop registration is required");
                File.WriteAllText(CloudApplicationOAuth.TestPath,json.Serialize(new{installed=new{client_id=client,client_secret=secret}}));
                check(CloudApplicationOAuth.LoadGoogle().client_id==client&&CloudApplicationOAuth.AvailabilityError("drive")==null,"Publisher desktop credentials enable Google browser login");
                account.AuthorizationClient=CloudApplicationOAuth.FileCompass;
                CloudStore.Initialize(account,CancellationToken.None).GetAwaiter().GetResult();
                var info=CloudStore.StartInfo(account,new[]{"config","create","remote","drive","config_is_local","false","--non-interactive"});
                check(info.EnvironmentVariables["RCLONE_CONFIG_REMOTE_CLIENT_ID"]==client&&info.EnvironmentVariables["RCLONE_CONFIG_REMOTE_CLIENT_SECRET"]==secret,"OAuth identity is provided to the helper process environment");
                check(!info.Arguments.Contains(client)&&!info.Arguments.Contains(secret),"Publisher credentials are not placed in the parent command line");
                // Local-only wizard step: no browser, no Google account, no token exchange.
                string response=CloudStore.Run(account,new[]{"config","create","remote","drive","config_is_local","false","--non-interactive"},CancellationToken.None,15000).GetAwaiter().GetResult();
                var question=json.Deserialize<CloudQuestion>(response);
                check(question.Option!=null&&question.Option.Name=="config_token","Real rclone uses the configured File Compass identity and skips the retired shared-ID question");
                bool fallbackRejected=false;try{CloudApplicationOAuth.PublisherAnswer(account,new CloudOption{Name="config_shared_client_id"});}catch(IOException){fallbackRejected=true;}
                check(fallbackRejected,"Own application cannot fall back to the retired rclone identity");
                var legacy=new CloudAccount{Provider="drive"};var local=new System.Diagnostics.ProcessStartInfo();CloudApplicationOAuth.Apply(legacy,local);
                check(local.EnvironmentVariables["RCLONE_CONFIG_REMOTE_CLIENT_ID"]==null,"Existing legacy tokens keep their identity until explicit reauthorization");
                foreach(var provider in new[]{"onedrive","dropbox"}){var process=new System.Diagnostics.ProcessStartInfo();CloudApplicationOAuth.Apply(new CloudAccount{Provider=provider,AuthorizationClient=CloudApplicationOAuth.FileCompass},process);check(process.EnvironmentVariables["RCLONE_CONFIG_REMOTE_CLIENT_ID"]==null,provider+" is not affected by Google OAuth configuration");}
                CloudStore.Save(new CloudAccounts{Accounts=new List<CloudAccount>{account}});
                check(CloudStore.Find(account.Id).AuthorizationClient==CloudApplicationOAuth.FileCompass,"Per-account application identity survives storage without duplicating publisher credentials");
                check(!File.ReadAllText(Path.Combine(CloudStore.Root,"accounts.xml")).Contains(secret),"Account list does not contain publisher credentials");
                var manager=new CloudManagerWindow(null);window=manager;manager.Show();manager.UpdateLayout();
                check(AdvancedToolsTests.Visual<TextBlock>(manager).Any(x=>x.IsVisible&&x.Text==L.Text("cloud.applicationLogin")),"Google account management identifies File Compass as the login application");
                NextStageTests.Draw(manager,Path.Combine(root,"own-login-configured.png"));manager.Close();window=null;
                File.Delete(CloudApplicationOAuth.TestPath);
                manager=new CloudManagerWindow(null);window=manager;manager.Show();manager.UpdateLayout();
                check(AdvancedToolsTests.Visual<Button>(manager).Any(x=>Convert.ToString(x.Content)==L.Text("cloud.signInAgain")&&!x.IsEnabled),"Incomplete publisher setup does not offer a misleading reauthorization action");
                NextStageTests.Draw(manager,Path.Combine(root,"own-login-registration-needed.png"));
                File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(root,"result.txt"),String.Join(Environment.NewLine,checks)+"\nFAIL: "+ex.GetType().Name+": "+ex.Message);return 1;}
            finally{if(window!=null)window.Close();CloudApplicationOAuth.TestPath=oldPath;CloudStore.TestRoot=oldRoot;}
        }
    }
}
