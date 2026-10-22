using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace CommanderCZ {
    public partial class FilePanel {
        internal bool HasCloudAccount(string id){return openConnections.Keys.Any(x=>x.Profile.CloudAccountId==id);}
    }
    public partial class MainWindow {
        internal void ShowCloudConnections(){ManageConnections(null,null,true);}
        internal bool CloudInUse(string id){return left.HasCloudAccount(id)||right.HasCloudAccount(id)||networkJobs.Any(x=>!x.Finished&&(x.Profile.CloudAccountId==id||(x.RelayTarget!=null&&x.RelayTarget.CloudAccountId==id)))||durableQueue.Pending().Any(x=>x.SourceProfileId=="cloud-"+id||x.TargetProfileId=="cloud-"+id);}
        internal bool CloudIsActive(string id){return active.IsRemote&&active.Remote.Profile.CloudAccountId==id;} internal async Task ConnectCloud(CloudAccount a){var profile=CloudRuntime.Profile(a);await ConnectNetworkTask(profile);}
    }
    internal sealed class CloudManagerWindow : Window {
        internal Window HostWindow; internal event Action<bool> BusyChanged; Window UiOwner {get{return HostWindow??this;}} internal bool AllowHostClose(){if(cancellation!=null){cancellation.Cancel();return false;}return true;} void CloseManager(){if(HostWindow!=null)HostWindow.Close();else Close();} readonly MainWindow main;
        readonly ListBox list=new ListBox{MinHeight=160};
        readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};
        readonly StackPanel buttons=new StackPanel();
        readonly TextBlock applicationStatus=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,8)};
        readonly Button signInAgain;
        readonly CloudAccounts accounts;
        CancellationTokenSource cancellation;
        readonly System.Collections.Generic.List<Button> selectionButtons=new System.Collections.Generic.List<Button>();
        internal CloudManagerWindow(MainWindow owner,bool managementOnly=false){
            main=owner;accounts=CloudStore.Load();CopyUi.Style(this,owner,L.Text("cloud.title"),680);Height=480;MinWidth=600;MinHeight=420;SizeToContent=SizeToContent.Manual;ResizeMode=ResizeMode.CanResize;
            DialogGeometry.Bound(this,760,500,600,420);
            var body=new DockPanel();Content=new Border{Child=body,Margin=new Thickness(12),Padding=new Thickness(12),Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)};
            var info=new TextBlock{Text=L.Text("cloud.intro"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(info,Dock.Top);body.Children.Add(info);
            DockPanel.SetDock(applicationStatus,Dock.Top);body.Children.Add(applicationStatus);
            DockPanel.SetDock(status,Dock.Bottom);body.Children.Add(status);
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(170)});body.Children.Add(row);
            row.Children.Add(new Border{Child=list,Background=System.Windows.Media.Brushes.White,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Margin=new Thickness(0,0,12,0)});
            Grid.SetColumn(buttons,1);row.Children.Add(buttons);
            var connect=CopyUi.Button(L.Text("cloud.connect"),async delegate {
                var a=list.SelectedItem as CloudAccount;if(a==null)return;
                await Work(async token=>{await Task.Run(()=>CloudRuntime.Ensure(a.Id),token);await main.ConnectCloud(a);});if(main.CloudIsActive(a.Id))CloseManager();
            });connect.IsDefault=!managementOnly;FluentTheme.Primary(connect);selectionButtons.Add(connect);if(!managementOnly)buttons.Children.Add(connect);
            buttons.Children.Add(CopyUi.Button(L.Text("cloud.add"),async delegate{
                var a=NewAccount();if(a==null)return;
                await Work(async token=>{
                    CloudApplicationOAuth.RequireReady(a.Provider);
                    await CloudStore.Initialize(a,token);
                    a.Port=CloudRuntime.FreePort();a.BridgePassword=NetworkSettingsStore.Protect(Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N"));
                    await Task.Run(()=>CloudKeys.Create(a),token);accounts.Accounts.Add(a);CloudStore.Save(accounts);Reload(a.Id);await CloudStore.Authorize(a,AskQuestion,token,OpenBrowser);CloudStore.Save(accounts);status.Text=L.Text(managementOnly?"cloud.signedInSettings":"cloud.signedIn");
                });
            }));
            var signIn=CopyUi.Button(L.Text("cloud.signInAgain"),async delegate{
                var a=list.SelectedItem as CloudAccount;if(a==null)return;if(main!=null&&main.CloudInUse(a.Id)){status.Text=L.Text("cloud.disconnectFirst");return;}
                await Work(async token=>{CloudApplicationOAuth.RequireReady(a.Provider);CloudRuntime.Stop(a.Id);await CloudStore.Reauthorize(a,AskQuestion,token,OpenBrowser);Reload(a.Id);status.Text=L.Text(managementOnly?"cloud.signedInSettings":"cloud.signedIn");});
            });signInAgain=signIn;buttons.Children.Add(signIn);selectionButtons.Add(signIn);
            var rename=CopyUi.Button(L.Text("action.rename"),delegate{
                var a=list.SelectedItem as CloudAccount;if(a==null)return;
                string name=CopyUi.Prompt(UiOwner,L.Text("action.rename"),a.Name);
                if(name==null||String.IsNullOrWhiteSpace(name))return;
                name=name.Trim();if(name.Length>120){MessageBox.Show(UiOwner,L.Text("cloud.nameTooLong"),L.Text("action.rename"));return;}
                string previous=a.Name;try{a.Name=name;CloudStore.Save(accounts);Reload(a.Id);}catch(Exception ex){a.Name=previous;status.Text=ex.Message;}
            });buttons.Children.Add(rename);selectionButtons.Add(rename);
            var remove=CopyUi.Button(L.Text("cloud.remove"),delegate{
                var a=list.SelectedItem as CloudAccount;if(a==null)return;
                if(main!=null&&main.CloudInUse(a.Id)){status.Text=L.Text("cloud.disconnectFirst");return;}
                if(MessageBox.Show(UiOwner,L.Text("cloud.removeConfirm"),L.Text("cloud.title"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                CloudRuntime.Stop(a.Id);
                try{
                    string dir=Path.GetFullPath(CloudStore.AccountPath(a));string root=Path.GetFullPath(CloudStore.Root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                    if(!dir.StartsWith(root,StringComparison.OrdinalIgnoreCase)||(Directory.Exists(dir)&&(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0))throw new IOException(L.Text("cloud.invalidAccount"));
                    CloudStore.RemoveAccountFiles(a);
                    accounts.Accounts.Remove(a);CloudStore.Save(accounts);Reload(null);
                }catch(Exception ex){status.Text=ex.Message;}
            });buttons.Children.Add(remove);selectionButtons.Add(remove);
            var close=CopyUi.Button(L.Text("action.close"),CloseManager);close.IsCancel=true;if(!managementOnly)buttons.Children.Add(close);
            var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{if(cancellation!=null)cancellation.Cancel();else CloseManager();});cancel.Tag="cloud-cancel";cancel.Visibility=Visibility.Collapsed;DockPanel.SetDock(cancel,Dock.Bottom);buttons.Children.Add(cancel);
            list.SelectionChanged+=delegate{UpdateSelectionButtons();};Reload(null);
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(cancellation!=null){cancellation.Cancel();e.Cancel=true;}};
        }
        void Reload(string id){list.ItemsSource=null;list.ItemsSource=accounts.Accounts;list.SelectedItem=accounts.Accounts.FirstOrDefault(x=>x.Id==id)??accounts.Accounts.FirstOrDefault();UpdateSelectionButtons();}
        void UpdateSelectionButtons(){
            foreach(var button in selectionButtons)button.IsEnabled=cancellation==null&&list.SelectedItem is CloudAccount;
            var account=list.SelectedItem as CloudAccount;string error=account==null?null:CloudApplicationOAuth.AvailabilityError(account.Provider);
            if(signInAgain!=null){if(error!=null)signInAgain.IsEnabled=false;signInAgain.ToolTip=error;}
            applicationStatus.Text=account!=null&&account.Provider=="drive"?(error??L.Text("cloud.applicationLogin")):"";
            applicationStatus.Visibility=applicationStatus.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
        }
        async Task Work(Func<CancellationToken,Task> work){
            if(cancellation!=null)return;var source=new CancellationTokenSource();cancellation=source;if(BusyChanged!=null)BusyChanged(true);
            list.IsEnabled=false;foreach(Button b in buttons.Children.OfType<Button>()){bool isCancel=Convert.ToString(b.Tag)=="cloud-cancel";b.IsEnabled=isCancel;if(isCancel)b.Visibility=Visibility.Visible;}
            status.Text=L.Text("cloud.waiting");
            try{await work(source.Token);}catch(OperationCanceledException){status.Text=L.Text("cloud.cancelled");}catch(Exception ex){status.Text=ex.Message;}
            finally{cancellation=null;if(BusyChanged!=null)BusyChanged(false);source.Dispose();list.IsEnabled=true;foreach(Button b in buttons.Children.OfType<Button>()){b.IsEnabled=true;if(Convert.ToString(b.Tag)=="cloud-cancel")b.Visibility=Visibility.Collapsed;}UpdateSelectionButtons();}
        }
        CloudAccount NewAccount(){
            var window=new Window();CopyUi.Style(window,UiOwner,L.Text("cloud.add"),450);
            var body=new StackPanel();window.Content=new Border{Child=body,Margin=new Thickness(12),Padding=new Thickness(14),Background=CompactTheme.PanelSurface,BorderBrush=CompactTheme.ChromeLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)};
            body.Children.Add(CopyUi.Label(L.Text("cloud.service")));var providers=new ComboBox{ItemsSource=new[]{"Google Drive","OneDrive","Dropbox"},SelectedIndex=0};body.Children.Add(providers);
            body.Children.Add(CopyUi.Label(L.Text("common.nameLabel")));var name=new TextBox{Text="Google Drive",Margin=new Thickness(0,5,0,12)};body.Children.Add(name);
            providers.SelectionChanged+=delegate{name.Text=Convert.ToString(providers.SelectedItem);};
            body.Children.Add(new TextBlock{Text=L.Text("cloud.browserHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,12)});
            var availability=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,10)};body.Children.Add(availability);
            var actions=FluentTheme.Actions();body.Children.Add(actions);
            var signInButton=CopyUi.Button(L.Text("cloud.signIn"),delegate{if(!String.IsNullOrWhiteSpace(name.Text)&&name.Text.Trim().Length<=120)window.DialogResult=true;});actions.Children.Add(signInButton);
            Action updateAvailability=delegate{string provider=new[]{"drive","onedrive","dropbox"}[providers.SelectedIndex];string error=CloudApplicationOAuth.AvailabilityError(provider);availability.Text=error??(provider=="drive"?L.Text("cloud.applicationLogin"):"");signInButton.IsEnabled=error==null;};
            providers.SelectionChanged+=delegate{updateAvailability();};updateAvailability();
            var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{window.DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);
            if(window.ShowDialog()!=true)return null;
            return new CloudAccount{Name=name.Text.Trim(),Provider=new[]{"drive","onedrive","dropbox"}[providers.SelectedIndex]};
        }
        void OpenBrowser(string url){
            Dispatcher.BeginInvoke(new Action(delegate{
                try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}
                catch{status.Text=L.Text("cloud.browserFailed");if(cancellation!=null)cancellation.Cancel();}
            }));
        }
        async Task<string> AskQuestion(CloudQuestion question){
            return await Dispatcher.InvokeAsync(new Func<string>(delegate{
                var dialog = new CloudQuestionDialog(UiOwner, question); return dialog.ShowDialog() == true ? dialog.ReadAnswer() : null;
            }));
        }
    }
}
