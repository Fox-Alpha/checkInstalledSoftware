using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.Win32;

using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;


namespace checkInstalledSoftware
{
    class Program
    {
        //	Rückgabewerte für Nagios
        #region Eigenschaften
        public enum nagiosStatus
        {
            Ok = 0,
            Warning = 1,
            Critical = 2,
            Unknown = 3
        }

        static string strJSONKonfigFile = "data\\settings.json";

        static int status = (int)nagiosStatus.Ok;

        static Dictionary<string, AppInformation> dicApplications;

        static string RegPath2Uninstall32 = "wow6432Node\\";
        static string RegPath2Uninstall = "Software\\{0}Microsoft\\Windows\\CurrentVersion\\Uninstall\\";

        static Settings setting;
		static int exportCount = 0;

        public static bool writeLog { get; private set; } = true;
		public static bool isWriteExport { get; private set; } = false;

		public static int Status
		{
			get
			{
				return status;
			}

			set
			{
				status = value;
			}
		}
		#endregion

		static int Main(string[] args)
        {
            dicApplications = new Dictionary<string, AppInformation>();
            string[] cmdLine;

			try
			{

				if ((cmdLine = Environment.GetCommandLineArgs ()).Length > 1)
				{
#if (DEBUG)
					//Console.WriteLine (Environment.CommandLine);
#endif
					string strTemp = cmdLine [1];
					if (Path.IsPathRooted (strTemp))
					{
						if (File.Exists (cmdLine [1]))
						{
							strJSONKonfigFile = cmdLine [1];
						}
					}
					else
					{
						strJSONKonfigFile = Path.Combine (new string [] { AppDomain.CurrentDomain.BaseDirectory, cmdLine [1] });
					}
				}

				if (File.Exists (strJSONKonfigFile))
				{
					// Laden der Parameter aus der Konfiguration
					ReadJSonKonfiguration (strJSONKonfigFile);
#if (DEBUG)
					//Console.WriteLine ("\r\n#####\r\n" + strJSONKonfigFile + "\r\n#####\r\n");
#endif

					WriteToLogFile (string.Format ("Start der Anwendung"), null);
					WriteToLogFile ("Speicherverbrauch: {0}", Environment.WorkingSet.ToString ());

					WriteToLogFile ("Laden der Such und Filter Bedingungen: {0}", new string [] { strJSONKonfigFile });
				}
				else
				{
					Console.WriteLine ("Es konnte keine Datei mit Einstellungen geladen werden|File=" + strJSONKonfigFile);
					//return (int) nagiosStatus.Warning;
					//Exit (exitCode);
					Environment.Exit ((int)nagiosStatus.Critical);
				}


				WriteToLogFile ("Lesen der Registry UNINSTALL Einträge", null);
				WriteToLogFile ("Speicherverbrauch: {0}", Environment.WorkingSet.ToString ());
				if (Environment.Is64BitOperatingSystem)
				{
					//  32 & 64 BIT Registry Bereiche lesen
					GetRegistryInformation (false);
					GetRegistryInformation (true);
				}
				else
				{
					//  nur 32 Bit vorhanden
					GetRegistryInformation (false);
				}

				WriteToLogFile ("Exportieren der Daten", null);
				WriteToLogFile ("Speicherverbrauch: {0}", Environment.WorkingSet.ToString ());
				ExportData ();
				
				if (setting.bUseOutputInNagios)
				{
					Console.WriteLine ("OK Es wurden {0} Anwendungen gefunden", exportCount);
					Status = (int) nagiosStatus.Ok;
				}
				else
				{
					Console.WriteLine ("Es wurden {1} von {0} Einträge exportiert", dicApplications.Count.ToString (), exportCount);

					Console.WriteLine ("Taste drücken zum fortsetzen ! ...");
					Console.ReadKey ();
				}
			}
			catch ( ThreadAbortException taex)
			{
				Console.WriteLine (taex.Message);
				Console.WriteLine (taex.InnerException.Message);
				return Status;
			}
#if (DEBUG)
#endif

            return Status;
        }

