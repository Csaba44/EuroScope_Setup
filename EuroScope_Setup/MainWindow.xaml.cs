using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace EuroScope_Setup
{
    public partial class MainWindow : Window
    {
        public static Coordinate mapCenterCoordinate = new Coordinate(47.43566192997341, 19.25721003945266);
        public static double mapRadiusMeters = 2000;
        public static string[] regionsToShow;

        public static string sectorFilePath;
        public static string asmgcsFilePath;

        public static List<RegionColor> regionColors;
        public static List<SectorRegion> sectorRegions;
        public static List<PolygonRegion> polygonRegions = new List<PolygonRegion>();

        public static SolidColorBrush nonClosedColor = new SolidColorBrush(Color.FromArgb(90, 255, 99, 99));

        public class RegionColor
        {
            public string name { get; private set; }
            public Color color { get; private set; }

            private Color IntToColor(int value)
            {
                byte r = (byte)(value & 0xFF);
                byte g = (byte)((value >> 8) & 0xFF);
                byte b = (byte)((value >> 16) & 0xFF);
                return Color.FromRgb(r, g, b);
            }

            public RegionColor(string name, int colorInt)
            {
                this.name = name;
                this.color = IntToColor(colorInt);
            }
        }

        public class Coordinate
        {
            public double Lat { get; set; }
            public double Lon { get; set; }

            public Coordinate(double lat, double lon)
            {
                Lat = lat;
                Lon = lon;
            }
        }

        public class SectorRegion
        {
            public string name { get; private set; }
            public Color color { get; private set; }
            public List<Coordinate> coordinates { get; private set; } = new List<Coordinate>();

            public SectorRegion(string name, Color color)
            {
                this.name = name;
                this.color = color;
            }   

            public void addCoordinate(string coordinate)
            {
                Coordinate coord = ParseCoordinate(coordinate);
                coordinates.Add(coord); 
            }
        }

        public class PolygonRegion
        {
            public string name { get; private set; }
            public Polygon Polygon { get; private set; }

            public PolygonRegion(string name, Polygon polygon)
            {
                this.name = name;
                this.Polygon = polygon;
            }
        }

        // Function to convert DMS (Degrees, Minutes, Seconds) to decimal degrees
        public static double DmsToDecimal(string dmsString)
        {
            // Remove N/S/E/W prefix and split by dots
            string cleanString = dmsString.Substring(1);
            string[] parts = cleanString.Split('.');

            if (parts.Length != 4)
                throw new ArgumentException("Invalid DMS format");

            // Parse degrees, minutes, seconds
            int degrees = int.Parse(parts[0]);
            int minutes = int.Parse(parts[1]);
            double seconds = double.Parse(parts[2] + "." + parts[3], CultureInfo.InvariantCulture);

            // Calculate decimal degrees
            double decimalDegrees = degrees + (minutes / 60.0) + (seconds / 3600.0);

            // Apply sign based on direction (N/E = positive, S/W = negative)
            if (dmsString.StartsWith("S") || dmsString.StartsWith("W"))
                decimalDegrees = -decimalDegrees;

            return decimalDegrees;
        }

        // Function to parse the coordinate data
        public static Coordinate? ParseCoordinate(string coordinateLine)
        {
            if (string.IsNullOrWhiteSpace(coordinateLine))
                return null;

            string[] parts = coordinateLine.Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return null;

            string latString = parts[parts.Length - 2];
            string lonString = parts[parts.Length - 1];

            if ((latString.StartsWith("N") || latString.StartsWith("S")) &&
                (lonString.StartsWith("E") || lonString.StartsWith("W")))
            {
                try
                {
                    double lat = DmsToDecimal(latString);
                    double lon = DmsToDecimal(lonString);
                    return new Coordinate(lat, lon);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing coordinate: {latString} {lonString} - {ex.Message}");
                }
            }

            return null;
        }


        // Function to convert geographic coordinates to pixel coordinates
        (double px, double py) ToPixelEquirectangular(
            double lat, double lon,
            double centerLat, double centerLon,
            int viewportWidth, int viewportHeight,
            double mapRadiusMeters)
        {
            // Calculate meters per degree at center latitude
            void MetersPerDegree(double latDeg, out double mPerDegLat, out double mPerDegLon)
            {
                double latRad = Math.PI * latDeg / 180.0;
                mPerDegLat = 111132.954 - 559.822 * Math.Cos(2 * latRad) + 1.175 * Math.Cos(4 * latRad);
                mPerDegLon = (Math.PI / 180.0) * 6378137.0 * Math.Cos(latRad);
            }

            // Calculate distance from center in meters
            MetersPerDegree(centerLat, out double mLat, out double mLon);
            double dxMeters = (lon - centerLon) * mLon; // meters east
            double dyMeters = (lat - centerLat) * mLat; // meters north

            // Calculate meters per pixel
            double halfViewPx = Math.Min(viewportWidth, viewportHeight) / 2.0;
            double metersPerPixel = mapRadiusMeters / halfViewPx;

            // Convert to pixel coordinates
            double px = viewportWidth / 2.0 + (dxMeters / metersPerPixel);
            double py = viewportHeight / 2.0 - (dyMeters / metersPerPixel);

            return (px, py);
        }


        public Coordinate FromPixelEquirectangular(
    double px, double py,
    double centerLat, double centerLon,
    int viewportWidth, int viewportHeight,
    double mapRadiusMeters)
        {
            // helper: meters per degree
            void MetersPerDegree(double latDeg, out double mPerDegLat, out double mPerDegLon)
            {
                double latRad = Math.PI * latDeg / 180.0;
                mPerDegLat = 111132.954 - 559.822 * Math.Cos(2 * latRad) + 1.175 * Math.Cos(4 * latRad);
                mPerDegLon = (Math.PI / 180.0) * 6378137.0 * Math.Cos(latRad);
            }

            // meters per degree at center
            MetersPerDegree(centerLat, out double mLat, out double mLon);

            // meters per pixel
            double halfViewPx = Math.Min(viewportWidth, viewportHeight) / 2.0;
            double metersPerPixel = mapRadiusMeters / halfViewPx;

            // dx/dy in meters relative to center
            double dxMeters = (px - viewportWidth / 2.0) * metersPerPixel;
            double dyMeters = (viewportHeight / 2.0 - py) * metersPerPixel;

            // convert back to lat/lon
            double lat = centerLat + (dyMeters / mLat);
            double lon = centerLon + (dxMeters / mLon);

            return new Coordinate(lat, lon);
        }

        public static List<SectorRegion> readCoordinates(string filename, List<RegionColor> regionColors)
        {
            /*string[] coordinateData = File.ReadAllLines(filename);
            return coordinateData;*/

            List<SectorRegion> sectorRegions = new List<SectorRegion>();

            StreamReader reader = new StreamReader(filename);

            bool inRegions = false; 

            SectorRegion currentRegion = null;

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (line.Contains("[REGIONS]")) inRegions = true;
                
                if (inRegions)
                {
                    // New sector region beginning
                    if (line.Contains("REGIONNAME"))
                    {
                        if (currentRegion != null)
                        {
                            sectorRegions.Add(currentRegion);
                        }

                        string regionName = line.Split(' ')[2];

                        string line2 = reader.ReadLine();

                        string[] line2Split = line2.Split(' ');
                        string regionColor = line2Split[0];

                        regionColor = regionColor.Replace(" ", "");
                        regionColor = regionColor.Replace("COLOR_", "");

                        Color regionFillColor;

                        foreach (var rColor in regionColors)
                        {
                            if (rColor.name == regionColor)
                            {
                                regionFillColor = rColor.color;
                                break;
                            }
                        }

                        string firstCoordinate = "";

                        for (int i = 1; i < line2Split.Length; i++)
                        {
                            if (line2Split[i].StartsWith("N") || line2Split[i].StartsWith("S"))
                            {
                                firstCoordinate = line2Split[i] + " " + line2Split[i + 1];

                                break;
                            }
                        }

                        currentRegion = new SectorRegion(regionName, regionFillColor);
                        currentRegion.addCoordinate(firstCoordinate);
                    } else
                    {
                        if (currentRegion != null)
                        {
                            string[] lineSplit = line.Split(' ');

                            string coordinate = "";

                            for (int i = 0; i < lineSplit.Length; i++)
                            {
                                if (lineSplit[i].StartsWith("N") || lineSplit[i].StartsWith("S"))
                                {
                                    coordinate = lineSplit[i] + " " + lineSplit[i + 1];

                                    break;
                                }
                            }
                            if (coordinate != "")
                            {
                                currentRegion.addCoordinate(coordinate);
                            }
                            
                        }
                    }

                    
                }
            }
            
            return sectorRegions;
        }

        public static List<RegionColor> readSectorColors(string filename)
        {
            List<RegionColor> regionColors = new List<RegionColor>();

            StreamReader reader = new StreamReader(filename);

            bool definesStarted = false;

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();

                if (line.Contains("#define COLOR_"))
                {
                    definesStarted = true;
                    string[] lineSplit = line.Split(' ');

                    string name = lineSplit[1].Replace("COLOR_", "");
                    int colorInt = int.Parse(lineSplit[lineSplit.Length - 1]);

                    RegionColor regionColor = new RegionColor(name, colorInt);
                    regionColors.Add(regionColor);
                }

                if (definesStarted && !line.Contains("#define COLOR_"))
                {
                    Console.WriteLine(line);
                    break;
                }
            }

            return regionColors;
        }

        public static bool IsPointInPolygon(Coordinate point, List<Coordinate> polygon)
        {
            if (polygon == null || polygon.Count < 3)
                return false;

            bool inside = false;
            int n = polygon.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Coordinate vi = polygon[i];
                Coordinate vj = polygon[j];

                // Check if point is on a vertex
                if (vi.Lat == point.Lat && vi.Lon == point.Lon)
                    return true;

                // Check if point is on horizontal edge
                if ((vi.Lat == point.Lat && vj.Lat == point.Lat) &&
                    ((vi.Lon <= point.Lon && point.Lon <= vj.Lon) ||
                     (vj.Lon <= point.Lon && point.Lon <= vi.Lon)))
                    return true;

                // Ray casting algorithm
                if ((vi.Lat > point.Lat) != (vj.Lat > point.Lat) &&
                    point.Lon < (vj.Lon - vi.Lon) * (point.Lat - vi.Lat) / (vj.Lat - vi.Lat) + vi.Lon)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static Dictionary<string, object> ParseConfigFile(string filePath)
        {
            var config = new Dictionary<string, object>();

            foreach (string line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    continue;

                string[] parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim('"').Trim();
                    string value = parts[1].Trim('"').Trim();

                    if (value.Contains(";"))
                    {
                        string[] items = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        config[key] = items.Select(item => item.Trim()).ToArray();
                    }
                    else
                    {
                        config[key] = value;
                    }
                }
            }

            return config;
        }


        private void SaveConfigFile(string filePath, Dictionary<string, object> config)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var kvp in config)
                {
                    if (kvp.Value is string stringValue)
                    {
                        writer.WriteLine($"{kvp.Key}=\"{stringValue}\"");
                    }
                    else if (kvp.Value is string[] arrayValue && arrayValue.Length > 0)
                    {
                        string joinedValue = string.Join(";", arrayValue);
                        writer.WriteLine($"{kvp.Key}=\"{joinedValue}\"");
                    }
                }
            }
        }

        private void initConfig()
        {
            var config = ParseConfigFile("settings.cfg");
            bool configUpdated = false;

            // Handle SECTORFILE
            sectorFilePath = config["SECTORFILE"] as string;
            if (string.IsNullOrEmpty(sectorFilePath))
            {
                sectorFilePath = BrowseForFile("Select Sector File", "SCT files (*.sct)|*.sct");
                if (!string.IsNullOrEmpty(sectorFilePath))
                {
                    config["SECTORFILE"] = sectorFilePath;
                    configUpdated = true;
                }
            }

            // Handle ASMGCS
            asmgcsFilePath = config["ASMGCS"] as string;
            if (string.IsNullOrEmpty(asmgcsFilePath))
            {
                asmgcsFilePath = BrowseForFile("Select ASMGCS File", "ASR files (*.asr)|*.asr");
                if (!string.IsNullOrEmpty(asmgcsFilePath))
                {
                    config["ASMGCS"] = asmgcsFilePath;
                    configUpdated = true;
                }
            }

            // Handle REGIONS_TO_SHOW
            regionsToShow = config["REGIONS_TO_SHOW"] as string[] ?? Array.Empty<string>();

            // Save config if updated
            if (configUpdated)
            {
                SaveConfigFile("settings.cfg", config);
            }
        }


        private string BrowseForFile(string title, string filter)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }

            return string.Empty;
        }

        public MainWindow()
        {
            InitializeComponent();
            initConfig();



            regionColors = readSectorColors(sectorFilePath);
            sectorRegions = readCoordinates(sectorFilePath, regionColors);



            foreach (var sector in sectorRegions)
            {
                bool isXCLS = sector.name.ToUpper().Contains("XCLS");



                bool showSector = false;


                foreach (var region in regionsToShow)
                {
                    if (sector.name.ToUpper().Contains(region.ToUpper()))
                    {
                        showSector = true;
                        break;
                    }
                }

                var polygon1 = new Polygon();


                polygon1.Visibility = showSector || isXCLS ? Visibility.Visible : Visibility.Hidden;
                polygon1.Cursor = isXCLS ? Cursors.Hand : Cursors.Arrow;

                polygon1.StrokeThickness = 1;


                polygon1.Fill = !isXCLS ? new SolidColorBrush(sector.color) : nonClosedColor;

                polygon1.DataContext = "no-save";

                var pointCollection1 = new PointCollection();

                foreach (var coord in sector.coordinates)
                {
                    var locOnScreen = ToPixelEquirectangular(
                    coord.Lat, coord.Lon,
                    mapCenterCoordinate.Lat, mapCenterCoordinate.Lon,
                    Convert.ToInt32(this.Width), Convert.ToInt32(this.Height), mapRadiusMeters);

                    Point screenCoord = new Point(locOnScreen.px, locOnScreen.py);
                    pointCollection1.Add(screenCoord);
                }

                polygon1.Points = pointCollection1;
                polygonRegions.Add(new PolygonRegion(sector.name, polygon1));
                MapProjection.Children.Add(polygon1);

            }
        }

        private void MapProjection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(MapProjection);

            Coordinate clickedCoordinate = FromPixelEquirectangular(
                point.X, point.Y,
                mapCenterCoordinate.Lat, mapCenterCoordinate.Lon,
                Convert.ToInt32(this.Width), Convert.ToInt32(this.Height), mapRadiusMeters);

            foreach (var sector in sectorRegions)
            {
                if (IsPointInPolygon(clickedCoordinate, sector.coordinates) && sector.name.ToUpper().Contains("XCLS"))
                {
                    Trace.WriteLine($"Clicked inside sector: {sector.name}");

                    foreach (var poly in polygonRegions)
                    {
                        if (poly.name == sector.name)
                        {
                            if (poly.Polygon.DataContext == "no-save")
                            {
                                poly.Polygon.DataContext = "save";
                                poly.Polygon.Fill = new SolidColorBrush(sector.color);

                            } else
                            {
                                poly.Polygon.DataContext = "no-save";
                                poly.Polygon.Fill = nonClosedColor;
                            }
                            //poly.Polygon.Visibility = poly.Polygon.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
                        }
                    }
                }
            }
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> xclsToSave = new List<string>();


            foreach (var poly in polygonRegions)
            {
                if (poly.Polygon.DataContext == "save" && poly.name.ToUpper().Contains("XCLS"))
                {
                    xclsToSave.Add(poly.name);
                }
            }


            List<string> lines = File.ReadAllLines(asmgcsFilePath).ToList();

            using (StreamWriter writer = new StreamWriter(asmgcsFilePath))
            {
                foreach (string line in lines)
                {
                    if (!line.Contains("XCLS"))
                    {
                        writer.WriteLine(line);
                    }
                }

                foreach (var xcls in xclsToSave)
                {
                    writer.WriteLine($"Regions:LHBP {xcls}:polygon");
                }
            }
        }

        private void showCloseableButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var poly in polygonRegions)
            {
                if (poly.Polygon.DataContext == "no-save" && poly.name.ToUpper().Contains("XCLS"))
                {
                    if (poly.Polygon.Fill == nonClosedColor)
                    {
                        poly.Polygon.Fill = new SolidColorBrush(Colors.Transparent);
                    } else
                    {
                        poly.Polygon.Fill = nonClosedColor;
                    }
                }
            }
        }
    }
}