using System.Windows;

namespace SLClient.Map;

public class MapNavigationModel
{
    private readonly Map map;

    public MapNavigationModel(Map map)
    {
        this.map = map;
    }

    public void ScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        map.OffsetX -= 1;
        map.RedrawMap();
    }

    public void ScrollRight_Click(object sender, RoutedEventArgs e)
    {
        map.OffsetX += 1;
        map.RedrawMap();
    }

    public void ScrollUp_Click(object sender, RoutedEventArgs e)
    {
        map.OffsetY -= 1;
        map.RedrawMap();
    }

    public void ScrollDown_Click(object sender, RoutedEventArgs e)
    {
        map.OffsetY += 1;
        map.RedrawMap();
    }

    public void ScrollZUp_Click(object sender, RoutedEventArgs e)
    {
        map.PosZ += 1;
        map.RedrawMap();
        map.UpdateWindowTitle();
    }

    public void ScrollZDown_Click(object sender, RoutedEventArgs e)
    {
        map.PosZ -= 1;
        map.RedrawMap();
        map.UpdateWindowTitle();
    }
}
