using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Data.Json;
using Windows.Data.Xml.Dom;
using System.Xml;
using System.IO;
using Bing.Maps;
using Windows.Foundation;
using Windows.Networking.Connectivity;

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

namespace DataStore
{
    public class MainDataStore
    {
        private static string agency = "52";
        private static string cache = "";
        private static string storePath = "db.json";
        private static string storeFolder = "DataSource";
        private static StorageFolder installLoc = Windows.ApplicationModel.Package.Current.InstalledLocation;


        private static bool IsThereInternet()
        {
            ConnectionProfile connections = NetworkInformation.GetInternetConnectionProfile();
            bool internet = connections != null && connections.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;

            // clean up
            connections = null;

            return internet;
        }

        private class TimeDictionary
        {
            private string originID, destID;
            private int currTimeInt;
            private int origTimeInt;
            private string currTime;
            private string currDay;
            private SortedDictionary<int, List<Tuple<string, JsonObject>>> timeDict;

            public TimeDictionary(string originID, string destID)
            {
                this.originID = originID;
                this.destID = destID;
                var hr = DateTime.Now.Hour;
                var min = DateTime.Now.Minute;
                var derp = DateTime.Now.DayOfWeek;
                var fack = derp.ToString().Substring(0, 3);
                currDay = fack;
                currTimeInt = hr * 60 + min;
                origTimeInt = currTimeInt;
                currTime = hr.ToString() + ":" + min.ToString();
                timeDict = new SortedDictionary<int, List<Tuple<string, JsonObject>>>();
            }

            private string NextDay(string day)
            {
                string nextDay = "";
                switch (day) {
                    case "Sun": nextDay = "Mon"; break;
                    case "Mon": nextDay = "Tue"; break;
                    case "Tue": nextDay = "Wed"; break;
                    case "Wed": nextDay = "Thu"; break;
                    case "Thu": nextDay = "Fri"; break;
                    case "Fri": nextDay = "Sat"; break;
                    case "Sat": nextDay = "Sun"; break;
                }
                return nextDay;
            }

            private bool AddCountdown(JsonObject obj, string day, bool secLoop)
            {
                int daysCanChangeInt = Int32.Parse(obj["days_change"].GetString());
                bool daysCanChange = (daysCanChangeInt == 1);
                string name = obj["name"].GetString();
                int minCountdown = 24 * 60;
                string minTime = "";
                int minTimeInt = 0;

                // find next time
                foreach (JsonValue val in obj["trips"].GetArray()) {
                    JsonObject tripObj = val.GetObject();

                    // if days can change, check that this trip can happen today
                    if (daysCanChange) {
                        string tripDays = tripObj["special"].GetString();
                        if (!tripDays.Contains(day))
                            continue;
                    }

                    // grab the next time for today
                    string time = tripObj[originID].GetString();
                    if (time == "") continue;
                    var fields = time.Split(':');
                    var hr = Int32.Parse(fields[0]);
                    if (secLoop || (hr < 5 && currTimeInt > 300))
                        hr += 24;
                    var min = Int32.Parse(fields[1]);
                    int timeInt = hr * 60 + min;
                    int countdown = timeInt - currTimeInt;
                    int countdownFromOrig = timeInt - origTimeInt;

                    // update the min if we have a new min
                    if (countdown < minCountdown && countdown > 0) {
                        minTimeInt = timeInt;
                        minCountdown = countdown;
                        minTime = time;
                    }
                }
                if (minCountdown < 24 * 60) {
                    List<Tuple<string, JsonObject>> derp = new List<Tuple<string, JsonObject>>();
                    int countdownFromOrig = minTimeInt - origTimeInt;
                    if (timeDict.ContainsKey(countdownFromOrig))
                        derp = timeDict[countdownFromOrig];
                    derp.Add(Tuple.Create<string, JsonObject>(minTime, obj));
                    timeDict[countdownFromOrig] = derp;
                    derp = null; // cleanup
                    return true;
                }

                // clean up

                return false;
            }

