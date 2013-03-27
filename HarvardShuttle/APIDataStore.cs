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
                string xml = GetHeader();
                xml += "<api_store>" + stopsAndRoutes +"</api_store>";
                StorageFile file = await localFolder.CreateFileAsync(dataStorePath, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, xml);
            }
        }

        private static string GetHeader()
        {
            string xml = "";/*"<?xml version='1.0' ?>" +
                "<!DOCTYPE api_store [" +
                "<!ELEMENT api_store (stops,routes) >" +
                "<!ELEMENT stops (stop+) >" +
                "<!ELEMENT routes (route+) >" +
                "<!ELEMENT stop (title, cs50name, stop_routes) >" +
                "<!ELEMENT title (#PCDATA) >" +
                "<!ELEMENT cs50name (#PCDATA) >" +
                //"<!ELEMENT transloc_name (#PCDATA) >" +
                "<!ELEMENT stop_routes (#PCDATA) >" +
                "<!ELEMENT route (#PCDATA) >" +
                "<!ATTLIST stop id ID #REQUIRED >" +
                "<!ATTLIST route id ID #REQUIRED >" +
                "]>";*/
            return xml;

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
            JsonArray data = obj["data"].GetArray();
            foreach (JsonValue stop in data) {
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
            dests += stop_id +",";
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

        public async static Task<DataGroup> GetDestinations(string origin) 
        {
            // Init data structures
            var itemGroup = DataSource.GetGroup("Group-1");
            DataGroup destGroup = new DataGroup("Dest-Group", "To", "", "Assets/DarkGray.png", "");
            Dictionary<string, string> idToTitle = new Dictionary<string,string>();
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

        public static void GetArrivalEstimates(string origin, string dest)
        {

        }

        public static void GetRoutes(string origin, string dest)
        {

        }

        public static void GetShuttles(string origin, string dest)
        {

        }
    }
}
