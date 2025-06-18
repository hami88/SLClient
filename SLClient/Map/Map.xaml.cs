using Microsoft.Win32;
using SLClient.Map.Classes;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SLClient.Map;

public partial class Map : Window
{
    private const int CellSize = 10;
    private const int CellSpacing = 10;
    private int totalCellSize => CellSize + CellSpacing;

    private int currentGridWidth;
    private int currentGridHeight;

    private int posX;
    private int posY;
    private int posZ = 0;       // Neue Z-Ebene (Höhe)

    private int offsetX = 0;
    private int offsetY = 0;
    private int offsetZ = 0;    // Offset für Ebene (optional, falls Scroll auf Z)

    private bool isReadOnly = false;

    private readonly string mapFolder = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SLClient",
        "Maps");

    private Rectangle? currentPosRect;

    // Zellen-Set mit Z-Koordinate
    private readonly HashSet<(int x, int y, int z)> visitedCells = [];

    // Linien mit Z-Koordinate für Start und Ende
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

        posX = currentGridWidth / 2;
        posY = currentGridHeight / 2;
        posZ = 0;

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

        currentGridWidth = (int)(availableWidth / totalCellSize);
        currentGridHeight = (int)(availableHeight / totalCellSize);

        MapCanvas.Width = currentGridWidth * totalCellSize;
        MapCanvas.Height = currentGridHeight * totalCellSize;

        if (posX >= currentGridWidth) posX = currentGridWidth - 1;
        if (posY >= currentGridHeight) posY = currentGridHeight - 1;
    }

    private void RedrawMap()
    {
        MapCanvas.Children.Clear();

        // Zuerst: Linien zeichnen (damit sie im Hintergrund liegen)
        foreach (var (fx, fy, fz, tx, ty, tz) in lines)
        {
            if ((fz == posZ || tz == posZ) && (IsCellVisible(fx, fy) || IsCellVisible(tx, ty)))
            {
                if (fz == posZ && tz == posZ)
                    DrawConnectionLine(fx, fy, tx, ty);
            }
        }

        // Danach: alle Zellen (damit sie im Vordergrund sind)
        foreach (var (x, y, z) in visitedCells)
        {
            if (z == posZ && IsCellVisible(x, y))
                MarkVisited(x, y);
        }

        DrawCurrentPosition();
    }


    private bool IsCellVisible(int x, int y)
    {
        return x >= offsetX && x < offsetX + currentGridWidth &&
               y >= offsetY && y < offsetY + currentGridHeight;
    }

    private void DrawCurrentPosition()
    {
        if (currentPosRect != null)
            MapCanvas.Children.Remove(currentPosRect);

        currentPosRect = new Rectangle
        {
            Width = CellSize,
            Height = CellSize,
            Fill = Brushes.Red,
            Stroke = Brushes.DarkRed,
            StrokeThickness = 1
        };

        Canvas.SetLeft(currentPosRect, posX * totalCellSize);
        Canvas.SetTop(currentPosRect, posY * totalCellSize);
        MapCanvas.Children.Add(currentPosRect);
    }

    private void MarkVisited(int x, int y)
    {
        double left = (x - offsetX) * totalCellSize;
        double top = (y - offsetY) * totalCellSize;

        // Hintergrundzelle (hellgrau)
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

        var (hasUp, hasDown) = GetZConnections(x, y, posZ);

        // Linkes Dreieck (oben, ↖) – GRÜN
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

        // Rechtes Dreieck (unten, ↗) – BLAU
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
            // Verbindung von (x,y,z) nach oben (z+1)
            if ((x1 == x && y1 == y && z1 == z && z2 == z + 1) ||
                (x2 == x && y2 == y && z2 == z && z1 == z + 1))
            {
                up = true;
            }

            // Verbindung von (x,y,z) nach unten (z-1)
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
        double startX = (fromX - offsetX) * totalCellSize + CellSize / 2.0;
        double startY = (fromY - offsetY) * totalCellSize + CellSize / 2.0;
        double endX = (toX - offsetX) * totalCellSize + CellSize / 2.0;
        double endY = (toY - offsetY) * totalCellSize + CellSize / 2.0;

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
        if (!TryParseDirection(direction, out int dx, out int dy, out int dz))
            return;

        int globalOldX = offsetX + posX;
        int globalOldY = offsetY + posY;
        int globalOldZ = offsetZ + posZ;

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

        // Neue globale Position setzen
        posZ = globalNewZ;

        // Zentriere neue globale Position im aktuellen Grid
        offsetX = globalNewX - currentGridWidth / 2;
        offsetY = globalNewY - currentGridHeight / 2;

        posX = currentGridWidth / 2;
        posY = currentGridHeight / 2;

        visitedCells.Add((globalOldX, globalOldY, globalOldZ));
        visitedCells.Add((globalNewX, globalNewY, globalNewZ));

        if (!isReadOnly)
            AddLine(globalOldX, globalOldY, globalOldZ, globalNewX, globalNewY, globalNewZ);

        RedrawMap();
    }

    private void AddLine(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        if (lines.Contains((x1, y1, z1, x2, y2, z2))) return;
        lines.Add((x1, y1, z1, x2, y2, z2));
    }

    private void ReadOnlyCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        isReadOnly = true;
    }

    private void ReadOnlyCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        isReadOnly = false;
    }

    private static bool TryParseDirection(string input, out int dx, out int dy, out int dz)
    {
        dx = dy = dz = 0;
        string dir = input.ToLowerInvariant();

        // Volle 3D-Diagonalrichtungen
        var map3D = new Dictionary<string, (int dx, int dy, int dz)>
        {
            // Nordost
            ["noob"] = (1, -1, 1),
            ["neu"] = (1, -1, 1),
            ["nordostoben"] = (1, -1, 1),
            ["northeastup"] = (1, -1, 1),
            ["nou"] = (1, -1, -1),
            ["ned"] = (1, -1, -1),
            ["nordostunten"] = (1, -1, -1),
            ["northeastdown"] = (1, -1, -1),

            // Nordwest
            ["nwup"] = (-1, -1, 1),
            ["nwob"] = (-1, -1, 1),
            ["nordwestoben"] = (-1, -1, 1),
            ["northwestup"] = (-1, -1, 1),
            ["nwdown"] = (-1, -1, -1),
            ["nordwestunten"] = (-1, -1, -1),
            ["northwestdown"] = (-1, -1, -1),

            // Südost
            ["soob"] = (1, 1, 1),
            ["seup"] = (1, 1, 1),
            ["südostoben"] = (1, 1, 1),
            ["southeastup"] = (1, 1, 1),
            ["sou"] = (1, 1, -1),
            ["sedown"] = (1, 1, -1),
            ["südostunten"] = (1, 1, -1),
            ["southeastdown"] = (1, 1, -1),

            // Südwest
            ["swob"] = (-1, 1, 1),
            ["swup"] = (-1, 1, 1),
            ["südwestoben"] = (-1, 1, 1),
            ["southwestup"] = (-1, 1, 1),
            ["swu"] = (-1, 1, -1),
            ["swdown"] = (-1, 1, -1),
            ["südwestunten"] = (-1, 1, -1),
            ["southwestdown"] = (-1, 1, -1),
        };

        if (map3D.TryGetValue(dir, out var delta3D))
        {
            dx = delta3D.dx;
            dy = delta3D.dy;
            dz = delta3D.dz;
            return true;
        }

        // 2D-Richtungen (inkl. Kurzformen)
        var map2D = new Dictionary<string, (int dx, int dy)>
        {
            ["n"] = (0, -1),
            ["north"] = (0, -1),
            ["norden"] = (0, -1),
            ["s"] = (0, 1),
            ["south"] = (0, 1),
            ["süden"] = (0, 1),
            ["sued"] = (0, 1),
            ["sueden"] = (0, 1),
            ["e"] = (1, 0),
            ["east"] = (1, 0),
            ["osten"] = (1, 0),
            ["o"] = (1, 0),
            ["w"] = (-1, 0),
            ["west"] = (-1, 0),
            ["westen"] = (-1, 0),

            ["ne"] = (1, -1),
            ["no"] = (1, -1),
            ["nordost"] = (1, -1),
            ["nordosten"] = (1, -1),
            ["northeast"] = (1, -1),
            ["nw"] = (-1, -1),
            ["nordwest"] = (-1, -1),
            ["nordwesten"] = (-1, -1),
            ["northwest"] = (-1, -1),
            ["se"] = (1, 1),
            ["so"] = (1, 1),
            ["südost"] = (1, 1),
            ["südosten"] = (1, 1),
            ["southeast"] = (1, 1),
            ["sw"] = (-1, 1),
            ["südwest"] = (-1, 1),
            ["südwesten"] = (-1, 1),
            ["southwest"] = (-1, 1)
        };

        if (map2D.TryGetValue(dir, out var delta2D))
        {
            dx = delta2D.dx;
            dy = delta2D.dy;
            return true;
        }

        // Z-Richtungen
        if (dir == "up" || dir == "ob" || dir == "oben") { dz = 1; return true; }
        if (dir == "down" || dir == "u" || dir == "unten") { dz = -1; return true; }

        return false;
    }

    #region Save
    private void SaveMapButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ordner anlegen, falls nicht vorhanden
            if (!Directory.Exists(mapFolder))
                Directory.CreateDirectory(mapFolder);

            // SaveFileDialog konfigurieren
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

                // Map-Daten serialisieren und speichern
                SaveMapToFile(filePath);

                // Nur Dateiname ohne Extension in der MessageBox anzeigen
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
        // Hier deine Map-Daten serialisieren und in die Datei schreiben
        // Beispiel: speichere einfach "Map-Daten" als Text
        // Ersetze diesen Teil durch deine eigene Speichermethode!

        string mapData = SerializeMap(); // Deine Methode, die die Map in Text oder Binary umwandelt

        File.WriteAllText(filePath, mapData);
    }

    private string SerializeMap()
    {
        var mapSaveData = new MapSaveData
        {
            CurrentPosition = new PositionData { X = posX + offsetX, Y = posY + offsetY, Z = posZ },
            VisitedCells = [],
            Lines = []
        };

        // VisitedCells übertragen
        foreach (var (x, y, z) in visitedCells)
        {
            mapSaveData.VisitedCells.Add(new CellData { X = x, Y = y, Z = z });
        }

        // Lines übertragen
        foreach (var (x1, y1, z1, x2, y2, z2) in lines)
        {
            mapSaveData.Lines.Add(new LineData { X1 = x1, Y1 = y1, Z1 = z1, X2 = x2, Y2 = y2, Z2 = z2 });
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

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

                // Nur Dateiname ohne Extension in der MessageBox anzeigen
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

        // Setze die geladenen Daten

        visitedCells.Clear();
        lines.Clear();

        foreach (var cell in mapSaveData.VisitedCells)
            visitedCells.Add((cell.X, cell.Y, cell.Z));

        foreach (var line in mapSaveData.Lines)
            lines.Add((line.X1, line.Y1, line.Z1, line.X2, line.Y2, line.Z2));

        // Position einstellen (relative zu offset)
        posZ = mapSaveData.CurrentPosition.Z;

        // Setze offset so, dass aktuelle Position in der Mitte ist
        offsetX = mapSaveData.CurrentPosition.X - currentGridWidth / 2;
        offsetY = mapSaveData.CurrentPosition.Y - currentGridHeight / 2;

        posX = currentGridWidth / 2;
        posY = currentGridHeight / 2;

        RedrawMap();
    }

    private void LoadSavedMaps()
    {
        if (!Directory.Exists(mapFolder))
            Directory.CreateDirectory(mapFolder);

        var files = Directory.GetFiles(mapFolder, "*.slmap");

        // Nur Dateinamen ohne Pfad
        var fileNames = files.Select(f => System.IO.Path.GetFileName(f)).ToList();

        // ComboBox mit <ungespeicherte Karte> initialisieren
        var items = new List<string> { "<ungespeicherte Karte>" };
        items.AddRange(fileNames);

        SavedMapsComboBox.ItemsSource = items;

        // <ungespeicherte Karte> als Standard-Auswahl, wenn keine andere Karte ausgewählt ist
        SavedMapsComboBox.SelectedIndex = 0;
    }

    private void SavedMapsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SavedMapsComboBox.SelectedItem is string selectedMap)
        {
            if (selectedMap == "<ungespeicherte Karte>")
                return;

            string filePath = System.IO.Path.Combine(mapFolder, selectedMap);

            if (File.Exists(filePath))
                LoadMapFromFile(filePath);
            else
                MessageBox.Show($"Datei nicht gefunden: {selectedMap}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    #endregion
}