        static void GetRegistryInformation(bool is64Bit)
        {
            //key.View = RegistryView.Registry64;

            string activePath = string.Format(RegPath2Uninstall, is64Bit ? "" : RegPath2Uninstall32);
            RegistryKey key = Registry.LocalMachine.OpenSubKey(activePath,false);
            string[] valueNames;

            if (key != null && key.SubKeyCount > 0)
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
                    value = string.Format("[{0}] {1}", valueType, key.GetValue(str).ToString());
                    dicTemp.Add(str, value);
                }
                appInf.AppRegistry = dicTemp;

                int i = 0;
                string appname = appInf.appName;

                //  TODO: Nahng nur wenn standard bereits vorhanden
                //  Dppelte Key verhindern
                if (dicApplications.ContainsKey(appname))
                {
                    while (dicApplications.ContainsKey(appname + "_" + i))
                        i++;    //  Incrementieren bis es passt
                    appInf.appName = appname + "_" + i;
                }

                dicApplications.Add(appInf.appName, appInf);
            }
        }

        static void ExportData()
        {
			//if (File.Exists (setting.strExportFileName + ".txt"))
			//{
			//	File.Delete (setting.strExportFileName + ".txt");
			//	//setting.bAppend2Logfile = true;
			//}

			if (dicApplications.Count > 0 )
            {
				//foreach (string name in dicApplications.Keys)
				//{
				//    Debug.WriteLine(name);
				//}
				int i = 0;
				WriteToExportFile (string.Format ("{0}: Installierte Software / Suchfilter: {1} / System:  {2}\r\n", DateTime.Now.ToString (), setting.strSearchTag, Environment.MachineName));

				foreach (AppInformation ai in dicApplications.Values)
                {
                    string strTemp;
					/*
					 * TODO: Filter nutzung
					 */

					if (!string.IsNullOrWhiteSpace(setting.strSearchTag) && !string.IsNullOrWhiteSpace (setting.strSearchPattern))
					{
						ai.AppRegistry.TryGetValue (setting.strSearchTag, out strTemp);

						if (setting.bUseRegEx&& !string.IsNullOrWhiteSpace(strTemp))
						{
							if(Regex.IsMatch (strTemp, setting.strSearchPattern, setting.bCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase))
							{
								WriteToExportFile (string.Format ("{2} - {0} - {1}", new [] { ai.appName, ai.appVersion, ai.appPublisher }));
								i++;
							}
						}
						else if (!setting.bUseRegEx && !string.IsNullOrWhiteSpace (strTemp))
						{
							if (strTemp == setting.strSearchPattern)
							{
								WriteToExportFile (string.Format ("{2} - {0} - {1}", new [] { ai.appName, ai.appVersion, ai.appPublisher }));
								i++;
							}
						}

					}
					else
					{
						Debug.WriteLine (string.Format ("Fehler: Es wurde kein Suchbereich angegeben"));
						WriteToLogFile (string.Format ("Fehler: Es wurde kein Suchbereich angegeben"));
						//	TODO: Alle Exportformate beachten
						WriteToExportFile (string.Format ("Fehler: Es wurde kein Suchbereich angegeben"));
						return;
					}

					/*#####*/

					ai.AppRegistry.TryGetValue("Publisher", out strTemp);

                    Debug.WriteLine(string.Format("{2} - {0} - {1}", ai.appName, ai.appVersion, strTemp));
					//	TODO: Alle Exportformate beachten
                }
                WriteToLogFile("Es wurden {1} von {0} Einträge exportiert", dicApplications.Count.ToString(), i.ToString());
				exportCount = i;
				isWriteExport = false;
			}
        }

        static private void ReadJSonKonfiguration(string JSonFile)
        {
            if (!File.Exists(JSonFile))
            {
                Debug.WriteLine("Fehlende Angabe oder Angegebene Konfiguration ungültig.");
                WriteToLogFile("Fehlende Angabe oder Angegebene Konfiguration ungültig.", "");
                return;
            }

            //	Setzen der Serializer Settings
            JsonSerializerSettings jsonSerializerSettings;

            jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Formatting = Formatting.Indented;
            jsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
            jsonSerializerSettings.NullValueHandling = NullValueHandling.Include;
            jsonSerializerSettings.StringEscapeHandling = StringEscapeHandling.EscapeNonAscii;
            jsonSerializerSettings.TypeNameHandling = TypeNameHandling.Auto;

            JsonSerializer serializer = JsonSerializer.CreateDefault(jsonSerializerSettings);

			//StreamWriter sw = new StreamWriter (@"data\exampleOut.json");
			//JsonWriter writer = new JsonTextWriter (sw);
			try
			{

				using (StreamReader sr = new StreamReader (JSonFile))
				{
					using (JsonReader reader = new JsonTextReader (sr))
					{
						setting = serializer.Deserialize<Settings> (reader);
#if (DEBUG)
						string output = JsonConvert.SerializeObject (setting, jsonSerializerSettings);
						Debug.WriteLine (output);
#endif
						if (!string.IsNullOrWhiteSpace (setting.strLogFile))
						{
							writeLog = true;
							WriteToLogFile ("Start Operations", "");
#if (DEBUG)
							//Console.WriteLine (output);
#endif
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine (string.Format("{0}\r\n{1}",ex.Message, ex.StackTrace));
				if (ex.Data.Count > 0)
				{
					foreach (var item in ex.Data)
					{
						Console.WriteLine (item.ToString ());
					}
				}
			}
        }

        static void WriteToLogFile(string MessageFormat, params string[] vals)
        {
            if (!string.IsNullOrWhiteSpace(setting.strLogFile))
            {
				if (!Directory.Exists(Path.GetDirectoryName(setting.strLogFile)))
				{
					Directory.CreateDirectory (Path.GetDirectoryName (setting.strLogFile));
				}


                if (!setting.bAppend2Logfile)
                {
                    if (File.Exists(setting.strLogFile))
                    {
                        File.Delete(setting.strLogFile);
                        setting.bAppend2Logfile = true;
                    }
                }

				try
				{
					using (StreamWriter sw = File.AppendText (setting.strLogFile))
					{
						if (vals != null && vals.Length > 0)
						{
							sw.Write (string.Format ("{0}: {1}\r\n", DateTime.Now.ToString (), string.Format (MessageFormat, vals)));
						}
						else
							sw.Write (string.Format ("{0}: {1}\r\n", DateTime.Now.ToString (), MessageFormat));
					}
				}
				catch (Exception)
				{

					throw;
				}
            }
        }

		static void WriteToExportFile (string MessageFormat, params string [] vals)
		{
			string tempExport = Path.Combine (new [] { setting.strExportTartgetDir, setting.strExportFileName + ".txt" });
			if (Path.IsPathRooted (setting.strExportTartgetDir))
			{
				if (File.Exists (tempExport))
				{
					tempExport = tempExport;
				}
			}
			else
			{
				tempExport = Path.Combine (new string [] { AppDomain.CurrentDomain.BaseDirectory, setting.strExportTartgetDir, setting.strExportFileName });
			}

			/* Wenn kein Verzeichnis angegeben wurde, wird das Verzeichnis der Anwendung verwendet */
			if (string.IsNullOrWhiteSpace (setting.strExportTartgetDir))
				setting.strExportTartgetDir = AppDomain.CurrentDomain.BaseDirectory;

			/* Wenn kein Dateiname angegeben wurde, Export abrechen */
			if (string.IsNullOrWhiteSpace(setting.strExportFileName))
			{
				WriteToLogFile ("Es wurde kein Dateiname für den Export angegeben");
				Console.WriteLine("Warn: Es wurde kein Dateiname für den Export angegeben");

				throw new ExportNoFileException("Export: Es wurde kein Dateiname für den Export angegeben");
			}

			if (!string.IsNullOrWhiteSpace (setting.strExportTartgetDir) && Directory.Exists(setting.strExportTartgetDir))
			{
				if (!isWriteExport)
				{
					if (File.Exists (tempExport))
					{
						File.Delete (tempExport);
						
					}
					isWriteExport = true;
				}

				try
				{
					//using (StreamWriter sw = File.AppendText (Path.Combine (new string [] { setting.strExportTartgetDir, setting.strExportFileName + ".txt" })))
					using (StreamWriter sw = File.AppendText (tempExport))
					{
						if (vals != null && vals.Length > 0)
						{
							//sw.Write (string.Format ("{0}: {1}\r\n", DateTime.Now.ToString (), string.Format (MessageFormat, vals)));
							sw.Write (string.Format ("{1}\r\n", DateTime.Now.ToString (), string.Format (MessageFormat, vals)));    //Zeitstempel wird nicht geschrieben
						}
						else
						{
							sw.Write (string.Format ("{1}\r\n", DateTime.Now.ToString (), MessageFormat));      //Zeitstempel wird nicht geschrieben
																												//sw.Write (string.Format ("{0}: {1}\r\n", DateTime.Now.ToString (), MessageFormat));
						}
					}
				}
				catch (Exception ex)
				{

					Console.WriteLine (ex.Message);
					System.Threading.Thread.CurrentThread.Abort ();
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

		public string appPublisher { get; set; }

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
                    appName = string.IsNullOrWhiteSpace(strTemp) ? "_N/A_": strTemp;                    
                }

				if (string.IsNullOrWhiteSpace (appPublisher))
				{
					value.TryGetValue ("Publisher", out strTemp);
					appPublisher = string.IsNullOrWhiteSpace (strTemp) ? "_NO_PUBLISHER_" : strTemp;
				}

				if (string.IsNullOrWhiteSpace(appVersion))
                {
                    value.TryGetValue("DisplayVersion", out strTemp);
                    appVersion = string.IsNullOrWhiteSpace(strTemp) ? "_0.0.0.0_" : strTemp;                    
                }

                if (string.IsNullOrWhiteSpace(appName))
                    Debug.WriteLine("Kein Name angegeben");

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

		[Option ('n', "nagios", Required = false, //Separator = ',',
		HelpText = "Consolen Output ist Nagiosgerecht formartiert")]
		public bool strOutputNagios { get; set; }
	}

	[JsonObject(MemberSerialization.OptIn)]
    class Settings
    {
        public Settings() { }

        [JsonProperty(PropertyName = "Search", Required = Required.Always)]
        public string strSearchTag { get; set; }

        [JsonProperty(PropertyName = "Pattern", Required = Required.Always)]
        public string strSearchPattern { get; set; }

        [JsonProperty(PropertyName = "UseRegEx", Required = Required.Always)]
        public bool bUseRegEx { get; set; } = false;

        [JsonProperty(PropertyName = "CaseSensitive")]
        public bool bCaseSensitive { get; set; } = false;

        [JsonProperty(PropertyName = "ExportTargetDir")]
        public string strExportTartgetDir { get; set; } = "";   // TODO: Verzeichnis der Anwendung benutzen

        [JsonProperty(PropertyName = "ExportFileName", Required = Required.Always)]
        public string strExportFileName { get; set; } = "ExportInstalledApplications";     //   TODO: Anwendungsungsname als Default

        [JsonProperty(PropertyName = "ExportFileFormat", Required = Required.Always)]
        public List<string> listFormat; // { get; set; } = new List<string>(){ "TXT" };

        [JsonProperty(PropertyName = "Log", Required = Required.Always)]
        public string strLogFile { get; set; } = "";

        [JsonProperty(PropertyName = "AppendLog", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool bAppend2Logfile { get; set; } = false;

        [JsonProperty(PropertyName = "LogSuffix", Required = Required.AllowNull)]
        public string strLogSuffix { get; set; } = "";

        [JsonProperty(PropertyName = "LogPrefix", Required = Required.AllowNull)]
        public string strLogPrefix { get; set; } = "";

		[JsonProperty (PropertyName = "UseOutputInNagios")]
		public bool bUseOutputInNagios { get; set; } = false;
	}

	public class ExportNoFileException : Exception
	{
		public ExportNoFileException () { }
		public ExportNoFileException (string message) : base (message)
		{
			Program.Status = (int)Program.nagiosStatus.Critical;
			//Thread.CurrentThread.Abort ();
			Environment.Exit ((int) Program.nagiosStatus.Critical);
		}
		public ExportNoFileException (string message, Exception inner) : base (message, inner) { }
		protected ExportNoFileException (
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base (info, context) { }
	}
}
