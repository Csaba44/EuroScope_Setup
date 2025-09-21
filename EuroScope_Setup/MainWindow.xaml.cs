using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EuroScope_Setup
{
    public partial class MainWindow : Window
    {
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

        // Function to convert DMS (Degrees, Minutes, Seconds) to decimal degrees
        double DmsToDecimal(string dmsString)
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
        List<Coordinate> ParseCoordinates(string[] coordinateLines)
        {
            var coordinates = new List<Coordinate>();

            foreach (string line in coordinateLines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                // Split by space and find the coordinate parts
                string[] parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2)
                {
                    // The last two parts should be the coordinates
                    string latString = parts[parts.Length - 2];
                    string lonString = parts[parts.Length - 1];

                    if (latString.StartsWith("N") || latString.StartsWith("S") &&
                        lonString.StartsWith("E") || lonString.StartsWith("W"))
                    {
                        try
                        {
                            double lat = DmsToDecimal(latString);
                            double lon = DmsToDecimal(lonString);
                            coordinates.Add(new Coordinate(lat, lon));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing coordinates: {latString} {lonString} - {ex.Message}");
                        }
                    }
                }
            }

            return coordinates;
        }

        // Function to convert geographic coordinates to pixel coordinates
        (double px, double py) ToPixelEquirectangular(
            double lat, double lon,
            double centerLat, double centerLon,
            int viewportWidth, int viewportHeight,
            double radiusMeters)
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
            double metersPerPixel = radiusMeters / halfViewPx;

            // Convert to pixel coordinates
            double px = viewportWidth / 2.0 + (dxMeters / metersPerPixel);
            double py = viewportHeight / 2.0 - (dyMeters / metersPerPixel);

            return (px, py);
        }

        string[] readCoordinates(string filename)
        {
            string[] coordinateData = File.ReadAllLines(filename);
            return coordinateData;
            
        } 

        public MainWindow()
        {
            InitializeComponent();

            string[] coordinateData = readCoordinates("coords.txt");

            List<Coordinate> coordinates = ParseCoordinates(coordinateData);

            var centerCoordinate = new Coordinate(47.43292231467408, 19.261434807795617);
            double radiusMeters = 2000;
            int width = 1280;
            int height = 720;

            var screenCoords = new List<Point>();
            foreach (var coord in coordinates)
            {
                var locOnScreen = ToPixelEquirectangular(
                    coord.Lat, coord.Lon,
                    centerCoordinate.Lat, centerCoordinate.Lon,
                    width, height, radiusMeters);

                screenCoords.Add(new Point(locOnScreen.px, locOnScreen.py));
                Console.WriteLine($"Lat: {coord.Lat:F9}, Lon: {coord.Lon:F9} -> X: {locOnScreen.px:F1}, Y: {locOnScreen.py:F1}");
            }

            var polygon1 = new Polygon();
            polygon1.Stroke = Brushes.Gray;
            polygon1.Fill = Brushes.Gray;
            polygon1.StrokeThickness = 2;

            var pointCollection1 = new PointCollection();
            foreach (var screenCoord in screenCoords) 
            {
                pointCollection1.Add(screenCoord);
            }
            polygon1.Points = pointCollection1;

            MapProjection.Children.Add(polygon1);

            /*foreach (var screenCoord in screenCoords)
            {
                var marker = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = Brushes.Blue,
                    Stroke = Brushes.DarkBlue,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(marker, screenCoord.X - 2);
                Canvas.SetTop(marker, screenCoord.Y - 2);
                MapProjection.Children.Add(marker);
            }*/
        }
    }
}