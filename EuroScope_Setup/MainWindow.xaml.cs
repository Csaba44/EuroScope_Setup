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
using static EuroScope_Setup.Helpers.ConfigHelper;
using static EuroScope_Setup.Helpers.CoordinateHelper;
using static EuroScope_Setup.Helpers.ActiveAircraftHelper;
using EuroScope_Setup.Helpers;

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

        private void initConfig()
        {
            var config = ParseConfigFile("settings.cfg");
            bool configUpdated = false;

            // Handle SECTORFILE
            sectorFilePath = config["SECTORFILE"] as string;
            if (string.IsNullOrEmpty(sectorFilePath) || !File.Exists(sectorFilePath))
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
            if (string.IsNullOrEmpty(asmgcsFilePath) || !File.Exists(asmgcsFilePath))
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

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            var helper = await ActiveAircraftHelper.CreateAsync(mapRadiusMeters, mapCenterCoordinate);
            List<Aircraft> aircraftList = helper.getAircraftOnGround();

            foreach (var aircraft in aircraftList)
            {
                var locOnScreen = ToPixelEquirectangular(
                  aircraft.coordinate.Lat, aircraft.coordinate.Lon,
                  mapCenterCoordinate.Lat, mapCenterCoordinate.Lon,
                  Convert.ToInt32(this.Width), Convert.ToInt32(this.Height), mapRadiusMeters);

                Ellipse ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(Colors.Blue),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 1,
                    ToolTip = $"{aircraft.callsign} ({aircraft.altitude} ft)",

                };
                Canvas.SetLeft(ellipse, locOnScreen.px - 5);
                Canvas.SetTop(ellipse, locOnScreen.py - 5);


                Trace.WriteLine($"{aircraft.name} added to {locOnScreen.px}x{locOnScreen.py}.");


                MapProjection.Children.Add(ellipse);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            initConfig();
            this.Loaded += MainWindow_Loaded;


           

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