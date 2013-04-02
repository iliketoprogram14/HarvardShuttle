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
    public sealed partial class BigPushPin : UserControl
    {
        public BigPushPin()
        {
            this.InitializeComponent();
        }

        public void SetBackground(SolidColorBrush brush)
        {
            this.thePin.Background = brush;
        }
    }
}
