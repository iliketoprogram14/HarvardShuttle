using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarvardShuttle.Data;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HarvardShuttle
{
    public class MainItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Main { get; set; }
        public DataTemplate Favorite { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            DataItem i = (DataItem)item;
            var crap = i.Group.ToString();
            return (i.Group.ToString() == "From") ? Main : Favorite;
        }
    }
}
