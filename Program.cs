using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.Win32;

using CommandLine;
using CommandLine.Text;


namespace checkInstalledSoftware
{
    class Program
    {
        //	Rückgabewerte für Nagios
        #region Eigenschaften
        enum nagiosStatus
        {
            Ok = 0,
            Warning = 1,
            Critical = 2,
            Unknown = 3
        }

        static int status = (int)nagiosStatus.Ok;

        static Dictionary<string, AppInformation> dicApplications;

        static string RegPath2Uninstall32 = "wow6432Node\\";
        static string RegPath2Uninstall = "Software\\{0}Microsoft\\Windows\\CurrentVersion\\Uninstall\\";
        #endregion

        static int Main(string[] args)
        {
            dicApplications = new Dictionary<string, AppInformation>();

            if (Environment.Is64BitOperatingSystem)
            {
                //  32 & 64 BIT Registry Bereiche lesen
                GetRegistryInformation(false);
                GetRegistryInformation(true); 
            }
            else
            {
                //  nur 32 Bit vorhanden
                GetRegistryInformation(false);
            }

            ExportData();

            return (int)nagiosStatus.Unknown;
        }

        static void GetRegistryInformation(bool is64Bit)
        {
            //key.View = RegistryView.Registry64;

            string activePath = string.Format(RegPath2Uninstall, is64Bit ? "" : RegPath2Uninstall32);
            RegistryKey key = Registry.LocalMachine.OpenSubKey(activePath,false);
            string[] valueNames;

            if (key != null && key.ValueCount > 0)
            {
                //  Alle Schlüssel der Installierten Anwendungen
                foreach (string  appKey in key.GetSubKeyNames())
                {
                    if((valueNames = key.OpenSubKey(appKey, false).GetValueNames()).Length > 0)
                    {
                        Add2Dictionary(valueNames, activePath + appKey);
                    }
                }
            }
        }

        static void Add2Dictionary(string[] valueList, string RegPath)
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath, false);
            AppInformation appInf;
            Dictionary<string, string> dicTemp;
            string valueType = "";
            string value;

            if (key != null && key.ValueCount > 0)
            {
                appInf = new AppInformation();
                dicTemp = new Dictionary<string, string>();

                appInf.appRegKey = RegPath;

                foreach (string str in valueList)
                {
                    switch( key.GetValueKind(str))
                    {
                        case RegistryValueKind.Binary:
                            valueType = "Binary";
                            break;
                        case RegistryValueKind.DWord:
                            valueType = "32-Bit DWord";
                            break;
                        case RegistryValueKind.ExpandString:
                            valueType = "String mit Vars";
                            break;
                        case RegistryValueKind.MultiString:
                            valueType = "Doppel-NULL String";
                            break;
                        case RegistryValueKind.None:
                            valueType = "Kein Datentyp";
                            break;
                        case RegistryValueKind.QWord:
                            valueType = "64-Bit DWord";
                            break;
                        case RegistryValueKind.String:
                            valueType = "String";
                            break;
                        case RegistryValueKind.Unknown:
                            valueType = "Unbekannt";
                            break;
                        default:
                            valueType = "N/A";
                            break;
                    }
                    value = string.Format("[{0}] {1}", valueType, key.GetValueKind(str).ToString());
                    dicTemp.Add(str, value);
                }
                appInf.AppRegistry = dicTemp;
                dicApplications.Add(appInf.appName, appInf);
            }
        }

        static void ExportData()
        {
            if (dicApplications.Count > 0 )
            {
                foreach (string name in dicApplications.Keys)
                {
                    Debug.WriteLine(name);
                }
            }
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
            set
            {
                string strTemp;
                if (string.IsNullOrWhiteSpace(appName))
                {
                    value.TryGetValue("DisplayName", out strTemp);
                    if (strTemp != string.Empty)
                    {
                        appName = strTemp == string.Empty ? "_N/A_": strTemp;
                    }
                }
                if (string.IsNullOrWhiteSpace(appVersion))
                {
                    value.TryGetValue("DisplayVersion", out strTemp);
                    if (strTemp != string.Empty)
                    {
                        appName = strTemp == string.Empty ? "_0.0.0.0_" : strTemp;
                    }
                }
                appRegistry = value;
            }
        }

        Dictionary<string, string> appRegistry;

        public AppInformation()
        {
            appRegistry = new Dictionary<string, string>();
        }

        public AppInformation(string _Name, string _Version, string _Key)
        {
            appName = _Name;
            appVersion = _Version;
            appRegKey = _Key;

            appRegistry = new Dictionary<string, string>();
        }

        ~AppInformation()
        {
            if (appRegistry != null)
            {
                appRegistry.Clear();
                appRegistry = null;
            }
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
