using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EuroScope_Setup
{
    public partial class MainWindow : Window
    {
        public static Coordinate mapCenterCoordinate = new Coordinate(47.43566192997341, 19.25721003945266);
        public static double mapRadiusMeters = 2000;

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

        List<SectorRegion> readCoordinates(string filename, List<RegionColor> regionColors)
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

        List<RegionColor> readSectorColors(string filename)
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

        public MainWindow()
        {
            InitializeComponent();

            List<RegionColor> regionColors = readSectorColors("sectorfile.sct");
            List<SectorRegion> sectorRegions = readCoordinates("sectorfile.sct", regionColors);






            foreach (var sector in sectorRegions)
            {

                var polygon1 = new Polygon();

                polygon1.Fill = new SolidColorBrush(sector.color);

                polygon1.StrokeThickness = 1;
                
                var pointCollection1 = new PointCollection();

                foreach (var coord in sector.coordinates)
                {
                    var locOnScreen = ToPixelEquirectangular(
                    coord.Lat, coord.Lon,
                    mapCenterCoordinate.Lat, mapCenterCoordinate.Lon,
                    Convert.ToInt32(this.Width), Convert.ToInt32(this.Height), mapRadiusMeters);

                    Point screenCoord = new Point(locOnScreen.px, locOnScreen.py);
                    Console.WriteLine($"Lat: {coord.Lat:F9}, Lon: {coord.Lon:F9} -> X: {locOnScreen.px:F1}, Y: {locOnScreen.py:F1}");
                    pointCollection1.Add(screenCoord);
                }

                polygon1.Points = pointCollection1;
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
            Trace.WriteLine($"X: {point.X}, Y: {point.Y}\n{clickedCoordinate.Lat}, {clickedCoordinate.Lon}");
        }
    }
}