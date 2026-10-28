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
            AddLocation(panel,L.Text("transfer.sourceLabel").Trim().TrimEnd(':'),source,sourceProfile);AddLocation(panel,L.Text("transfer.targetLabel").Trim().TrimEnd(':'),target,targetProfile);
            var collisions=OperationDialogLayout.Group(panel,L.Text("transfer.existingFilesLabel").TrimEnd(':'));
            bool scp = (sourceProfile != null && sourceProfile.Protocol == NetworkProtocol.SCP) || (targetProfile != null && targetProfile.Protocol == NetworkProtocol.SCP) || source.StartsWith("scp://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("scp://", StringComparison.OrdinalIgnoreCase);
            bool relay = sourceProfile != null && targetProfile != null;
            var resume = CopyUi.Button(L.Text("transfer.copyAndResume"), delegate { Rule = NetworkCopyRule.Resume; DialogResult = true; });
            bool cloudUpload=targetProfile!=null&&!String.IsNullOrEmpty(targetProfile.CloudAccountId); if(cloudUpload)resume.Content=L.Text("cloud.restartTransfer"); resume.ToolTip = L.Text(cloudUpload?"cloud.restartHelp":"transfer.resumeVerificationHelp");
            collisions.Children.Add(resume);
            if (relay) resume.ToolTip = scp ? L.Text("transfer.scpResumeUnsupportedHelp") : L.Text("transfer.partialResumeChecksHelp");
            collisions.Children.Add(new TextBlock { Text = cloudUpload?L.Text("cloud.restartHelp"):relay ? L.Text("transfer.serverRelayBufferHelp") + (scp ? L.Text("transfer.scpRestartHint") : L.Text("transfer.ftpSftpResumeHint")) : scp ? L.Text("transfer.scpSkipCompletedHint") : L.Text("transfer.quickResumeHint"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(4, 2, 4, 6) });
            var explanation=OperationDialogLayout.Group(details,L.Text("transfer.existingFilesLabel").TrimEnd(':'));
            explanation.Children.Add(new TextBlock{Text=cloudUpload?L.Text("cloud.restartHelp"):relay?(scp?L.Text("transfer.scpResumeUnsupportedHelp"):L.Text("transfer.partialResumeChecksHelp")):L.Text("transfer.resumeVerificationHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted});
            var buttons=layout.Actions;
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true;buttons.Children.Add(cancel);
            var overwrite = CopyUi.Button(L.Text("transfer.copyAndOverwrite"), delegate { Rule = NetworkCopyRule.Overwrite; DialogResult = true; });buttons.Children.Add(overwrite);
            var skip = CopyUi.Button(L.Text("transfer.copyAndSkip"), delegate { Rule = NetworkCopyRule.Skip; DialogResult = true; });skip.IsDefault=true;FluentTheme.Primary(skip);buttons.Children.Add(skip);CopyUi.EnterDefault(this,skip);

        }
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
