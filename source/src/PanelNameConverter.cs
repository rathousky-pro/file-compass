using System;using System.Globalization;using System.Windows.Data;
namespace FileCompass {
 internal sealed class PanelNameConverter:IMultiValueConverter {
  public object Convert(object[] values,Type type,object parameter,CultureInfo culture){if(values.Length<5)return "";bool clean=values[4] is bool&&(bool)values[4],folder=values[2] is bool&&(bool)values[2],location=values[3] is string;return clean&&!folder&&!location&&!(values.Length>5&&values[5] is bool&&(bool)values[5])?values[1]:values[0];}
  public object[] ConvertBack(object value,Type[] types,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
 internal sealed class PanelValueConverter:IMultiValueConverter {
  public object Convert(object[] values,Type type,object parameter,CultureInfo culture){if(values.Length<5)return "";string original=values[0] as string??"";bool clean=values[4] is bool&&(bool)values[4];if(!clean||original.Length==0)return original;if((string)parameter=="date"){if(values[1] is DateTime&&((DateTime)values[1])!=DateTime.MinValue)return ((DateTime)values[1]).ToString(L.Language=="cs"?"dd. MM. yyyy":"d",CultureInfo.GetCultureInfo(L.Language));return original;}if(original=="<DIR>")return "—";if(original.StartsWith("<",StringComparison.Ordinal)||!(values[2] is long))return original;return CopyUi.Bytes((long)values[2]);}
  public object[] ConvertBack(object value,Type[] types,object parameter,CultureInfo culture){throw new NotSupportedException();}
 }
}