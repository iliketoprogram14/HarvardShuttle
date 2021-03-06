﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using System.Net.Http;
using Windows.Data.Xml;
using System.IO;
using System.Net;
using System.Xml;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Networking.Connectivity;
using Windows.Data.Json;
using System.Diagnostics;

namespace DataStore
{
    public class Scheduler
    {
        private static string store = "last_origin_dest.xml";
        private static int maxListings = 30;
        private static int maxNotifications = 32;

        public async static Task<Tuple<List<string>, string, string>> CreateSchedule(string new_origin, string new_dest)
        {
            List<string> newResults = new List<string>();
            string newNumMinutes = "";
            string newUnits = "";

            Tuple<string, string> serviceResults = await CallService(new_origin, new_dest, newResults);
            newNumMinutes = serviceResults.Item1;
            newUnits = serviceResults.Item2;
            UpdateLastOriginDest(new_origin, new_dest);
            return Tuple.Create<List<string>, string, string>(newResults, newNumMinutes, newUnits);
        }

        public async static Task CreateExtendedSchedule()
        {
            StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(store);
            string storeXml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(storeXml);

            string origin = doc.GetElementsByTagName("origin")[0].InnerText;
            string dest = doc.GetElementsByTagName("dest")[0].InnerText;

            Task.WaitAll(CallService(origin, dest, null));

            // clean up
            doc = null;
            file = null;
        }
        
        private static int GetNewMinuteCountdown(string timeStr)
        {
            string[] time = timeStr.Split(':');
            int departMin = Convert.ToInt32(time[1]);
            int departHour = Convert.ToInt32(time[0]);

            int minuteCountdown = departHour * 60 + departMin - (DateTime.Now.TimeOfDay.Hours * 60 + DateTime.Now.TimeOfDay.Minutes);
            if (minuteCountdown < 0) minuteCountdown = (24 * 60) + minuteCountdown;

            // clean up
            time = null;

            return minuteCountdown;
        }
        
        private async static void UpdateLastOriginDest(string new_origin, string new_dest)
        {
            string xml = "<last_trip>";
            xml += "<origin>" + new_origin + "</origin>";
            xml += "<dest>" + new_dest + "</dest>";
            xml += "</last_trip>";

            bool fileExists = true;
            StorageFile file = null;
            try {
                file = await ApplicationData.Current.LocalFolder.GetFileAsync(store);
            }
            catch (Exception) {
                fileExists = false;
            }
            if (!fileExists)
                file = await ApplicationData.Current.LocalFolder.CreateFileAsync(store, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, xml);

            // clean up
            file = null;
        }

        private async static Task<Tuple<string, string>> CallService(string origin, string dest, List<string> results)
        {
            string newNumMinutes = "";
            string newUnits = "";
            
            List<string> times = await MainDataStore.GetTimesForSchedule(20, origin, dest);
            if (times.Count == 0)
                return Tuple.Create<string, string>("", "");

            TileUpdater updater = CreateNewTileUpdater();

            int minuteCountdown = 0;
            int numNotifications = 0;
            int numListings = 0;
            foreach (string time in times) {

                minuteCountdown = GetNewMinuteCountdown(time);

                // add listings to main UI
                if (results != null) {
                    if (numNotifications == 0) {
                        Tuple<string, string> countdownBoxResults = SetCountdownBox(minuteCountdown);
                        newNumMinutes = countdownBoxResults.Item1;
                        newUnits = countdownBoxResults.Item2;
                    }
                    else
                        AddListing(minuteCountdown, results);
                    numListings++;
                }

                // add tile notifications
                if (numNotifications <= maxNotifications)
                    numNotifications = AddTileNotifications(minuteCountdown, numNotifications, origin, dest, updater);

                if (numListings > maxListings)
                    break;
            }

            if (results != null && results.Count == 0)
                results.Add("No further times scheduled.");

            // clean up
            updater = null;
            times.Clear(); times = null;

            return Tuple.Create<string, string>(newNumMinutes, newUnits);
        }

        private static bool IsThereInternet()
        {
            ConnectionProfile connections = NetworkInformation.GetInternetConnectionProfile();
            bool internet = connections != null && connections.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
            connections = null;
            return internet;
        }

        private static int GetMinuteCountdown(string nodeValue)
        {
            string[] arr = nodeValue.Split('T');
            string[] date = arr[0].Split('-');
            string[] time = arr[1].Split(':');
            int departMin = Convert.ToInt32(time[1]);
            int departHour = Convert.ToInt32(time[0]);

            int minuteCountdown = departHour * 60 + departMin - (DateTime.Now.TimeOfDay.Hours * 60 + DateTime.Now.TimeOfDay.Minutes);
            if (minuteCountdown < 0) minuteCountdown = (24 * 60) + minuteCountdown;

            return minuteCountdown;
        }

