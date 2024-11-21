using System.Drawing;

namespace ScreenMap.Logic.Messages;

public class IMessage
{
    
}

public class RevealAtMessage : IMessage
{
    public RevealAtMessage(PointF unscaledPoint, float brushSize, bool reveal)
    {
        Location = unscaledPoint;
        BrushSize = brushSize;
        Reveal = reveal;
    }

    public PointF Location { get; set; }
    public float BrushSize { get; set; }
    public bool Reveal { get; set; }
}

public class NewImageMessage : IMessage
{
    public string Name;
    public byte[] Data;

    public NewImageMessage(string name, byte[] data)
    {
        Name = name;
        Data = data;
    }
}
public class CenterAtMessage : IMessage
{
    public PointF Location { get; set; }
}

public class ZoomInMessage : IMessage
{
    public int Ticks { get; set; }
}

public class GridDataMessage : IMessage
{
    public int OffsetX { get; }
    public int OffsetY { get; }
    public float CellSize { get; }

    public GridDataMessage(int offsetX, int offsetY, float cellSize)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        CellSize = cellSize;
    }
}

public class ClientRectangleMessage : IMessage
{
    public RectangleF Rectangle { get; set; }
}

