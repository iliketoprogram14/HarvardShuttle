using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace HarvardShuttle
{
    public sealed partial class ColorCodeBox : UserControl
    {
        public ColorCodeBox(string color, string route)
        {
            this.InitializeComponent();

            byte r = byte.Parse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(255, r, g, b));
            this.colorRect.Fill = brush;
            this.routeName.Text = route;
        }
    }
}
