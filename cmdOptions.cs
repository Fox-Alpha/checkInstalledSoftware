using System.Collections.Generic;

using CommandLine;

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
    class cmdOptions
    {

        [Option('x', "export", Required = false, Separator = ',',
        HelpText = "Ausgabe der Listen als: [CSV|TXT|JSON|XML]")]

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
}
