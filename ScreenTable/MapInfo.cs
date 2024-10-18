using System.Runtime.Serialization;

namespace ScreenTable;

[DataContract]
public class MapInfo
{
    [DataMember]
    public int OffsetX = 5;
    [DataMember]
    public int OffsetY = 5;
    [DataMember]
    public float CellSize = 48;
    [DataMember]
    public string FileName = null;

    public Point Center { get; set; } = new Point(100, 100);
}