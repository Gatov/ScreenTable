using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace ScreenMap.Controls;

public class ScrollableContainer : XtraScrollableControl
{
    protected override void OnMouseWheelCore(MouseEventArgs ev)
    {
        // do nothing
    }
}