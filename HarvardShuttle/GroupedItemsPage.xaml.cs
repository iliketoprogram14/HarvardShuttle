using HarvardShuttle.Data;
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
            // TODO: Create an appropriate data model for your problem domain to replace the sample data
            await InitFavoritesStore();
            StorageFile file = await localFolder.GetFileAsync(favoritesStore);
            string favoritesXml = await FileIO.ReadTextAsync(file);
            SampleDataSource.UpdateFavorites(favoritesXml);
            var sampleDataGroups = SampleDataSource.GetGroups((String)navigationParameter);
            this.DefaultViewModel["Groups"] = sampleDataGroups;
            var result = await BackgroundExecutionManager.RequestAccessAsync();
        }

        private async Task InitFavoritesStore()
        {
            bool fileExists = false;
            try
            {
                StorageFile file = await localFolder.GetFileAsync(favoritesStore);
                fileExists = true;
            }
            catch (Exception) { }

            if (!fileExists)
            {
                string xmlHeader =
                    /*"<?xml version='1.0' ?>" +
                    "<!DOCTYPE favorites [" +
                    "  <!ELEMENT favorites (trip)>" +
                    "  <!ELEMENT trip     (#PCDATA)>" +
                    "  <!ATTLIST origin CDATA #REQUIRED>" +
                    "  <!ATTLIST dest CDATA #REQUIRED>" +
                    "]>" +*/
                    "<favorites>" + 
                    "<trip origin=\"Boylston Gate\" dest=\"Quad\"></trip>" +
                    "<trip origin=\"Quad\" dest=\"Boylston Gate\"></trip>" +
                    "</favorites>";
                StorageFile file = await localFolder.CreateFileAsync(favoritesStore, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, xmlHeader);
            }
        }

        /// <summary>
        /// Invoked when a group header is clicked.
        /// </summary>
        /// <param name="sender">The Button used as a group header for the selected group.</param>
        /// <param name="e">Event data that describes how the click was initiated.</param>
        void Header_Click(object sender, RoutedEventArgs e)
        {
            // Determine what group the Button instance represents
            var group = (sender as FrameworkElement).DataContext;

            // Navigate to the appropriate destination page, configuring the new page
            // by passing required information as a navigation parameter
            this.Frame.Navigate(typeof(GroupDetailPage), ((SampleDataGroup)group).UniqueId);
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
            var item = (SampleDataItem)e.ClickedItem;
            if (item.Group.UniqueId.Equals("Group-1"))
                this.Frame.Navigate(typeof(Destination), item);
            else
            {
                string origin = item.Title;
                string dest = item.Subtitle;
                this.Frame.Navigate(typeof(TripResults), Tuple.Create(origin, dest));
            }

        }
    }
}
