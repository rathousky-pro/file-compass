using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace FileCompass {
    internal static class CloudICloud {
        internal const string Provider="iclouddrive";
        internal static async Task<string> Begin(CloudAccount account,Func<CloudQuestion,Task<string>> ask,CancellationToken token,Func<Dictionary<string,string>,Task<string>> run=null) {
            token.ThrowIfCancellationRequested();
            string apple=await ask(new CloudQuestion{Provider=Provider,Option=new CloudOption{Name="apple_id",Required=true}}).ConfigureAwait(false);
            if(String.IsNullOrWhiteSpace(apple))throw new OperationCanceledException();token.ThrowIfCancellationRequested();
            string password=await ask(new CloudQuestion{Provider=Provider,Option=new CloudOption{Name="password",Required=true,IsPassword=true}}).ConfigureAwait(false);
            if(String.IsNullOrEmpty(password))throw new OperationCanceledException();token.ThrowIfCancellationRequested();
            // Pass credentials through a short-lived child environment, never command arguments.
            // config/create writes them directly into the account's encrypted configuration.
            var environment=CreateEnvironment(apple.Trim(),password);password=null;
            try {
                if(run!=null)return await run(environment).ConfigureAwait(false);
                return await CloudStore.Run(account,new[]{"rc","--loopback","config/create"},token,180000,environment).ConfigureAwait(false);
            }finally{environment.Clear();}
        }
        internal static Dictionary<string,string> CreateEnvironment(string apple,string password) {
            string json=new JavaScriptSerializer().Serialize(new{name="remote",type=Provider,parameters=new{apple_id=apple,password=password,service="drive"},opt=new{nonInteractive=true,obscure=true}});
            return new Dictionary<string,string>{{"RCLONE_JSON",json}};
        }
        internal static void PrepareQuestion(CloudQuestion question) {
            question.Provider=Provider;
            if(question.Option!=null&&(question.Option.Name=="config_2fa"||question.Option.Name=="config_2fa_sms")){question.Option.Required=true;question.Option.IsPassword=true;}
        }
    }
}
