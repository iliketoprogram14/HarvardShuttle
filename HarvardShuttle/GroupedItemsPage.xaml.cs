using HarvardShuttle.Data;
using HarvardShuttle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using Windows.UI.Popups;
using Windows.UI.ApplicationSettings;
using Windows.UI.Core;

// The Grouped Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234231

namespace HarvardShuttle
{
    /// <summary>
    /// A page that displays a grouped collection of items.
    /// </summary>
    public sealed partial class GroupedItemsPage : HarvardShuttle.Common.LayoutAwarePage
    {
        public static string favoritesStorePath = "favorites.xml";
        public static StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
        private DataSource myDataSource;
        public static BackgroundAccessStatus asyncStatus = BackgroundAccessStatus.Unspecified;

        public GroupedItemsPage()
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
        protected override async void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            // Update the store
            await InitFavoritesStore();
            StorageFile file = await localFolder.GetFileAsync(favoritesStorePath);
            string favoritesXml = await FileIO.ReadTextAsync(file);
            myDataSource = new DataSource();
            myDataSource.UpdateFavorites(favoritesXml);

            // Load the view
            this.DefaultViewModel["Groups"] = myDataSource.AllGroups;
            this.itemGridView.SelectionChanged += itemGridView_SelectionChanged;
            this.itemGridView.SelectedIndex = -1;
            this.pageTitle.Text = "Harvard Shuttle";

            if (asyncStatus == BackgroundAccessStatus.Unspecified)
                asyncStatus = await BackgroundExecutionManager.RequestAccessAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            this.itemGridView.SelectionChanged -= itemGridView_SelectionChanged;
        }

        private async Task InitFavoritesStore()
        {
            bool fileExists = true;
            try {
                StorageFile file = await localFolder.GetFileAsync(favoritesStorePath);
                fileExists = true;
            }
            catch (Exception) {
                fileExists = false;
            }

            if (!fileExists) {
                string xmlHeader =
                    "<favorites>" + 
                    "<trip origin=\"Boylston Gate\" dest=\"Quad\"></trip>" +
                    "<trip origin=\"Quad\" dest=\"Mass Ave Garden St\"></trip>" +
                    "</favorites>";
                StorageFile file = await localFolder.CreateFileAsync(favoritesStorePath, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, xmlHeader);
            }
        }

        /// <summary>
        /// Invoked when an item within a group is clicked.
        /// </summary>
        /// <param name="sender">The GridView (or ListView when the application is snapped)
        /// displaying the item clicked.</param>
        /// <param name="e">Event data that describes the item clicked.</param>
        private void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to the appropriate destination page, configuring the new page
            // by passing required information as a navigation parameter
            var item = (DataItem)e.ClickedItem;
            if (item.Group.UniqueId.Equals("All-Group"))
                this.Frame.Navigate(typeof(Destination), item);
            else {
                string origin = item.Title;
                string dest = item.Subtitle;
                this.Frame.Navigate(typeof(TripResults), Tuple.Create(origin, dest));
            }

        }

        private void itemGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = (DataItem)this.itemGridView.SelectedItem;
 
            if (item == null || item.Group.Title != "Favorite Trips") {
                this.itemGridView.SelectedIndex = -1;
                DeleteFavButton.Visibility = Visibility.Collapsed;
                bottomAppBar.IsOpen = false;
                return;
            }

            DeleteFavButton.Visibility = Visibility.Visible;
            bottomAppBar.IsOpen = true;
        }

        private async void DeleteFavButton_Click(object sender, RoutedEventArgs e)
        {
            bottomAppBar.IsOpen = false;

            // Get selected item
            var item = (DataItem)this.itemGridView.SelectedItem;
            string origin = item.Title;
            string dest = item.Subtitle;

            // Remove it from the observable collection
            var g = (ObservableCollection<DataGroup>)this.groupedItemsViewSource.Source;
            var groupa = g[0];
            var itemsa = groupa.Items;
            var idx = itemsa.IndexOf(item);
            itemsa.Remove(item);

            // Update the favorite store
            StorageFile file = await localFolder.GetFileAsync(favoritesStorePath);
            string toRemove = "<trip origin=\"" + origin + "\" dest=\"" + dest + "\"></trip>";
            string xml = await FileIO.ReadTextAsync(file);
            xml = xml.Replace(toRemove, "");
            await FileIO.WriteTextAsync(file, xml);
        }

        private void itemGridView_RightTapped_1(object sender, RightTappedRoutedEventArgs e)
        {
            if (this.itemGridView.SelectedItem != null)
                this.bottomAppBar.IsOpen = false;
            else
                this.bottomAppBar.IsOpen = true;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.bottomAppBar.IsOpen = false;
            SettingsPane.Show();
        }

    }
}
