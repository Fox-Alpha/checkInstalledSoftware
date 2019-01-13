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
using System.Security.Cryptography;

/*
	TODO:
		- Durchsuchen von Pfaden im Dateisystem, zum Beispiel c:\Programme\[PATTERN]\*oder c:\programme (x86)\[PATTERN]\*
		--	Durchsuvchen der Unterverzeichnisse zum Beispiel nach .exe Anwendungen
		--- Auslesen der Dateiinformationen und abgleich mit den Registry Werten um Dubletten zu vermeiden
		- Export als CSV datei
		- Export in SQLite DB File
		-- Verwendung von EntityFramework ???
		-- Durchsuchen der SQLite nach einem [PATTERN]
		+ AppInformation: Erweitern um das Feld, Installations Pfad
		# Anwendung für "Man in the Middle" erstellen. ASP .NET Core unter Linux
		-- Nimmt die Ergebnisse der Clients an und schreibt diese in die SQLite DB
		-- Nimmt Anfragen aus dem Browser oder einer GUI Anwendung an um Ergebnisse Abzufragen
		# Datenmodell:
		--	Host, DateTime, Anwendungsname, Hersteller, Version, InstallPfad, Hashwert 
		--- Hashwert aus Name, Hersteller, Version (evtl, Host) zur eindeutigen Identifizierung und Update
*/

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

		public static bool writeLog { get; set; } // C# 6.0 = true;
		public static bool isWriteExport { get; set; } // C# 6.0 = false;

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
			writeLog = true;
			isWriteExport = false;
			
            dicApplications = new Dictionary<string, AppInformation>();
            string[] cmdLine;

			try
            {

                if ((cmdLine = Environment.GetCommandLineArgs()).Length > 1)
                {
                    string strTemp = cmdLine[1];

                    if (Path.IsPathRooted(strTemp))
                    {
                        if (File.Exists(cmdLine[1]))
                        {
                            strJSONKonfigFile = cmdLine[1];
                        }
                    }
                    else if (File.Exists(cmdLine[1]))
                    {
                        strJSONKonfigFile = Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, cmdLine[1] });
                    }
                    else
                    {
                        Console.WriteLine("Die angegebene Konfigurationsdatei konnte nicht gefunden werden. Diese scheint nicht zu existieren. {strTemp}");
                        WriteToLogFile("Die angegebene Konfigurationsdatei konnte nicht gefunden werden. Diese scheint nicht zu existieren. {0}", new string [] { strTemp});
                        return (int)nagiosStatus.Critical;
                    }
                }
                
                // Lesen der Settings aus der JSON Datei
                ReadSettingsFromJSONConfigFile();
                // TODO: STatus prüfen und ggf abrechen

                WriteToLogFile("Exportieren der Daten", null);
                WriteToLogFile("Speicherverbrauch: {0}", Environment.WorkingSet.ToString());
                WriteToLogFile("Speicherverbrauch: {0}", Environment.WorkingSet.ToString());

                // Lesen der Uninstall Einträger der Registry
                ReadApplicatiosnFromRegistry();

                // Wenn Aktiv, auch Verzeichnisse durchsuchen
                if (setting.bSearchFileSystem)
                {
                    DurchsucheUnterverzeichnisseNachApplicationen();
                }

                WriteToLogFile("Exportieren der Daten", null);
                ExportData();

                if (setting.bUseOutputInNagios)
                {
                    Console.WriteLine("OK Es wurden {0} Anwendungen gefunden", exportCount);
                    WriteToLogFile("Einlesen der Konfiguration", null);
                    Status = (int)nagiosStatus.Ok;
                }
                else
                {
                    Console.WriteLine("Es wurden {1} von {0} Einträge exportiert", dicApplications.Count.ToString(), exportCount);

                    Console.WriteLine("Taste drücken zum fortsetzen ! ...");
                    Console.ReadKey();
                }
            }
            catch ( ThreadAbortException taex)
			{
				Console.WriteLine (taex.Message);
				Console.WriteLine (taex.InnerException.Message);
				return Status;
			}

            return Status;
        }

        private static void ReadSettingsFromJSONConfigFile()
        {
            if (File.Exists(strJSONKonfigFile))
            {
                // Laden der Parameter aus der Konfiguration

                if (!ReadJSonKonfigurationFile(strJSONKonfigFile))
                {
                    WriteToLogFile("Fehler beim laden der Einstellungen: {0}", new string[] { strJSONKonfigFile });
                    status = (int)nagiosStatus.Critical;
                }

                WriteToLogFile(string.Format("Start der Anwendung"), null);
                WriteToLogFile("Speicherverbrauch: {0}", Environment.WorkingSet.ToString());

                WriteToLogFile("Laden der Such und Filter Bedingungen: {0}", new string[] { strJSONKonfigFile });
            }
            else
            {
                Console.WriteLine("Es konnte keine Datei mit Einstellungen geladen werden|File=" + strJSONKonfigFile);

                status = (int)nagiosStatus.Critical;
            }
        }

        private static void ReadApplicatiosnFromRegistry()
        {
            WriteToLogFile("Lesen der Registry UNINSTALL Einträge", null);
            WriteToLogFile("Speicherverbrauch: {0}", Environment.WorkingSet.ToString());
            if (Environment.Is64BitOperatingSystem)
            {
                //  32 & 64 BIT Registry Bereiche lesen
                //					GetRegistryInformation (false);
                //					GetRegistryInformation (true);

                //  32 & 64 BIT Registry Bereiche lesen
                Debug.WriteLine("... 32 Bit Registry");
                WriteToLogFile("... 32 Bit Registry", null);
                Debug.WriteLine("-------------------------------------------");
                Console.WriteLine("... 32 Bit Registry");
                GetRegistryInformation(false);
                Debug.WriteLine("... 64 Bit Registry");
                WriteToLogFile("... 64 Bit Registry", null);
                Debug.WriteLine("-------------------------------------------");
                Console.WriteLine("... 64 Bit Registry");
                GetRegistryInformation(true);
            }
            else
            {
                //  nur 32 Bit vorhanden
                Console.WriteLine("... 32 Bit Registry");
                GetRegistryInformation(true);

                //  nur 32 Bit vorhanden
                //					GetRegistryInformation (true);
            }
        }

        private static async void DurchsucheUnterverzeichnisseNachApplicationen()
        {
            // Verzeichnisse durchsucht werden sollen, dann müssen die weiterenOptionen gegeben sein.
            //if (string.IsNullOrWhiteSpace(setting.strSearchFolderPattern)) { };

            if (setting.lstSearchFilePath.Count >= 1)
            {
                List<string> validPath = new List<string>();
                DurchsucheVerzeichnisse Verzeichnisse;

                //validPath = await Task.Factory.StartNew(Verzeichnisse.LeseUnterverzeichnis(@"c:\temp");

                foreach (var path in setting.lstSearchFilePath)
                {
                    if (!Path.IsPathRooted(path))
                    {
                        if (Directory.Exists(path))
                        {
                            try
                            {
                                Verzeichnisse = new DurchsucheVerzeichnisse(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, path }));
                                validPath.AddRange(await Task<List<string>>.Factory.StartNew(Verzeichnisse.LeseUnterverzeichniss));
                            }
                            catch (UnauthorizedAccessException UnAuthFile)
                            {
                                Console.WriteLine("UnAuthFile: {0}", UnAuthFile.Message);
                                Console.ReadLine();
                            }
                        }
                            //validPath.AddRange(await Directory.GetDirectories(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, path })).ToList());
                    }
                    else
                    {
                        try
                        {
                            if (Directory.Exists(path))
                            {
                                //var temp = await Task<List<string>>.Factory.StartNew(Verzeichnisse.LeseUnterverzeichniss());
                                //validPath.AddRange((List<string>)temp.toArray());

                                Verzeichnisse = new DurchsucheVerzeichnisse(path);
                                validPath.AddRange(await Task<List<string>>.Factory.StartNew(Verzeichnisse.LeseUnterverzeichniss));
                            }
                        }
                        catch (UnauthorizedAccessException UnAuthFile)
                        {
                            //Console.WriteLine("UnAuthFile: {0}", UnAuthFile.Message);
                            //Console.ReadLine();
                            //continue;
                        }
                        catch (Exception ex)
                        {
                            //throw new Exception("Durchsuchen eines Verzeichnisses nicht möglich: {ex.message}");
                        }
                    }
                    //continue;
                }
                if (validPath.Count > 0)
                {
                    foreach (var dir in validPath)
                    {
                        if (Regex.IsMatch(dir, setting.strSearchFolderPattern))
                        {
                            //Aufruf über Async ... Await ????
                            //ApplicationFileList(path);
                            // Console.WriteLine(dir);
                            ApplicationFileList(dir);
                        }
                    }
                }
            }

            //if (setting.lstSearchFileExt.Count >= 1) { };

            //if (setting.iSearchFolderDepth) { };

        }

        static void ApplicationFileList(string PathToSearch)
        {
            DirectoryInfo diTop = new DirectoryInfo(PathToSearch);

            try
            {
                //foreach (var fi in diTop.EnumerateFiles())
                //{
                //    try
                //    {
                //        // Display each file over 10 MB;
                //        if (fi.Length > 10000000)
                //        {
                //            Console.WriteLine("{0}\t\t{1}", fi.FullName, fi.Length.ToString("N0"));
                //        }
                //    }
                //    catch (UnauthorizedAccessException UnAuthTop)
                //    {
                //        Console.WriteLine("{0}", UnAuthTop.Message);
                //    }
                //}

                List<FileVersionInfo> fviApplication = new List<FileVersionInfo>();

                //foreach (var di in diTop.EnumerateDirectories(string.IsNullOrEmpty(PathToSearch) ? "*" : setting.strSearchFolderPattern ))
                //foreach (var di in diTop.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                //{
                    try
                    {
                        foreach (var fi in diTop.EnumerateFiles("*.exe", SearchOption.AllDirectories))
                        {
                            try
                            {
                                fviApplication.Add(FileVersionInfo.GetVersionInfo(fi.FullName));
                            }
                            catch (UnauthorizedAccessException UnAuthFile)
                            {
                                Console.WriteLine("UnAuthFile: {0}", UnAuthFile.Message);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException UnAuthSubDir)
                    {
                        Console.WriteLine("UnAuthSubDir: {0}", UnAuthSubDir.Message);
                    }
                //}
            }
            catch (DirectoryNotFoundException DirNotFound)
            {
                Console.WriteLine("{0}", DirNotFound.Message);
            }
            catch (UnauthorizedAccessException UnAuthDir)
            {
                Console.WriteLine("UnAuthDir: {0}", UnAuthDir.Message);
            }
            catch (PathTooLongException LongPath)
            {
                Console.WriteLine("{0}", LongPath.Message);
            }
        }

        static void GetRegistryInformation(bool is64Bit)
        {
			RegistryKey key;
            string valueType = "";
            string value = "";
            Dictionary<string, string> dicTemp;

            string activePath = string.Empty;
			
			//	Wenn die Anwendung kein 64Bit Process ist, kann diese nur mit Umweg auf den 64Bit Bereich der Registry zugreifen
			if (is64Bit && !Environment.Is64BitProcess) 
			{
	            activePath = string.Format(RegPath2Uninstall, "");
	            key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(activePath,false);				
			}
			else
			{
	            activePath = string.Format(RegPath2Uninstall, is64Bit ? "" : RegPath2Uninstall32);
	            key = Registry.LocalMachine.OpenSubKey(activePath,false);
			}
            string[] valueNames;

            if (key != null && key.SubKeyCount > 0)
            {
                dicTemp = new Dictionary<string, string>();

                //  Alle Schlüssel der Installierten Anwendungen
                foreach (string  appKey in key.GetSubKeyNames())
                {
                    if((valueNames = key.OpenSubKey(appKey, false).GetValueNames()).Length > 0)
                    {
                        foreach (string str in valueNames)
                        {
                            switch (key.GetValueKind(str))
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
                        //Add2Dictionary(valueNames, activePath + appKey);
                        Add2Dictionary(dicTemp, activePath + appKey);
                    }
                }
            }
        }

        //static void Add2Dictionary(string[] valueList, string RegPath, bool isRegistryKey=true)
        static void Add2Dictionary(Dictionary<string, string> dicTemp, string RegPath, bool isRegistryKey = true)
        {
            //  TODO: Try ... Catch bei Registry Zugriff
            //  ArgumentException, UnauthorizedAccessException, SecurityException
//            RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath, false);
 			//RegistryKey key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(RegPath,false);
            AppInformation appInf;
            
            
            

            //if (key != null && key.ValueCount > 0)
            //{
                appInf = new AppInformation
                {
                    appRegKey = RegPath,
                    appRegistry = dicTemp,
                    appIsRegistryPath = isRegistryKey
                };

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
            //}
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
				WriteToTxtExportFile (string.Format ("{0}: Installierte Software / Suchfilter: {1} / System:  {2}\r\n", DateTime.Now.ToString (), setting.strSearchTag, Environment.MachineName));

				foreach (AppInformation ai in dicApplications.Values)
                {
                    string strTemp;
                    /*
					 * TODO: Filter nutzung
                     * TODO: Refactoring für Ausgabeformatliste
					 */

                    //##### Refavtor #####

                    //Prüfen ob ein Filterobject in der Konfiguration angegeben wurde. z.B. Publisher, Versionm, AppName, etc.
                    ai.appRegistry.TryGetValue(setting.strSearchTag, out strTemp);


                    if (!string.IsNullOrWhiteSpace(strTemp))
                    {
                        // Per Setting, ein RegEx auf den Registrywert anwenden
                        if (setting.bUseRegEx)
                        {
                            // Wert per RegEx gefunden ?
                            // Vergleich Case Senstitiv ? Settings !
                            if (Regex.IsMatch(strTemp, setting.strSearchPattern, setting.bCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase))
                            {
                                if (setting.listFormat.Count > 0)
                                {
                                    foreach (var exoFormat in setting.listFormat)
                                    {
                                        switch (exoFormat)
                                        {
                                            case "TXT":
                                                WriteToTxtExportFile(new[] { ai.appName, ai.appVersion, ai.appPublisher });
                                                break;
                                            case "CSV":
                                                break;
                                                // Weitere Formate
                                            case "XML":
                                            case "SQL":
                                                break;
                                            default:
                                                WriteToLogFile(string.Format("Fehler: Das angegebene Dateiformat wird für den Export nicht unterstützt: {0}", exoFormat));
                                                Console.WriteLine(string.Format("Fehler: Das angegebene Dateiformat wird für den Export nicht unterstützt: {0}", exoFormat));
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Fehler: Es wurde kein Suchbereich angegeben"));
                        WriteToLogFile(string.Format("Fehler: Es wurde kein Suchbereich angegeben"));
                        //	TODO: Alle Exportformate beachten
                        WriteToTxtExportFile(string.Format("Fehler: Es wurde kein Suchbereich angegeben"));
                        return;
                    }

                    WriteToLogFile("Es wurden {1} von {0} Einträge exportiert", dicApplications.Count.ToString(), i.ToString());
                    exportCount = i;
                    isWriteExport = false;

                    return;
                    //##### ######## #####

                    if (!string.IsNullOrWhiteSpace(setting.strSearchTag) && !string.IsNullOrWhiteSpace (setting.strSearchPattern))
					{
						ai.appRegistry.TryGetValue (setting.strSearchTag, out strTemp);

						if (setting.bUseRegEx&& !string.IsNullOrWhiteSpace(strTemp))
						{
							if(Regex.IsMatch (strTemp, setting.strSearchPattern, setting.bCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase))
							{
                                //WriteToTxtExportFile (string.Format ("{2} - {0} - {1}", new [] { ai.appName, ai.appVersion, ai.appPublisher }));
                                WriteToTxtExportFile(new[] { ai.appName, ai.appVersion, ai.appPublisher });
                                i++;
							}
						}
						else if (!setting.bUseRegEx && !string.IsNullOrWhiteSpace (strTemp))
						{
							//	if (strTemp == setting.strSearchPattern)
							if(Regex.Match(strTemp, setting.strSearchPattern).Success)
							{
								//WriteToTxtExportFile (string.Format ("{2} - {0} - {1}", new [] { ai.appName, ai.appVersion, ai.appPublisher }));
                                WriteToTxtExportFile(new[] { ai.appName, ai.appVersion, ai.appPublisher });
                                i++;
							}
						}

					}
					else
					{
						Debug.WriteLine (string.Format ("Fehler: Es wurde kein Suchbereich angegeben"));
						WriteToLogFile (string.Format ("Fehler: Es wurde kein Suchbereich angegeben"));
						//	TODO: Alle Exportformate beachten
						//WriteToTxtExportFile (string.Format ("Fehler: Es wurde kein Suchbereich angegeben"));
                        WriteToTxtExportFile(new[] { "Fehler: Es wurde kein Suchbereich angegeben" });
                        return;
					}

					/*#####*/

					ai.appRegistry.TryGetValue("Publisher", out strTemp);

                    Debug.WriteLine(string.Format("{2} - {0} - {1}", ai.appName, ai.appVersion, strTemp));
					//	TODO: Alle Exportformate beachten
                }
			}
        }

        static private bool ReadJSonKonfigurationFile(string JSonFile)
        {
            if (!File.Exists(JSonFile))
            {
                Debug.WriteLine("Fehlende Angabe oder Angegebene Konfiguration ungültig.");
                Console.WriteLine("Fehlende Angabe oder Angegebene Konfiguration ungültig.");
                WriteToLogFile("Fehlende Angabe oder Angegebene Konfiguration ungültig.", "");
                status = (int)nagiosStatus.Critical;
                return false;
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
                WriteToLogFile("Konfiguration wurde eingelesen", null);

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
                status = (int)nagiosStatus.Critical;
                return false;

			}
            return true;
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

		//static void WriteToTxtExportFile (string MessageFormat, params string [] vals)
        static void WriteToTxtExportFile(params string[] vals)
        {
			/* Wenn kein Verzeichnis angegeben wurde, wird das Verzeichnis der Anwendung verwendet */
			if (string.IsNullOrWhiteSpace (setting.strExportTartgetDir))
				setting.strExportTartgetDir = Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "\\Data" });

			/* Wenn kein Dateiname angegeben wurde, Export abrechen */
			if (string.IsNullOrWhiteSpace(setting.strExportFileName))
			{
				WriteToLogFile ("Es wurde kein Dateiname für den Export angegeben");
				Console.WriteLine("Warn: Es wurde kein Dateiname für den Export angegeben");

				throw new ExportNoFileException("Export: Es wurde kein Dateiname für den Export angegeben");
			}

            string tempExport = "";


			if (Path.IsPathRooted (setting.strExportTartgetDir))
			{
				//if (File.Exists (tempExport))
				//{
				tempExport = Path.Combine(new[] { setting.strExportTartgetDir, setting.strExportFileName + ".txt" });
                //}
			}
			else
			{
				tempExport = Path.Combine (new string [] { AppDomain.CurrentDomain.BaseDirectory, setting.strExportTartgetDir, setting.strExportFileName });
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
                        //  Wenn mehr als ein Parameter übergeben wurde, dann als string "joinen"
						if (vals != null && vals.Length > 1)
						{
                            //sw.Write (string.Format ("{0}: {1}\r\n", DateTime.Now.ToString (), string.Format (MessageFormat, vals)));
                            //sw.Write (string.Format ("{1}\r\n", DateTime.Now.ToString (), string.Format (MessageFormat, vals)));    //Zeitstempel wird nicht geschrieben
                            sw.Write(string.Join(" - ", vals));
						}
                        //  Ansonsten nur den ersten Paremeter ausgeben
						else if (vals != null && vals.Length == 1)
                        {
							sw.Write (string.Format ("{0}\r\n", vals[0]));      //Zeitstempel wird nicht geschrieben
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

        static void WriteToCsvExportFile(string MessageFormat, params string[] vals)
        {
            /* Wenn kein Verzeichnis angegeben wurde, wird das Verzeichnis der Anwendung verwendet */
            if (string.IsNullOrWhiteSpace(setting.strExportTartgetDir))
                setting.strExportTartgetDir = Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "\\Data" });

            /* Wenn kein Dateiname angegeben wurde, Export abrechen */
            if (string.IsNullOrWhiteSpace(setting.strExportFileName))
            {
                WriteToLogFile("Es wurde kein Dateiname für den Export angegeben");
                Console.WriteLine("Warn: Es wurde kein Dateiname für den Export angegeben");

                throw new ExportNoFileException("Export: Es wurde kein Dateiname für den Export angegeben");
            }

            string tempExport = "";


            if (Path.IsPathRooted(setting.strExportTartgetDir))
            {
                //if (File.Exists (tempExport))
                //{
                tempExport = Path.Combine(new[] { setting.strExportTartgetDir, setting.strExportFileName + ".csv" });
                //}
            }
            else
            {
                tempExport = Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, setting.strExportTartgetDir, setting.strExportFileName });
            }


            if (!string.IsNullOrWhiteSpace(setting.strExportTartgetDir) && Directory.Exists(setting.strExportTartgetDir))
            {
                if (!isWriteExport)
                {
                    if (File.Exists(tempExport))
                    {
                        File.Delete(tempExport);
                    }
                    isWriteExport = true;
                }

                try
                {
                    //using (StreamWriter sw = File.AppendText (Path.Combine (new string [] { setting.strExportTartgetDir, setting.strExportFileName + ".txt" })))
                    using (StreamWriter sw = File.AppendText(tempExport))
                    {
                        if (vals != null && vals.Length > 0)
                        {
                            //sw.Write (string.Format ("{0}: {1}\r\n", DateTime.Now.ToString (), string.Format (MessageFormat, vals)));
                            sw.Write(string.Format("{1}\r\n", DateTime.Now.ToString(), string.Format(MessageFormat, vals)));    //Zeitstempel wird nicht geschrieben
                        }
                        else
                        {
                            sw.Write(string.Format("{1}\r\n", DateTime.Now.ToString(), MessageFormat));      //Zeitstempel wird nicht geschrieben
                                                                                                             //sw.Write (string.Format ("{0}: {1}\r\n", DateTime.Now.ToString (), MessageFormat));
                        }
                    }
                }
                catch (Exception ex)
                {

                    Console.WriteLine(ex.Message);
                    System.Threading.Thread.CurrentThread.Abort();
                }
            }
        }

        static void WriteToSQLiteExportFile(string MessageFormat, params string[] vals)
        {

        }
    }
    class DurchsucheVerzeichnisse
    {
        string SubFolder{ get; set; } = "";

        public DurchsucheVerzeichnisse(string Folder)
        {
            if (!string.IsNullOrWhiteSpace(Folder))
            {
                SubFolder = Folder;
            }
        }

        //public List<string> LeseUnterVerzeichnisse()
        //{
        //    return new List<string> { "Test1", "Test3" } ;
        //}

        public List<string> LeseUnterverzeichniss()
        {
            try
            {
                return Directory.GetDirectories(SubFolder, "*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
                return null;
            }
            
            //return new List<string> { "Test 123" };
        }


    }

    class AppInformation
    {
        //  Klasse zum aufnehmen der Informationen aus der Registry
        public bool appIsRegistryPath { get; set; }

        private string _appRegKey;
        public string appRegKey
        {
            get { return _appRegKey; }
            set { _appRegKey = value; }
        }

		public string appPublisher { get; set; }
        public string appHashValue { get; private set; }

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

        Dictionary<string, string> _appRegistry;
        public Dictionary<string, string> appRegistry
        {
            get { return _appRegistry; }
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

                if (string.IsNullOrWhiteSpace(appHashValue))
                    appHashValue = CalculateMD5Hash(string.Format("{0}{1}{2}", appName, appVersion, appPublisher));

                if (string.IsNullOrWhiteSpace(appName))
                    Debug.WriteLine("Kein Name angegeben");

                _appRegistry = value;
            }
        }

        public AppInformation()
        {
            appRegistry = new Dictionary<string, string>();
        }

        public AppInformation(string _Name, string _Version, string _Key, string _publisher = "", bool _appIsRegistryPath = true)
        {
            appName = _Name;
            appVersion = _Version;
            appRegKey = _Key;
            appPublisher = _publisher;

            appIsRegistryPath = _appIsRegistryPath;

            appHashValue = CalculateMD5Hash(string.Format("{0}{1}{2}", appName, appVersion, appPublisher));

            appRegistry = new Dictionary<string, string>();
        }

        private string CalculateMD5Hash(string input)
        {

            // step 1, calculate MD5 hash from input

            MD5 md5 = MD5.Create();

            byte[] inputBytes = Encoding.ASCII.GetBytes(input);

            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("{0:x2}"));
            }

            return sb.ToString();
        }

        ~AppInformation()
        {
            if (_appRegistry != null)
            {
                _appRegistry.Clear();
                _appRegistry = null;
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
        public bool bUseRegEx { get; set; } // C# 6.0  = false;

        [JsonProperty(PropertyName = "CaseSensitive")]
        public bool bCaseSensitive { get; set; } // C# 6.0  = false;

        [JsonProperty(PropertyName = "ExportTargetDir")]
        public string strExportTartgetDir { get; set; } // C# 6.0  = "";   // TODO: Verzeichnis der Anwendung benutzen

        [JsonProperty(PropertyName = "ExportFileName", Required = Required.Always)]
        public string strExportFileName { get; set; } // C# 6.0  = "ExportInstalledApplications";     //   TODO: Anwendungsungsname als Default

        [JsonProperty(PropertyName = "ExportFileFormat", Required = Required.Always)]
        public List<string> listFormat { get; set; } // = new List<string>(){ "TXT" };

        [JsonProperty(PropertyName = "Log", Required = Required.Always)]
        public string strLogFile { get; set; } // C# 6.0  = "";

        [JsonProperty(PropertyName = "AppendLog", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool bAppend2Logfile { get; set; } // C# 6.0  = false;

        [JsonProperty(PropertyName = "LogSuffix", Required = Required.AllowNull)]
        public string strLogSuffix { get; set; } // C# 6.0  = "";

        [JsonProperty(PropertyName = "LogPrefix", Required = Required.AllowNull)]
        public string strLogPrefix { get; set; } // C# 6.0  = "";

		[JsonProperty (PropertyName = "UseOutputInNagios")]
		public bool bUseOutputInNagios { get; set; } // C# 6.0  = false;

        [JsonProperty(PropertyName = "SearchFileSystem", Required = Required.Always)]
        public bool bSearchFileSystem { get; set; } // C# 6.0  = false;

        [JsonProperty(PropertyName = "SearchFolderPattern", Required = Required.AllowNull)]
        public string strSearchFolderPattern { get; set; } // C# 6.0  = "";

        [JsonProperty(PropertyName = "SearchFilePath", Required = Required.AllowNull)]
        public List<string> lstSearchFilePath { get; set; } // = new List<string>(){ "TXT" };

        [JsonProperty(PropertyName = "SearchFileExt", Required = Required.Always)]
        public List<string> lstSearchFileExt { get; set; } // = new List<string>(){ "TXT" };

        [JsonProperty(PropertyName = "SearchSubFolder")]
        public bool bSearchSubFolder { get; set; } // C# 6.0  = false;

        [JsonProperty(PropertyName = "SearchFolderDepth", Required = Required.AllowNull)]
        public bool iSearchFolderDepth { get; set; } // C# 6.0  = false;

        /*
         "SearchFileSystem": "true",
        "SearchFolderPattern": "(A|a)lpha(\\s|[-]{0,1})(C|c)om",
        "SearchFileExt": [ "exe" ],
        "SearchSubFolder": [ "c:\\Programme", "c:\\programme (x86)" ],
        "SearchFolderDepth": 2,
        */
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
