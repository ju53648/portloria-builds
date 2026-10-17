# Portloria & Co. – Alpha-Downloads

Versionierter, öffentlicher **Testkanal** für Portloria & Co. (Lumorix).

- **Downloadseite:** https://ju53648.github.io/portloria-builds/
- **Testlauncher:** in den GitHub Releases (PortloriaLauncher.exe).
- **Windows-Spielarchiv:** in den versionierten Releases. Es ist nicht nötig, dieses Archiv manuell herunterzuladen, wenn der Launcher verwendet wird.
- **Updates:** Der Testlauncher liest `latest.json` aus dem Hauptbranch, prüft Archivgröße und SHA-256 und installiert neuere Spielversionen.
- **Spielstände:** bleiben bei Updates erhalten, sofern sie im Saved-Ordner unter dem installierten Spielverzeichnis liegen. Bitte Alpha-Spielstände zusätzlich sichern.

## Hinweise

Dies ist **Alpha-Software**, keine fertige öffentliche Spielveröffentlichung. Onlinefunktionen verwenden Epic Online Services; solange die EAS-Anwendung im Entwurfsmodus ist, können separate Testberechtigungen nötig sein. Windows kann bei dem derzeit **nicht digital signierten** Testlauncher eine Warnung anzeigen. Lade ihn nur von diesem Repository herunter.

## Neue Version veröffentlichen

1. Den getesteten Windows-Spielbuild ohne persönliche Spielstände als ZIP packen.
2. Das ZIP mit einer neuen Versionsnummer `vX.Y.Z-alpha` als GitHub-Release-Asset hochladen.
3. Nach erfolgreichem Upload den SHA-256-Hash und die tatsächliche Archivgröße berechnen.
4. In `latest.json` `version`, `downloadUrl`, `sha256` und `sizeBytes` auf die neue Version setzen und committen.
5. Bereits installierte Testlauncher finden die neue Version beim nächsten Start.

Die erste Version lädt bei jedem Update weiterhin das vollständige Archiv; Delta-Updates sind nicht implementiert.

## Sicherheitsmodell

Der Launcher beschränkt Downloads auf HTTPS-Release-Assets dieses Repositorys, verlangt SHA-256 und schützt gegen ZIP-Verzeichnisausbruch. Bei Fehlern wird die alte Installation nicht absichtlich überschrieben. Eine reguläre digitale Codesignatur ist noch nicht vorhanden.