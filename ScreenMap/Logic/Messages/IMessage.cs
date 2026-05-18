using System.Drawing;

namespace ScreenMap.Logic.Messages;

public class MapMessage
{
}

public class RevealAtMessage : MapMessage
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

public class MarkAtMessage : MapMessage
{
    public MarkAtMessage(PointF unscaledPoint, int radius, int color, int id)
    {
        Location = unscaledPoint;
        Radius = radius;
        ArgbColor = color;
        Id = id;
    }

    public PointF Location { get; set; }
    public int Radius { get; set; }
    public int ArgbColor { get; set; }
    public int Id { get; set; }
}
public class NewImageMessage : MapMessage
{
    public string Name;
    public byte[] Data;

    public NewImageMessage(string name, byte[] data)
    {
        Name = name;
        Data = data;
    }
}
public class CenterAtMessage : MapMessage
{
    public PointF Location { get; set; }
}

public class ZoomInMessage : MapMessage
{
    public int Ticks { get; set; }
}

public class GridDataMessage : MapMessage
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

public class ClientRectangleMessage : MapMessage
{
    public RectangleF Rectangle { get; set; }
}

public class GridVisibilityMessage : MapMessage
{
    public bool ShowGrid { get; }

    public GridVisibilityMessage(bool showGrid)
    {
        ShowGrid = showGrid;
    }
}

