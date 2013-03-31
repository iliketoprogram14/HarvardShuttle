using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HarvardShuttle.Data;
using Windows.Storage;
using Windows.Data.Json;
using Windows.Data.Xml.Dom;
using System.Xml;
using System.IO;
using Bing.Maps;

/// Format of xml
/// <api_data_store>
///   <stops>
///     <stop id="1">
///       <title>title</title>
///       <cs50name>cs50name</cs50name>
///       <transloc_name>tname</transloc_name>
///       <routes>route_ids</routes>
///     </stop>
///   </stops>
///   <routes>
///     <route id="1">dest_ids</route>
///   <routes>
/// </api_data_store>

namespace HarvardShuttle
{
    public class APIDataStore
    {
        private static string dataStorePath = "api_data_store.xml";
        public static StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
        private static string agency = "52";

        #region Store Initialization
        public async static void InitDataStore(List<DataItem> items)
        {
            bool fileExists = true;
            try {
                StorageFile file = await localFolder.GetFileAsync(dataStorePath);
            }
            catch (Exception) {
                fileExists = false;
            }

            if (!fileExists) {
                string stopsAndRoutes = await GetStopsAndRoutes(items);
                string xml = "<api_store>" + stopsAndRoutes + "</api_store>";
                StorageFile file = await localFolder.CreateFileAsync(dataStorePath, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, xml);
            }
        }

        private async static Task<string> GetStopsAndRoutes(List<DataItem> items)
        {
            // Download stops and get json response
            string url = "http://api.transloc.com/1.1/stops.json?agencies=" + agency;
            var client = new System.Net.Http.HttpClient();
            HttpResponseMessage response = client.GetAsync(url).Result;
            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject obj = JsonObject.Parse(responseString);

            // Init data structures
            List<string> titles = new List<string>(); // list of titles for items
            foreach (DataItem item in items)
                titles.Add(item.Title);

            Dictionary<string, string> cs50NameMap = await GetCS50Names(titles); // maps titles to cs50 names
            Dictionary<string, string> routeMap = new Dictionary<string, string>(); // maps routes to stops

            // Parse json string into an xml list of stops
            string xml = "<stops>";
            foreach (JsonValue stop in obj["data"].GetArray()) {
                var stopObj = stop.GetObject();

                // Get the title for the current transloc item
                string currTitle = "Nothing :(";
                int idx = titles.IndexOf(stopObj["name"].GetString().Replace("& ", ""));
                if (idx != -1)
                    currTitle = titles[idx];
                else if (stopObj["name"].GetString().Contains("ilab"))
                    currTitle = "i-Lab";
                else
                    continue; // if there's a transloc location that's not in titles, skip it

                // Create the xml for the stop
                //id='" + stopObj["stop_id"].GetString() + "'>";
                xml += "<stop s_id='" + stopObj["stop_id"].GetString() + "'>";
                xml += "<title>" + currTitle + "</title>";
                xml += "<cs50name>" + cs50NameMap[currTitle] + "</cs50name>";
                //xml += "<transloc_name>" + stopObj["name"].GetString() + "</transloc_name>";
                // Construct this destination's routes and populate the routeMap at the same time
                xml += "<stop_routes>";
                foreach (JsonValue routeVal in stopObj["routes"].GetArray()) {
                    string route = routeVal.GetString();
                    xml += route + ",";
                    routeMap = UpdateRouteMap(routeMap, route, stopObj["stop_id"].GetString());
                }
                xml = xml.Remove(xml.Length - 1, 1); // remove last comma
                xml += "</stop_routes>";
                xml += "</stop>";
            }
            xml += "</stops>";

            xml += WriteRoutes(routeMap);
            return xml;
        }

        private async static Task<Dictionary<string, string>> GetCS50Names(List<string> titles)
        {
            string url = "http://shuttleboy.cs50.net/api/1.2/stops?output=json";
            var client = new System.Net.Http.HttpClient();
            HttpResponseMessage response = client.GetAsync(url).Result;

            // get json response
            string responseString = await response.Content.ReadAsStringAsync();
            responseString = "{\"stops\": " + responseString + "}";
            JsonObject obj = JsonObject.Parse(responseString);

            // add titles to list of strings
            List<string> cs50Stops = new List<string>();
            JsonArray arr = obj["stops"].GetArray();
            foreach (JsonValue stop in arr) {
                var stopObj = stop.GetObject();
                cs50Stops.Add(stopObj["stop"].GetString());
            }

            // map titles to cs50 titles
            Dictionary<string, string> cs50Map = new Dictionary<string, string>();
            foreach (string title in titles) {
                if (cs50Stops.Contains(title)) {
                    cs50Map.Add(title, title);
                    continue;
                }
                switch (title) {
                    case "Kennedy School":
                        if (cs50Stops.Contains("HKS")) {
                            cs50Map.Add(title, "HKS");
                            continue;
                        }
                        break;
                    case "Law School":
                        if (cs50Stops.Contains("Pound Hall")) {
                            cs50Map.Add(title, "Pound Hall");
                            continue;
                        }
                        break;
                }
                cs50Map.Add(title, "Nothing!!!");
            }
            return cs50Map;
        }

