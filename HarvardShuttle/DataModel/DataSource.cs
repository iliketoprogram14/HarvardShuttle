using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections.Specialized;
using System.Xml;
using Windows.Data.Xml.Dom;
using System.IO;

// The data model defined by this file serves as a representative example of a strongly-typed
// model that supports notification when members are added, removed, or modified.  The property
// names chosen coincide with data bindings in the standard item templates.
//
// Applications may use this model as a starting point and build on it, or discard it entirely and
// replace it with something appropriate to their needs.

namespace HarvardShuttle.Data
{
    /// <summary>
    /// Base class for <see cref="DataItem"/> and <see cref="DataGroup"/> that
    /// defines properties common to both.
    /// </summary>
    [Windows.Foundation.Metadata.WebHostHidden]
    public abstract class DataCommon : HarvardShuttle.Common.BindableBase
    {
        private static Uri _baseUri = new Uri("ms-appx:///");

        public DataCommon(String uniqueId, String title, String subtitle, String imagePath, String description)
        {
            this._uniqueId = uniqueId;
            if (imagePath == "") {
                this._imagePath = "StopImages/" + title.Replace(" ", "").Replace("-", "").ToLower().ToString() + ".jpg";
                this._imagePath2 = "StopImages/" + subtitle.Replace(" ", "").Replace("-", "").ToLower().ToString() + ".jpg";
            }
            else {
                this._imagePath = imagePath;
                this._imagePath2 = "";
            }
            this._title = title;
            this._subtitle = subtitle;
            this._description = description;
        }

        private string _uniqueId = string.Empty;
        public string UniqueId
        {
            get { return this._uniqueId; }
            set { this.SetProperty(ref this._uniqueId, value); }
        }

        private string _title = string.Empty;
        public string Title
        {
            get { return this._title; }
            set { this.SetProperty(ref this._title, value); }
        }

        private string _subtitle = string.Empty;
        public string Subtitle
        {
            get { return this._subtitle; }
            set { this.SetProperty(ref this._subtitle, value); }
        }

        private string _description = string.Empty;
        public string Description
        {
            get { return this._description; }
            set { this.SetProperty(ref this._description, value); }
        }

        private ImageSource _image = null;
        private String _imagePath = null;
        public ImageSource Image
        {
            get
            {
                if (this._image == null && this._imagePath != null)
                {
                    this._image = new BitmapImage(new Uri(DataCommon._baseUri, this._imagePath));
                }
                return this._image;
            }

            set
            {
                this._imagePath = null;
                this.SetProperty(ref this._image, value);
            }
        }

        public void SetImage(String path)
        {
            this._image = null;
            this._imagePath = path;
            this.OnPropertyChanged("Image");
        }

        private ImageSource _image2 = null;
        private String _imagePath2 = null;
        public ImageSource Image2
        {
            get
            {
                if (this._image2 == null && this._imagePath2 != null) {
                    this._image2 = new BitmapImage(new Uri(DataCommon._baseUri, this._imagePath2));
                }
                return this._image2;
            }

            set
            {
                this._imagePath2 = null;
                this.SetProperty(ref this._image2, value);
            }
        }

        public void SetImage2(String path)
        {
            this._image2 = null;
            this._imagePath2 = path;
            this.OnPropertyChanged("Image");
        }

        public override string ToString()
        {
            return this.Title;
        }