            public void AddObj(JsonObject obj)
            {
                string days = obj["days"].GetString();
                if (days.Contains(currDay) && AddCountdown(obj, currDay, false))
                    return;
                string nextDay = NextDay(currDay);
                if (days.Contains(nextDay))
                    AddCountdown(obj, nextDay, true);
            }

            public string PopAndUpdate()
            {
                int countdown = 0;
                string nextTimeStr = "";
                JsonObject routeObj = new JsonObject();
                foreach (var derp in timeDict) {
                    countdown = derp.Key;
                    List<Tuple<string, JsonObject>> blegh = derp.Value;
                    var firstTuple = blegh[0];
                    nextTimeStr = firstTuple.Item1;
                    routeObj = firstTuple.Item2;
                    blegh.RemoveAt(0);
                    if (blegh.Count == 0)
                        timeDict.Remove(countdown);
                    else
                        timeDict[countdown] = blegh;
                    blegh = null;
                    break;
                }

                if (nextTimeStr == "")
                    return ""; // this actually can happen sometimes

                var fields = nextTimeStr.Split(':');
                var hr = Int32.Parse(fields[0]);
                var min = Int32.Parse(fields[1]);
                int nextTimeInt = hr * 60 + min;
                currTimeInt = nextTimeInt;
                currTime = nextTimeStr;
                AddObj(routeObj);

                // clean up
                routeObj = null;

                return nextTimeStr;
            }

            public void clear()
            {
                timeDict.Clear();
            }
        }

        private static async Task<string> RefreshCache()
        {
            if (cache == "") {
                var folder = await installLoc.GetFolderAsync(storeFolder);
                StorageFile f = await folder.GetFileAsync(storePath);
                cache = await FileIO.ReadTextAsync(f);
                f = null;
            }
            return cache;
        }

        private async static Task<JsonObject> GetSegments()
        {
            if (!IsThereInternet())
                return null;

            string url = "http://api.transloc.com/1.1/segments.json?agencies=" + agency;
            var client = new System.Net.Http.HttpClient();
            HttpResponseMessage response = client.GetAsync(url).Result;
            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject obj = JsonObject.Parse(responseString);
            JsonObject segmentArr = obj["data"].GetObject();

            // clean up
            response.Dispose();
            response = null;
            client.Dispose();
            client = null;

            return segmentArr;
        }

        public async static Task<string> GetRouteName(string routeID)
        {
            string store = await RefreshCache();
            JsonObject obj = JsonObject.Parse(store);
            JsonArray routes = obj["routes"].GetArray();

            string routeTitle = "default";
            foreach (JsonValue val in routes) {
                JsonObject routeObj = val.GetObject();
                if (routeObj["id"].GetString() == routeID) {
                    routeTitle = routeObj["name"].GetString();
                    break;
                }
            }

            routes = null;
            obj = null;

            return routeTitle;
        }

        public async static Task<HashSet<string>> GetDestinations(string origin)
        {
            // Init data structures
            Dictionary<string, string> idToTitle = new Dictionary<string, string>();
            HashSet<string> destSet = new HashSet<string>(); // same as DestGroup, but for constant time contain
            List<string> originRoutes = new List<string>();

            // Grab the API data store
            string store = await RefreshCache();
            JsonObject obj = JsonObject.Parse(store);
            JsonArray stops = obj["stops"].GetArray();
            JsonArray routes = obj["routes"].GetArray();

            // Grab the routes of the origin, and populate idToTitle for everything
            foreach (JsonValue val in stops) {
                JsonObject stop = val.GetObject();
                string stopId = stop["id"].GetString();
                string stopName = stop["title"].GetString();
                if (stopName == origin) {
                    foreach (JsonValue routeVal in stop["routes"].GetArray())
                        originRoutes.Add(routeVal.GetString());
                }
                idToTitle[stopId] = stopName;
            }

            // Grab all the stops that share routes with the origin
            foreach (JsonValue val in routes) {
                JsonObject routeObj = val.GetObject();
                string route_id = routeObj["id"].GetString();
                if (!originRoutes.Contains(route_id))
                    continue;
                foreach (JsonValue stop_val in routeObj["stops"].GetArray()) {
                    string stop_id = stop_val.GetString();
                    string stopTitle = idToTitle[stop_id];
                    if (!destSet.Contains(stopTitle) && stopTitle != origin)
                        destSet.Add(stopTitle);
                }
            }

            // clean up
            obj = null;
            originRoutes = null;
            idToTitle = null;

            return destSet;
        }