        private static Dictionary<string, string> UpdateRouteMap(Dictionary<string, string> routeMap, string route, string stop_id)
        {
            string dests = (routeMap.ContainsKey(route)) ? routeMap[route] : "";
            dests += stop_id + ",";
            routeMap[route] = dests;
            return routeMap;
        }

        private static string WriteRoutes(Dictionary<string, string> routeMap)
        {
            string xml = "<routes>";
            foreach (var route in routeMap.Keys) {
                string dests = routeMap[route];
                dests = dests.Remove(dests.Length - 1, 1);
                xml += "<route r_id='" + route + "'>" + dests + "</route>";
            }
            xml += "</routes>";
            return xml;
        }
        #endregion

        public static async Task<Tuple<string, string>> GetCS50Names(string origin, string dest)
        {
            StorageFile file = await localFolder.GetFileAsync(dataStorePath);
            string xml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            string origin_cs50name = "", dest_cs50name = "";
            foreach (XmlElement elem in doc.GetElementsByTagName("stop")) {
                string title = elem.GetElementsByTagName("title")[0].InnerText;
                if (title == origin)
                    origin_cs50name = elem.GetElementsByTagName("cs50name")[0].InnerText;
                else if (title == dest)
                    dest_cs50name = elem.GetElementsByTagName("cs50name")[0].InnerText;
                if (origin_cs50name != "" && dest_cs50name != "")
                    break;
            }

            return Tuple.Create<string, string>(origin_cs50name, dest_cs50name);
        }

        public async static Task<DataGroup> GetDestinations(string origin)
        {
            // Init data structures
            var itemGroup = DataSource.GetGroup("Group-1");
            DataGroup destGroup = new DataGroup("Dest-Group", "To", "", "Assets/DarkGray.png", "");
            Dictionary<string, string> idToTitle = new Dictionary<string, string>();
            HashSet<string> destSet = new HashSet<string>(); // same as DestGroup, but for constant time contain
            List<string> originRoutes = new List<string>();

            // Grab the API data store
            StorageFile file = await localFolder.GetFileAsync(dataStorePath);
            string xml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            // Grab the routes of the origin, and populate idToTitle for everything
            foreach (XmlElement elem in doc.GetElementsByTagName("stop")) {
                string stopId = elem.GetAttribute("s_id");
                string stopName = elem.GetElementsByTagName("title")[0].InnerText;
                if (stopName == origin) {
                    originRoutes = elem.GetElementsByTagName("stop_routes")[0].InnerText.ToString().Split(',').ToList<string>();
                }
                idToTitle[stopId] = stopName;
            }

            // Grab all the stops that share routes with the origin
            foreach (XmlElement elem in doc.GetElementsByTagName("route")) {
                string route_id = elem.GetAttribute("r_id");
                if (!originRoutes.Contains(route_id))
                    continue;
                List<string> stop_ids = elem.InnerText.Split(',').ToList<string>();
                foreach (string stop_id in stop_ids) {
                    string stopTitle = idToTitle[stop_id];
                    if (!destSet.Contains(stopTitle) && stopTitle != origin)
                        destSet.Add(stopTitle);
                }
            }

            // Populate destGroup with all stops that share routes with the origin
            foreach (DataItem item in itemGroup.Items)
                if (destSet.Contains(item.Title))
                    destGroup.Items.Add(item);

            return destGroup;
        }

