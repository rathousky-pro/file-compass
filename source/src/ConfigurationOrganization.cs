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
   var iconPage=pages[1];var appIcons=FindSetting(iconPage,"settings.applicationIconsTitle");var variants=FluentUiTestsSafe.Children<GroupBox>(iconPage).Where(x=>x.Header is RadioButton).ToArray();var themeGrid=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};foreach(var variant in variants){DetachSetting(variant);themeGrid.Children.Add(variant);}var sizesGrid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};sizesGrid.Children.Add(SettingSection("Horní lišta",buttonIconSize));sizesGrid.Children.Add(SettingSection("Disky",driveIconSize));SetSettingPages(iconPage,SettingTab("Ikona aplikace",appIcons),SettingTab("Lišta a disky",themeGrid,sizesGrid));
   // pages are in the final category order: appearance, icons, panels, archives, viewers, startup, network, operations, updates.
   var panel=pages[2];var operations=pages[7];
   var units=FindSetting(panel,"settings.sizeUnits");var visibility=FindSetting(panel,"settings.fileVisibility");var views=FindSetting(panel,"columns.autoTitle");var refresh=FindSetting(panel,"settings.panelRefreshGroup");
   var behaviorGroups=FluentUiTestsSafe.Children<GroupBox>(fileBehavior).ToArray();var general=FluentUiTestsSafe.Children<CheckBox>(behaviorGroups[0]).ToArray();var copy=FluentUiTestsSafe.Children<CheckBox>(behaviorGroups[1]).ToArray();var deletion=behaviorGroups[2];
   var display=SettingTab("Zobrazení souborů",units,visibility,SettingSection("Názvy a podrobnosti",general[7],general[8]));
   var control=SettingTab("Řazení a ovládání",SettingSection("Řazení",general[1],general[2]),SettingSection("Výběr a navigace",general[3],general[4],general[5]));
   var updates=SettingTab("Obnovování a pohledy",views,refresh);
   SetSettingPages(panel,display,control,updates);
   SetSettingPages(operations,SettingTab("Kopírování",SettingSection("Ověření a metadata",copy.Take(3).ToArray()),SettingSection("Oprávnění a rozsah",copy.Skip(3).ToArray())),SettingTab("Mazání a přejmenování",deletion,SettingSection("Přejmenování",general[0])));
   var archive=pages[3];var info=archive.Children.OfType<TextBlock>().Skip(2).ToArray();
   var tools=SettingTab("Externí archivátory",ExternalTool("RAR / WinRAR",rarPath),ExternalTool("ARJ",arjPath),ExternalTool("LHA / LZH",lhaPath));
   var archiveInfo=SettingTab("Formáty a chování",info.Concat(new FrameworkElement[]{SettingSection("Časové údaje archivu",general[6])}).ToArray());
   DetachSetting(archiveLimits);archiveLimits.Tag="settings-section-page";
   SetSettingPages(archive,archiveInfo,tools,archiveLimits);
   var viewers=pages[4];var imageGroup=FindSetting(viewers,"settings.imagesLabel");var mediaGroup=FindSetting(viewers,"settings.audioAndVideoLabel");var editorGroup=FindSetting(viewers,"settings.textEditorLabel");var viewerHelp=viewers.Children.OfType<TextBlock>().Last();SetSettingPages(viewers,SettingTab("Obrázky a multimédia",imageGroup,mediaGroup),SettingTab("Text a kód",editorGroup,internalEditor,internalCodePreview,viewerHelp));
  }
 }
}
