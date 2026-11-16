using System;using System.Linq;using System.Windows;using System.Windows.Controls;
namespace FileCompass {
 public sealed partial class ConfigurationDialog {
  static void DetachSetting(FrameworkElement element){var panel=element.Parent as Panel;if(panel!=null){panel.Children.Remove(element);return;}var decorator=element.Parent as Decorator;if(decorator!=null){decorator.Child=null;return;}var content=element.Parent as ContentControl;if(content!=null)content.Content=null;}
  static StackPanel SettingStack(params FrameworkElement[] fields){var stack=new StackPanel();foreach(var field in fields){DetachSetting(field);stack.Children.Add(field);}return stack;}
  static GroupBox SettingSection(string title,params FrameworkElement[] fields){return new GroupBox{Header=title,Content=SettingStack(fields),Margin=new Thickness(0,0,0,12)};}
  static GroupBox SettingTab(string title,params FrameworkElement[] sections){return new GroupBox{Header=title,Tag="settings-section-page",Content=SettingStack(sections)};}
  static GroupBox FindSetting(StackPanel page,string key){return FluentUiTestsSafe.Children<GroupBox>(page).First(x=>Convert.ToString(x.Header)==L.Text(key));}
  static void SetSettingPages(StackPanel page,params GroupBox[] tabs){var title=page.Children[0];var description=page.Children[1];page.Children.Clear();page.Children.Add(title);page.Children.Add(description);foreach(var tab in tabs)page.Children.Add(tab);}
  GroupBox ExternalTool(string name,TextBox path){var row=path.Parent as DockPanel;var host=row.Parent as StackPanel;var buttons=host.Children.OfType<Button>().ToArray();DetachSetting(row);var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.Children.Add(row);var actions=new WrapPanel{Margin=new Thickness(8,0,0,0)};foreach(var button in buttons){DetachSetting(button);button.MinWidth=0;actions.Children.Add(button);}Grid.SetColumn(actions,1);grid.Children.Add(actions);return SettingSection(name,grid);}
  void OrganizeSettings(StackPanel[] pages){
   var iconPage=pages[1];var appIcons=FindSetting(iconPage,"settings.applicationIconsTitle");var variants=FluentUiTestsSafe.Children<GroupBox>(iconPage).Where(x=>x.Header is RadioButton).ToArray();var themeGrid=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};foreach(var variant in variants){DetachSetting(variant);themeGrid.Children.Add(variant);}var sizesGrid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};sizesGrid.Children.Add(SettingSection(L.Text("configsection.0"),buttonIconSize));sizesGrid.Children.Add(SettingSection(L.Text("newui.13"),driveIconSize));SetSettingPages(iconPage,SettingTab(L.Text("newui.12"),appIcons),SettingTab(L.Text("configsection.1"),themeGrid,sizesGrid));
   // pages are in the final category order: appearance, icons, panels, archives, viewers, startup, network, operations, updates.
   var panel=pages[2];var operations=pages[7];
   var units=FindSetting(panel,"settings.sizeUnits");var visibility=FindSetting(panel,"settings.fileVisibility");var views=FindSetting(panel,"columns.autoTitle");var refresh=FindSetting(panel,"settings.panelRefreshGroup");
   var behaviorGroups=FluentUiTestsSafe.Children<GroupBox>(fileBehavior).ToArray();var general=FluentUiTestsSafe.Children<CheckBox>(behaviorGroups[0]).ToArray();var copy=FluentUiTestsSafe.Children<CheckBox>(behaviorGroups[1]).ToArray();var deletion=behaviorGroups[2];
   var display=SettingTab(L.Text("configsection.2"),units,visibility,SettingSection(L.Text("configsection.3"),general[6],general[7]));
   var control=SettingTab(L.Text("configsection.4"),SettingSection(L.Text("configsection.5"),general[1],general[2]),SettingSection(L.Text("configsection.6"),general[3],general[4]));
   var updates=SettingTab(L.Text("configsection.7"),views,refresh);
   SetSettingPages(panel,display,control,updates);
   SetSettingPages(operations,SettingTab(L.Text("configsection.8"),SettingSection(L.Text("configsection.9"),copy.Take(3).ToArray()),SettingSection(L.Text("configsection.10"),copy.Skip(3).ToArray())),SettingTab(L.Text("configsection.11"),deletion,SettingSection(L.Text("configsection.12"),general[0])));
   var archive=pages[3];var info=archive.Children.OfType<TextBlock>().Skip(2).ToArray();
   var tools=SettingTab(L.Text("configsection.13"),ExternalTool("RAR / WinRAR",rarPath),ExternalTool("ARJ",arjPath),ExternalTool("LHA / LZH",lhaPath));
   var archiveInfo=SettingTab(L.Text("configsection.14"),info.Concat(new FrameworkElement[]{SettingSection(L.Text("configsection.15"),general[5])}).ToArray());
   DetachSetting(archiveLimits);archiveLimits.Tag="settings-section-page";
   SetSettingPages(archive,archiveInfo,tools,archiveLimits);
   var viewers=pages[4];var imageGroup=FindSetting(viewers,"settings.imagesLabel");var mediaGroup=FindSetting(viewers,"settings.audioAndVideoLabel");var editorGroup=FindSetting(viewers,"settings.textEditorLabel");var viewerHelp=viewers.Children.OfType<TextBlock>().Last();SetSettingPages(viewers,SettingTab(L.Text("configsection.16"),imageGroup,mediaGroup),SettingTab(L.Text("configsection.17"),editorGroup,internalEditor,internalCodePreview,hoverFilePreview,viewerHelp));
  }
 }
}
