using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass
{
    public sealed class NetworkCopyDialog : Window
    {
        public NetworkCopyRule Rule;
        public NetworkCopyDialog(Window owner, int count, string source, string target, NetworkProfile sourceProfile = null, NetworkProfile targetProfile = null, bool move = false)
        {
            CopyUi.Style(this, owner, move ? L.Text("network.moveTitle") : L.Text("network.copyTitle"), 780);
            var summary=(move ? L.Text("transfer.moveQuestionPrefix") : L.Text("transfer.copyQuestionPrefix")) + (count == 1 ? L.Text("transfer.singleItemQuestion") : count + L.Text("transfer.multipleItemsQuestionSuffix"));
            if(move)summary+=" · "+L.Text("transfer.moveSourceDeletionHelp");
            var layout=new OperationDialogLayout(this,L.Text(move?"network.moveTitle":"network.copyTitle"),summary);
            var panel=layout.AddPage(L.Text("operation.basic"));var details=layout.AddPage(L.Text("transfer.progressDetails"));
            panel.Children.Add(new TextBlock{Text=summary,Foreground=FluentTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
            AddLocation(panel,L.Text("transfer.sourceLabel").Trim().TrimEnd(':'),source,sourceProfile);AddLocation(panel,L.Text("transfer.targetLabel").Trim().TrimEnd(':'),target,targetProfile);
            var collisions=OperationDialogLayout.Group(panel,L.Text("transfer.existingFilesLabel").TrimEnd(':'));
            bool scp=(sourceProfile!=null&&sourceProfile.Protocol==NetworkProtocol.SCP)||(targetProfile!=null&&targetProfile.Protocol==NetworkProtocol.SCP)||source.StartsWith("scp://",StringComparison.OrdinalIgnoreCase)||target.StartsWith("scp://",StringComparison.OrdinalIgnoreCase);
            bool relay=sourceProfile!=null&&targetProfile!=null,cloudUpload=targetProfile!=null&&!String.IsNullOrEmpty(targetProfile.CloudAccountId);
            string resumeHint=cloudUpload?L.Text("cloud.restartHelp"):relay?(scp?L.Text("transfer.scpResumeUnsupportedHelp"):L.Text("transfer.partialResumeChecksHelp")):scp?L.Text("transfer.scpSkipCompletedHint"):L.Text("transfer.quickResumeHint");
            RulePicker.Items.Add(new ComboBoxItem{Content=L.Text("action.overwrite"),Tag=NetworkCopyRule.Overwrite});RulePicker.Items.Add(new ComboBoxItem{Content=L.Text("action.skip"),Tag=NetworkCopyRule.Skip});RulePicker.Items.Add(new ComboBoxItem{Content=L.Text(cloudUpload?"cloud.restartTransfer":"action.resumeTransfer"),Tag=NetworkCopyRule.Resume});
            RulePicker.MinHeight=32;RulePicker.HorizontalAlignment=HorizontalAlignment.Stretch;System.Windows.Automation.AutomationProperties.SetName(RulePicker,L.Text("transfer.existingFilesLabel"));collisions.Children.Add(RulePicker);
            var hint=new TextBlock{Text=resumeHint,TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed};collisions.Children.Add(hint);RulePicker.SelectionChanged+=delegate{var selected=RulePicker.SelectedItem as ComboBoxItem;if(selected!=null)Rule=(NetworkCopyRule)selected.Tag;hint.Visibility=Rule==NetworkCopyRule.Resume?Visibility.Visible:Visibility.Collapsed;layout.Refit();};SelectRule(NetworkCopyRule.Skip);
            var explanation=OperationDialogLayout.Group(details,L.Text("transfer.existingFilesLabel").TrimEnd(':'));
            explanation.Children.Add(new TextBlock{Text=cloudUpload?L.Text("cloud.restartHelp"):relay?(scp?L.Text("transfer.scpResumeUnsupportedHelp"):L.Text("transfer.partialResumeChecksHelp")):L.Text("transfer.resumeVerificationHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted});
            var buttons=layout.Actions;var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{DialogResult=false;});cancel.IsCancel=true;buttons.Children.Add(cancel);
            Start=CopyUi.Button(L.Text(move?"action.move":"action.copy"),delegate{DialogResult=true;});Start.IsDefault=true;FluentTheme.Primary(Start);buttons.Children.Add(Start);CopyUi.EnterDefault(this,Start);
        }
        internal readonly ComboBox RulePicker=new ComboBox();
        internal readonly Button Start;
        internal void SelectRule(NetworkCopyRule rule){foreach(ComboBoxItem item in RulePicker.Items)if((NetworkCopyRule)item.Tag==rule){RulePicker.SelectedItem=item;return;}}
        static void AddLocation(StackPanel panel, string direction, string location, NetworkProfile profile)
        {
            Uri uri;
            bool remote = Uri.TryCreate(location, UriKind.Absolute, out uri) && !uri.IsFile;
            string heading = direction + ": ", details = location, path = null;
            if(profile!=null&&!String.IsNullOrEmpty(profile.CloudAccountId)){heading+=profile.DisplayLabel;} else if (profile != null || remote)
            {
                heading += (profile == null ? uri.Scheme.ToUpperInvariant() : profile.Protocol.ToString()) + " · " + (profile == null ? uri.Host : profile.Name);
                details = profile == null ? uri.Authority : (String.IsNullOrWhiteSpace(profile.UserName) ? "" : profile.UserName + "@") + profile.Host + ":" + profile.Port;
                path = remote ? Uri.UnescapeDataString(uri.AbsolutePath) : location;
            }
            else heading += L.Text("transfer.localFolderLabel");
            var body = new StackPanel { Margin = new Thickness(6, 4, 6, 4) };
            if(profile==null&&!remote)body.Children.Add(new PathTextBlock{FullPath=details,Foreground=FluentTheme.Muted,Margin=new Thickness(0,2,0,0)});else body.Children.Add(new TextBlock { Text = details, Foreground = FluentTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = details, Margin = new Thickness(0, 2, 0, 0) });
            if (path != null) body.Children.Add(new PathTextBlock { Prefix=L.Text("transfer.folderPrefix"),FullPath=path,Margin=new Thickness(0,2,0,0) });
            panel.Children.Add(new GroupBox { Header=heading,Content=body,Margin=new Thickness(0,0,0,12),Padding=new Thickness(8) });
        }
    }
}
