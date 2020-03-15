using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;

namespace checkInstalledSoftware
{
    class AppInformationMap : ClassMap<AppInformation>
    {
        public AppInformationMap ()
        {
            Map(m => m.appName).Name("Application_Name");
            Map(m => m.appVersion).Name("Application_Version");
            Map(m => m.appRegKey).Name("Application_Registry_Key");
            Map(m => m.appPublisher).Name("Application_publisher");

            Map(m => m.appIsRegistryPath).Name("Application_appIsRegistryPath");

            Map(m => m.appHashValue).Name("Application_Hash_Value");

            //AutoMap(CultureInfo.InvariantCulture);
            //Map(m => m.appRegistry).Ignore;
            Map(m => m.appRegistry).Name("Application_appRegistry").Ignore();
        }
    }
}
