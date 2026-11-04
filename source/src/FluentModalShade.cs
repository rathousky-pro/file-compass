using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
namespace FileCompass {
 internal sealed class FluentModalShade : Adorner {
  internal FluentModalShade(UIElement owner):base(owner){IsHitTestVisible=false;}
  protected override void OnRender(DrawingContext dc){dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(65,24,52,85)),null,new Rect(AdornedElement.RenderSize));}
 }
}
