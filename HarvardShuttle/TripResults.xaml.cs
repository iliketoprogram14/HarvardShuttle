using HarvardShuttle.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.ApplicationModel.Background;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace HarvardShuttle
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class TripResults : HarvardShuttle.Common.LayoutAwarePage
    {
        public static TileUpdater updater = null;
        private string currOrigin;
        private string currDest;

        public TripResults()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            Tuple<string, string> items = (Tuple<string, string>)navigationParameter;
            currOrigin = items.Item1;
            currDest = items.Item2;

            GetSchedule(currOrigin, currDest);
            RegisterBackgroundTask();
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
        }

        private async void GetSchedule(String origin, String dest)
        {
            string url = "http://shuttleboy.cs50.net/api/1.2/trips?a=" + origin +
                "&b=" + dest + "&output=xml";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            if (updater == null)
                updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.Clear();

            int resultCount = 0;
            bool shouldExit = false;
            int numNotifications = 0;
            
            using (WebResponse response = await request.GetResponseAsync())
            using (Stream responseStream = response.GetResponseStream())
            using (XmlReader reader = XmlReader.Create(responseStream))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "departs")
                    {
                        reader.Read();
                        int minuteCountdown = GetMinuteCountdown(reader.Value);

                        // update list with times
                        if (numNotifications == 0)
                            UpdateMainUI(minuteCountdown, origin, dest);
                        else
                            AddListing(minuteCountdown, numNotifications);

                        // add tile notifications
                        if (numNotifications == 0 || numNotifications <= 15)
                            numNotifications = AddTileNotifications(minuteCountdown, numNotifications, origin, dest, updater);

                        resultCount += 1;
                        if (resultCount > 20) 
                            shouldExit = true;
                    }
                    if (shouldExit) break;
                }
            }

            this.ResultsList.Height = (this.ResultsList.Items.Count * 20 < this.Height) ? 
                this.ResultsList.Items.Count * 20 : this.Height;
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

        private void UpdateMainUI(int minuteCountdown, string origin, string dest)
        {
            this.numMinutesTextBlock.Text = minuteCountdown.ToString();
            this.originTextBlock.Text = origin;
            this.destTextBlock.Text = dest;
        }

        private void AddListing(int minuteCountdown, int numNotifications)
        {
            int hourCountdown = minuteCountdown / 60;
            minuteCountdown = minuteCountdown % 60;

            string msg = (hourCountdown == 0) ? "" : hourCountdown.ToString() + " hour" + ((hourCountdown == 1) ? " " : "s ");
            msg += minuteCountdown.ToString() + " minute" + ((minuteCountdown == 1) ? "" : "s");

            this.ResultsList.Items.Add(msg);

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

        private static void RegisterBackgroundTask() {
            bool backgroundTaskRunning = false;
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == "BackgroundLiveTiles")
                    task.Value.Unregister(true);
            }

            if (backgroundTaskRunning) return;

            //var result = await BackgroundExecutionManager.RequestAccessAsync();
            BackgroundTaskBuilder builder = new BackgroundTaskBuilder();

            // Friendly string name identifying the background task
            builder.Name = "BackgroundLiveTiles";
            // Class name
            builder.TaskEntryPoint = "TileBackground.LiveTileUpdater";

            IBackgroundTrigger trigger = new TimeTrigger(15, false);
            builder.SetTrigger(trigger);
            IBackgroundCondition condition = new SystemCondition(SystemConditionType.InternetAvailable);
            builder.AddCondition(condition);

            IBackgroundTaskRegistration taskRegistration = builder.Register();
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // add to the favorites screen
            StorageFile file = await GroupedItemsPage.localFolder.GetFileAsync(GroupedItemsPage.favoritesStore);
            string favoritesXml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(favoritesXml);
            SampleDataGroup favsGroup = SampleDataSource.GetGroup("Group-2");

            int i = 1;
            bool favExists = false;
            foreach (XmlElement elem in doc.GetElementsByTagName("trip"))
            {
                string origin = elem.GetAttribute("origin");
                string dest = elem.GetAttribute("dest");

                if (origin.Equals(currOrigin) && dest.Equals(currDest))
                {
                    favExists = true;
                    break;
                }

                SampleDataItem item = new SampleDataItem("Group-2-Item-" + i.ToString(), origin, dest, "", "from " + origin + " to " + dest, "", favsGroup);
                SampleDataSource.GetGroup("Group-2").Items.Add(item);

                i++;
            }

            if (!favExists)
            {
                string[] delim = { "</favorites>" };
                string[] blah = favoritesXml.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                string newxml = blah[0] + "<trip origin=\"" + 
                    currOrigin + "\" dest=\"" + currDest + "\"></trip></favorites>" ;
                await FileIO.WriteTextAsync(file, newxml);
            }
        }

    }
}
