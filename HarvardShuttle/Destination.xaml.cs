using HarvardShuttle.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class Destination : HarvardShuttle.Common.LayoutAwarePage
    {
        private DataItem origin;

        public Destination()
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
            // Create an origin group and a destination group
            var originGroup = DataSource.GetGroup("Group-1");
            DataGroup destGroup = new DataGroup("Dest-Group", "To", "", "Assets/DarkGray.png", "");
            ObservableCollection<DataGroup> toGroup = new ObservableCollection<DataGroup>();
            origin = (DataItem)navigationParameter;

            // Add all destinations that aren't the origin to the destination group
            foreach (DataItem item in originGroup.Items) {
                if (!origin.Equals(item)) 
                    destGroup.Items.Add(item);
            }

            toGroup.Add(destGroup);

            // Update the view
            this.DefaultViewModel["Groups"] = toGroup;
            this.DefaultViewModel["Group"] = destGroup;
            this.DefaultViewModel["Items"] = destGroup.Items;

            this.pageTitle.Text = "From " + origin;
        }

        void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var dest = (DataItem)e.ClickedItem;
            this.Frame.Navigate(typeof(TripResults), Tuple.Create(origin.Title, dest.Title));
        }
    }
}