        public async static Task<string> GetArrivalEstimates(string origin, string dest)
        {
            Tuple<string, string, IEnumerable<string>> idsAndCommonRotes = await GetCommonRoutesAndStopIds(origin, dest);
            string origin_id = idsAndCommonRotes.Item1;
            string dest_id = idsAndCommonRotes.Item2;
            IEnumerable<string> common_routes = idsAndCommonRotes.Item3;

            // Download stops and get json response
            string url = "http://api.transloc.com/1.1/arrival-estimates.json?agencies=" + agency + "&stops=" + origin_id;
            var client = new System.Net.Http.HttpClient();
            HttpResponseMessage response = client.GetAsync(url).Result;
            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject obj = JsonObject.Parse(responseString);

            string arrivalStr = "";
            JsonArray dataArr = obj["data"].GetArray();
            if (dataArr.Count != 0) {
                var arrivals = dataArr[0].GetObject()["arrivals"].GetArray();
                foreach (JsonValue arrival in arrivals) {
                    var arrival_obj = arrival.GetObject();
                    string route_id = arrival_obj["route_id"].GetString();

                    // we have an arrival
                    if (common_routes.Contains(route_id)) {
                        arrivalStr = arrival_obj["arrival_at"].GetString().Split('T')[1].Split('-')[0];
                        break;
                    }
                }
            }

            if (arrivalStr != "") {
                string[] time = arrivalStr.Split(':');
                int departMin = Convert.ToInt32(time[1]);
                int departHour = Convert.ToInt32(time[0]);

                int minuteCountdown = departHour * 60 + departMin - (DateTime.Now.TimeOfDay.Hours * 60 + DateTime.Now.TimeOfDay.Minutes);
                if (minuteCountdown < 0)
                    minuteCountdown = (24 * 60) + minuteCountdown;
                arrivalStr = minuteCountdown.ToString();
            }

            return arrivalStr;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="dest"></param>
        /// <returns>Dictionary that maps route ids to a tuple of a color, the route name, and a list of segment ids</returns>
        public async static Task<Dictionary<string, Tuple<string, string, List<LocationCollection>>>> GetRoutes(string origin, string dest)
        {
            bool checkForAllRoutes = (origin == "" && dest == "");

            Tuple<string, string, IEnumerable<string>> idsAndCommonRotes = (checkForAllRoutes) ? null : await GetCommonRoutesAndStopIds(origin, dest);
            IEnumerable<string> common_routes = (checkForAllRoutes) ? new List<string>() : idsAndCommonRotes.Item3;

            Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap = new Dictionary<string, Tuple<string, string, List<LocationCollection>>>();

            // Download routes and get json response
            string url = "http://api.transloc.com/1.1/routes.json?agencies=" + agency;
            var client = new System.Net.Http.HttpClient();
            HttpResponseMessage response = client.GetAsync(url).Result;
            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject obj = JsonObject.Parse(responseString);

            // parse object
            JsonArray routeArr = obj["data"].GetObject()["52"].GetArray();
            if (routeArr.Count == 0)
                return routeMap;

            JsonObject segmentMap = await GetSegments();

            //var routeArr = dataArr[0].GetObject()["52"].GetArray(); // does this fail when empty?
            foreach (JsonValue routeVal in routeArr) {
                var route_obj = routeVal.GetObject();
                string route_id = route_obj["route_id"].GetString();
                List<LocationCollection> locationCollectionList = new List<LocationCollection>();

                // we found a route that's common to the origin and the destination
                if (checkForAllRoutes || common_routes.Contains(route_id)) {
                    string name = route_obj["long_name"].GetString();
                    string color = route_obj["color"].GetString();
                    JsonArray segmentArr = route_obj["segments"].GetArray();
                    foreach (JsonValue segment_id in segmentArr) {
                        string segmentID = segment_id.Stringify().Replace("\"", "");
                        var derp = segmentMap[segmentID].Stringify();
                        string encoded_segment = segmentMap[segmentID].Stringify().Replace("\"", "").Replace("\\\\", "\\");
                        List<Location> locations = DecodeLatLong(encoded_segment);
                        LocationCollection segment_ids = new LocationCollection();
                        //if (segmentID == "4028995") {
                        foreach (Location l in locations)
                            segment_ids.Add(l);
                        locationCollectionList.Add(segment_ids);
                        //}
                    }
                    routeMap[route_id] = Tuple.Create<string, string, List<LocationCollection>>(name, color, locationCollectionList);
                }
            }
            return routeMap;
        }

        private async static Task<JsonObject> GetSegments()
        {
            string url = "http://api.transloc.com/1.1/segments.json?agencies=" + agency;
            var client = new System.Net.Http.HttpClient();
            HttpResponseMessage response = client.GetAsync(url).Result;
            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject obj = JsonObject.Parse(responseString);

            Dictionary<string, List<Location>> segmentMap = new Dictionary<string, List<Location>>();

            JsonObject segmentArr = obj["data"].GetObject();

            return segmentArr;
        }

        private async static Task<Tuple<string, string, IEnumerable<string>>> GetCommonRoutesAndStopIds(string origin, string dest)
        {
            // Grab the API data store
            StorageFile file = await localFolder.GetFileAsync(dataStorePath);
            string xml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            // Get ids of origin and dest and common routes
            string originID = "";
            string destID = "";
            List<string> origin_routes = new List<string>();
            List<string> dest_routes = new List<string>();
            foreach (XmlElement elem in doc.GetElementsByTagName("stop")) {
                string stopName = elem.GetElementsByTagName("title")[0].InnerText;
                if (stopName == origin) {
                    originID = elem.GetAttribute("s_id");
                    origin_routes = elem.GetElementsByTagName("stop_routes")[0].InnerText.Split(',').ToList<string>();
                }
                else if (stopName == dest) {
                    destID = elem.GetAttribute("s_id");
                    dest_routes = elem.GetElementsByTagName("stop_routes")[0].InnerText.Split(',').ToList<string>();
                }
                if (originID != "" && destID != "")
                    break;
            }
            IEnumerable<string> common_routes = origin_routes.Intersect<string>(dest_routes);

            return Tuple.Create<string, string, IEnumerable<string>>(originID, destID, common_routes);
        }

        public static List<Location> DecodePolyline(string polyline)
        {
            if (polyline == null || polyline == "")
                return null;

            char[] polylinechars = polyline.ToCharArray();
            int index = 0;
            List<Location> locations = new List<Location>();
            int currentLat = 0;
            int currentLng = 0;
            int next5bits;
            int sum;
            int shifter;

            while (index < polylinechars.Length) {
                // calculate next latitude
                sum = 0;
                shifter = 0;
                do {
                    next5bits = (int)polylinechars[index++] - 63;
                    sum |= (next5bits & 31) << shifter;
                    shifter += 5;
                } while (next5bits >= 32 && index < polylinechars.Length);

                if (index >= polylinechars.Length)
                    break;

                currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                //calculate next longitude
                sum = 0;
                shifter = 0;
                do {
                    next5bits = (int)polylinechars[index++] - 63;
                    sum |= (next5bits & 31) << shifter;
                    shifter += 5;
                } while (next5bits >= 32 && index < polylinechars.Length);

                if (index >= polylinechars.Length && next5bits >= 32)
                    break;

                currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                double lat = Convert.ToDouble(currentLat) / 100000.0;
                double lng = Convert.ToDouble(currentLng) / 100000.0;
                Location loc = new Location(lat, lng);
                locations.Add(loc);
            }

            return locations;
        }

        /// <summary>
        /// decodes a string into a list of latlon objects
        /// from http://www.soulsolutions.com.au/Articles/Encodingforperformance.aspx
        /// </summary>
        /// <param name="encoded">encoded string</param>
        /// <returns>list of latlon</returns>
        private static List<Location> DecodeLatLong(string encoded)
        {
            List<Location> locs = new List<Location>();

            int index = 0;
            int lat = 0;
            int lng = 0;

            int len = encoded.Length;
            while (index < len) {
                lat += decodePoint(encoded, index, out index);
                if (index < len) {
                    lng += decodePoint(encoded, index, out index);
                }

                double latf = lat * 1e-5;
                double lngf = lng * 1e-5;

                Location l = new Location(latf, lngf);

                locs.Add(l);
            }

            return locs;
        }

        /// <summary>
        /// decodes the cuurent chunk into a single integer value
        /// from http://www.soulsolutions.com.au/Articles/Encodingforperformance.aspx
        /// </summary>
        /// <param name="encoded">the complete encodered string</param>
        /// <param name="startindex">the current position in that string</param>
        /// <param name="finishindex">output - the position we end up in that string</param>
        /// <returns>the decoded integer</returns>
        private static int decodePoint(string encoded, int startindex, out int finishindex)
        {
            int b;
            int shift = 0;
            int result = 0;
            int minASCII = 63;
            int binaryChunkSize = 5;
            do {
                //get binary encoding
                b = Convert.ToInt32(encoded[startindex++]) - minASCII;
                //binary shift
                result |= (b & 0x1f) << shift;
                //move to next chunk
                shift += binaryChunkSize;
            } while (b >= 0x20); //see if another binary value
            //if negivite flip
            int dlat = (((result & 1) > 0) ? ~(result >> 1) : (result >> 1));
            //set output index
            finishindex = startindex;
            return dlat;
        }


        public static void GetShuttles(string origin, string dest)
        {

        }
    }
}
