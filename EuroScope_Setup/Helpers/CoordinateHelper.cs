using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static EuroScope_Setup.MainWindow;
using System.Drawing;

namespace EuroScope_Setup.Helpers
{
    class CoordinateHelper
    {

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

        public static Coordinate FromPixelEquirectangular(
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

        public static (double px, double py) ToPixelEquirectangular(
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
    }
}
