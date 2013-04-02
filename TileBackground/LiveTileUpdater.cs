using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using System.Net.Http;
using Windows.Data.Xml;
using System.IO;
using System.Net;
using System.Xml;
using DataStore;

namespace TileBackground
{
    public sealed class LiveTileUpdater : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();
            //Scheduler.CreateExtendedSchedule();
            DataStore.Scheduler.CreateExtendedSchedule();
            deferral.Complete();
        }


    }
}
