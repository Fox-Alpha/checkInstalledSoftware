using System;

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
    public class ExportNoFileException : Exception
	{
		public ExportNoFileException () { }
		public ExportNoFileException (string message) : base (message)
		{
			Program.Status = (int)Program.nagiosStatus.Critical;
			Environment.Exit ((int) Program.nagiosStatus.Critical);
		}
		public ExportNoFileException (string message, Exception inner) : base (message, inner) { }
		protected ExportNoFileException (
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base (info, context) { }
	}
}