        public async static Task<string> GetArrivalEstimates(string origin, string dest)
        {
            if (!IsThereInternet())
                return "";

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

            // clean up
            obj = null;
            response.Dispose();
            response = null;
            client.Dispose();
            client = null;
            common_routes = null;
            idsAndCommonRotes = null;

            return arrivalStr;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="dest"></param>
        /// <returns>Dictionary that maps route ids to a tuple of the route name, its color, and a list of segment collections</returns>
        public async static Task<Dictionary<string, Tuple<string, string, List<List<Point>>>>> GetRoutes(string origin, string dest)
        {
            bool checkForAllRoutes = (origin == "" && dest == "");

            Tuple<string, string, IEnumerable<string>> idsAndCommonRotes = (checkForAllRoutes) ? null : await GetCommonRoutesAndStopIds(origin, dest);
            IEnumerable<string> common_routes = (checkForAllRoutes) ? new List<string>() : idsAndCommonRotes.Item3;

            if (!IsThereInternet()) {
                common_routes = null;
                idsAndCommonRotes = null;
                return null;
            }

            Dictionary<string, Tuple<string, string, List<List<Point>>>> routeMap = new Dictionary<string, Tuple<string, string, List<List<Point>>>>();
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
                List<List<Point>> locationCollectionList = new List<List<Point>>();

                // we found a route that's common to the origin and the destination
                if (checkForAllRoutes || common_routes.Contains(route_id)) {
                    string name = route_obj["long_name"].GetString();
                    string color = route_obj["color"].GetString();
                    JsonArray segmentArr = route_obj["segments"].GetArray();
                    foreach (JsonValue segment_id in segmentArr) {
                        string segmentID = segment_id.Stringify().Replace("\"", "");
                        var derp = segmentMap[segmentID].Stringify();
                        string encoded_segment = segmentMap[segmentID].Stringify().Replace("\"", "").Replace("\\\\", "\\");
                        List<Point> locations = DecodeLatLong(encoded_segment);
                        locationCollectionList.Add(locations);
                    }
                    routeMap[route_id] = Tuple.Create<string, string, List<List<Point>>>(name, color, locationCollectionList);
                }
            }

            // clean up
            segmentMap = null;
            obj = null;
            response.Dispose();
            response = null;
            client.Dispose();
            client = null;
            common_routes = null;
            idsAndCommonRotes = null;

            return routeMap;
        }

        public async static Task<Tuple<string, string, IEnumerable<string>>> GetCommonRoutesAndStopIds(string origin, string dest)
        {
            // Grab the API data store
            string store = await RefreshCache();
            JsonObject obj = JsonObject.Parse(store);
            JsonArray stops = obj["stops"].GetArray();

            string originID = "";
            string destID = "";
            List<string> origin_routes = new List<string>();
            List<string> dest_routes = new List<string>();
            foreach (JsonValue stopVal in stops) {
                JsonObject stop = stopVal.GetObject();
                string name = stop["name"].GetString();
                if (name == origin) {
                    originID = stop["id"].GetString();
                    var routeArray = stop["routes"].GetArray();
                    foreach (var routeVal in routeArray)
                        origin_routes.Add(routeVal.GetString());
                }
                else if (name == dest) {
                    destID = stop["id"].GetString();
                    var routeArray = stop["routes"].GetArray();
                    foreach (var routeVal in routeArray)
                        dest_routes.Add(routeVal.GetString());
                }
                if (originID != "" && destID != "")
                    break;
            }
            IEnumerable<string> common_routes = origin_routes.Intersect<string>(dest_routes);
            Tuple<string, string, IEnumerable<string>> commonRoutesAndStopIds = Tuple.Create<string, string, IEnumerable<string>>(originID, destID, common_routes);

            // clean up
            common_routes = null;
            origin_routes = null;
            dest_routes = null;
            obj = null;

            return commonRoutesAndStopIds;
        }

