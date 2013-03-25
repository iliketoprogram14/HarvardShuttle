﻿using HarvardShuttle.Data;
using TileBackground;
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
        private bool isFav;
        private string favoritesXmlCache;
        private StorageFile file;

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
        protected async override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            Tuple<string, string> items = (Tuple<string, string>)navigationParameter;
            currOrigin = items.Item1;
            currDest = items.Item2;

            file = await GroupedItemsPage.localFolder.GetFileAsync(GroupedItemsPage.favoritesStore);
            favoritesXmlCache = await FileIO.ReadTextAsync(file);

            isFav = false;

            // Update the schedule asynchronously
            Scheduler.CreateSchedule(currOrigin, currDest, this.ResultsList, this.Height, this.numMinutesTextBlock);
            UpdateOriginDest(currOrigin, currDest);

            // Update style of favorite button
            UpdateFavButton(currOrigin, currDest);

            // Register the background task
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

        /// <summary>
        /// Updates the UI with the origin and destination
        /// </summary>
        /// <param name="origin">The origin of the trip</param>
        /// <param name="dest">The destination of the trip</param>
        private void UpdateOriginDest(string origin, string dest)
        {
            this.originTextBlock.Text = origin;
            this.destTextBlock.Text = dest;
        }

        private async void UpdateFavButton(string currOrigin, string currDest)
        {
            // Grab the favorites xml
            //StorageFile file = await GroupedItemsPage.localFolder.GetFileAsync(GroupedItemsPage.favoritesStore);
            //string favoritesXml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(favoritesXmlCache);

            int i = 1;
            bool favExists = false;
            foreach (XmlElement elem in doc.GetElementsByTagName("trip")) {
                string origin = elem.GetAttribute("origin");
                string dest = elem.GetAttribute("dest");

                if (origin.Equals(currOrigin) && dest.Equals(currDest)) {
                    favExists = true;
                    break;
                }

                i++;
            }

            isFav = favExists;
            UpdateButtonStyle();
        }

        private void UpdateButtonStyle()
        {
            if (isFav)
                this.FavoriteButton.Style = (Style)Application.Current.Resources["UnfavoriteAppBarButtonStyle"];
            else 
                this.FavoriteButton.Style = (Style)Application.Current.Resources["FavoriteAppBarButtonStyle"];
        }

        /// <summary>
        /// Registers a background task to update the live tile
        /// </summary>
        private static void RegisterBackgroundTask()
        {
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == "BackgroundLiveTiles")
                    task.Value.Unregister(true);
            }

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

            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                var derp = task.ToString();
            }
        }

        /// <summary>
        /// Event handler for favorites button; adds trip to the favorites section on the main screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //StorageFile file = await GroupedItemsPage.localFolder.GetFileAsync(GroupedItemsPage.favoritesStore);
            //string favoritesXml = await FileIO.ReadTextAsync(file);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(favoritesXmlCache);

            // remove from favorites
            string newxml;
            if (isFav) {
                string toRemove = "<trip origin=\"" + currOrigin + "\" dest=\"" + currDest + "\"></trip>";
                newxml = favoritesXmlCache.Replace(toRemove, "");
            }
            // add to favorites
            else {
                string[] delim = { "</favorites>" };
                string[] blah = favoritesXmlCache.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                newxml = blah[0] + "<trip origin=\"" +
                    currOrigin + "\" dest=\"" + currDest + "\"></trip></favorites>";
            }
            favoritesXmlCache = newxml;
            isFav = !isFav;
            UpdateButtonStyle();
            await FileIO.WriteTextAsync(file, favoritesXmlCache);
        }

    }
}
