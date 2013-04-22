using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ApplicationSettings;
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
    public sealed partial class SettingsFlyout : UserControl
    {
        public SettingsFlyout(string type)
        {
            this.InitializeComponent();

            switch (type) {
                case "privacy":
                    this.Title.Text = "Privacy policy";
                    this.H1.Text = "";
                    this.H2.Text = "";
                    this.H3.Text = "";
                    this.H4.Text = "";
                    this.Body1.Text = "This application does not collect any personal information, location information, or usage data.";
                    this.Body2.Text = "Internet access is only used to retrieve data from the Transloc Open API.";
                    this.Body3.Text = "By using Harvard Shuttle, you consent to our privacy policy.";
                    this.Body4.Text = "";
                    break;
                case "help":
                    this.Title.Text = "Help";
                    this.H1.Text = "Find a trip!";
                    this.Body1.Text = "Find a trip by selecting a 'from' location and then a 'to' location. The schedule for the routes that hit both of those stops will appear. Harvard Shuttle's tile will update automatically with countdowns of shuttle arrivals to the 'from' location.";
                    this.H2.Text="Favorite Trips";
                    this.Body2.Text="After finding a trip, you can favorite that trip by right-clicking the anywhere on the trip screen (or swiping from the top or bottom on a touch screen). The newly favorited trip will then appear on the front page to the right of the 'from' locations.";
                    this.H3.Text="Live tile";
                    this.Body3.Text="After finding a trip, Harvard Shuttle's start screen tile will update automatically with countdowns of shuttle arrivals to the 'from' location for the trip.";
                    this.H4.Text="About";
                    this.Body4.Text = "This app uses the Transloc Open API for live updates.";
                    break;
                default:
                    break;

            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (Parent is Popup)
                ((Popup)Parent).IsOpen = false;
            SettingsPane.Show();
        }
    }
}