        public async static Task<List<string>> GetTimesForSchedule(int num, string origin, string dest)
        {
            Tuple<string, string, IEnumerable<string>> routes_and_ids = await MainDataStore.GetCommonRoutesAndStopIds(origin, dest);
            string originID = routes_and_ids.Item1;
            string destID = routes_and_ids.Item2;
            List<string> routes = routes_and_ids.Item3.ToList<string>();

            // Grab the data store
            string store = await RefreshCache();
            JsonObject obj = JsonObject.Parse(store);
            JsonArray routeJson = obj["routes"].GetArray();

            // Grab only the route objects you need
            TimeDictionary timeDict = new TimeDictionary(originID, destID);
            foreach (JsonValue routeVal in routeJson) {
                JsonObject routeObj = routeVal.GetObject();
                if (routes.Contains(routeObj["id"].GetString())) {
                    timeDict.AddObj(routeObj);
                }
            }

            // Create an ordered list of times
            List<string> times = new List<string>();
            while (times.Count < num) {
                string nextTime = timeDict.PopAndUpdate();
                if (nextTime == "") break; // this could happen if a user picks a trip that's only on weekends, but it's a weekday
                times.Add(nextTime);
            }

            // cleanup
            routeJson = null;
            obj = null;
            routes = null;
            routes_and_ids = null;
            timeDict.clear(); timeDict = null;

            return times;
        }

        public async static Task<List<Tuple<string, Point>>> GetShuttles(List<string> routeIDs)
        {
            if (!IsThereInternet())
                return null;

            string routeStr = "";
            foreach (string route in routeIDs)
                routeStr += route + ",";

            // Download stops and get json response
            string url = "http://api.transloc.com/1.1/vehicles.json?agencies=" + agency + "&routes=" + routeStr;
            var client = new System.Net.Http.HttpClient();
            HttpResponseMessage response = client.GetAsync(url).Result;
            string responseString = await response.Content.ReadAsStringAsync();
            JsonObject obj = JsonObject.Parse(responseString);

            var derp = obj["data"].GetObject();
            var derp2 = derp[agency].GetArray();

            List<Tuple<string, Point>> locsWithIDs = new List<Tuple<string, Point>>();
            foreach (JsonValue val in derp2) {
                var vehicleObj = val.GetObject();
                var loc = vehicleObj["location"].GetObject();
                double lat = loc["lat"].GetNumber();
                double lng = loc["lng"].GetNumber();
                string routeID = vehicleObj["route_id"].GetString();
                locsWithIDs.Add(Tuple.Create<string, Point>(routeID, new Point(lat, lng)));
            }

            // cleanup
            obj = null;
            response.Dispose();
            response = null;
            client.Dispose();
            client = null;

            return locsWithIDs;
        }

        #region Polyline
        /// <summary>
        /// decodes a string into a list of latlon objects
        /// from http://www.soulsolutions.com.au/Articles/Encodingforperformance.aspx
        /// </summary>
        /// <param name="encoded">encoded string</param>
        /// <returns>list of latlon</returns>
        private static List<Point> DecodeLatLong(string encoded)
        {
            List<Point> locs = new List<Point>();

            int index = 0;
            int lat = 0;
            int lng = 0;

            int len = encoded.Length;
            while (index < len) {
                lat += decodePoint(encoded, index, out index);
                if (index < len)
                    lng += decodePoint(encoded, index, out index);

                double latf = lat * 1e-5;
                double lngf = lng * 1e-5;

                Point l = new Point(latf, lngf);
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
                //get binary encoding, shift, and move to next chunk
                b = Convert.ToInt32(encoded[startindex++]) - minASCII;
                result |= (b & 0x1f) << shift;
                shift += binaryChunkSize;
            } while (b >= 0x20); //see if another binary value
            // negative flip, and set output index
            int dlat = (((result & 1) > 0) ? ~(result >> 1) : (result >> 1));
            finishindex = startindex;
            return dlat;
        }
        #endregion

    }
}
