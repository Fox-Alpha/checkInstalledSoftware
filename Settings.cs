using System.Collections.Generic;
using Newtonsoft.Json;

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
    }
}
