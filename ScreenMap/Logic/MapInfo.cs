using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

namespace ScreenMap.Logic;

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

    [DataMember]
    public Point Center { get; set; } = new Point(100, 100);
    [DataMember]
    public List<Operation> History { get; set; } = new List<Operation>();
}

[DataContract]
public class Operation
{
    [DataMember]
    public float X;
    [DataMember]
    public float Y;
    [DataMember]
    public OperationType Type;
    [DataMember]
    public int Value;
}

public enum OperationType
{
    [DataMember(Name="R")]
    RevealAt,
    [DataMember(Name="H")]
    Hide
}
