namespace SLClient.Map;

public static class DirectionParser
{
    public static bool TryParseDirection(string input, out int dx, out int dy, out int dz)
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
}
