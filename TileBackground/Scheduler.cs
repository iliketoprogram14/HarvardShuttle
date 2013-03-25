using System;
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


namespace TileBackground
{
    public static class Scheduler
    {
        private static string store = "last_origin_dest.xml";
        private static int maxListings = 30;
        private static int maxNotifications = 15;

        public static void CreateSchedule(string new_origin, string new_dest, ListView results, double height, TextBlock box)
        {
            CallService(new_origin, new_dest, results, box);
            results.Height = (results.Items.Count * 20 < height) ?
                results.Items.Count * 20 : height;
            UpdateLastOriginDest(new_origin, new_dest);
        }

        public async static void CreateExtendedSchedule()
        {
            StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(Scheduler.store);
            string storeXml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(storeXml);

            string origin = doc.GetElementsByTagName("origin")[0].InnerText;
            string dest = doc.GetElementsByTagName("dest")[0].InnerText;

            CallService(origin, dest, null, null);
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
                file = await ApplicationData.Current.LocalFolder.GetFileAsync(Scheduler.store);
            } catch (Exception) {
                fileExists = false;
            }
            if (!fileExists)
                file = await ApplicationData.Current.LocalFolder.CreateFileAsync(Scheduler.store, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, xml);
        }

        private async static void CallService(string origin, string dest, ListView resultsList, TextBlock box)
        {
            string url = "http://shuttleboy.cs50.net/api/1.2/trips?a=" + origin +
                "&b=" + dest + "&output=xml";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            TileUpdater updater = CreateNewTileUpdater();

            // get the next countdown from Shuttleboy
            int minuteCountdown = 0;
            int numNotifications = 0;
            int numListings = 0;

            try
            {
                var client = new System.Net.Http.HttpClient();
                HttpResponseMessage response = client.GetAsync(url).Result;
                using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                using (XmlReader reader = XmlReader.Create(responseStream))
                {
                    bool shouldExit = false;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "departs")
                        {
                            reader.Read();
                            minuteCountdown = GetMinuteCountdown(reader.Value);

                            // add listings to main UI
                            if (resultsList != null)
                            {
                                if (numNotifications == 0)
                                    box.Text = minuteCountdown.ToString();
                                else
                                    AddListing(minuteCountdown, resultsList);
                                numListings++;
                            }

                            // add tile notifications
                            if (numNotifications <= maxNotifications)
                                numNotifications = AddTileNotifications(minuteCountdown, numNotifications, origin, dest, updater);

                            if (numListings > maxListings)
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                int d = 0;
            }
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

        private static void AddListing(int minuteCountdown, ListView resultsList)
        {
            int hourCountdown = minuteCountdown / 60;
            minuteCountdown = minuteCountdown % 60;

            string msg = (hourCountdown == 0) ? "" : hourCountdown.ToString() + " hour" + ((hourCountdown == 1) ? " " : "s ");
            msg += minuteCountdown.ToString() + " minute" + ((minuteCountdown == 1) ? "" : "s");

            resultsList.Items.Add(msg);
        }

        private static int AddTileNotifications(int nextMinuteCountdown, int numNotifications, string origin, string dest, TileUpdater updater)
        {
            XmlDocument tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquareText02);

            // set the branding to none
            XmlElement bindElem = (XmlElement)tileXml.GetElementsByTagName("binding").Item(0);
            bindElem.SetAttribute("branding", "None");

            // set text
            XmlNodeList tileTextAttributes = tileXml.GetElementsByTagName("text");
            tileTextAttributes[1].InnerText = origin + " to " + dest;

            // set current live tile
            DateTime now = DateTime.Now;
            if (numNotifications == 0)
            {
                tileTextAttributes[0].InnerText = GenTileString(nextMinuteCountdown);
                var tileNotification = new TileNotification(tileXml);
                tileNotification.ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(1.0);
                tileNotification.Tag = "0";
                updater.Update(tileNotification);
                numNotifications++;
            }

            nextMinuteCountdown -= numNotifications;

            // schedule future live tiles
            double i = (double)numNotifications;
            while (nextMinuteCountdown >= 0 && i <= maxNotifications)
            {
                tileTextAttributes[0].InnerText = GenTileString(nextMinuteCountdown);
                var tileNotification = new ScheduledTileNotification(tileXml, now.AddMinutes(i));
                tileNotification.ExpirationTime = now.AddMinutes(1.0 + i); //DateTime.UtcNow.AddMinutes(1.0 + i);
                tileNotification.Tag = i.ToString();
                updater.AddToSchedule(tileNotification);
                i++;
                nextMinuteCountdown--;
            }

            return (int)i;
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
            {
                updater.RemoveFromSchedule(tile);
            }
            return updater;
        }

    }
}
