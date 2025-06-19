using Microsoft.Win32;
using SLClient.Map.Classes;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SLClient.Map;

public partial class Map : Window
{
    private const string UnsavedMapLabel = "<ungespeicherte Karte>";
    private const int CellSize = 10;
    private const int CellSpacing = 10;
    private int TotalCellSize
    {
        get
        {
            return CellSize + CellSpacing;
        }
    }

    private int CurrentGridWidth { get; set; }
    private int CurrentGridHeight { get; set; }

    private DateTime LastClickTime { get; set; }
    private const int DoubleClickTime = 300;

    public int PosX { get; set; }
    public int PosY { get; set; }
    public int PosZ { get; set; } = 0;

    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;
    public int OffsetZ { get; set; } = 0;

    private bool isReadOnly = false;
    private bool isDirty = false;
    private string? lastSelectedMapName;

    private readonly string mapFolder = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SLClient",
        "Maps");

    private Rectangle? CurrentPosRect { get; set; }

    private readonly HashSet<(int x, int y, int z)> visitedCells = [];
    private readonly List<(int x1, int y1, int z1, int x2, int y2, int z2)> lines = [];

    public Map()
    {
        InitializeComponent();
        LoadSavedMaps();

        Loaded += Map_Loaded;
        MapCanvas.SizeChanged += MapCanvas_SizeChanged;

        Loaded += (s, e) =>
        {
            var screenWidth = SystemParameters.WorkArea.Width;
            var screenHeight = SystemParameters.WorkArea.Height;

            Left = screenWidth - ActualWidth;
            Top = screenHeight - ActualHeight;
        };
    }

    private void Map_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateGridSize();

        PosX = CurrentGridWidth / 2;
        PosY = CurrentGridHeight / 2;
        PosZ = 0;

        RedrawMap();
    }

    private void MapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGridSize();
        RedrawMap();
    }

    private void UpdateGridSize()
    {
        double availableWidth = MapCanvas.ActualWidth;
        double availableHeight = MapCanvas.ActualHeight;

        CurrentGridWidth = (int)(availableWidth / TotalCellSize);
        CurrentGridHeight = (int)(availableHeight / TotalCellSize);

        MapCanvas.Width = CurrentGridWidth * TotalCellSize;
        MapCanvas.Height = CurrentGridHeight * TotalCellSize;

        if (PosX >= CurrentGridWidth) PosX = CurrentGridWidth - 1;
        if (PosY >= CurrentGridHeight) PosY = CurrentGridHeight - 1;
    }

    public void RedrawMap()
    {
        MapCanvas.Children.Clear();

        foreach (var (fx, fy, fz, tx, ty, tz) in lines)
        {
            if ((fz == PosZ || tz == PosZ) && (IsCellVisible(fx, fy) || IsCellVisible(tx, ty)))
            {
                if (fz == PosZ && tz == PosZ)
                    DrawConnectionLine(fx, fy, tx, ty);
            }
        }

        foreach (var (x, y, z) in visitedCells)
        {
            if (z == PosZ && IsCellVisible(x, y))
                MarkVisited(x, y);
        }

        DrawCurrentPosition();
    }

    private bool IsCellVisible(int x, int y)
    {
        return x >= OffsetX && x < OffsetX + CurrentGridWidth &&
               y >= OffsetY && y < OffsetY + CurrentGridHeight;
    }

    private void DrawCurrentPosition()
    {
        if (CurrentPosRect != null)
            MapCanvas.Children.Remove(CurrentPosRect);

        CurrentPosRect = new Rectangle
        {
            Width = CellSize,
            Height = CellSize,
            Fill = Brushes.Red,
            Stroke = Brushes.DarkRed,
            StrokeThickness = 1
        };

        Canvas.SetLeft(CurrentPosRect, PosX * TotalCellSize);
        Canvas.SetTop(CurrentPosRect, PosY * TotalCellSize);
        MapCanvas.Children.Add(CurrentPosRect);
    }

    private void MarkVisited(int x, int y)
    {
        double left = (x - OffsetX) * TotalCellSize;
        double top = (y - OffsetY) * TotalCellSize;

        var rect = new Rectangle
        {
            Width = CellSize,
            Height = CellSize,
            Fill = Brushes.LightGray,
            Stroke = Brushes.Gray,
            StrokeThickness = 1
        };
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        MapCanvas.Children.Add(rect);

        var (hasUp, hasDown) = GetZConnections(x, y, PosZ);

        if (hasUp)
        {
            var greenTriangle = new Polygon
            {
                Fill = Brushes.LimeGreen,
                StrokeThickness = 0,
                Points =
        [
            new Point(0, 0),
            new Point(CellSize, 0),
            new Point(0, CellSize)
        ]
            };
            Canvas.SetLeft(greenTriangle, left);
            Canvas.SetTop(greenTriangle, top);
            MapCanvas.Children.Add(greenTriangle);
        }

        if (hasDown)
        {
            var blueTriangle = new Polygon
            {
                Fill = Brushes.DodgerBlue,
                StrokeThickness = 0,
                Points =
        [
            new Point(CellSize, CellSize),
            new Point(0, CellSize),
            new Point(CellSize, 0)
        ]
            };
            Canvas.SetLeft(blueTriangle, left);
            Canvas.SetTop(blueTriangle, top);
            MapCanvas.Children.Add(blueTriangle);
        }
    }

    private (bool hasUp, bool hasDown) GetZConnections(int x, int y, int z)
    {
        bool up = false;
        bool down = false;

        foreach (var (x1, y1, z1, x2, y2, z2) in lines)
        {
            if ((x1 == x && y1 == y && z1 == z && z2 == z + 1) ||
                (x2 == x && y2 == y && z2 == z && z1 == z + 1))
            {
                up = true;
            }

            if ((x1 == x && y1 == y && z1 == z && z2 == z - 1) ||
                (x2 == x && y2 == y && z2 == z && z1 == z - 1))
            {
                down = true;
            }
        }
        return (up, down);
    }

    private void DrawConnectionLine(int fromX, int fromY, int toX, int toY)
    {
        double startX = (fromX - OffsetX) * TotalCellSize + CellSize / 2.0;
        double startY = (fromY - OffsetY) * TotalCellSize + CellSize / 2.0;
        double endX = (toX - OffsetX) * TotalCellSize + CellSize / 2.0;
        double endY = (toY - OffsetY) * TotalCellSize + CellSize / 2.0;

        var line = new Line
        {
            X1 = startX,
            Y1 = startY,
            X2 = endX,
            Y2 = endY,
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };

        MapCanvas.Children.Add(line);
    }

    public void Move(string direction)
    {
        if (!DirectionParser.TryParseDirection(direction, out int dx, out int dy, out int dz))
            return;

        int globalOldX = OffsetX + PosX;
        int globalOldY = OffsetY + PosY;
        int globalOldZ = OffsetZ + PosZ;

        int globalNewX = globalOldX + dx;
        int globalNewY = globalOldY + dy;
        int globalNewZ = globalOldZ + dz;

        if (isReadOnly)
        {
            bool visited = visitedCells.Contains((globalNewX, globalNewY, globalNewZ));
            bool connected = lines.Exists(l =>
                (l.x1 == globalOldX && l.y1 == globalOldY && l.z1 == globalOldZ &&
                 l.x2 == globalNewX && l.y2 == globalNewY && l.z2 == globalNewZ) ||
                (l.x1 == globalNewX && l.y1 == globalNewY && l.z1 == globalNewZ &&
                 l.x2 == globalOldX && l.y2 == globalOldY && l.z2 == globalOldZ));

            if (!(visited && connected))
                return;
        }

        PosZ = globalNewZ;

        OffsetX = globalNewX - CurrentGridWidth / 2;
        OffsetY = globalNewY - CurrentGridHeight / 2;

        PosX = CurrentGridWidth / 2;
        PosY = CurrentGridHeight / 2;

        bool wasNew = visitedCells.Add((globalOldX, globalOldY, globalOldZ));
        wasNew |= visitedCells.Add((globalNewX, globalNewY, globalNewZ));

        if (!isReadOnly && AddLine(globalOldX, globalOldY, globalOldZ, globalNewX, globalNewY, globalNewZ))
        {
            wasNew = true;
        }

        if (globalOldZ != globalNewZ)
            wasNew = true;

        if (wasNew)
            isDirty = true;

        RedrawMap();
    }

    private bool AddLine(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        var newLine = (x1, y1, z1, x2, y2, z2);
        if (lines.Contains(newLine))
            return false;

        lines.Add(newLine);
        return true;
    }

    private void ReadOnlyCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        isReadOnly = true;
    }

    private void ReadOnlyCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        isReadOnly = false;
    }

    #region Save
    private void SaveMapButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(mapFolder))
                Directory.CreateDirectory(mapFolder);

            var dlg = new SaveFileDialog
            {
                InitialDirectory = mapFolder,
                Filter = "SLClient Map-Dateien (*.slmap)|*.slmap",
                DefaultExt = ".slmap",
                AddExtension = true,
                Title = "Karte speichern unter"
            };

            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;

                SaveMapToFile(filePath);

                string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
                MessageBox.Show($"Karte erfolgreich gespeichert: {fileNameWithoutExtension}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Speichern der Karte:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveMapToFile(string filePath)
    {
        string mapData = SerializeMap();

        File.WriteAllText(filePath, mapData);

        isDirty = false;
        lastSelectedMapName = System.IO.Path.GetFileName(filePath);
        LoadSavedMaps(true);
        UpdateWindowTitle();
    }

    private string SerializeMap()
    {
        var mapSaveData = new MapSaveData
        {
            CurrentPosition = new PositionData { X = PosX + OffsetX, Y = PosY + OffsetY, Z = PosZ },
            VisitedCells = [],
            Lines = []
        };

        foreach (var (x, y, z) in visitedCells)
            mapSaveData.VisitedCells.Add(new CellData { X = x, Y = y, Z = z });

        foreach (var (x1, y1, z1, x2, y2, z2) in lines)
            mapSaveData.Lines.Add(new LineData { X1 = x1, Y1 = y1, Z1 = z1, X2 = x2, Y2 = y2, Z2 = z2 });

        var options = new JsonSerializerOptions { WriteIndented = true };

        return JsonSerializer.Serialize(mapSaveData, options);
    }

    private void LoadMapButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(mapFolder))
                Directory.CreateDirectory(mapFolder);

            var dlg = new OpenFileDialog
            {
                InitialDirectory = mapFolder,
                Filter = "SLClient Map-Dateien (*.slmap)|*.slmap",
                DefaultExt = ".slmap",
                Title = "Karte laden"
            };

            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;
                LoadMapFromFile(filePath);

                string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
                MessageBox.Show($"Karte erfolgreich geladen: {fileNameWithoutExtension}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Laden der Karte:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadMapFromFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var mapSaveData = JsonSerializer.Deserialize<MapSaveData>(json);

        if (mapSaveData == null)
            throw new Exception("Ungültige Kartendatei.");

        isDirty = false;
        lastSelectedMapName = System.IO.Path.GetFileName(filePath);

        visitedCells.Clear();
        lines.Clear();

        foreach (var cell in mapSaveData.VisitedCells)
            visitedCells.Add((cell.X, cell.Y, cell.Z));

        foreach (var line in mapSaveData.Lines)
            lines.Add((line.X1, line.Y1, line.Z1, line.X2, line.Y2, line.Z2));

        PosZ = mapSaveData.CurrentPosition.Z;

        OffsetX = mapSaveData.CurrentPosition.X - CurrentGridWidth / 2;
        OffsetY = mapSaveData.CurrentPosition.Y - CurrentGridHeight / 2;

        PosX = CurrentGridWidth / 2;
        PosY = CurrentGridHeight / 2;

        RedrawMap();
        UpdateWindowTitle();
    }

    private void NewMapButton_Click(object sender, RoutedEventArgs e)
    {
        if (isDirty)
        {
            var result = MessageBox.Show(
                "Es sind ungespeicherte Änderungen vorhanden. Möchten Sie die aktuelle Karte speichern?",
                "Neue Karte erstellen",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SaveMapButton_Click(this, new RoutedEventArgs());
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return; // Abbrechen
            }
            UpdateWindowTitle();
        }

        // Leeren der Daten
        visitedCells.Clear();
        lines.Clear();
        isDirty = false;
        lastSelectedMapName = null;

        // Zurück zur Ausgangsposition
        PosX = CurrentGridWidth / 2;
        PosY = CurrentGridHeight / 2;
        PosZ = 0;
        OffsetX = 0;
        OffsetY = 0;
        OffsetZ = 0;

        // Kombobox zurücksetzen
        SavedMapsComboBox.SelectionChanged -= SavedMapsComboBox_SelectionChanged;
        SavedMapsComboBox.SelectedItem = UnsavedMapLabel;
        SavedMapsComboBox.SelectionChanged += SavedMapsComboBox_SelectionChanged;

        RedrawMap();
    }

    private void LoadSavedMaps(bool resetSelection = false)
    {
        if (!Directory.Exists(mapFolder))
            Directory.CreateDirectory(mapFolder);

        var files = Directory.GetFiles(mapFolder, "*.slmap");
        var fileNames = files.Select(f => System.IO.Path.GetFileName(f)).ToList();

        var items = new List<string> { UnsavedMapLabel };
        items.AddRange(fileNames);

        SavedMapsComboBox.ItemsSource = items;
        if (!resetSelection)
            SavedMapsComboBox.SelectedIndex = 0;
    }

    private void SavedMapsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SavedMapsComboBox.SelectedItem is not string selectedMap)
            return;

        if (selectedMap == lastSelectedMapName)
            return;

        if (selectedMap == UnsavedMapLabel)
        {
            // Verhalten wie Klick auf "Neu"-Button
            NewMapButton_Click(sender, new RoutedEventArgs());
            return;
        }

        if (isDirty)
        {
            var result = MessageBox.Show(
                "Es sind Änderungen vorhanden. Sollen diese gespeichert werden?",
                "Änderungen speichern?",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (lastSelectedMapName != null && lastSelectedMapName != UnsavedMapLabel)
                {
                    string existingPath = System.IO.Path.Combine(mapFolder, lastSelectedMapName);
                    SaveMapToFile(existingPath);
                }
                else
                {
                    SaveMapButton_Click(this, new RoutedEventArgs());
                }
            }
            else if (result == MessageBoxResult.Cancel)
            {
                SavedMapsComboBox.SelectionChanged -= SavedMapsComboBox_SelectionChanged;
                SavedMapsComboBox.SelectedItem = lastSelectedMapName ?? UnsavedMapLabel;
                SavedMapsComboBox.SelectionChanged += SavedMapsComboBox_SelectionChanged;
                return;
            }
        }

        if (selectedMap != UnsavedMapLabel)
        {
            string filePath = System.IO.Path.Combine(mapFolder, selectedMap);
            if (File.Exists(filePath))
            {
                LoadMapFromFile(filePath);
            }
            else
            {
                MessageBox.Show($"Datei nicht gefunden: {selectedMap}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        lastSelectedMapName = selectedMap;
    }
    #endregion

    #region MapViewNavigation

    private void ScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        OffsetX -= 1;
        RedrawMap();
    }

    private void ScrollRight_Click(object sender, RoutedEventArgs e)
    {
        OffsetX += 1;
        RedrawMap();
    }

    private void ScrollUp_Click(object sender, RoutedEventArgs e)
    {
        OffsetY -= 1;
        RedrawMap();
    }

    private void ScrollDown_Click(object sender, RoutedEventArgs e)
    {
        OffsetY += 1;
        RedrawMap();
    }

    private void ScrollZUp_Click(object sender, RoutedEventArgs e)
    {
        PosZ += 1;
        RedrawMap();
        UpdateWindowTitle();
    }

    private void ScrollZDown_Click(object sender, RoutedEventArgs e)
    {
        PosZ -= 1;
        RedrawMap();
        UpdateWindowTitle();
    }

    /// <summary>
    /// Zeigt den aktuellen Z-Wert im Fenster-Titel an.
    /// </summary>
    public void UpdateWindowTitle()
    {
        string mapName = string.IsNullOrWhiteSpace(lastSelectedMapName) ? "Unbekannte Karte" : System.IO.Path.GetFileNameWithoutExtension(lastSelectedMapName);

        Title = $"Karte: {mapName} – Z: {PosZ}";
    }

    #endregion

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        var diff = (now - LastClickTime).TotalMilliseconds;

        if (diff < DoubleClickTime)
        {
            Point clickPos = e.GetPosition(MapCanvas);

            // Lokale Koordinaten im sichtbaren Canvas-Bereich
            int localX = (int)(clickPos.X / TotalCellSize);
            int localY = (int)(clickPos.Y / TotalCellSize);

            // Globale Koordinaten berechnen (für VisitedCells etc.)
            int globalX = localX + OffsetX;
            int globalY = localY + OffsetY;

            // Spieler-Position relativ zum neuen Offset setzen
            PosX = CurrentGridWidth / 2;
            PosY = CurrentGridHeight / 2;

            // Neues Offset zentriert den Spieler auf die neue globale Koordinate
            OffsetX = globalX - PosX;
            OffsetY = globalY - PosY;

            // Karte neu zeichnen
            RedrawMap();
            UpdateWindowTitle();
        }

        LastClickTime = now;
    }
}
