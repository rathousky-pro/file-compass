using System;
using System.Windows;
using System.Windows.Controls;

namespace FileCompass {
    internal sealed class SshLoginDialog : Window {
        internal readonly TextBox User = new TextBox { MinHeight=32, VerticalContentAlignment=VerticalAlignment.Center };
        internal readonly CheckBox UseKey = new CheckBox { Margin=new Thickness(0,12,0,0) };
        internal SshLoginDialog(Window owner,NetworkProfile profile) {
            CopyUi.Style(this,owner,L.Text("ssh.terminalTitle"),520);
            StackPanel actions;var root=SettingsUx.Frame(this,L.Text("ssh.loginHeading"),out actions);
            var form=new StackPanel{Margin=new Thickness(20,16,20,20)};root.Children.Add(form);
            form.Children.Add(new TextBlock{Text=profile.Host+":"+profile.Port+" · "+profile.Path,Foreground=FluentTheme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});
            form.Children.Add(CopyUi.Label(L.Text("network.usernameLabel")));User.Text=profile.UserName;form.Children.Add(User);
            UseKey.Content=L.Text("ssh.useProfileKey");UseKey.IsEnabled=!String.IsNullOrWhiteSpace(profile.PrivateKeyPath)||profile.SshAgent;form.Children.Add(UseKey);
            form.Children.Add(new TextBlock{Text=L.Text("ssh.loginHelp"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,12,0,0)});
            var cancel=CopyUi.Button(L.Text("action.cancel"),delegate{DialogResult=false;});cancel.IsCancel=true;actions.Children.Add(cancel);
            var next=CopyUi.Button(L.Text("ssh.loginContinue"),delegate{DialogResult=true;});next.IsDefault=true;FluentTheme.Primary(next);actions.Children.Add(next);
            User.TextChanged+=delegate{next.IsEnabled=!String.IsNullOrWhiteSpace(User.Text)&&User.Text.IndexOfAny(new[]{'\r','\n','\0'})<0;};next.IsEnabled=!String.IsNullOrWhiteSpace(User.Text);
            Loaded+=delegate{User.Focus();User.SelectAll();};
        }
        internal void Apply(NetworkProfile profile) {
            profile.UserName=User.Text.Trim();profile.SshPasswordOnly=UseKey.IsChecked!=true;
            if(profile.SshPasswordOnly){profile.SshAgent=false;profile.PrivateKeyPath="";}
        }
    }
}
