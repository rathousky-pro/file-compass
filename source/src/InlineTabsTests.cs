#if APP_TESTS
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace FileCompass {
 public partial class FilePanel {
  internal void VerifyCompactTabWidths(Action<bool,string> check)
  {
   UpdateLayout();
   var path=FluentUiTests.Children<PathTextBlock>(activeTabFace).Single();
   check(path.FullPath==CurrentPath,"the active tab receives the entire current path");
   var inactive=tabStrip.Children.OfType<Border>().Where(face=>face!=activeTabFace).ToArray();
   check(inactive.All(face=>face.ActualWidth>=56),"inactive tabs keep room for their folder names");
   var measure = new TextBlock { Text = TabCaption(tabIndex), FontFamily = path.FontFamily, FontSize = path.FontSize,
       FontWeight = path.FontWeight };
   measure.Measure(new Size(Double.PositiveInfinity, 30));
   check(activeTabFace.ActualWidth <= measure.DesiredSize.Width + 57,"active tab width is capped by its measured path and controls");
   var captions=inactive.Select(face=>(TextBlock)FluentUiTests.Children<Button>(face).First(button=>Equals(button.Tag,"panel-tab")).Content);
   check(captions.All(caption=>!caption.Text.TrimEnd('\\','/').Contains("\\")),"inactive tab captions show only the last folder");
  }
  internal void VerifyInactiveTabCommand(Action<bool,string> check)
  {
   int originalIndex = tabIndex;
   int destinationIndex = (tabIndex + 1) % tabs.Count;
   var expectedPath = tabs[destinationIndex].Saved.Path;
   var face = (Border)tabStrip.Children[destinationIndex];
   var include = face.ContextMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Header,L.Text("projection.includeChildren")));
   include.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
   NavigationTests.Wait(TabNavigationTask);
   NavigationTests.Wait(NavigationTask);
   check(tabIndex==destinationIndex&&IsFlatView&&CurrentPath==expectedPath,"right-click command applies to its own inactive tab");
   ToggleFlatView(); NavigationTests.Wait(NavigationTask);
   SwitchTab(originalIndex); NavigationTests.Wait(TabNavigationTask);
  }
  internal void VerifyInactiveTabCaption(string path, Action<bool,string> check)
  {
   UpdateLayout();
   int inactiveIndex = tabs.FindIndex(tab => tab.Saved.Path == path);
   var face = (Border)tabStrip.Children[inactiveIndex];
   var button = FluentUiTests.Children<Button>(face).Single(candidate => Equals(candidate.Tag, "panel-tab"));
   var text = (TextBlock)button.Content;
   var measure = new TextBlock { Text = text.Text, FontFamily = text.FontFamily, FontSize = text.FontSize };
   measure.Measure(new Size(Double.PositiveInfinity, 30));
   check(face.BorderThickness.Top>0 && (face.Background as System.Windows.Media.SolidColorBrush)?.Color.A==0,
       "inactive tab has a visible outline and transparent background");
   check(text.Text==Path.GetFileName(path)&&text.ActualWidth>=measure.DesiredSize.Width,
       "an inactive folder name gets its full measured width while an active drive root is short");
  }
  internal double ActiveTabWidth {get{return activeTabFace.ActualWidth;}}
  internal void VerifyInlineTabRow(Action<bool,string> check)
  {
   UpdateLayout();
   check(cleanBreadcrumbFrame.IsVisible&&tabRow.IsVisible&&cleanBreadcrumbDock.Children.Contains(tabRow)&&
       cleanBreadcrumbFrame.BorderThickness.Bottom>0,"tabs share an outlined path row with a visible lower edge");
   check(tabRow.Children.Contains(newTabButton)&&activeTabFace!=null,"the plus button remains visible next to the scrollable tabs");
  }

 }
 internal static class InlineTabsTests {
  internal static int Run(string root){Directory.CreateDirectory(root);var checks=new List<string>();MainWindow w=null;string motif=AppearanceTheme.Motif;Action<bool,string> check=(yes,text)=>{if(!yes)throw new Exception(text);checks.Add(text);};try{SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));L.ChangeLanguage("cs");FluentTheme.Ensure();AppearanceTheme.Apply("Light");string home=Directory.CreateDirectory(Path.Combine(root,"Dokumenty")).FullName;string other=Directory.CreateDirectory(Path.Combine(root,"Fotografie")).FullName;foreach(string theme in new[]{"Original","Balanced"}){AppearanceTheme.SetMotif(theme);var prefs=new ProgramSettings{LeftFolder=home,RightFolder=home,SaveTabsOnExit=true};prefs.Save();w=new MainWindow(null,prefs){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};w.Show();NavigationTests.WaitPanels(w);var panels=FluentUiTests.Children<FilePanel>(w).ToArray();var left=panels[0];var right=panels[1];
    for(int i=0;i<24;i++)w.StoreFolderBookmark("Dokumenty "+i,"Práce",left);check(w.BookmarkCount==24&&left.TabCount==1&&right.TabCount==1,theme+": saving many bookmarks does not open tabs");
    var favoriteButton=FluentUiTests.Children<Button>(w).Single(button=>System.Windows.Automation.AutomationProperties.GetAutomationId(button)=="PanelFavorites");
    favoriteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    check(favoriteButton.ContextMenu.Items.OfType<MenuItem>().Any(item=>Equals(item.Header,"Práce") && item.Items.Count==24),
        theme+": favorites button opens existing saved folders");
    favoriteButton.ContextMenu.IsOpen=false;
    NavigationTests.Wait(w.OpenBookmarkTab(new FavoriteLocation{Name="Fotografie",Left=other,LeftLocation=new SavedTab{Path=other}}));check(left.TabCount==2&&left.CurrentPath==other&&right.TabCount==1,theme+": bookmark opens one tab in the selected panel");NavigationTests.Wait(right.OpenLocationTab(new SavedTab{Path=other}));NavigationTests.Wait(right.OpenLocationTab(new SavedTab{Path=home}));w.UpdateLayout();left.VerifyInactiveTabCommand(check);left.VerifyInlineTabRow(check);right.VerifyInlineTabRow(check);left.VerifyCompactTabWidths(check);right.VerifyCompactTabWidths(check);check(left.TabCount==2&&right.TabCount==3&&w.BookmarkCount==24,theme+": two left and three right tabs are independent of 24 saved bookmarks");NextStageTests.Draw(w,Path.Combine(root,theme+"-inline-tabs.png"));
    var saved=ProgramSettings.Load();check(saved.Favorites.Count==24&&saved.LeftTabs.Length==2&&saved.RightTabs.Length==3,theme+": bookmarks and open tabs are serialized separately");w.Close();w=null;w=new MainWindow(null,saved){Left=-18000,Top=-18000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};w.Show();NavigationTests.WaitPanels(w);panels=FluentUiTests.Children<FilePanel>(w).ToArray();left=panels[0];right=panels[1];check(left.TabCount==2&&right.TabCount==3&&w.BookmarkCount==24,theme+": restart restores only open tabs and keeps all saved bookmarks");w.UpdateLayout();left.VerifyCompactTabWidths(check);right.VerifyCompactTabWidths(check);left.CloseTab(1);NavigationTests.Wait(left.TabNavigationTask);check(left.TabCount==1&&w.BookmarkCount==24,theme+": closing a tab does not remove its saved bookmark");left.UpdateLayout();left.VerifyCompactTabWidths(check);check(left.ActiveTabWidth<left.ActualWidth,theme+": the single tab follows its path rather than filling the panel");left.RestoreTabs(new[]{new SavedTab{Path=Path.GetPathRoot(home)},new SavedTab{Path=Path.GetPathRoot(home)}},1);NavigationTests.Wait(left.NavigationTask);w.UpdateLayout();left.VerifyCompactTabWidths(check);NextStageTests.Draw(w,Path.Combine(root,theme+"-root-tabs.png"));
    string longName=Directory.CreateDirectory(Path.Combine(root,"Dlouhý název složky pro ověření šířky")).FullName;
    left.RestoreTabs(new[]{new SavedTab{Path=longName},new SavedTab{Path=Path.GetPathRoot(home)}},1);
    NavigationTests.Wait(left.NavigationTask);left.VerifyInactiveTabCaption(longName,check);
    var missing=Path.Combine(root,"missing-"+theme);NavigationTests.Wait(w.OpenBookmarkTab(new FavoriteLocation{Name="Chybí",Left=missing,LeftLocation=new SavedTab{Path=missing}}));check(left.HasUnavailableTab&&left.SaveTabs()[left.CurrentTabIndex].Path==missing,theme+": missing bookmark target remains explicit in its new tab");w.Close();w=null;
   }File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"PASS: "+checks.Count+" checks"}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(root,"result.txt"),checks.Concat(new[]{"FAIL: "+ex}));return 1;}finally{if(w!=null)w.Close();AppearanceTheme.SetMotif(motif);}}
 }
}
#endif
