using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
 internal sealed class FluentPalette {
  internal readonly string Id,Name,Note,Accent,Ink,Frame,Soft,Selection,Line,Navigation;
  internal FluentPalette(string id,string name,string note,string accent,string ink,string frame,string soft,string selection,string line,string navigation){Id=id;Name=name;Note=note;Accent=accent;Ink=ink;Frame=frame;Soft=soft;Selection=selection;Line=line;Navigation=navigation;}
 }
 internal static class FluentColors {
  internal static readonly FluentPalette[] All={new FluentPalette("azure","Azur","Svěží modrý rám","#075dce","#123d70","#cddff7","#e4effd","#cfe3ff","#a5bfdf","#112e50"),
new FluentPalette("cobalt","Kobalt","Výrazná královská modř","#1649c2","#17376c","#c4d3f5","#e4ebfc","#d0dcff","#a4b8df","#142950"),
new FluentPalette("ocean","Oceán","Petrolejová a ledová","#086b80","#164d5d","#c8e2ea","#e3f2f5","#c9eaf0","#a0c5ce","#10333f"),
new FluentPalette("indigo","Indigo","Modrofialový charakter","#5845bb","#342862","#d8d0f0","#efebfa","#e1d8fa","#bcaed9","#282342"),
new FluentPalette("sage","Šalvěj","Klidná zelenomodrá","#27664e","#234c3d","#cfe2d9","#e8f2ed","#d4eadd","#abc7b8","#1a342b"),
new FluentPalette("graphite","Grafit","Neutrální profesionální","#405c7a","#263f59","#d2dce7","#edf1f6","#d8e4f0","#b1c0d0","#202d3c"),
new FluentPalette("ruby","Rubín","Vínová a jemně růžový rám","#A52346","#65253B","#EACFD8","#F9EBF0","#F3D3DE","#CDA6B5","#452335"),
new FluentPalette("rose","Růže","Tlumená růžová","#A63875","#65304F","#EDD3E3","#FBEEF5","#F5D9E9","#D1ADC3","#45263A"),
new FluentPalette("amber","Jantar","Teplá medová","#935A08","#61441D","#EFDFC3","#FBF4E8","#F7E4C2","#CDB993","#403222"),
new FluentPalette("forest","Les","Sytá lesní zelená","#21623B","#254B32","#D0E2D2","#EDF6EE","#D6EBD8","#A9C6AE","#21382A"),
new FluentPalette("plum","Švestka","Teplá fialová","#783F98","#4F305F","#E2D2EB","#F5EDF9","#EBD9F3","#C0A9CE","#35243F"),
new FluentPalette("sand","Písek","Neutrální teplý rám","#79604A","#4F4135","#E5DCCF","#F7F3EC","#EBDFCD","#C2B49F","#37312B"),
new FluentPalette("sky","Světlá modrá","Jemný nebeský rám","#2068A4","#284B6D","#E1EEFC","#F2F7FD","#D5E8FA","#B8CFE4","#203A56"),
new FluentPalette("mint","Světlá zelená","Svěží světlý zelený rám","#39764A","#2E5038","#E4F0E5","#F4F9F3","#D8EBD9","#BCD5BF","#263F2D"),
new FluentPalette("silver","Světlá šedá","Neutrální stříbrný rám","#536373","#374652","#ECEFF2","#F7F8FA","#DFE6ED","#C9D1D9","#2B343F")};
  internal static FluentPalette Current=All[0];
  internal static readonly SolidColorBrush Navigation=new SolidColorBrush((Color)ColorConverter.ConvertFromString(All[0].Navigation));
  internal static readonly SolidColorBrush SectionLine=new SolidColorBrush(SectionColor(All[0]));
  static Color SectionColor(FluentPalette palette){var line=(Color)ColorConverter.ConvertFromString(palette.Line);var ink=(Color)ColorConverter.ConvertFromString(palette.Ink);return Color.FromRgb((byte)((line.R+ink.R)/2),(byte)((line.G+ink.G)/2),(byte)((line.B+ink.B)/2));}
  internal static string Normalize(string id){foreach(var p in All)if(p.Id==id)return id;return "azure";}
  internal static void Select(string id){foreach(var p in All)if(p.Id==Normalize(id)){Current=p;break;}Navigation.Color=(Color)ColorConverter.ConvertFromString(Current.Navigation);SectionLine.Color=SectionColor(Current);AppearanceTheme.Apply("Light");}
  internal static string Map(string original,string fallback){
   var p=Current;
   switch(original.ToUpperInvariant()){
    case "#20242A":return "#172433";
    case "#596572":return "#526578";
    case "#0067C0":case "#1457A2":return p.Accent;
    case "#85C5F4":return p.Selection;
    case "#D5D9DF":case "#DCE8F3":case "#EAF2FF":case "#EFF5FF":return p.Frame;
    case "#DDEDFC":case "#E4F0FF":case "#D0E7FA":case "#DCEBFF":return p.Selection;
    case "#98A5B5":case "#BCC3CD":case "#DDDFE5":case "#79A9DF":case "#89BFE5":case "#C6D8F5":return p.Line;
    case "#FFFFFF":return "#FFFFFF";
    default:return p.Soft;
   }
  }
  internal static void Choices(Panel parent,Action<string> selected){
   var group=FluentTheme.Section(parent,"Barevný motiv · Fluent Contrast");
   group.Children.Add(new TextBlock{Text="Náhled se projeví ihned. Použít změnu uloží; Zrušit vrátí předchozí motiv.",TextWrapping=TextWrapping.Wrap,Foreground=FluentTheme.Muted,Margin=new Thickness(0,0,0,10)});
   var grid=new WrapPanel();group.Children.Add(grid);
   foreach(var p in All){var palette=p;var label=new StackPanel{Orientation=Orientation.Horizontal};label.Children.Add(new Border{Width=24,Height=24,CornerRadius=new CornerRadius(6),Background=CompactTheme.ColorBrush(p.Accent),Margin=new Thickness(0,0,10,0)});label.Children.Add(new TextBlock{Text=p.Name,VerticalAlignment=VerticalAlignment.Center});var choice=new RadioButton{GroupName="FluentColor",Content=label,ToolTip=p.Note,IsChecked=Current.Id==p.Id,MinWidth=150,Margin=new Thickness(0,5,12,9)};choice.Checked+=delegate{Select(palette.Id);selected(palette.Id);};grid.Children.Add(choice);}
  }
 }
}
