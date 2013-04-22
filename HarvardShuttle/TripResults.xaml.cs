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
using DataStore;
using Windows.System.Threading;
using System.Diagnostics;

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
        private Dictionary<string,string> commonRouteIDsColors;
        private ThreadPoolTimer uiUpdaterTimer;
        private ThreadPoolTimer PeriodicTimer;

        public TripResults()
        {
            this.InitializeComponent();
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

            // Register the background task
            if (GroupedItemsPage.asyncStatus != BackgroundAccessStatus.Denied &&
                GroupedItemsPage.asyncStatus != BackgroundAccessStatus.Unspecified)
                RegisterBackgroundTask();

            this.pageTitle.Text = "Trip Results";
            UpdateOriginDest(currOrigin, currDest);

            // grab the route map (route id -> (name, color, segment list)
            commonRouteIDsColors = new Dictionary<string, string>();
            Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap;
            routeMap = await PopulateUI();
            uiUpdaterTimer = ThreadPoolTimer.CreatePeriodicTimer(UpdateUI, TimeSpan.FromSeconds(30));
            if (routeMap == null)
                return;

            // cache route IDs and their colors
            commonRouteIDsColors = new Dictionary<string, string>();
            foreach (var key in routeMap.Keys)
                commonRouteIDsColors[key] = routeMap[key].Item2;

            MakePolylines(routeMap);

            // update color codes on UI
            foreach (var key in commonRouteIDsColors.Keys) {
                string name = routeMap[key].Item1;
                this.ColorCodePanel.Children.Add(new ColorCodeBox(commonRouteIDsColors[key], name));
            }

            // plot shuttles
            List<Tuple<string, Point>> shuttleLocs = await MainDataStore.GetShuttles(commonRouteIDsColors.Keys.ToList<string>());
            if (shuttleLocs != null) // there is internet
                AddShuttles(shuttleLocs);
            PeriodicTimer = ThreadPoolTimer.CreatePeriodicTimer(UpdateShuttles, TimeSpan.FromMilliseconds(2500));

            FadeInMap();

            // clean up
            shuttleLocs = null;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (PeriodicTimer != null)
                PeriodicTimer.Cancel();
            PeriodicTimer = null;
            if (uiUpdaterTimer != null)
                uiUpdaterTimer.Cancel();
            uiUpdaterTimer = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        #region UI Updaters
        private bool EstimatedWaitGreaterThanScheduledWait()
        {
            string estimateUnits = this.estimatedMinutesTextBlock.Text;
            string scheduledUnits = this.minutesTextBlock.Text;

            if (estimateUnits.Contains("minute") && scheduledUnits.Contains("hour"))
                return false;
            if (estimateUnits.Contains("hour") && scheduledUnits.Contains("minute"))
                return true;

            double estimate = Double.Parse(this.estimateBox.Text);
            double scheduled = Double.Parse(this.numMinutesTextBlock.Text);

            return (estimate > scheduled);
        }

        private bool EstimatedWaitIsForSecondShuttle()
        {
            string estimateUnits = this.estimatedMinutesTextBlock.Text;
            string nextScheduledTime = this.ResultsList.Items[0].ToString();

            if (estimateUnits == "minutes" && nextScheduledTime.Contains("hour"))
                return false;
            if (estimateUnits == "hours" && !nextScheduledTime.Contains("minute"))
                return true;

            double nextScheduled = 0;
            if (nextScheduledTime.Contains("hour")) {
                string[] delim = {"hour", "hours", "minute", "minutes"};
                string[] fields = nextScheduledTime.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                nextScheduled += Double.Parse(fields[0]);
                if (fields.Length > 1)
                    nextScheduled += Double.Parse(fields[1]);
            } else {
                string[] delim = {"minute", "minutes"};
                string[] fields = nextScheduledTime.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                nextScheduled += Double.Parse(fields[0]);
            }

            double estimate = Double.Parse(this.estimateBox.Text);
            double scheduled = nextScheduled;

            return (Math.Abs(estimate - scheduled) <= 2.5);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scheduleResults"></param>
        /// <param name="numMinutes"></param>
        /// <param name="minutes"></param>
        /// <param name="estimateTextBlockText"></param>
        /// <returns>true if running, false otherwise </returns>
        private bool UpdateTextBoxes(List<string> scheduleResults, string numMinutes, string minutes, string estimateTextBlockText)
        {
            if (ResultsList.Items.Count == 0)
                foreach (string s in scheduleResults)
                    this.ResultsList.Items.Add(s);
            // potentially dangerous...
            else 
                for (int i = 0; i < scheduleResults.Count; i++)
                    this.ResultsList.Items[i] = scheduleResults[i];

            this.numMinutesTextBlock.Text = numMinutes;
            this.minutesTextBlock.Text = minutes;
            this.estimateBox.Text = estimateTextBlockText;
            this.estimatedMinutesTextBlock.Text = "minutes";

            if (estimateBox.Text != "") {
                // if boardingTime > scheduledTime, changed boarding box to say "Probably leaving in (may need to span more columns)
                if (EstimatedWaitGreaterThanScheduledWait()) {
                    // the estimate may be for the shuttle after next; if it is, then that means the current shuttle is here and waiting for departure
                    if (EstimatedWaitIsForSecondShuttle()) {
                        this.estimateBox.Text = "0";
                        this.boardingTextBlock.Visibility = Visibility.Visible;
                        this.realEstimateTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else {
                        this.boardingTextBlock.Visibility = Visibility.Collapsed;
                        this.realEstimateTextBlock.Text = "Will probably arrive in";
                        this.realEstimateTextBlock.Visibility = Visibility.Visible;
                    }
                }
                else {
                    this.boardingTextBlock.Visibility = Visibility.Visible;
                    this.realEstimateTextBlock.Visibility = Visibility.Collapsed;
                }
                this.notRunningTextBlock.Visibility = Visibility.Collapsed;
                this.notRunningTextBlock.Visibility = Visibility.Collapsed;
                this.shuttleMap.Visibility = Visibility.Visible;
            }
            else {
                if (numMinutes == "")
                    this.scheduledTextBlock.Text = "No routes within the next 24 hours.";
                this.realEstimateTextBlock.Visibility = Visibility.Visible;
                this.boardingTextBlock.Visibility = Visibility.Collapsed;
                this.estimateBox.Visibility = Visibility.Collapsed;
                this.estimatedMinutesTextBlock.Visibility = Visibility.Collapsed;
                this.shuttleMap.Visibility = Visibility.Collapsed;
                this.notRunningTextBlock.Visibility = Visibility.Visible;
                this.notRunningTextBlock2.Visibility = Visibility.Visible;
                return false;
            }
            return true;
        }

        private Dictionary<string, Tuple<string, string, List<LocationCollection>>> 
            ConvertToRouteMap(Dictionary<string, Tuple<string, string, List<List<Point>>>> routeMap2)
        {
            Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap = new Dictionary<string, Tuple<string, string, List<LocationCollection>>>();
            foreach (var k in routeMap2.Keys) {
                var val = routeMap2[k];
                List<List<Point>> locations = val.Item3;
                List<LocationCollection> locs = new List<LocationCollection>();
                foreach (var list in locations) {
                    LocationCollection col = new LocationCollection();
                    foreach (var loc in list) {
                        Location l = new Location(loc.X, loc.Y);
                        col.Add(l);
                        l = null;
                    }
                    locs.Add(col);
                    col = null;
                }
                routeMap[k] = Tuple.Create<string, string, List<LocationCollection>>(val.Item1, val.Item2, locs);
                locs = null;
            }
            return routeMap;
        }

        private async Task<Dictionary<string, Tuple<string, string, List<LocationCollection>>>> PopulateUI()
        {
            // Grab arrival estimates and the schedule
            string estimateTextBlockText = await Task<string>.Run(() => MainDataStore.GetArrivalEstimates(currOrigin, currDest));
            Tuple<List<string>, string, string> scheduleResults = await DataStore.Scheduler.CreateSchedule(currOrigin, currDest);

            // Update the UI
            bool isRunning = UpdateTextBoxes(scheduleResults.Item1, scheduleResults.Item2, scheduleResults.Item3, estimateTextBlockText);
            if (!isRunning)
                return null;

            // Create a route map for updating the color code and the routes
            Dictionary<string, Tuple<string, string, List<List<Point>>>> routeMap2;
            routeMap2 = await MainDataStore.GetRoutes(currOrigin, currDest);
            Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap = ConvertToRouteMap(routeMap2);

            // update the color code UI
            // if there are no routes, hide map and color code, replace with message that there are no routes currently running
            if (routeMap.Keys.Count == 0) {
                this.shuttleMap.Visibility = Visibility.Collapsed;
                this.ColorCodePanel.Visibility = Visibility.Collapsed;
                this.notRunningTextBlock.Visibility = Visibility.Visible;
                this.notRunningTextBlock2.Visibility = Visibility.Visible;
                routeMap = null; // return null if there's nothing to show
            }

            // clean up
            routeMap2 = null;
            scheduleResults = null;

            return routeMap;
        }

        private async void UpdateUI(ThreadPoolTimer timer)
        {
            // Perform the data computations
            string estimateTextBlockText = await Task<string>.Run(() => MainDataStore.GetArrivalEstimates(currOrigin, currDest));
            Tuple<List<string>, string, string> scheduleResults = await DataStore.Scheduler.CreateSchedule(currOrigin, currDest);

            // Update the UI
            bool isRunning = false;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                isRunning = UpdateTextBoxes(scheduleResults.Item1, scheduleResults.Item2, scheduleResults.Item3, estimateTextBlockText);
            });
            if (!isRunning) {
                scheduleResults = null;
                return;
            }

            // Grab more data
            Dictionary<string, Tuple<string, string, List<List<Point>>>> routeMap2;
            routeMap2 = await MainDataStore.GetRoutes(currOrigin, currDest);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap = ConvertToRouteMap(routeMap2);
                // If there are no routes, hide map and color code, replace with message that there are no routes currently running
                if (routeMap.Keys.Count == 0) {
                    this.shuttleMap.Visibility = Visibility.Collapsed;
                    this.ColorCodePanel.Visibility = Visibility.Collapsed;
                    this.notRunningTextBlock.Visibility = Visibility.Visible;
                    this.notRunningTextBlock2.Visibility = Visibility.Visible;
                }
                else {
                    // Update the cache of route IDs and their colors
                    commonRouteIDsColors.Clear(); // race condition here with shuttles, but it will be resolved the period after if there's a failure
                    foreach (var key in routeMap.Keys)
                        commonRouteIDsColors[key] = routeMap[key].Item2;
                }

                // clean up
                routeMap = null;
            });

            // clean up
            routeMap2 = null;
            scheduleResults = null;
        }

        private void FadeInMap()
        {
            Storyboard story = new Storyboard();
            DoubleAnimation anim = new DoubleAnimation();
            anim.Duration = TimeSpan.FromMilliseconds(1100);
            anim.From = 0;
            anim.To = 1;
            story.Children.Add(anim);
            Storyboard.SetTarget(anim, shuttleMap);
            Storyboard.SetTargetProperty(anim, "UIElement.Opacity");
            story.Begin();

            // clean up
            anim = null;
            story = null;
        }
        #endregion

        #region Shuttles
        private void AddShuttles(List<Tuple<string, Point>> shuttleLocs)
        {
            if (shuttleLocs == null)
                return;

            foreach (var shutteLoc in shuttleLocs) {
                ShuttlePin p = new ShuttlePin();
                string routeColor = commonRouteIDsColors[shutteLoc.Item1];
                byte r = byte.Parse(routeColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(routeColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(routeColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                p.SetBackground(new SolidColorBrush(Color.FromArgb(255, r, g, b)));

                Point pt = shutteLoc.Item2;
                MapLayer.SetPositionAnchor(p, p.GetAnchor());
                MapLayer.SetPosition(p, new Location(pt.X, pt.Y));
                shuttleMap.Children.Add(p);
            }
        }

        private async void UpdateShuttles(ThreadPoolTimer timer)
        {
            List<Tuple<string, Point>> shuttleLocs = await MainDataStore.GetShuttles(commonRouteIDsColors.Keys.ToList<string>());
            var oldShuttles = shuttleMap.Children.ToList<UIElement>();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () => {
                foreach (var oldShuttle in oldShuttles) {
                    if (oldShuttle.GetType() == typeof(ShuttlePin))
                        shuttleMap.Children.Remove(oldShuttle);
                }
                AddShuttles(shuttleLocs);
            });

            // clean up
            shuttleLocs = null;
            oldShuttles = null;
        }
        #endregion

        #region Polylines
        private void AddPolyline(string routeColor, LocationCollection collection)
        {
            byte r = byte.Parse(routeColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(routeColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(routeColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            MapShapeLayer shapeLayer = new MapShapeLayer();
            MapPolyline polyline = new MapPolyline();
            polyline.Locations = collection;
            polyline.Color = Color.FromArgb(180, r, g, b);
            polyline.Width = 8;
            shapeLayer.Shapes.Add(polyline);
            shuttleMap.ShapeLayers.Add(shapeLayer);

            // clean up
            polyline = null;
            shapeLayer = null;
        }

        private double GetHash(LocationCollection collection)
        {
            double hash = 0;
            foreach (Location loc in collection)
                hash += loc.Latitude + loc.Longitude;
            return hash;
        }

        private void MakePolylines(Dictionary<string, Tuple<string, string, List<LocationCollection>>> routeMap)
        {
            // ultimately is list of routes -> segments
            // split up route collections into route collections that include "shared routes"
            Dictionary<double, Tuple<LocationCollection, List<string>>> segmentToIdMap = new Dictionary<double, Tuple<LocationCollection, List<string>>>();
            foreach (string routeId in routeMap.Keys) {
                Tuple<string, string, List<LocationCollection>> routeData = routeMap[routeId];
                List<LocationCollection> locs = routeData.Item3;
                foreach (LocationCollection collection in locs) {
                    double key = GetHash(collection);
                    List<string> idList = new List<string>();
                    if (segmentToIdMap.ContainsKey(key))
                        idList = segmentToIdMap[key].Item2;
                    idList.Add(routeId);
                    segmentToIdMap[key] = Tuple.Create<LocationCollection, List<string>>(collection, idList);

                    // clean up
                    idList = null;
                }
            }

            foreach (double key in segmentToIdMap.Keys) {
                LocationCollection collection = segmentToIdMap[key].Item1;
                List<string> idList = segmentToIdMap[key].Item2;
                if (idList.Count == 1) {
                    string routeId = idList[0];
                    string routeColor = routeMap[routeId].Item2;
                    AddPolyline(routeColor, collection);
                }
                // there may be a segment shared by many routes
                else {
                    int i = 0;
                    int n = idList.Count;
                    Location prevLoc = null;
                    double totalDist = 0;
                    double maxDist = 0.00025;
                    foreach (Location loc in collection) {
                        if (prevLoc != null) {
                            double dist = Math.Sqrt(Math.Pow((prevLoc.Longitude - loc.Longitude), 2) + Math.Pow((prevLoc.Latitude - prevLoc.Latitude), 2));
                            // split up the segment pair into smaller segments
                            if (dist > maxDist) {
                                int numSteps = (int)Math.Ceiling(dist / maxDist);
                                double latDiff = (loc.Latitude - prevLoc.Latitude) / numSteps;
                                double lngDiff = (loc.Longitude - prevLoc.Longitude) / numSteps;
                                for (int j = 0; j < numSteps; j++) {
                                    Location new_loc1 = new Location(prevLoc.Latitude + latDiff * j, prevLoc.Longitude + lngDiff * j);
                                    Location new_loc2 = new Location(prevLoc.Latitude + latDiff * (j + 1), prevLoc.Longitude + lngDiff * (j + 1));
                                    LocationCollection newLocCollection = new LocationCollection();
                                    newLocCollection.Add(new_loc1);
                                    newLocCollection.Add(new_loc2);
                                    string routeId = idList[i];
                                    string routeColor = routeMap[routeId].Item2;
                                    AddPolyline(routeColor, newLocCollection);
                                    i = (i + 1) % n;

                                    // clean up
                                    new_loc1 = null;
                                    new_loc2 = null;
                                    newLocCollection.Clear(); newLocCollection = null;
                                }
                            }
                            else {
                                // add polyline between loc and prevLoc
                                LocationCollection pair = new LocationCollection();
                                pair.Add(prevLoc);
                                pair.Add(loc);
                                string routeId = idList[i];
                                string routeColor = routeMap[routeId].Item2;
                                AddPolyline(routeColor, pair);
                                totalDist += dist;
                                if (totalDist > maxDist) {
                                    totalDist = 0;
                                    i = (i + 1) % n;
                                }
                                pair = null; // clean up
                            }
                        }
                        prevLoc = loc;
                    }
                    prevLoc = null; // clean up
                }
            }
            // clean up
            segmentToIdMap.Clear();
            segmentToIdMap = null;
        }
        #endregion

        #region UI helpers
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

            // clean up
            doc = null;
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

            // clean up
            condition = null;
            trigger = null;
            builder = null;
        }
        #endregion

        #region Event Handlers
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

            // clean up
            doc = null;
        }
        #endregion
    }
}