        public override bool Equals(object obj)
        {
            DataCommon d = obj as DataCommon;
            if (d == null)
                return false;
            return _uniqueId == d._uniqueId && _title == d._title && _subtitle == d._subtitle;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Generic item data model.
    /// </summary>
    public class DataItem : DataCommon
    {
        public DataItem(String uniqueId, String title, String subtitle, String imagePath, String description, String content, DataGroup group)
            : base(uniqueId, title, subtitle, imagePath, description)
        {
            this._content = content;
            this._group = group;
        }

        private string _content = string.Empty;
        public string Content
        {
            get { return this._content; }
            set { this.SetProperty(ref this._content, value); }
        }

        private DataGroup _group;
        public DataGroup Group
        {
            get { return this._group; }
            set { this.SetProperty(ref this._group, value); }
        }
    }

    /// <summary>
    /// Generic group data model.
    /// </summary>
    public class DataGroup : DataCommon
    {
        private int itemLimit = 20;

        public DataGroup(String uniqueId, String title, String subtitle, String imagePath, String description)
            : base(uniqueId, title, subtitle, imagePath, description)
        {
            Items.CollectionChanged += ItemsCollectionChanged;
        }

        private void ItemsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Provides a subset of the full items collection to bind to from a GroupedItemsPage
            // for two reasons: GridView will not virtualize large items collections, and it
            // improves the user experience when browsing through groups with large numbers of
            // items.
            //
            // A maximum of 12 items are displayed because it results in filled grid columns
            // whether there are 1, 2, 3, 4, or 6 rows displayed

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewStartingIndex < itemLimit)
                    {
                        TopItems.Insert(e.NewStartingIndex,Items[e.NewStartingIndex]);
                        if (TopItems.Count > itemLimit)
                        {
                            TopItems.RemoveAt(itemLimit);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldStartingIndex < itemLimit && e.NewStartingIndex < itemLimit)
                    {
                        TopItems.Move(e.OldStartingIndex, e.NewStartingIndex);
                    }
                    else if (e.OldStartingIndex < itemLimit)
                    {
                        TopItems.RemoveAt(e.OldStartingIndex);
                        TopItems.Add(Items[itemLimit-1]);
                    }
                    else if (e.NewStartingIndex < itemLimit)
                    {
                        TopItems.Insert(e.NewStartingIndex, Items[e.NewStartingIndex]);
                        TopItems.RemoveAt(itemLimit);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldStartingIndex < itemLimit)
                    {
                        var d = TopItems;
                        var b = TopItems[e.OldStartingIndex];
                        TopItems.RemoveAt(e.OldStartingIndex);
                        if (Items.Count >= itemLimit)
                        {
                            TopItems.Add(Items[itemLimit-1]);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldStartingIndex < itemLimit)
                    {
                        TopItems[e.OldStartingIndex] = Items[e.OldStartingIndex];
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    TopItems.Clear();
                    while (TopItems.Count < Items.Count && TopItems.Count < itemLimit)
                    {
                        TopItems.Add(Items[TopItems.Count]);
                    }
                    break;
            }
        }

        private ObservableCollection<DataItem> _items = new ObservableCollection<DataItem>();
        public ObservableCollection<DataItem> Items
        {
            get
            {
                return this._items;
            }
        }

        private ObservableCollection<DataItem> _topItem = new ObservableCollection<DataItem>();
        public ObservableCollection<DataItem> TopItems
        {
            get { return this._topItem; }
        }
    }

    /// <summary>
    /// Creates a collection of groups and items with hard-coded content.
    /// 
    /// DataSource initializes with placeholder data rather than live production
    /// data so that sample data is provided at both design-time and run-time.
    /// </summary>
    public sealed class DataSource
    {
        private static DataSource _sampleDataSource = new DataSource();
        public List<String> locations = new List<String>();

        private ObservableCollection<DataGroup> _allGroups = new ObservableCollection<DataGroup>();
        public ObservableCollection<DataGroup> AllGroups
        {
            get { return this._allGroups; }
        }

        public static IEnumerable<DataGroup> GetGroups(string uniqueId)
        {
            if (!uniqueId.Equals("AllGroups")) throw new ArgumentException("Only 'AllGroups' is supported as a collection of groups");
            
            return _sampleDataSource.AllGroups;
        }

        public static DataGroup GetGroup(string uniqueId)
        {
            // Simple linear search is acceptable for small data sets
            var matches = _sampleDataSource.AllGroups.Where((group) => group.UniqueId.Equals(uniqueId));
            if (matches.Count() == 1) return matches.First();
            return null;
        }

        public static DataItem GetItem(string uniqueId)
        {
            // Simple linear search is acceptable for small data sets
            var matches = _sampleDataSource.AllGroups.SelectMany(group => group.Items).Where((item) => item.UniqueId.Equals(uniqueId));
            if (matches.Count() == 1) return matches.First();
            return null;
        }

        private void initLocations()
        {
            locations.Add("Boylston Gate");
            locations.Add("Harvard Square");
            locations.Add("i-Lab");
            locations.Add("Kennedy School");
            locations.Add("Lamont Library");
            locations.Add("Law School");
            locations.Add("Mass Ave Garden St");
            locations.Add("Mather House");
            locations.Add("Maxwell Dworkin");
            locations.Add("Memorial Hall");
            locations.Add("Peabody Terrace");
            locations.Add("Quad");
            locations.Add("Soldiers Field Park");
            locations.Add("Stadium");
            locations.Add("Winthrop House");
            //locations.Add("20 Garden St");
        }

        public DataSource()
        {
            String ITEM_CONTENT = String.Format("Item Content: {0}\n\n{0}\n\n{0}\n\n{0}\n\n{0}\n\n{0}\n\n{0}",
                        "Curabitur class aliquam vestibulum nam curae maecenas sed integer cras phasellus suspendisse quisque donec dis praesent accumsan bibendum pellentesque condimentum adipiscing etiam consequat vivamus dictumst aliquam duis convallis scelerisque est parturient ullamcorper aliquet fusce suspendisse nunc hac eleifend amet blandit facilisi condimentum commodo scelerisque faucibus aenean ullamcorper ante mauris dignissim consectetuer nullam lorem vestibulum habitant conubia elementum pellentesque morbi facilisis arcu sollicitudin diam cubilia aptent vestibulum auctor eget dapibus pellentesque inceptos leo egestas interdum nulla consectetuer suspendisse adipiscing pellentesque proin lobortis sollicitudin augue elit mus congue fermentum parturient fringilla euismod feugiat");

            var group1 = new DataGroup("Group-1",
                    "From",
                    "Group Subtitle: 1",
                    "Assets/DarkGray.png",
                    "Group Description: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus tempor scelerisque lorem in vehicula. Aliquam tincidunt, lacus ut sagittis tristique, turpis massa volutpat augue, eu rutrum ligula ante a ante");

            initLocations();
            int len = locations.Count;
            for (int i = 0; i < len; i++)
            {
                String uid = "Group-1-Item-"+(i + 1).ToString();
                string path = "StopImages/" + locations[i].Replace(" ", "").Replace("-",""). ToLower().ToString() + ".jpg";
                group1.Items.Add(new DataItem(uid, locations[i], "", path, "", "", group1));
            }
            this.AllGroups.Add(group1);

            var group2 = new DataGroup("Group-2",
                    "Favorite Trips",
                    "Group Subtitle: 2",
                    "Assets/LightGraySmall.png",
                    "Group Description: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus tempor scelerisque lorem in vehicula. Aliquam tincidunt, lacus ut sagittis tristique, turpis massa volutpat augue, eu rutrum ligula ante a ante");
            this.AllGroups.Add(group2);
        }

        public void UpdateFavorites(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            DataGroup favsGroup = this.AllGroups[1];// DataSource.GetGroup("Group-2");
            favsGroup.Items.Clear();

            int i = 1;
            foreach (XmlElement elem in doc.GetElementsByTagName("trip"))
            {
                string origin = elem.GetAttribute("origin");
                string dest = elem.GetAttribute("dest");

                DataItem item = new DataItem("Group-2-Item-"+i.ToString(), origin, dest, "", "from "+origin+" to "+dest, "", favsGroup);
                //DataSource.GetGroup("Group-2").Items.Add(item);
                favsGroup.Items.Add(item);
                
                i++;
            }
        }
    }
}
