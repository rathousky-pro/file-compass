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
            CopyUi.Style(this, owner, move ? L.Text("network.moveTitle") : L.Text("network.copyTitle"), 510);
            var panel = new StackPanel { Margin = new Thickness(10) }; Content = panel;
            panel.Children.Add(new TextBlock { Text = (move ? L.Text("transfer.moveQuestionPrefix") : L.Text("transfer.copyQuestionPrefix")) + (count == 1 ? L.Text("transfer.singleItemQuestion") : count + L.Text("transfer.multipleItemsQuestionSuffix")), FontWeight = FontWeights.Bold, Margin = new Thickness(2, 0, 2, 7) });
            if (move) panel.Children.Add(CopyUi.Label(L.Text("transfer.moveSourceDeletionHelp")));
            AddLocation(panel, "Z", source, sourceProfile); AddLocation(panel, "Do", target, targetProfile);
            panel.Children.Add(new TextBlock { Text = L.Text("transfer.existingFilesLabel"), Margin = new Thickness(2, 6, 2, 2) });
            bool scp = (sourceProfile != null && sourceProfile.Protocol == NetworkProtocol.SCP) || (targetProfile != null && targetProfile.Protocol == NetworkProtocol.SCP) || source.StartsWith("scp://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("scp://", StringComparison.OrdinalIgnoreCase);
            bool relay = sourceProfile != null && targetProfile != null;
            var resume = CopyUi.Button(L.Text("transfer.copyAndResume"), delegate { Rule = NetworkCopyRule.Resume; DialogResult = true; });
            bool cloudUpload=targetProfile!=null&&!String.IsNullOrEmpty(targetProfile.CloudAccountId); if(cloudUpload)resume.Content=L.Text("cloud.restartTransfer"); resume.ToolTip = L.Text(cloudUpload?"cloud.restartHelp":"transfer.resumeVerificationHelp");
            panel.Children.Add(resume);
            if (relay) resume.ToolTip = scp ? L.Text("transfer.scpResumeUnsupportedHelp") : L.Text("transfer.partialResumeChecksHelp");
            panel.Children.Add(new TextBlock { Text = cloudUpload?L.Text("cloud.restartHelp"):relay ? L.Text("transfer.serverRelayBufferHelp") + (scp ? L.Text("transfer.scpRestartHint") : L.Text("transfer.ftpSftpResumeHint")) : scp ? L.Text("transfer.scpSkipCompletedHint") : L.Text("transfer.quickResumeHint"), TextWrapping = TextWrapping.Wrap, Foreground = FluentTheme.Muted, Margin = new Thickness(4, 2, 4, 6) });
            var buttons = new Grid(); buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var skip = CopyUi.Button(L.Text("transfer.copyAndSkip"), delegate { Rule = NetworkCopyRule.Skip; DialogResult = true; }); skip.IsDefault = true; buttons.Children.Add(skip);
            CopyUi.EnterDefault(this, skip);
            var overwrite = CopyUi.Button(L.Text("transfer.copyAndOverwrite"), delegate { Rule = NetworkCopyRule.Overwrite; DialogResult = true; }); Grid.SetColumn(overwrite, 1); buttons.Children.Add(overwrite);
            var cancel = CopyUi.Button(L.Text("action.cancel"), delegate { DialogResult = false; }); cancel.IsCancel = true; Grid.SetColumn(cancel, 2); buttons.Children.Add(cancel); panel.Children.Add(buttons);
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
            body.Children.Add(new TextBlock { Text = heading, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = heading });
            if(profile==null&&!remote)body.Children.Add(new PathTextBlock{FullPath=details,Foreground=FluentTheme.Muted,Margin=new Thickness(0,2,0,0)});else body.Children.Add(new TextBlock { Text = details, Foreground = FluentTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = details, Margin = new Thickness(0, 2, 0, 0) });
            if (path != null) body.Children.Add(new PathTextBlock { Prefix=L.Text("transfer.folderPrefix"),FullPath=path,Margin=new Thickness(0,2,0,0) });
            panel.Children.Add(new Border { Child = body, Background = CompactTheme.ThemeBrush("#E6E6E6"), CornerRadius = new CornerRadius(3), BorderBrush = CompactTheme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(2, 2, 2, 6), Padding = new Thickness(4) });
        }
    }
}

