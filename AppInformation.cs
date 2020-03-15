using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;

/*
	TODO:
	>	- Durchsuchen von Pfaden im Dateisystem, zum Beispiel c:\Programme\[PATTERN]\*oder c:\programme (x86)\[PATTERN]\*
	>	--	Durchsuvchen der Unterverzeichnisse zum Beispiel nach .exe Anwendungen
		--- Auslesen der Dateiinformationen und abgleich mit den Registry Werten um Dubletten zu vermeiden
		- Export als CSV datei
		- Export in SQLite DB File
		-- Verwendung von EntityFramework ???
		-- Durchsuchen der SQLite nach einem [PATTERN]
	>	+ AppInformation: Erweitern um das Feld, Installations Pfad
		# Anwendung für "Man in the Middle" erstellen. ASP .NET Core unter Linux
		-- Nimmt die Ergebnisse der Clients an und schreibt diese in die SQLite DB
		-- Nimmt Anfragen aus dem Browser oder einer GUI Anwendung an um Ergebnisse Abzufragen
		# Datenmodell:
		--	Host, DateTime, Anwendungsname, Hersteller, Version, InstallPfad, Hashwert 
		--- Hashwert aus Name, Hersteller, Version (evtl, Host) zur eindeutigen Identifizierung und Update
*/

namespace checkInstalledSoftware
{
    class AppInformation
    {
        //  Klasse zum aufnehmen der Informationen aus der Registry
        public bool appIsRegistryPath { get; set; }
        public string appRegKey { get; set; }

        public string appPublisher { get; set; }
        public string appHashValue { get; private set; }
        public string appName { get; set; }

        public string appVersion { get; set; }

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
            _appRegistry = new Dictionary<string, string>();
        }

        public AppInformation(string _Name, string _Version, string _Key, string _publisher = "", bool _appIsRegistryPath = true)
        {
            appName = _Name;
            appVersion = _Version;
            appRegKey = _Key;
            appPublisher = _publisher;

            appIsRegistryPath = _appIsRegistryPath;

            appHashValue = CalculateMD5Hash(string.Format("{0}{1}{2}", appName, appVersion, appPublisher));

            _appRegistry = new Dictionary<string, string>();
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
}
