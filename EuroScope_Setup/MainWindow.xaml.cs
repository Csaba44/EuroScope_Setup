using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace EuroScope_Setup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public static (double Latitude, double Longitude) ConvertDmsToDecimal(string dmsCoord)
        {
            // N047.26.15.993 -> 47.437775833
            // E019.15.55.278 -> 19.265355

            try
            {
                string direction = dmsCoord.Substring(0, 1);
                string rest = dmsCoord.Substring(1);

                string[] parts = rest.Split('.');

                if (parts.Length < 3)
                    throw new ArgumentException("Érvénytelen DMS formátum");

                int degrees = int.Parse(parts[0]);
                int minutes = int.Parse(parts[1]);
                double seconds = double.Parse(parts[2] + (parts.Length > 3 ? "." + parts[3] : ""));

                double decimalValue = degrees + (minutes / 60.0) + (seconds / 3600.0);

                if (direction == "S" || direction == "W")
                    decimalValue = -decimalValue;

                return (direction == "N" || direction == "S") ?
                    (decimalValue, 0) : (0, decimalValue);
            }
            catch
            {
                throw new ArgumentException("Unknown coordinate format.");
            }
        }

        public static (double Lat, double Lon) ParseCoordinates(string latDms, string lonDms)
        {
            var lat = ConvertDmsToDecimal(latDms);
            var lon = ConvertDmsToDecimal(lonDms);

            return (lat.Latitude, lon.Longitude);
        }


        public static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }


        public MainWindow()
        {
            InitializeComponent();
            var coordinates = new List<(double Lat, double Lon, string Name)>
            {
                (40.712776, -74.005974, "New York"),
                (35.689487, 139.691711, "Tokió"),
                (48.856613, 2.352222, "Párizs"),
                (-23.550520, -46.633308, "São Paulo"),
                (-33.868820, 151.209290, "Sydney")
            };


            int width = 1280;
            int height = 720;



            for (int i = 0; i < coordinates.Count; i++)
            {
                var coord = coordinates[i];

                double radius = width / (2 * Math.PI);
                const int FE = 180;

                double lonRad = DegreesToRadians(coord.Lon + FE);
                double x = lonRad * radius;

                double latRad = DegreesToRadians(coord.Lat);
                double verticalOffsetFromEquator = radius * Math.Log(Math.Tan(Math.PI / 4 + latRad / 2));

                double y = height / 2 - verticalOffsetFromEquator;
                  



                Label lbl = new Label
                {
                    Content = coord.Name,
                    Foreground = Brushes.Red,
                    Background = Brushes.Transparent,
                    FontSize = 15,
                    Margin = new Thickness(x, y, 0, 0),
                };

                MapProjection.Children.Add(lbl);

                Trace.WriteLine($"{x} {y}");
            }


        }
    }
}