using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace FileCompass {
    // Measures actual glyph widths rather than assuming a fixed character count.
    internal sealed class PathTextBlock : TextBlock {
        internal static readonly DependencyProperty FullPathProperty=DependencyProperty.Register("FullPath",typeof(string),typeof(PathTextBlock),new PropertyMetadata("",Changed));
        internal static readonly DependencyProperty PrefixProperty=DependencyProperty.Register("Prefix",typeof(string),typeof(PathTextBlock),new PropertyMetadata("",Changed));
        public string FullPath {get{return (string)GetValue(FullPathProperty);}set{SetValue(FullPathProperty,value??"");}}
        public string Prefix {get{return (string)GetValue(PrefixProperty);}set{SetValue(PrefixProperty,value??"");}}
        static void Changed(DependencyObject sender,DependencyPropertyChangedEventArgs args){((PathTextBlock)sender).RefreshPath();}
        internal PathTextBlock() {
            TextWrapping=TextWrapping.NoWrap;TextTrimming=TextTrimming.CharacterEllipsis;ClipToBounds=true;FlowDirection=FlowDirection.LeftToRight;
            VerticalAlignment=VerticalAlignment.Center;
            SizeChanged+=delegate{RefreshPath();};Loaded+=delegate{RefreshPath();};
        }
        static PathTextBlock() {
            foreach(var property in new[]{FontFamilyProperty,FontSizeProperty,FontWeightProperty,FontStyleProperty,FontStretchProperty})property.OverrideMetadata(typeof(PathTextBlock),new FrameworkPropertyMetadata(Changed));
        }
        internal void SetPath(string prefix,string path){Prefix=prefix;FullPath=path;}
        void RefreshPath() {
            string full=(Prefix??"")+(FullPath??"");ToolTip=full;System.Windows.Automation.AutomationProperties.SetName(this,full);
            if(ActualWidth<=0){Text=full;return;}
            var typeface=new Typeface(FontFamily,FontStyle,FontWeight,FontStretch);double dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;
            Func<string,double> measure=text=>new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,typeface,FontSize,Brushes.Black,dpi).WidthIncludingTrailingWhitespace;
            Text=Compact(FullPath??"",Prefix??"",Math.Max(0,ActualWidth-2),measure);
        }
        static int RootEnd(string path) {
            int scheme=path.IndexOf("://",StringComparison.Ordinal);
            if(scheme>0){int end=path.IndexOf('/',scheme+3);return end<0?path.Length:end+1;}
            if(path.StartsWith("\\\\",StringComparison.Ordinal)){int server=path.IndexOf('\\',2);int share=server<0?-1:path.IndexOf('\\',server+1);return share<0?Math.Min(path.Length,2):share+1;}
            if(path.Length>=3&&path[1]==':')return 3;
            if(path.StartsWith("/",StringComparison.Ordinal))return 1;
            return 0;
        }
        static string Head(string value,int length){return value.Substring(0,length);}
        // StringInfo keeps surrogate pairs and combining marks intact while trimming.
        internal static string Compact(string path,string prefix,double width,Func<string,double> measure) {
            string full=prefix+path;if(measure(full)<=width)return full;
            int slash=Math.Max(path.LastIndexOf('\\'),path.LastIndexOf('/')),root=RootEnd(path);
            string tail=slash>=0?path.Substring(slash):path;string start=path.Substring(0,Math.Min(root,path.Length));
            if(slash>=root&&measure(prefix+start+"…"+tail)<=width) {
                var stops=StringInfo.ParseCombiningCharacters(path.Substring(0,slash));int low=0,high=stops.Length;
                while(low<high){int mid=(low+high+1)/2;int count=mid==stops.Length?slash:stops[mid];if(measure(prefix+Head(path,count)+"…"+tail)<=width)low=mid;else high=mid-1;}
                int length=low==stops.Length?slash:stops[low];length=Math.Max(root,length);
                // Prefer whole leading directories to an arbitrary directory fragment.
                int boundary=Math.Max(path.LastIndexOf('\\',Math.Max(0,length-1)),path.LastIndexOf('/',Math.Max(0,length-1)));
                if(boundary>=root)length=boundary+1;
                return prefix+Head(path,length)+"…"+tail;
            }
            var elements=StringInfo.ParseCombiningCharacters(path);int keep=Math.Min(root,path.Length);
            if(keep==0&&elements.Length>0)keep=elements.Length>1?elements[1]:path.Length;
            start=Head(path,keep);
            if(measure(prefix+start+"…")>width){start=Head(path,Math.Min(3,path.Length));if(measure(prefix+start+"…")>width){start="";if(measure(prefix+"…")>width)return "…";}}
            int lo=0,hi=elements.Length;
            while(lo<hi){int mid=(lo+hi+1)/2;int index=elements.Length-mid;string suffix=index<elements.Length?path.Substring(elements[index]):"";if(measure(prefix+start+"…"+suffix)<=width)lo=mid;else hi=mid-1;}
            return prefix+start+"…"+(lo>0?path.Substring(elements[elements.Length-lo]):"");
        }
    }
}
