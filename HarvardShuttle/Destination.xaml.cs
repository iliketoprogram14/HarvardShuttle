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
        private SampleDataItem origin;

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
            // TODO: Assign a collection of bindable groups to this.DefaultViewModel["Groups"]
            var originGroup = SampleDataSource.GetGroup("Group-1");
            SampleDataGroup destGroup = new SampleDataGroup("To-Group", "To", "", "Assets/DarkGray.png", "");
            ObservableCollection<SampleDataGroup> toGroup = new ObservableCollection<SampleDataGroup>();
            origin = (SampleDataItem)navigationParameter;

            foreach (SampleDataItem item in originGroup.Items) {
                if (!navigationParameter.Equals(item)) destGroup.Items.Add(item);
            }
            toGroup.Add(destGroup);

            this.DefaultViewModel["Groups"] = toGroup;
            this.DefaultViewModel["Group"] = originGroup;
            this.DefaultViewModel["Items"] = originGroup.Items;
        }

        void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var dest = (SampleDataItem)e.ClickedItem;
            this.Frame.Navigate(typeof(TripResults), Tuple.Create(origin.Title, dest.Title));
        }
    }
}
