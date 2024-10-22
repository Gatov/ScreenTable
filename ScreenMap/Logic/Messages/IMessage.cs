using System.Drawing;

namespace ScreenMap.Logic.Messages;

public class IMessage
{
    
}

public class RevealAtMessage : IMessage
{
    public PointF Location { get; set; }
    public int BrushSize { get; set; }
    public bool Reveal { get; set; }
}

public class CenterAtMessage : IMessage
{
    public PointF Location { get; set; }
}

public class ZoomInMessage : IMessage
{
    private int Ticks { get; set; }
}

public class ClientRectangleMessage : IMessage
{
    public RectangleF Rectangle { get; set; }
}