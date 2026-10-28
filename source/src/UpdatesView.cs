using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace FileCompass {
 internal sealed class UpdateRow {
    public string Id{get;set;}public string Name{get;set;}public string Installed{get;set;}public string Available{get;set;}public string Status{get;set;}
    internal VendorUpdate Package;
 }
 internal sealed class UpdatesView : StackPanel,IDisposable {
    readonly UpdateManager manager;
    internal readonly DataGrid Items=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,CanUserResizeColumns=false,MinColumnWidth=0,HeadersVisibility=DataGridHeadersVisibility.Column,SelectionMode=DataGridSelectionMode.Single,Height=286,Margin=new Thickness(0,12,0,12),RowHeight=36};
    readonly TextBlock feedback=Text(),details=Text(),source=Text(),location=Text();
    readonly Button check,download,apply,rollback,vendor;
    readonly CheckBox daily=new CheckBox{Content=L.Text("updates.daily"),Margin=new Thickness(0,12,0,0)};
    readonly MainWindow main;
    internal UpdatesView(MainWindow owner){
        main=owner;manager=owner==null?null:owner.Updates;
        var actions=new DockPanel();check=CopyUi.Button(L.Text("updates.check"),async delegate{if(manager!=null){if(manager.Checking)manager.CancelCheck();else await manager.Check();}});DockPanel.SetDock(check,Dock.Right);actions.Children.Add(check);actions.Children.Add(feedback);Children.Add(actions);
        foreach(var definition in new[]{new[]{"updates.component","Name","1.1"},new[]{"updates.current","Installed",".7"},new[]{"updates.available","Available",".7"},new[]{"common.status","Status","1.6"}})Items.Columns.Add(new DataGridTextColumn{Header=L.Text(definition[0]),Binding=new Binding(definition[1]),Width=new DataGridLength(Double.Parse(definition[2],System.Globalization.CultureInfo.InvariantCulture),DataGridLengthUnitType.Star)});
        var cell=new Style(typeof(TextBlock));cell.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));cell.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Text"){RelativeSource=RelativeSource.Self}));foreach(DataGridTextColumn column in Items.Columns)column.ElementStyle=cell;
        Items.SizeChanged+=delegate{double width=Math.Max(200,Items.ActualWidth-SystemParameters.VerticalScrollBarWidth-12);double[] shares={.24,.20,.14,.42};for(int i=0;i<4;i++)Items.Columns[i].Width=Math.Floor(width*shares[i]);};
        Items.SelectionChanged+=delegate{Selection();};Children.Add(Items);
        var body=new StackPanel();body.Children.Add(details);body.Children.Add(source);body.Children.Add(location);
        var buttons=new WrapPanel{Margin=new Thickness(0,12,0,0)};body.Children.Add(buttons);
        download=CopyUi.Button(L.Text("updates.download"),delegate{var row=Items.SelectedItem as UpdateRow;if(row!=null&&manager!=null){manager.Download(row.Package);Render();}});FluentTheme.Primary(download);buttons.Children.Add(download);
        apply=CopyUi.Button(L.Text("updates.apply"),delegate{if(manager!=null)manager.ApplyPending();});buttons.Children.Add(apply);
        rollback=CopyUi.Button(L.Text("updates.rollback"),delegate{var row=Items.SelectedItem as UpdateRow;if(row!=null&&manager!=null)manager.Rollback(row.Id);});buttons.Children.Add(rollback);
        vendor=CopyUi.Button(L.Text("updates.vendor"),delegate{var row=Items.SelectedItem as UpdateRow;if(row!=null&&(VendorUpdates.Ids.Contains(row.Id)||row.Id=="imagemagick"))Process.Start(new ProcessStartInfo(row.Id=="imagemagick"?"https://github.com/dlemstra/Magick.NET":VendorUpdates.Home(row.Id)){UseShellExecute=true});});buttons.Children.Add(vendor);
        Children.Add(new Border{Child=body,Padding=new Thickness(12),CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(1),BorderBrush=CompactTheme.Line,Background=CompactTheme.PanelSurface});
        Children.Add(new TextBlock{Text=L.Text("updates.timing"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,12,0,0)});
        daily.IsChecked=UpdateRegistry.State().Daily;Children.Add(daily);
        Children.Add(new TextBlock{Text=L.Text("updates.immediate"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,8,0,0)});
        if(manager!=null)manager.Changed+=Render;Render();
        Loaded+=async delegate{if(manager!=null)await manager.Inventory();};
    }
    static TextBlock Text(){return new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,3,0,3)};}
    internal void SavePreferences(){if(String.IsNullOrEmpty(UpdateRegistry.StateError))UpdateRegistry.Edit(x=>x.Daily=daily.IsChecked==true);}
    internal void Render(){
        string selected=(Items.SelectedItem as UpdateRow)?.Id;
        var state=UpdateRegistry.State();var rows=new List<UpdateRow>();
        foreach(string id in VendorUpdates.Ids){var item=state.Available.FirstOrDefault(x=>x.Id==id);string current=VendorUpdates.InstalledVersion(id);var pending=state.Pending.FirstOrDefault(x=>x.Id==id);string status=L.Text("updates.notChecked");
            if(item!=null){if(!String.IsNullOrEmpty(item.Error))status=L.Text("updates.unverified");else{Version installed,available;status=Version.TryParse(current,out installed)&&Version.TryParse(item.Version,out available)&&installed>=available?L.Text("updates.currentState"):L.Text("updates.newVersion");}}
            if(pending!=null)status=L.Text(UpdateRegistry.RequiresRestart(id)?"updates.restartRequired":"updates.waiting");
            if(state.RestoreBundled.Contains(id))status=L.Text("updates.restartRequired");
            rows.Add(new UpdateRow{Id=id,Name=VendorUpdates.Name(id),Installed=current,Available=item?.Version??"—",Status=status,Package=item});
        }
        rows.Add(new UpdateRow{Id="imagemagick",Name="Magick.NET / OpenMP",Installed=typeof(ImageMagick.MagickNET).Assembly.GetName().Version.ToString(3),Available="—",Status=L.Text("updates.withApp")});
        rows.Add(new UpdateRow{Id="libraries",Name=L.Text("updates.libraries"),Installed=L.Text("updates.withApp"),Available="—",Status=L.Text("updates.withApp")});
        rows.Add(new UpdateRow{Id="app",Name="File Compass",Installed=BuildInfo.DisplayVersion+" · "+BuildInfo.Build,Available="—",Status=L.Text("updates.appNotConfigured")});
        Items.ItemsSource=rows;Items.SelectedItem=rows.FirstOrDefault(x=>x.Id==selected)??rows[0];
        check.IsEnabled=manager!=null;check.Content=L.Text(manager!=null&&manager.Checking?"action.cancel":"updates.check");
        feedback.Text=manager!=null&&!String.IsNullOrEmpty(manager.Feedback)?manager.Feedback:state.LastSuccessUtc==DateTime.MinValue?L.Text("updates.notChecked"):L.Text("updates.lastCheck")+state.LastSuccessUtc.ToLocalTime().ToString("g");Selection();
        if(!String.IsNullOrEmpty(UpdateRegistry.StateError))feedback.Text=L.Text("updates.failed")+UpdateRegistry.StateError;daily.IsEnabled=String.IsNullOrEmpty(UpdateRegistry.StateError);
    }
    void Selection(){
        if(download==null)return;var row=Items.SelectedItem as UpdateRow;if(row==null)return;
        var state=UpdateRegistry.State();bool supported=VendorUpdates.Ids.Contains(row.Id);
        details.Text=row.Id=="app"?L.Text("updates.appHelp"):row.Id=="imagemagick"?L.Text("updates.imageMagickHelp"):row.Id=="libraries"?L.Text("updates.librariesHelp"):!String.IsNullOrEmpty(row.Package?.Error)?row.Package.Error:row.Id=="ffmpeg"?L.Text("updates.ffmpegSource"):L.Text("updates.packageHelp");
        source.Text=row.Id=="imagemagick"?L.Text("updates.source")+"https://github.com/dlemstra/Magick.NET":supported?L.Text("updates.source")+(row.Package?.Source??VendorUpdates.Home(row.Id)):"";
        location.Text=row.Id=="imagemagick"?L.Text("updates.location")+typeof(ImageMagick.MagickNET).Assembly.Location:supported?L.Text("updates.location")+UpdateRegistry.Resolve(VendorUpdates.Files(row.Id)[0]):"";
        details.ToolTip=row.Package?.Notes;
        download.IsEnabled=supported&&manager!=null&&manager.CanDownload(row.Package)&&row.Status!=L.Text("updates.currentState")&&!state.Pending.Any(x=>x.Id==row.Id);
        apply.IsEnabled=supported&&manager!=null&&!main.UpdateWorkRunning&&state.Pending.Any(x=>x.Id==row.Id)&&!UpdateRegistry.RequiresRestart(row.Id);
        rollback.IsEnabled=supported&&manager!=null&&!main.UpdateWorkRunning&&state.Active.Any(x=>x.Id==row.Id)&&!state.RestoreBundled.Contains(row.Id);
        vendor.IsEnabled=supported||row.Id=="imagemagick";
    }
    public void Dispose(){if(manager!=null)manager.Changed-=Render;}
 }
}
