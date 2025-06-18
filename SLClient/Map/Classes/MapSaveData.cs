namespace SLClient.Map.Classes;

public class MapSaveData
{
    public PositionData CurrentPosition { get; set; }
    public List<CellData> VisitedCells { get; set; } = [];
    public List<LineData> Lines { get; set; } = [];
}
