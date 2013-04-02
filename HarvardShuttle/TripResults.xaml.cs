using HarvardShuttle.Data;
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
using Bing.Maps;
using Windows.UI;
using Windows.UI.Xaml.Media.Animation;

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
            UpdateFavoritesCache(currOrigin, currDest);

            this.pageTitle.Text = "Trip Results";
            this.estimateBox.Text = await Task<string>.Run(() => APIDataStore.GetArrivalEstimates(currOrigin, currDest));
            this.estimatedMinutesTextBlock.Text = "minutes";
            UpdateOriginDest(currOrigin, currDest);

            // Update the schedule asynchronously
            ScheduleGenerator.CreateNewSchedule(currOrigin, currDest, this.ResultsList, this.Height, this.numMinutesTextBlock, this.minutesTextBlock);

            // Register the background task
            if (GroupedItemsPage.asyncStatus != BackgroundAccessStatus.Denied &&
                GroupedItemsPage.asyncStatus != BackgroundAccessStatus.Unspecified)
                RegisterBackgroundTask();


            Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap;
            if (estimateBox.Text != "") {
                // if boardingTime > scheduledTime, changed boarding box to say "Probably leaving in (may need to span more columns)
                // Get Routes (returns routes shared between origin and dest that are active, mapped to tuple of color, name and list of segments)
                routeMap = await APIDataStore.GetRoutes(currOrigin, currDest);
            }
            else {
                // "No estimate for boarding time" (may need to span more columns)
                // make invisible estimateBox, estimateMinutesBox
                // get all routes
                routeMap = await APIDataStore.GetRoutes("", "");
            }

            // if there are no routes, hide map and color code, replace with message that there are no routes currently running
            if (routeMap.Keys.Count == 0) {
                this.shuttleMap.Visibility = Visibility.Collapsed;
                // collapse color code
                // display message that no routes are running
                return;
            }

            MakePolylines(routeMap);

            // update color codes on UI

            // plot shuttles
            AddShuttles(routeMap);
            // make background task to plot shuttles every 1-2 seconds

            // Fade the map in
            Storyboard story = new Storyboard();
            DoubleAnimation anim = new DoubleAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(1100);
            anim.From = 0;
            anim.To = 1;
            story.Children.Add(anim);
            Storyboard.SetTarget(anim, shuttleMap);
            Storyboard.SetTargetProperty(anim, "UIElement.Opacity");
            story.Begin();
        }

        private async void AddShuttles(Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap)
        {
            List<Tuple<string, Location>> shuttleLocs = await APIDataStore.GetShuttles(routeMap.Keys.ToList<string>());
            foreach (var derp in shuttleLocs) {
                BigPushPin p = new BigPushPin();
                string routeColor = routeMap[derp.Item1].Item2;
                byte r = byte.Parse(routeColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(routeColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(routeColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                p.SetBackground(new SolidColorBrush(Color.FromArgb(255, r, g, b)));

                MapLayer.SetPositionAnchor(p, new Point(23/2, 33/2));
                MapLayer.SetPosition(p, derp.Item2);
                shuttleMap.Children.Add(p);
            }
        }

        private void MakePolylines(Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap)
        {
            // maps pairs of locations (small segments) to a list of corresponding IDs
            //Dictionary<Tuple<Location, Location>, List<string>> segmentMap = new Dictionary<Tuple<Location, Location>, List<string>>();
            Dictionary<double, Tuple<Location, Location, List<string>>> segmentMap = new Dictionary<double, Tuple<Location, Location, List<string>>>();
            foreach (string routeId in routeMap.Keys) {
                Tuple<string, string, List<LocationCollection>> routeData = routeMap[routeId];
                List<LocationCollection> locs = routeData.Item3;
                foreach (LocationCollection collection in locs) {
                    Location prevLoc = null;
                    foreach (Location loc in collection) {
                        if (prevLoc != null) {
                            double key = prevLoc.Latitude + prevLoc.Longitude + loc.Latitude + loc.Longitude;
                            //var locPair = Tuple.Create<Location, Location>(prevLoc, loc);
                            List<string> id_list = new List<string>();
                            if (segmentMap.ContainsKey(key)) {
                                id_list = segmentMap[key].Item3;
                            }
                            if (!id_list.Contains(routeId))
                                id_list.Add(routeId);
                            segmentMap[key] = Tuple.Create<Location, Location, List<string>>(prevLoc, loc, id_list);
                        }
                        prevLoc = loc;
                    }
                }
            }

            foreach (double key in segmentMap.Keys) {
                var val = segmentMap[key];
                List<string> ids = val.Item3;
                Location loc1 = val.Item1;
                Location loc2 = val.Item2;
                if (ids.Count > 1) {
                    int n = ids.Count;
                    double latDiff = (loc1.Latitude - loc2.Latitude) / n;
                    double lngDiff = (loc1.Longitude - loc2.Longitude) / n;

                    for (int i = 0; i < n; i++) {
                        Location new_loc1 = new Location(loc1.Latitude + latDiff * i, loc1.Longitude + lngDiff * i);
                        Location new_loc2 = new Location(loc1.Latitude + latDiff * (i + 1), loc1.Longitude + lngDiff * (i + 1));
                        LocationCollection locCollection = new LocationCollection();
                        locCollection.Add(new_loc1);
                        locCollection.Add(new_loc2);
                        // split up into n finer grained location pairs
                        // add each pair with a different id and color
                        string routeId = ids[i];
                        string routeColor = routeMap[routeId].Item2;
                        byte r = byte.Parse(routeColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                        byte g = byte.Parse(routeColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                        byte b = byte.Parse(routeColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                        MapShapeLayer shapeLayer = new MapShapeLayer();
                        MapPolyline polyline = new MapPolyline();
                        polyline.Locations = locCollection;
                        polyline.Color = Color.FromArgb(150, r, g, b);
                        polyline.Width = 7;
                        shapeLayer.Shapes.Add(polyline);
                        shuttleMap.ShapeLayers.Add(shapeLayer);
                    }
                }
                else {
                    LocationCollection locCollection = new LocationCollection();
                    locCollection.Add(loc1);
                    locCollection.Add(loc2);
                    string routeId = ids[0];
                    string routeColor = routeMap[routeId].Item2;
                    byte r = byte.Parse(routeColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    byte g = byte.Parse(routeColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    byte b = byte.Parse(routeColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    MapShapeLayer shapeLayer = new MapShapeLayer();
                    MapPolyline polyline = new MapPolyline();
                    polyline.Locations = locCollection;
                    polyline.Color = Color.FromArgb(150, r, g, b);
                    polyline.Width = 7;
                    shapeLayer.Shapes.Add(polyline);
                    shuttleMap.ShapeLayers.Add(shapeLayer);

                }
            }
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

        private async void UpdateFavoritesCache(string origin, string dest)
        {
            file = await GroupedItemsPage.localFolder.GetFileAsync(GroupedItemsPage.favoritesStorePath);
            favoritesXmlCache = await FileIO.ReadTextAsync(file);

            isFav = false;

            // Update style of favorite button
            UpdateFavButton(origin, dest);
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

        private void UpdateFavButton(string currOrigin, string currDest)
        {
            // Grab the favorites xml
            //StorageFile file = await GroupedItemsPage.localFolder.GetFileAsync(GroupedItemsPage.favoritesStorePath);
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
            foreach (var task in BackgroundTaskRegistration.AllTasks) {
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

            foreach (var task in BackgroundTaskRegistration.AllTasks) {
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
            //StorageFile file = await GroupedItemsPage.localFolder.GetFileAsync(GroupedItemsPage.favoritesStorePath);
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
