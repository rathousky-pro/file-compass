using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommanderCZ.NetworkTransport;

namespace CommanderCZ
{
    internal sealed class RemoteEditDocument : IDisposable
    {
        readonly RemoteConnection template; internal readonly string RemotePath, LocalPath; internal RemoteFileInfo Expected;
        string expectedEdges;
        string LocalEdges(CancellationToken token)
        {
            using (var input = File.OpenRead(LocalPath)) { var parts = new System.Collections.Generic.List<string>(); foreach (long start in new[] { 0L,Math.Max(0,input.Length - 65536) }.Distinct()) { input.Position = start; parts.Add(TransferCheckpoint.Hash(input,Math.Min(65536,input.Length - start),token.ThrowIfCancellationRequested)); } return String.Join(";",parts); }
        }
        internal RemoteEditDocument(RemoteConnection browsing,string remote,string root = null)
        {
            template = browsing.TransferConnection(); RemotePath = RemoteConnection.Normalize(remote);
            root = root ?? System.IO.Path.Combine(AppPaths.ConfigDirectory, "edits");
            string folder = Path.Combine(root,Guid.NewGuid().ToString("N")); TransferCheckpoint.RejectLink(root); Directory.CreateDirectory(folder);
            string name = RemoteConnection.Name(remote); FileOperations.ValidateName(name); LocalPath = Path.Combine(folder,name);
        }
        internal void Download(CancellationToken token)
        {
            using (var connection = template.TransferConnection()) using (token.Register(delegate { connection.Session.Abort(); })) {
                token.ThrowIfCancellationRequested(); connection.OpenTransfer(); Expected = connection.Session.GetFileInfo(RemotePath);
                if (Expected.IsDirectory || Expected.FileType == 'L') throw new IOException(L.Text("remoteEditor.regularFileRequired"));
                connection.Session.GetFiles(RemotePath,LocalPath).Check(); token.ThrowIfCancellationRequested();
                if (!NetworkWork.Same(Expected,connection.Session.GetFileInfo(RemotePath))) throw new IOException(L.Text("remoteEditor.changedDuringDownload"));
                expectedEdges = template.Profile.Protocol == NetworkProtocol.SCP ? null : LocalEdges(token);
            }
        }
        internal void Save(CancellationToken token)
        {
            if (Expected == null) throw new IOException(L.Text("remoteEditor.fileNotLoaded"));
            using (var input = new FileStream(LocalPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            using (var connection = template.TransferConnection()) using (token.Register(delegate { connection.Session.Abort(); })) {
                token.ThrowIfCancellationRequested(); connection.OpenTransfer();
                var before = connection.Session.GetFileInfo(RemotePath);
                if (!NetworkWork.Same(Expected,before)) throw new IOException(L.Text("remoteEditor.serverMetadataChangedPrefix") + LocalPath);
                Action validate = delegate { token.ThrowIfCancellationRequested(); if (expectedEdges != null && connection.Session.EditEdges(RemotePath,Expected.Length) != expectedEdges) throw new IOException(L.Text("remoteEditor.serverContentsChangedPrefix") + LocalPath); };
                validate();
                connection.Session.UploadRelay(input,input.Length,File.GetLastWriteTime(LocalPath),RemotePath,Expected,delegate { token.ThrowIfCancellationRequested(); },validate,delegate { });
                Expected = connection.Session.GetFileInfo(RemotePath);
                expectedEdges = template.Profile.Protocol == NetworkProtocol.SCP ? null : LocalEdges(token);
            }
        }
        public void Dispose() { template.Dispose(); }
    }
    public partial class MainWindow
    {
        async void EditRemoteSelected()
        {
            var entry = active.List.SelectedItem as Entry; if (entry == null || entry.IsDirectory || entry.IsParent) return;
            var browsing = active.Remote; var document = new RemoteEditDocument(browsing,entry.FullPath);
            var window = new Window(); CopyUi.Style(window,this,L.Text("remoteEditor.title"),650); var body = new StackPanel { Margin = new Thickness(12) }; window.Content = body;
            var location = new TextBox { Text = document.LocalPath,IsReadOnly = true,Margin = new Thickness(0,4,0,8) };
            body.Children.Add(FluentThemeSection(browsing.Profile.Name + " · " + entry.FullPath,location));
            var status = CopyUi.Label(L.Text("remoteEditor.downloadingWorkingCopy")); body.Children.Add(status);
            body.Children.Add(CopyUi.Label(L.Text("remoteEditor.workingCopyInstructions")));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal }; body.Children.Add(buttons);
            bool working = true; CancellationTokenSource cancellation = new CancellationTokenSource();
            var open = CopyUi.Button(L.Text("action.openEditor"),delegate { try { FileViewers.Open(window,document.LocalPath,true); } catch (Exception ex) { status.Text = ex.Message; } }); open.IsEnabled = false; buttons.Children.Add(open);
            var save = CopyUi.Button(L.Text("remoteEditor.saveToServer"),async delegate {
                if (working) return; working = true; buttons.IsEnabled = false; networkCopies++; cancellation.Dispose(); cancellation = new CancellationTokenSource(); status.Text = L.Text("remoteEditor.uploadingChanges");
                try { await Task.Run(delegate { document.Save(cancellation.Token); }); status.Text = L.Text("remoteEditor.savedWorkingCopyPrefix") + document.LocalPath; }
                catch (Exception ex) { status.Text = L.Text("remoteEditor.saveFailedPrefix") + ex.Message; }
                finally { working = false; buttons.IsEnabled = true; networkCopies--; }
            }); save.IsEnabled = false; buttons.Children.Add(save);
            buttons.Children.Add(CopyUi.Button(L.Text("action.close"),window.Close));
            body.Children.Add(CopyUi.Button(L.Text("transfer.cancelActiveTransfer"),delegate { cancellation.Cancel(); }));
            window.Closing += delegate(object s,System.ComponentModel.CancelEventArgs e) { if (working) { cancellation.Cancel(); e.Cancel = true; status.Text = L.Text("transfer.cancellingStatus"); } };
            window.Closed += delegate { document.Dispose(); cancellation.Dispose(); }; window.Show(); networkCopies++;
            try { await Task.Run(delegate { document.Download(cancellation.Token); }); open.IsEnabled = save.IsEnabled = true; status.Text = L.Text("remoteEditor.readyWithConflictChecks"); FileViewers.Open(window,document.LocalPath,true); }
            catch (Exception ex) { status.Text = L.Text("remoteEditor.openFailedPrefix") + ex.Message; }
            finally { working = false; networkCopies--; }
        }
        static GroupBox FluentThemeSection(string title,UIElement content) { return new GroupBox { Header = title,Content = content,Padding = new Thickness(8) }; }
    }
}
namespace CommanderCZ.NetworkTransport
{
    public sealed partial class Session
    {
        internal string EditEdges(string path,long size)
        { return String.Join(";",new[] { 0L,Math.Max(0,size - 65536) }.Distinct().Select(start => size == 0 ? TransferCheckpoint.Hash(new MemoryStream(),0,CheckAbort) : RelayRangeHash(path,start,(int)Math.Min(65536,size - start)))); }
    }
}

