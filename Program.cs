using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;


namespace checkInstalledSoftware
{
    class Program
    {
        //	Rückgabewerte für Nagios
        enum nagiosStatus
        {
            Ok = 0,
            Warning = 1,
            Critical = 2,
            Unknown = 3
        }

        static int status = (int)nagiosStatus.Ok;

        static int Main(string[] args)
        {
            return (int)nagiosStatus.Unknown;
        }
    }

    class AppInformation
    {
        //  Klasse zum aufnehmen der Informationen aus der Registry

        private string _appRegKey;

        public string appRegKey
        {
            get { return _appRegKey; }
            set { _appRegKey = value; }
        }

        private string _appName;

        public string appName
        {
            get { return _appName; }
            set { _appName = value; }
        }

        private string _appVersion;

        public string appVersion
        {
            get { return _appVersion; }
            set { _appVersion = value; }
        }

        public Dictionary<string, string> AppRegistry
        {
            get { return appRegistry; }
            set { appRegistry = value; }
        }

        Dictionary<string, string> appRegistry;

        public AppInformation() { }

        public AppInformation(string _Name, string _Version, string _Key)
        {
            appName = _Name;
            appVersion = _Version;
            appRegKey = _Key;
        }
    }

    class cmdOptions
    {

        [Option('x', "export", Required = false, Separator = ',',
        HelpText = "Ausgabe der Listen als: [CSV|TXT|JSON|XML]")]
        //public string strFiles { get; set; }
        public IEnumerable<string> expFiles { get; set; }

        [Option('s', "searchtype", Required = false, Default = "AUTO",
        HelpText = "Nach was gesucht werden soll [Hersteller=Vendor|Name|Version]")]
        public string strTextType { get; set; }

        [Option('p', "pattern", Required = false, //Separator = ',',
        HelpText = "Welcher Begriff gesucht wird")]
        public string strFilter { get; set; }
    }
}
