using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        public MainWindow()
        {
            InitializeComponent();

            var coordinates = new List<(double Lat, double Lon, string Name)>
            {
                (47.430571802977056, 19.249855532823133, "31L"),
                (47.4486356747824, 19.221007125800583, "13R"),
                (47.445485782872424, 19.257853953044314, "13L"),
                (47.42314421475645, 19.29364357764242, "31R"),
                (47.4386376244894, 19.25703634696732, "TWR"),
            };

            var centerCoordinate = new Coordinate(47.43292231467408, 19.261434807795617);
            double radiusMeters = 2000;
            int width = 1280;
            int height = 720;

            foreach (var coord in coordinates)
            {
                var locOnScreen = ToPixelEquirectangular(
                    coord.Lat, coord.Lon,
                    centerCoordinate.Lat, centerCoordinate.Lon,
                    width, height, radiusMeters);

                Label lbl = new Label
                {
                    Content = coord.Name,
                    Foreground = Brushes.Red,
                    Background = Brushes.Transparent,
                    FontSize = 20,
                    Margin = new Thickness(locOnScreen.px, locOnScreen.py, 0, 0),
                };

                MapProjection.Children.Add(lbl);
                Trace.WriteLine($"{coord.Name}: {locOnScreen.px:F1}, {locOnScreen.py:F1}");
            }
        }
    }
}