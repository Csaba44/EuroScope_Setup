using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static EuroScope_Setup.Helpers.CoordinateHelper;
using Newtonsoft.Json;

namespace EuroScope_Setup.Helpers
{
    class Aircraft
    {
        public string cid { get; private set; }
        public string name { get; private set; }
        public string callsign { get; private set; }
        public Coordinate coordinate { get; private set; }
        public int altitude { get; private set; }
        public int groundspeed { get; private set; }

        public Aircraft(string cid, string name, string callsign, double latitude, double longitude, int altitude, int groundspeed)
        {
            this.cid = cid;
            this.name = name;
            this.callsign = callsign;
            this.coordinate = new Coordinate(latitude, longitude);
            this.altitude = altitude;
            this.groundspeed = groundspeed;
        }
    }

    class ActiveAircraftHelper
    {
        public static List<Aircraft> activeAircraft = new List<Aircraft>();

        public double mapRadiusMeters { get; private set; }
        public Coordinate mapCenterCoordinate { get; private set; }

        private static async Task fetchJsonAsync(string url)
        {
            using HttpClient client = new HttpClient();

            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string jsonString = await response.Content.ReadAsStringAsync();

            var data = JsonConvert.DeserializeObject<dynamic>(jsonString);

            foreach (var aircraft in data.pilots)
            {
                Aircraft newAircraft = new Aircraft(
                    (string)aircraft.cid,
                    (string)aircraft.name,
                    (string)aircraft.callsign,
                    (double)aircraft.latitude,
                    (double)aircraft.longitude,
                    (int)aircraft.altitude,
                    (int)aircraft.groundspeed
                );

                activeAircraft.Add(newAircraft);
            }
        }


        public static async Task<ActiveAircraftHelper> CreateAsync(double mapRadiusMeters, Coordinate mapCenterCoordinate)
        {
            await fetchJsonAsync("https://data.vatsim.net/v3/vatsim-data.json");
            return new ActiveAircraftHelper(mapRadiusMeters, mapCenterCoordinate);
        }

        private ActiveAircraftHelper(double mapRadiusMeters, Coordinate mapCenterCoordinate)
        {
            this.mapRadiusMeters = mapRadiusMeters;
            this.mapCenterCoordinate = mapCenterCoordinate;
        }

        public List<Aircraft> getAircraftOnGround()
        {
            return activeAircraft.Where(a => a.altitude < 1000).ToList();
        }
    }
}
