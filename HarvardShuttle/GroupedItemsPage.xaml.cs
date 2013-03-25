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

// The Grouped Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234231

namespace HarvardShuttle
{
    /// <summary>
    /// A page that displays a grouped collection of items.
    /// </summary>
    public sealed partial class GroupedItemsPage : HarvardShuttle.Common.LayoutAwarePage
    {
        public static string favoritesStore = "favorites.xml";
        public static StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
        private bool hasLoaded;
        private ObservableCollection<DataGroup> myGroups;
        DataSource myDataSource;

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
            hasLoaded = false;

            // Update the store
            await InitFavoritesStore();
            StorageFile file = await localFolder.GetFileAsync(favoritesStore);
            string favoritesXml = await FileIO.ReadTextAsync(file);
            myDataSource = new DataSource();
            myDataSource.UpdateFavorites(favoritesXml);

            // Load the view
            //var a = DataSource.AllGroups();
            //var groups = (ObservableCollection<DataGroup>)DataSource.GetGroups((String)navigationParameter);
            var derp = myDataSource.AllGroups;
            this.DefaultViewModel["Groups"] = derp;
            //this.groupedItemsViewSource.Source = myDataSource.AllGroups;
            myGroups = myDataSource.AllGroups;
            //this.itemGridView.ItemsSource = groups;
            var result = await BackgroundExecutionManager.RequestAccessAsync();

            this.itemGridView.SelectionChanged += itemGridView_SelectionChanged;
            this.itemGridView.SelectedIndex = -1;
            hasLoaded = true;
        }

        private async Task InitFavoritesStore()
        {
            bool fileExists = true;
            try {
                StorageFile file = await localFolder.GetFileAsync(favoritesStore);
                fileExists = true;
            }
            catch (Exception) {
                fileExists = false;
            }

            if (!fileExists) {
                string xmlHeader =
                    "<favorites>" + 
                    "<trip origin=\"Boylston Gate\" dest=\"Quad\"></trip>" +
                    "<trip origin=\"Quad\" dest=\"Boylston Gate\"></trip>" +
                    "</favorites>";
                StorageFile file = await localFolder.CreateFileAsync(favoritesStore, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, xmlHeader);
            }
        }

        /// <summary>
        /// Invoked when an item within a group is clicked.
        /// </summary>
        /// <param name="sender">The GridView (or ListView when the application is snapped)
        /// displaying the item clicked.</param>
        /// <param name="e">Event data that describes the item clicked.</param>
        void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to the appropriate destination page, configuring the new page
            // by passing required information as a navigation parameter
            var item = (DataItem)e.ClickedItem;
            if (item.Group.UniqueId.Equals("Group-1"))
                this.Frame.Navigate(typeof(Destination), item);
            else
            {
                string origin = item.Title;
                string dest = item.Subtitle;
                this.Frame.Navigate(typeof(TripResults), Tuple.Create(origin, dest));
            }

        }

        private void Item_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
        }

        private void itemGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!hasLoaded)
                return;
            var item = (DataItem)this.itemGridView.SelectedItem;

            if (item.Group.Title != "Favorite Trips")
                return;

            bottomAppBar.IsOpen = !bottomAppBar.IsOpen;
        }

        private async void MyFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            bottomAppBar.IsOpen = false;

            var item = (DataItem)this.itemGridView.SelectedItem;
            string origin = item.Title;
            string dest = item.Subtitle;
            //var fack = this.DefaultViewModel["Groups"];
            //DataSource.ClearFavorite(item);
            /*DataGroup g = groups[1];
            var list = g.Items;
            var idx = g.Items.IndexOf(item);
            var blegh = g.Items.GetType();
            g.Items.Remove(item);*/
            var g = (ObservableCollection<DataGroup>)this.groupedItemsViewSource.Source;
            var groupa = g[1];
            var itemsa = groupa.Items;
            var idx = itemsa.IndexOf(item);
            //itemsa.Remove(item);


            StorageFile file = await localFolder.GetFileAsync(favoritesStore);
            string toRemove = "<trip origin=\"" + origin + "\" dest=\"" + dest + "\"></trip>";
            string xml = await FileIO.ReadTextAsync(file);
            xml = xml.Replace(toRemove, "");
            await FileIO.WriteTextAsync(file, xml);

            //myDataSource.UpdateFavorites(xml);
            var b = myGroups[1];
            var it = b.Items;
            //it.Remove(item);
            myDataSource = new DataSource();
            myDataSource.UpdateFavorites(xml);

            // Load the view
            //var a = DataSource.AllGroups();
            //var groups = (ObservableCollection<DataGroup>)DataSource.GetGroups((String)navigationParameter);
            var derp = myDataSource.AllGroups;
            myGroups = myDataSource.AllGroups;
            this.itemGridView.ItemsSource = myGroups;
            this.itemGridView.da
            //this.groupedItemsViewSource.Source = myDataSource.AllGroups;
            //this.DefaultViewModel["Groups"] = derp;
            //this.groupedItemsViewSource.Source = myDataSource.AllGroups;
            //myGroups = myDataSource.AllGroups;


            //var fack = this.groupedItemsViewSource.Source;


            // Load the view
            //var a = DataSource.AllGroups();
            //var groupsa = (ObservableCollection<DataGroup>)DataSource.GetGroups("AllGroups");
            //this.DefaultViewModel["Groups"] = groupsa;


            //DataSource.UpdateFavorites(favoritesStore.Replace(toRemove,""));
        }

    }
}
