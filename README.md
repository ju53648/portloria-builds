# Portloria & Co. – Alpha-Downloads

Versionierter, öffentlicher **Testkanal** für Portloria & Co. (Lumorix).

- **Downloadseite:** https://ju53648.github.io/portloria-builds/
- **Testlauncher:** in den GitHub Releases (PortloriaLauncher.exe).
- **Windows-Spielarchiv:** in den versionierten Releases. Es ist nicht nötig, dieses Archiv manuell herunterzuladen, wenn der Launcher verwendet wird.
- **Updates:** Der Testlauncher liest `latest.json` aus dem Hauptbranch, prüft Archivgröße und SHA-256 und installiert neuere Spielversionen.
- **Spielstände:** bleiben bei Updates erhalten, sofern sie im Saved-Ordner unter dem installierten Spielverzeichnis liegen. Bitte Alpha-Spielstände zusätzlich sichern.

## Hinweise

Dies ist **Alpha-Software**, keine fertige öffentliche Spielveröffentlichung. Onlinefunktionen verwenden Epic Online Services; solange die EAS-Anwendung im Entwurfsmodus ist, können separate Testberechtigungen nötig sein. Windows kann bei dem derzeit **nicht digital signierten** Testlauncher eine Warnung anzeigen. Lade ihn nur von diesem Repository herunter.

## Neue Version veröffentlichen – immer mit Delta-Patch

**Verbindlich:** Jeder weitere PORTLORIA-Spielrelease muss ein geprüftes Delta zur vorherigen öffentlichen Version enthalten. Das vollständige ZIP bleibt ausschließlich für Erstinstallationen, Reparatur und inkompatible alte Installationen verfügbar.

1. Einen getesteten Windows-Spielbuild als neue Versions-ZIP paketieren. Die genaue ZIP der letzten öffentlichen Version als Delta-Basis aufbewahren.
2. Auf dem autorisierten Entwicklungs-PC **Update-Planen.cmd** unter `E:\Lumorix\Projects\portloria-distribution` verwenden. Nicht nur das ZIP manuell hochladen.
3. Der Planer erzeugt mit **BuildPortloriaDelta.py** automatisch einen verschiebungsunabhängigen Bytebereichs-Patch (auch für Unreal-IoStore-Dateien), führt mit **VerifyPortloriaDelta.py** eine vollständige Byte-für-Byte-Prüfung durch und blockiert ungeprüfte Veröffentlichungen.
4. Der Publisher legt erst das vollständige ZIP **und** das Patch-ZIP als Release-Assets ab. Erst dann aktualisiert er gemeinsam `manifest.json`, `launcher-manifest.json` und `latest.json`.
5. Launcher mit passender, funktionsfähiger Basisversion wählen automatisch das kleinste valide direkte Delta anhand von `patches[]`. Der vollständige Download bleibt aus Sicherheitsgründen als Fallback erhalten. Reparaturen laden gegebenenfalls weiterhin das volle Paket.

Wenn der notwendige Patch fehlt oder nicht kleiner bzw. nicht verifizierbar ist, darf die Veröffentlichung nicht unbemerkt stattfinden. Eine Vollversion ohne Patch erfordert eine begründete, protokollierte Notfall-Ausnahme. Ein Versionswechsel allein darf keinen pauschalen Voll-Download erzwingen.

Technische Regel: `E:\Lumorix\Projects\portloria-distribution\DELTA-RELEASE-POLICY.md`.

## Sicherheitsmodell

Der Launcher beschränkt Downloads auf HTTPS-Release-Assets dieses Repositorys, verlangt SHA-256 und schützt gegen ZIP-Verzeichnisausbruch. Bei Fehlern wird die alte Installation nicht absichtlich überschrieben. Eine reguläre digitale Codesignatur ist noch nicht vorhanden.