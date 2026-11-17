using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
namespace FileCompass {
 internal sealed class UpdateRow {
    public string Id{get;set;}public string Name{get;set;}public string Installed{get;set;}public string Available{get;set;}public string InstalledBuild{get;set;}="";public string AvailableBuild{get;set;}="";public string Status{get;set;}
    internal VendorUpdate Package;
 }
 internal sealed class UpdatesView : StackPanel,IDisposable {
    readonly UpdateManager manager;
    internal readonly DataGrid Items=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,CanUserResizeColumns=false,MinColumnWidth=0,HeadersVisibility=DataGridHeadersVisibility.Column,SelectionMode=DataGridSelectionMode.Single,Height=286,Margin=new Thickness(0,12,0,12),RowHeight=36};
    internal readonly DataGrid AppItems=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,CanUserResizeColumns=false,MinColumnWidth=0,HeadersVisibility=DataGridHeadersVisibility.Column,SelectionMode=DataGridSelectionMode.Single};
    bool rendering;
    UpdateRow SelectedRow { get { return (AppItems.SelectedItem as UpdateRow) ?? (Items.SelectedItem as UpdateRow); } }
    readonly TextBlock feedback=Text(),details=Text(),source=Text(),location=Text();
    readonly Button check,download,apply,rollback,vendor;
    readonly CheckBox daily=new CheckBox{Content=L.Text("updates.daily"),Margin=new Thickness(0,12,0,0)};
    readonly MainWindow main;
    internal UpdatesView(MainWindow owner){
        main=owner;manager=owner==null?null:owner.Updates;
        var actions=new DockPanel();check=CopyUi.Button(L.Text("updates.check"),async delegate{if(manager!=null){if(manager.Checking)manager.CancelCheck();else await manager.Check();}});DockPanel.SetDock(check,Dock.Right);actions.Children.Add(check);actions.Children.Add(feedback);Children.Add(actions);
        ConfigureGrid(AppItems,90);ConfigureGrid(Items,160);
        AppItems.SelectionChanged+=delegate{if(!rendering&&AppItems.SelectedItem!=null){Items.SelectedItem=null;Selection();}};
        Items.SelectionChanged+=delegate{if(!rendering&&Items.SelectedItem!=null){AppItems.SelectedItem=null;Selection();}};
        var body=new StackPanel();body.Children.Add(details);body.Children.Add(source);body.Children.Add(location);
        var buttons=new WrapPanel{Margin=new Thickness(0,12,0,0)};body.Children.Add(buttons);
        download=CopyUi.Button(L.Text("updates.download"),delegate{var row=SelectedRow;if(row!=null&&manager!=null){manager.Download(row.Package);Render();}});FluentTheme.Primary(download);buttons.Children.Add(download);
        apply=CopyUi.Button(L.Text("updates.appInstall"),async delegate{if(manager!=null&&SelectedRow?.Id=="app")await manager.InstallApplication();});buttons.Children.Add(apply);
        rollback=CopyUi.Button(L.Text("updates.rollback"),delegate{var row=SelectedRow;if(row!=null&&manager!=null)manager.Rollback(row.Id);});buttons.Children.Add(rollback);
        vendor=CopyUi.Button(L.Text("updates.vendor"),delegate{var row=SelectedRow;if(row!=null&&(VendorUpdates.Ids.Contains(row.Id)||row.Id=="app"))Process.Start(new ProcessStartInfo(row.Id=="app"?ApplicationUpdates.Home:VendorUpdates.Home(row.Id)){UseShellExecute=true});});buttons.Children.Add(vendor);
        Children.Add(new Border{Child=body,Padding=new Thickness(12),CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(1),BorderBrush=CompactTheme.Line,Background=CompactTheme.PanelSurface});
        Children.Add(new TextBlock{Text=L.Text("updates.timing"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,12,0,0)});
        daily.IsChecked=UpdateRegistry.State().Daily;Children.Add(daily);
        Children.Add(new TextBlock{Text=L.Text("updates.immediate"),TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,8,0,0)});
        body.Children.Remove(buttons);body.Children.Remove(details);Children.Clear();

        details.TextWrapping=TextWrapping.NoWrap;details.TextTrimming=TextTrimming.CharacterEllipsis;details.Margin=new Thickness(0,4,0,4);source.Visibility=location.Visibility=Visibility.Collapsed;
        buttons.Margin=new Thickness(0,4,0,4);daily.Margin=new Thickness(0,4,0,0);daily.ToolTip=L.Text("updates.timing")+"\n"+L.Text("updates.immediate");
        Children.Add(actions);Children.Add(Section("updates.applicationSection"));Children.Add(AppItems);Children.Add(Section("updates.componentsSection"));Children.Add(Items);Children.Add(buttons);Children.Add(details);Children.Add(daily);
        if(manager!=null)manager.Changed+=Render;Render();
        Items.Loaded+=async delegate{if(manager!=null)await manager.Inventory();};
    }
    static TextBlock Section(string key){return new TextBlock{Text=L.Text(key),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,0)};}
    static void ConfigureGrid(DataGrid grid,double height){
        foreach(var definition in new[]{new[]{"updates.component","Name","1.1"},new[]{"updates.current","Installed",".7"},new[]{"updates.available","Available",".7"},new[]{"common.status","Status","1.6"}})grid.Columns.Add(new DataGridTextColumn{Header=L.Text(definition[0]),Binding=new Binding(definition[1]),Width=new DataGridLength(Double.Parse(definition[2],System.Globalization.CultureInfo.InvariantCulture),DataGridLengthUnitType.Star)});
        var cell=new Style(typeof(TextBlock));cell.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));cell.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Text"){RelativeSource=RelativeSource.Self}));foreach(DataGridTextColumn column in grid.Columns)column.ElementStyle=cell;
        // Explicit 2-line cell templates: TextBlocks are individually arranged by a StackPanel.
        // Unlike a wrapped DataGridTextColumn, neither line can be clipped by the cell's text presenter.
        foreach(int index in new[]{1,2}) {
            string main=index==1?"Installed":"Available", build=index==1?"InstalledBuild":"AvailableBuild";
            var column=new DataGridTemplateColumn{Header=grid.Columns[index].Header,Width=grid.Columns[index].Width};
            var visual=new FrameworkElementFactory(typeof(StackPanel));
            visual.SetValue(StackPanel.OrientationProperty,Orientation.Vertical);
            visual.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            var versionText=new FrameworkElementFactory(typeof(TextBlock));
            versionText.SetBinding(TextBlock.TextProperty,new Binding(main));
            versionText.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);
            visual.AppendChild(versionText);
            var buildText=new FrameworkElementFactory(typeof(TextBlock));
            buildText.SetBinding(TextBlock.TextProperty,new Binding(build));
            buildText.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.None);
            buildText.SetValue(TextBlock.FontSizeProperty,11.0);
            buildText.SetValue(TextBlock.MarginProperty,new Thickness(0,2,0,0));
            var visibility=new Style(typeof(TextBlock));
            var hidden=new DataTrigger{Binding=new Binding(build),Value=""};
            hidden.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));
            visibility.Triggers.Add(hidden);
            buildText.SetValue(FrameworkElement.StyleProperty,visibility);
            visual.AppendChild(buildText);
            column.CellTemplate=new DataTemplate{VisualTree=visual};
            grid.Columns.RemoveAt(index);grid.Columns.Insert(index,column);
        }
        grid.SizeChanged+=delegate{double width=Math.Max(200,grid.ActualWidth-SystemParameters.VerticalScrollBarWidth-12);double[] shares={.23,.29,.29,.19};for(int i=0;i<4;i++)grid.Columns[i].Width=Math.Floor(width*shares[i]);};
        grid.Height=height;grid.RowHeight=Double.NaN;grid.SelectionUnit=DataGridSelectionUnit.FullRow;grid.GridLinesVisibility=DataGridGridLinesVisibility.None;
        var compactRow=new Style(typeof(DataGridRow));compactRow.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,26.0));compactRow.Setters.Add(new Setter(Control.BackgroundProperty,System.Windows.Media.Brushes.Transparent));compactRow.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));
        var appHeight=new DataTrigger{Binding=new Binding("Id"),Value="app"};appHeight.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,52.0));compactRow.Triggers.Add(appHeight);
        compactRow.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='DataGridRow'><Border Background='{TemplateBinding Background}' BorderBrush='{StaticResource CompassLine}' BorderThickness='0,0,0,1' SnapsToDevicePixels='True'><DataGridCellsPresenter/></Border></ControlTemplate>")));
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ThemeBrush("#F0F5FB")));compactRow.Triggers.Add(hover);
        var selectedRow=new Trigger{Property=DataGridRow.IsSelectedProperty,Value=true};selectedRow.Setters.Add(new Setter(Control.BackgroundProperty,CompactTheme.ThemeBrush("#DCEBFF")));selectedRow.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));compactRow.Triggers.Add(selectedRow);grid.RowStyle=compactRow;
        var cellStyle=new Style(typeof(DataGridCell));cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));cellStyle.Setters.Add(new Setter(Control.BackgroundProperty,System.Windows.Media.Brushes.Transparent));cellStyle.Setters.Add(new Setter(Control.ForegroundProperty,FluentTheme.Text));cellStyle.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='DataGridCell'><Border Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter VerticalAlignment='Center'/></Border></ControlTemplate>")));grid.CellStyle=cellStyle;
        grid.Margin=new Thickness(0,6,0,6);ScrollViewer.SetVerticalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);
    }
    static TextBlock Text(){return new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,3,0,3)};}
    internal static string ReleaseStatus(VendorUpdate item,string current){Version installed=null,available=null;bool versionKnown=Version.TryParse(current,out installed)&&Version.TryParse(item.Version,out available);bool metadataOnly=!String.IsNullOrEmpty(item.Error)&&String.IsNullOrEmpty(item.Hash)&&VendorUpdates.ValidOrigin(item);if(!String.IsNullOrEmpty(item.Error)&&!metadataOnly)return L.Text("updates.unverified");if(versionKnown&&installed>=available)return L.Text("updates.currentState");return L.Text(metadataOnly?"updates.manualRelease":"updates.newVersion");}
    internal void SavePreferences(){if(String.IsNullOrEmpty(UpdateRegistry.StateError))UpdateRegistry.Edit(x=>x.Daily=daily.IsChecked==true);}
    internal void Render(){
        string selected=SelectedRow?.Id;
        var state=UpdateRegistry.State();var rows=new List<UpdateRow>();
        foreach(string id in VendorUpdates.Ids){var item=state.Available.FirstOrDefault(x=>x.Id==id);string current=VendorUpdates.InstalledVersion(id);var pending=state.Pending.FirstOrDefault(x=>x.Id==id);string status=L.Text("updates.notChecked");
            if(item!=null)status=ReleaseStatus(item,current);
            if(pending!=null)status=L.Text(UpdateRegistry.RequiresRestart(id)?"updates.restartRequired":"updates.waiting");
            if(state.RestoreBundled.Contains(id))status=L.Text("updates.restartRequired");
            rows.Add(new UpdateRow{Id=id,Name=VendorUpdates.Name(id),Installed=current,Available=item?.Version??"—",Status=status,Package=item});
        }
        var app=state.Available.FirstOrDefault(x=>x.Id=="app");string appStatus=app==null?L.Text("updates.notChecked"):!String.IsNullOrEmpty(app.Error)?L.Text("updates.unverified"):String.IsNullOrEmpty(app.Version)?L.Text("updates.appNoRelease"):ApplicationUpdates.IsNewer(app)?L.Text("updates.newVersion"):L.Text("updates.currentState");
        try{if(ApplicationUpdates.Pending()!=null)appStatus=L.Text("updates.appReady");}catch(Exception ex){appStatus=ex.Message;}
        string currentVersion=BuildInfo.DisplayVersion;
        string availableVersion=String.IsNullOrEmpty(app?.Version)?"—":app.Version+(String.IsNullOrEmpty(app.Channel)?"":" "+app.Channel);
        var appRow=new UpdateRow{Id="app",Name="File Compass",Installed=currentVersion,InstalledBuild="Build "+BuildInfo.Build,Available=availableVersion,AvailableBuild=String.IsNullOrEmpty(app?.Build)?"":"Build "+app.Build,Status=appStatus,Package=app};
        rendering=true;try{Items.ItemsSource=rows;AppItems.ItemsSource=new[]{appRow};if(selected=="app"||String.IsNullOrEmpty(selected)){Items.SelectedItem=null;AppItems.SelectedItem=appRow;}else{AppItems.SelectedItem=null;Items.SelectedItem=rows.FirstOrDefault(x=>x.Id==selected)??rows[0];}}finally{rendering=false;}
        check.IsEnabled=manager!=null;check.Content=L.Text(manager!=null&&manager.Checking?"action.cancel":"updates.check");
        feedback.Text=manager!=null&&!String.IsNullOrEmpty(manager.Feedback)?manager.Feedback:state.LastSuccessUtc==DateTime.MinValue?L.Text("updates.notChecked"):L.Text("updates.lastCheck")+state.LastSuccessUtc.ToLocalTime().ToString("g");Selection();
        if(!String.IsNullOrEmpty(UpdateRegistry.StateError))feedback.Text=L.Text("updates.failed")+UpdateRegistry.StateError;daily.IsEnabled=String.IsNullOrEmpty(UpdateRegistry.StateError);
    }
    void Selection(){
        if(download==null)return;var row=SelectedRow;if(row==null)return;
        var state=UpdateRegistry.State();bool supported=VendorUpdates.Ids.Contains(row.Id);
        if(row.Id=="app"){
            PendingApplicationUpdate pending=null;string error=null;try{pending=ApplicationUpdates.Pending();}catch(Exception ex){error=ex.Message;}
            details.Text=error??row.Package?.Error??L.Text("updates.appHelp");source.Text=L.Text("updates.source")+ApplicationUpdates.Home;location.Text=L.Text("updates.location")+AppPaths.ProgramDirectory;details.ToolTip=details.Text+"\n"+source.Text+"\n"+location.Text+"\n"+row.Package?.Notes;
            download.Content=L.Text("updates.appDownload");download.IsEnabled=error==null&&pending==null&&manager!=null&&manager.CanDownload(row.Package);apply.Content=L.Text("updates.appInstall");apply.Visibility=pending!=null?Visibility.Visible:Visibility.Collapsed;apply.IsEnabled=error==null&&pending!=null&&manager!=null&&!manager.Downloading&&!main.UpdateWorkRunning;rollback.Visibility=Visibility.Collapsed;rollback.IsEnabled=false;vendor.IsEnabled=true;return;
        }
        download.Content=L.Text("updates.download");apply.Visibility=Visibility.Collapsed;apply.IsEnabled=false;rollback.Visibility=Visibility.Visible;
        details.Text=!String.IsNullOrEmpty(row.Package?.Error)?row.Package.Error:row.Id=="ffmpeg"?L.Text("updates.ffmpegSource"):L.Text("updates.packageHelp");
        source.Text=supported?L.Text("updates.source")+(row.Package?.Source??VendorUpdates.Home(row.Id)):"";
        location.Text=supported?L.Text("updates.location")+UpdateRegistry.Resolve(VendorUpdates.Files(row.Id)[0]):"";
        details.ToolTip=details.Text+"\n"+source.Text+"\n"+location.Text+"\n"+row.Package?.Notes;
        download.IsEnabled=supported&&manager!=null&&manager.CanDownload(row.Package)&&row.Status!=L.Text("updates.currentState")&&!state.Pending.Any(x=>x.Id==row.Id);
        rollback.IsEnabled=supported&&manager!=null&&!main.UpdateWorkRunning&&state.Active.Any(x=>x.Id==row.Id)&&!state.RestoreBundled.Contains(row.Id);
        vendor.IsEnabled=supported;
    }
    public void Dispose(){if(manager!=null)manager.Changed-=Render;}
 }
}
