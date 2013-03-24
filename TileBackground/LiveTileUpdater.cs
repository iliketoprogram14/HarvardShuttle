using System;
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

namespace TileBackground
{
    public sealed class LiveTileUpdater
    {
        private static TileUpdater updater = null;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();

            if (updater == null)
                updater = TileUpdateManager.CreateTileUpdaterForApplication(); 
            updater.EnableNotificationQueue(true);

            GetData(); 
            deferral.Complete();
        }

        private void GetData() {
            var textNodes = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquareBlock).GetElementsByTagName("text");
            int timeLeft = Int32.Parse(textNodes[0].InnerText);
            string locations = textNodes[1].InnerText;
            
            CallService(locations, timeLeft);
        }

        private async void CallService(string locations, int timeLeft) {
            string[] delim = { " to " };
            string[] originDest = locations.Split(delim, StringSplitOptions.RemoveEmptyEntries);
            string origin = originDest[0]; string dest = originDest[1];

            string url = "http://shuttleboy.cs50.net/api/1.2/trips?a=" + origin +
                "&b=" + dest + "&output=xml";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            updater.Clear();

            // get the next countdown from Shuttleboy
            int minuteCountdown = 0;
            int numNotifications = 0;
            using (WebResponse response = await request.GetResponseAsync())
            using (Stream responseStream = response.GetResponseStream())
            using (XmlReader reader = XmlReader.Create(responseStream))
            {
                bool shouldExit = false;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "departs")
                    {
                        reader.Read();
                        minuteCountdown = GetMinuteCountdown(reader.Value);

                        // add tile notifications
                        if (numNotifications == 0 || numNotifications <= 15)
                            numNotifications = AddTileNotifications(minuteCountdown, numNotifications, origin, dest, updater);
                        else
                            shouldExit = true;
                    }
                    if (shouldExit) break;
                }
            }
        }

        private int GetMinuteCountdown(string nodeValue)
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

        private static int AddTileNotifications(int nextMinuteCountdown, int numNotifications, string origin, string dest, TileUpdater updater)
        {
            XmlDocument tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquareBlock);

            // set the branding to none
            XmlElement bindElem = (XmlElement)tileXml.GetElementsByTagName("binding").Item(0);
            bindElem.SetAttribute("branding", "None");

            // set text
            XmlNodeList tileTextAttributes = tileXml.GetElementsByTagName("text");
            tileTextAttributes[1].InnerText = origin + " to " + dest;

            // first live tile
            if (numNotifications == 0)
            {
                tileTextAttributes[0].InnerText = nextMinuteCountdown.ToString();
                var tileNotification = new TileNotification(tileXml);
                tileNotification.ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(1.0);
                tileNotification.Tag = "0";
                updater.Update(tileNotification);
                numNotifications++;
            }

            nextMinuteCountdown -= numNotifications;

            // set notification
            double i = (double)numNotifications;
            while (nextMinuteCountdown >= 0 && i < 15)
            {
                tileTextAttributes[0].InnerText = nextMinuteCountdown.ToString();
                var tileNotification = new ScheduledTileNotification(tileXml, DateTime.Now.AddMinutes(i));
                tileNotification.ExpirationTime = DateTime.UtcNow.AddMinutes(1.0 + i);
                tileNotification.Tag = i.ToString();
                updater.AddToSchedule(tileNotification);
                i++;
                nextMinuteCountdown--;
            }

            return (int)i;
        }

    }
}
