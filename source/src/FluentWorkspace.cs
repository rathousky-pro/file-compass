using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace FileCompass {
 public partial class MainWindow {
  DockPanel fluentLegacyMenu;
  Border fluentDrawer;
  Menu fluentCascadeMenu;
  TextBox fluentCommandSearch;
  FrameworkElement fluentSearchBox;
  internal void CloseFluentDrawer(){if(fluentCascadeMenu!=null)CascadeNavigation.Close(fluentCascadeMenu);if(fluentDrawer!=null)fluentDrawer.Visibility=Visibility.Collapsed;}
  internal void InstallFluentWorkspace(DockPanel root,Grid split){
   FluentMaximize.Attach(this);WindowStyle=WindowStyle.None;System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome{CaptionHeight=0,GlassFrameThickness=new Thickness(0),ResizeBorderThickness=new Thickness(5),UseAeroCaptionButtons=false});
   var workspace=new Grid();workspace.Children.Add(split);root.Children.Add(workspace);
   var top=fluentLegacyMenu.Parent as Panel;
   top.Children.Remove(fluentLegacyMenu);
   var navigation=new DockPanel{Margin=new Thickness(16,8,16,8)};
   var menu=CopyUi.Button("Menu",delegate{ToggleFluentDrawer();});menu.MinWidth=100;
   var menuCaption=new StackPanel{Orientation=Orientation.Horizontal};var hamburger=new StackPanel{Width=18,Height=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0),Tag="menu-hamburger"};for(int line=0;line<3;line++)hamburger.Children.Add(new System.Windows.Shapes.Rectangle{Width=18,Height=2,Fill=FluentTheme.Text,Margin=new Thickness(0,0,0,line<2?3:0)});menuCaption.Children.Add(hamburger);menuCaption.Children.Add(new TextBlock{Text="Menu",VerticalAlignment=VerticalAlignment.Center});menu.Content=menuCaption;System.Windows.Automation.AutomationProperties.SetName(menu,"Menu");
   System.Windows.Automation.AutomationProperties.SetAutomationId(menu,"FluentMenu");
   DockPanel.SetDock(menu,Dock.Left);navigation.Children.Add(menu);
   var closeWindow=CopyUi.Button("×",Close);DockPanel.SetDock(closeWindow,Dock.Right);navigation.Children.Add(closeWindow);var maximize=CopyUi.Button("□",delegate{WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;});DockPanel.SetDock(maximize,Dock.Right);navigation.Children.Add(maximize);
   var color=CopyUi.Button("—",delegate{WindowState=WindowState.Minimized;});DockPanel.SetDock(color,Dock.Right);navigation.Children.Add(color);
   InstallUnifiedSearch();
   var identity=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0),Tag="main-brand"};var appIcon=new Image{Width=28,Height=28,Margin=new Thickness(0,0,10,0)};appIcon.SetBinding(Image.SourceProperty,new System.Windows.Data.Binding("Icon"){Source=this});identity.Children.Add(appIcon);identity.Children.Add(new TextBlock{Text=ApplicationBrand.Name,Foreground=FluentTheme.Text,FontSize=15,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});navigation.Children.Add(identity);
   navigation.MouseLeftButtonDown+=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e){if(e.OriginalSource is TextBlock)DragMove();};top.Children.Insert(0,navigation);
   var body=new StackPanel{Margin=new Thickness(12)};
   var header=new DockPanel();var close=new Button{Content="×",Width=32,Height=30,Background=Brushes.Transparent,Foreground=Brushes.White,BorderThickness=new Thickness(0)};close.Click+=delegate{ToggleFluentDrawer();};DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);header.Children.Add(new TextBlock{Text="File Compass",Foreground=Brushes.White,FontSize=19,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});body.Children.Add(header);
   Action<string> section=delegate(string text){body.Children.Add(new TextBlock{Text=text,Foreground=Brushes.LightSteelBlue,FontSize=11,Margin=new Thickness(8,12,0,5)});};
   Action<string,Action> link=delegate(string title,Action action){var b=new Button{Content=title,Style=DrawerControls.ActionStyle,FontSize=13,Padding=new Thickness(8,6,8,6)};b.Click+=delegate{if(busy||PanelsLoading)return;CloseFluentDrawer();action();};body.Children.Add(b);};
   section("RYCHLÉ AKCE");
   link(L.Text("action.copy"),delegate{Transfer(false);});link(L.Text("action.newFolder"),NewFolder);
   var shortcutGrid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};int shortcutIndex=0;foreach(var shortcut in body.Children.OfType<Button>().ToArray()){body.Children.Remove(shortcut);var caption=new StackPanel{Orientation=Orientation.Horizontal};caption.Children.Add(new TextBlock{Text=shortcutIndex++==0?"\uE8C8":"\uE8F4",FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=16,Margin=new Thickness(0,0,6,0),Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center});caption.Children.Add(new TextBlock{Text=Convert.ToString(shortcut.Content),Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center});shortcut.Content=caption;shortcut.Background=CompactTheme.ColorBrush("#244666");shortcut.Margin=new Thickness(0,2,4,2);shortcutGrid.Children.Add(shortcut);}body.Children.Add(shortcutGrid);
   section("MENU");
   var roots=new System.Collections.Generic.List<MenuItem>();
   foreach(var original in fluentLegacyMenu.Children.OfType<Menu>().OrderByDescending(x=>x.Items.Count).ToArray())foreach(var item in original.Items.OfType<MenuItem>().ToArray()){original.Items.Remove(item);roots.Add(item);}
   MenuItem about=null;foreach(var rootItem in roots){var item=rootItem.Items.OfType<MenuItem>().FirstOrDefault(x=>Convert.ToString(x.Header)==L.Text("app.aboutMenu"));if(item!=null){about=item;rootItem.Items.Remove(item);if(rootItem.Items.Count>0&&rootItem.Items[rootItem.Items.Count-1] is Separator)rootItem.Items.RemoveAt(rootItem.Items.Count-1);break;}}
   // Promote the existing tools group; no duplicate handlers or missing commands.
   var commands=roots.FirstOrDefault(x=>Convert.ToString(x.Header)==L.Text("menu.commands"));
   if(commands!=null){var tools=commands.Items.OfType<MenuItem>().FirstOrDefault(x=>Convert.ToString(x.Header)==L.Text("analyzer.menuGroup"));if(tools!=null){commands.Items.Remove(tools);tools.Header=L.Text("operation.tools");roots.Insert(roots.IndexOf(commands)+1,tools);}}
   string[] iconKeys={"menu.file","menu.selection","menu.edit","menu.commands","operation.tools","media.menu","connections.title","menu.view","menu.configuration","menu.start","action.help"};
   string[] glyphs={"\uE8A5","\uE73A","\uE70F","\uE756","\uE90F","\uE714","\uE968","\uE8A9","\uE713","\uE768","\uE897"};
   foreach(var item in roots){int index=Array.FindIndex(iconKeys,key=>L.Text(key)==Convert.ToString(item.Header));if(index>=0)item.Icon=new TextBlock{Text=glyphs[index],FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=16,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center};}
   fluentCascadeMenu=CascadeNavigation.Build(roots.ToArray(),CloseFluentDrawer);body.Children.Add(fluentCascadeMenu);
   body.Children.Add(new Border{Height=1,Background=CompactTheme.ColorBrush("#42617C"),Margin=new Thickness(6,10,6,6)});
   link(L.Text("favorites.openMenu"),delegate{ShowFavorites();});
   link(L.Text("queue.copyTitle"),ShowCopyQueue);
   var drawerBody=new DockPanel();if(about!=null){about.Icon=new TextBlock{Text="\uE946",FontFamily=new FontFamily("Segoe MDL2 Assets"),Foreground=Brushes.White,FontSize=16};System.Windows.Automation.AutomationProperties.SetAutomationId(about,"FluentAbout");var aboutMenu=CascadeNavigation.Build(new[]{about},CloseFluentDrawer);aboutMenu.Tag="cascade-about";var aboutFooter=new Border{Tag="drawer-about-footer",Padding=new Thickness(12,8,12,8),BorderBrush=CompactTheme.ColorBrush("#42617C"),BorderThickness=new Thickness(0,1,0,0),Child=aboutMenu};DockPanel.SetDock(aboutFooter,Dock.Bottom);drawerBody.Children.Add(aboutFooter);}drawerBody.Children.Add(new ScrollViewer{Tag="drawer-scroll",Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   fluentDrawer=new Border{Tag="fluent-drawer",Width=250,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Stretch,Margin=new Thickness(8,0,0,8),Background=FluentColors.Navigation,CornerRadius=new CornerRadius(8),Visibility=Visibility.Collapsed,Child=drawerBody};
   InstallDrawerSearch(drawerBody,roots.Concat(about==null?new MenuItem[0]:new[]{about}).ToArray());
   Panel.SetZIndex(fluentDrawer,20);workspace.Children.Add(fluentDrawer);
   split.PreviewMouseDown+=delegate{CloseFluentDrawer();};
   Deactivated+=delegate{CloseFluentDrawer();};
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape&&fluentDrawer.IsVisible){CloseFluentDrawer();menu.Focus();e.Handled=true;}};
  }
  internal void ToggleFluentDrawer(){if(fluentDrawer.IsVisible)CloseFluentDrawer();else{fluentDrawer.Visibility=Visibility.Visible;fluentCommandSearch.Clear();fluentCommandSearch.Focus();}}
 }
}