        private static void AddListing(int minuteCountdown, List<string> resultsList)
        {
            int hourCountdown = minuteCountdown / 60;
            minuteCountdown = minuteCountdown % 60;

            string msg = (hourCountdown == 0) ? "" : hourCountdown.ToString() + " hour" + ((hourCountdown == 1) ? " " : "s ");
            msg += minuteCountdown.ToString() + " minute" + ((minuteCountdown == 1) ? "" : "s");

            resultsList.Add(msg);
        }

        private static int AddTileNotifications(int nextMinuteCountdown, int numNotifications, string origin, string dest, TileUpdater updater)
        {
            XmlDocument tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquareText02);
            XmlDocument wTileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWideBlockAndText01);
            var node = tileXml.ImportNode(wTileXml.GetElementsByTagName("binding").Item(0), true);
            tileXml.GetElementsByTagName("visual").Item(0).AppendChild(node);

            // set the branding to none
            XmlElement bindElem = (XmlElement)tileXml.GetElementsByTagName("binding").Item(0);
            bindElem.SetAttribute("branding", "Name");
            bindElem = (XmlElement)tileXml.GetElementsByTagName("binding").Item(1);
            bindElem.SetAttribute("branding", "Name");

            // Set text
            XmlNodeList tileTextAttributes = tileXml.GetElementsByTagName("binding").Item(0).ChildNodes;
            XmlNodeList wTileTextAttributes = tileXml.GetElementsByTagName("binding").Item(1).ChildNodes;
            tileTextAttributes[1].InnerText = origin + " to " + dest;
            wTileTextAttributes[0].InnerText = origin + " to ";
            wTileTextAttributes[1].InnerText = dest;

            // set current live tile
            DateTime now = DateTime.Now;
            if (numNotifications == 0) {
                string tileStr = GenTileString(nextMinuteCountdown);
                tileTextAttributes[0].InnerText = tileStr;
                SetWideTileAttr(wTileTextAttributes, tileStr);
                var tileNotification = new TileNotification(tileXml);
                tileNotification.ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(1.0);
                tileNotification.Tag = "0";
                updater.Update(tileNotification);
                numNotifications++;
                tileNotification = null;
            }

            nextMinuteCountdown -= numNotifications;

            // schedule future live tiles
            double i = (double)numNotifications;
            while (nextMinuteCountdown >= 0 && i <= maxNotifications) {
                string tileStr = GenTileString(nextMinuteCountdown);
                tileTextAttributes[0].InnerText = tileStr;
                SetWideTileAttr(wTileTextAttributes, tileStr);
                var tileNotification = new ScheduledTileNotification(tileXml, now.AddMinutes(i));
                tileNotification.ExpirationTime = now.AddMinutes(1.0 + i); //DateTime.UtcNow.AddMinutes(1.0 + i);
                tileNotification.Tag = i.ToString();
                updater.AddToSchedule(tileNotification);
                i++;
                nextMinuteCountdown--;
                tileNotification = null;
            }

            // clean up
            wTileTextAttributes = null;
            tileTextAttributes = null;
            bindElem = null;
            node = null;
            wTileXml = null;
            tileXml = null;

            return (int)i;
        }

        private static void SetWideTileAttr(XmlNodeList tileAttr, string tileStr)
        {
            if (tileStr.Contains(" min")) {
                tileAttr[4].InnerText = tileStr.Replace(" min", "");
                tileAttr[5].InnerText = "minutes";
            }
            else {
                string hoursNoRemainder = tileStr.Replace(" hrs", "").Split('.')[0];
                tileAttr[4].InnerText = hoursNoRemainder;
                tileAttr[5].InnerText = (hoursNoRemainder == "1") ? "hour" : "hours";
            }
        }

        private static Tuple<string, string> SetCountdownBox(int minutes)
        {
            string newNumMinutes = "";
            string newUnits = "";
            if (minutes < 100) {
                newNumMinutes = minutes.ToString();
                newUnits = "minutes";
            }
            else {
                double hours = Math.Round((double)minutes / 60.0, 1);
                newNumMinutes = hours.ToString();
                newUnits = "hours";
            }
            return Tuple.Create<string, string>(newNumMinutes, newUnits);
        }

        private static string GenTileString(int minutes)
        {
            if (minutes < 100)
                return minutes.ToString() + " min";

            double hours = Math.Round((double)minutes / 60.0, 1);

            return hours.ToString() + " hrs";
        }

        private static TileUpdater CreateNewTileUpdater()
        {
            var updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.EnableNotificationQueue(true);
            updater.Clear();
            foreach (var tile in updater.GetScheduledTileNotifications())
                updater.RemoveFromSchedule(tile);
            return updater;
        }

    }
}
