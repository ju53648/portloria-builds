using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PortloriaBeta {
 internal sealed class Release {
  internal string Version, Url, Hash;
  internal long Size;
 }
 internal static class Engine {
  internal const string Endpoint="https://raw.githubusercontent.com/ju53648/portloria-builds/main/latest.json";
  internal static string Home { get {
#if TEST_ONLY
   string env=Environment.GetEnvironmentVariable("PORTLORIA_TEST_HOME");
   if(!string.IsNullOrEmpty(env))return env;
#endif
   return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PortloriaAndCo");
  }}
  internal static string InstallLocationFile {get{return Path.Combine(Home,"game-directory.txt");}}
  internal static string Game {get{
   try{
    string location=File.ReadAllText(InstallLocationFile,Encoding.UTF8).Trim();
    if(Path.IsPathRooted(location)&&location.Length>3)return Path.GetFullPath(location);
   }catch{}
   return Path.Combine(Home,"Game");
  }}
  internal static void ChooseInstallFolder(string parent){
   if(string.IsNullOrEmpty(parent)||!Path.IsPathRooted(parent))throw new InvalidDataException("Installationspfad ist ungueltig.");
   string folder=Path.GetFullPath(Path.Combine(parent,"Portloria & Co."));
   Directory.CreateDirectory(Home);
   File.WriteAllText(InstallLocationFile,folder,Encoding.UTF8);
  }
  internal static void RequireSpace(long downloadBytes){
   string root=Path.GetPathRoot(Game);
   var drive=new DriveInfo(root);
   if(!drive.IsReady || drive.AvailableFreeSpace<downloadBytes*3)
    throw new IOException("Auf "+root+" werden mindestens "+SizeText(downloadBytes*3)+" freier Platz fuer Download und Entpacken benoetigt. Bitte einen anderen Installationsordner waehlen.");
  }
  internal static string Exe {get{return Path.Combine(Game,"Windows","RetailGame.exe");}}
  internal static string State {get{return Path.Combine(Home,"installed-version.txt");}}
  internal static bool IsGameInstalled(){return File.Exists(Exe);}
  internal static string LocalVersion(){
   try{return File.ReadAllText(State,Encoding.UTF8).Trim();}catch{return "0.0.0";}
  }
  internal static bool Newer(string latest,string installed){
   Version a,b;
   if(!System.Version.TryParse(latest,out a)||!System.Version.TryParse(installed,out b))
    throw new InvalidDataException("Ungueltige Versionsnummer.");
   return a>b;
  }
  internal static Release Latest(){
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   string endpoint=Endpoint;
#if TEST_ONLY
   string test=Environment.GetEnvironmentVariable("PORTLORIA_TEST_MANIFEST");
   if(!string.IsNullOrEmpty(test))endpoint=test;
#endif
   string raw;
#if TEST_ONLY
   if(File.Exists(endpoint))raw=File.ReadAllText(endpoint,Encoding.UTF8);
   else
#endif
   using(WebClient web=new WebClient()){
    web.Encoding=Encoding.UTF8;
    web.Headers.Add(HttpRequestHeader.CacheControl,"no-cache");
    raw=web.DownloadString(endpoint);
   }
   var serializer=new JavaScriptSerializer();
   var fields=serializer.Deserialize<Dictionary<string,object>>(raw);
   Release r=new Release{Version=Convert.ToString(fields["version"]),Url=Convert.ToString(fields["downloadUrl"]),
    Hash=Convert.ToString(fields["sha256"]).ToLowerInvariant(),Size=Convert.ToInt64(fields["sizeBytes"])};
   if(!Regex.IsMatch(r.Version,@"^\d+\.\d+\.\d+$") ||
      !Regex.IsMatch(r.Hash,@"^[0-9a-f]{64}$") ||
      r.Size<1 ||r.Size>8L*1024*1024*1024)
    throw new InvalidDataException("Ungueltige Update-Metadaten.");
   bool allowed=r.Url.StartsWith("https://github.com/ju53648/portloria-builds/releases/download/",StringComparison.OrdinalIgnoreCase);
#if TEST_ONLY
   allowed=allowed||File.Exists(r.Url);
#endif
   if(!allowed)throw new InvalidDataException("Release-URL liegt ausserhalb des freigegebenen Kanals.");
   return r;
  }
  internal static string SizeText(long n){return (n/1073741824.0).ToString("0.00")+" GiB";}
  internal static void Download(Release r,string file,Action<int,string> progress) {
#if TEST_ONLY
   if(File.Exists(r.Url)){File.Copy(r.Url,file,true);progress(100,"Testarchiv kopiert");return;}
#endif
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   if(File.Exists(file)&&new FileInfo(file).Length>r.Size)File.Delete(file);
   Exception lastError=null;
   for(int attempt=1;attempt<=8;attempt++){
    long have=File.Exists(file)?new FileInfo(file).Length:0;
    if(have==r.Size){progress(100,"Bereits heruntergeladen – pruefe Pruefsumme ...");return;}
    try{
     var request=(HttpWebRequest)WebRequest.Create(r.Url);
     request.UserAgent="PortloriaBetaLauncher/0.2";
     request.Timeout=60000;
     request.ReadWriteTimeout=120000;
     request.KeepAlive=false;
     if(have>0)request.AddRange(have);
     using(var response=(HttpWebResponse)request.GetResponse()){
      if(have>0 && response.StatusCode!=HttpStatusCode.PartialContent){
       if(File.Exists(file))File.Delete(file);
       throw new IOException("Server hat den Teil-Download nicht akzeptiert. Starte erneut.");
      }
      if(have>0){
       string range=response.Headers[HttpResponseHeader.ContentRange]??"";
       if(!range.StartsWith("bytes "+have+"-",StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Downloadserver meldet falschen Dateiabschnitt.");
      }
      using(var input=response.GetResponseStream())
      using(var dest=new FileStream(file,FileMode.OpenOrCreate,FileAccess.Write,FileShare.None,1024*1024)){
       dest.Seek(have,SeekOrigin.Begin);
       byte[] buffer=new byte[1024*1024];
       int count,last=-1;long total=have;
       while((count=input.Read(buffer,0,buffer.Length))>0){
        dest.Write(buffer,0,count);total+=count;
        if(total>r.Size)throw new InvalidDataException("Download groesser als die freigegebene Datei.");
        int pct=(int)(total*100L/r.Size);
        if(pct!=last){last=pct;progress(Math.Min(100,pct),"Herunterladen: "+SizeText(total)+" / "+SizeText(r.Size));}
       }
      }
     }
     if(new FileInfo(file).Length==r.Size){
      progress(100,"Download abgeschlossen.");
      return;
     }
     lastError=new IOException("Verbindung wurde vorzeitig beendet.");
    }catch(WebException ex){lastError=ex;}
     catch(IOException ex){lastError=ex;}
    if(attempt<8){
     progress((int)((File.Exists(file)?new FileInfo(file).Length:0)*100L/r.Size),
      "Verbindung unterbrochen. Setze Download fort (Versuch "+(attempt+1)+"/8) ...");
     Thread.Sleep(Math.Min(12000,2000*attempt));
    }
   }
   throw new IOException("Download konnte nicht abgeschlossen werden. Bereits geladene Daten bleiben erhalten.",lastError);
  }
  internal static void Verify(Release r,string file){
   if(new FileInfo(file).Length!=r.Size)throw new InvalidDataException("Falsche Archivgroesse.");
   using(var sha=SHA256.Create())
   using(var stream=File.OpenRead(file)){
    byte[] digest=sha.ComputeHash(stream);
    StringBuilder b=new StringBuilder();
    foreach(byte val in digest)b.Append(val.ToString("x2"));
    if(!string.Equals(b.ToString(),r.Hash,StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("SHA-256-Pruefsumme falsch: Download verworfen.");
   }
  }
  internal static void Extract(string zip,string target,Action<int,string> progress){
   string root=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   long extracted=0;
   using(var archive=ZipFile.OpenRead(zip)){
    if(archive.Entries.Count>10000)throw new InvalidDataException("Zu viele Dateien.");
    for(int i=0;i<archive.Entries.Count;i++){
     var entry=archive.Entries[i];
     string name=entry.FullName.Replace('/','\\');
     if(!name.StartsWith("Windows\\",StringComparison.OrdinalIgnoreCase) ||
        name.IndexOf(':')>=0||name.IndexOf('\0')>=0)
      throw new InvalidDataException("Unzulaessiger Archivpfad.");
     string full=Path.GetFullPath(Path.Combine(root,name));
     if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("Pfad verlässt Installationsordner.");
     if(name.EndsWith("\\",StringComparison.Ordinal)){Directory.CreateDirectory(full);continue;}
     extracted+=entry.Length;
     if(extracted>15L*1024*1024*1024)throw new InvalidDataException("Updatearchiv ist zu gross.");
     Directory.CreateDirectory(Path.GetDirectoryName(full));
     using(var input=entry.Open())
     using(var output=new FileStream(full,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024))
      input.CopyTo(output);
     if(i%3==0||i==archive.Entries.Count-1)
      progress((int)((i+1)*100.0/archive.Entries.Count),"Entpacken: "+(i+1)+" von "+archive.Entries.Count);
    }
   }
   if(!File.Exists(Path.Combine(root,"Windows","RetailGame.exe")))
    throw new InvalidDataException("RetailGame.exe fehlt.");
  }
  internal static void CopyDirectory(string from,string to){
   if(!Directory.Exists(from))return;
   Directory.CreateDirectory(to);
   foreach(string file in Directory.GetFiles(from))File.Copy(file,Path.Combine(to,Path.GetFileName(file)),true);
   foreach(string sub in Directory.GetDirectories(from))
    CopyDirectory(sub,Path.Combine(to,Path.GetFileName(sub)));
  }
  internal static void Install(Release r,string zip,Action<int,string> progress){
   Directory.CreateDirectory(Home);
   string staged=Game+".updating";
   string backup=Game+".previous";
   if(!Directory.Exists(Game)&&Directory.Exists(backup))Directory.Move(backup,Game);
   if(Directory.Exists(staged))Directory.Delete(staged,true);
   Extract(zip,staged,progress);
   if(Directory.Exists(Game)){
    foreach(string relative in new[]{@"Windows\RetailGame\Saved",@"Windows\Saved"})
     CopyDirectory(Path.Combine(Game,relative),Path.Combine(staged,relative));
   }
   if(Directory.Exists(backup))Directory.Delete(backup,true);
   bool moved=false;
   try{
    if(Directory.Exists(Game)){Directory.Move(Game,backup);moved=true;}
    Directory.Move(staged,Game);
    File.WriteAllText(State,r.Version,Encoding.UTF8);
   }catch{
    if(moved){
     if(Directory.Exists(Game))Directory.Delete(Game,true);
     if(Directory.Exists(backup))Directory.Move(backup,Game);
    }
    throw;
   }
   // Previous version retained until next update for recovery.
  }
 }
 internal sealed class Launcher:Form {
  private Label title,sub,status,version,note;
  private Button play;
  private ProgressBar progress;
  private volatile bool busy;
  internal Launcher(){
   Text="Portloria & Co. – Testlauncher";
   ClientSize=new Size(690,367);
   MinimumSize=MaximumSize=Size;
   FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
   StartPosition=FormStartPosition.CenterScreen;
   BackColor=Color.FromArgb(18,20,33);ForeColor=Color.White;
   Font=new Font("Segoe UI",10);
   title=new Label{Text="PORTLORIA & CO.",AutoSize=true,Font=new Font("Segoe UI",26,FontStyle.Bold),Location=new Point(36,30)};
   sub=new Label{Text="BUILD YOUR BUSINESS. TOGETHER.",AutoSize=true,ForeColor=Color.FromArgb(186,163,243),Location=new Point(39,83)};
   version=new Label{Text="Installiert: "+Engine.LocalVersion()+" · Testkanal",AutoSize=true,Location=new Point(40,121),ForeColor=Color.FromArgb(180,192,209)};
   status=new Label{Text="Version wird geprueft ...",Size=new Size(610,46),Location=new Point(40,155)};
   progress=new ProgressBar{Location=new Point(40,210),Size=new Size(606,20)};
   note=new Label{Text="Automatische Versionspruefung · SHA-256-Verifikation · Spielstaende erhalten",Location=new Point(40,244),Size=new Size(610,35),ForeColor=Color.FromArgb(161,171,191)};
   play=new Button{Text="Bitte warten ...",Location=new Point(40,290),Size=new Size(606,44),Enabled=false,
    BackColor=Color.FromArgb(131,85,215),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
   play.FlatAppearance.BorderSize=0;
   play.Click+=delegate{if(!Engine.IsGameInstalled()) StartCheck();else StartGame();};
   Controls.AddRange(new Control[]{title,sub,version,status,progress,note,play});
   Shown+=delegate{StartCheck();};
  }
  private void UI(Action a){if(!IsDisposed&&IsHandleCreated)BeginInvoke(a);}
  private void UpdateProgress(string message,int pct){
   UI(delegate{if(busy){status.Text=message;progress.Value=Math.Max(0,Math.Min(100,pct));}});
  }
  private void StartCheck(){
   if(busy)return;busy=true;play.Enabled=false;
   Task.Run(delegate{
    try{
     UpdateProgress("Pruefe Online-Version ...",1);
     Release latest=Engine.Latest();
     bool required=!Engine.IsGameInstalled()||Engine.Newer(latest.Version,Engine.LocalVersion());
     if(required){
      if(!Engine.IsGameInstalled()&&!File.Exists(Engine.InstallLocationFile)){
       bool selected=false;
       Invoke(new Action(delegate{
        using(var dlg=new FolderBrowserDialog()){
         dlg.Description="Waehle das Laufwerk und den Ordner fuer Portloria & Co. (ca. 6 GB frei benoetigt).";
         dlg.ShowNewFolderButton=true;
         dlg.SelectedPath=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
         if(dlg.ShowDialog(this)==DialogResult.OK){
          Engine.ChooseInstallFolder(dlg.SelectedPath);selected=true;
         }
        }
       }));
       if(!selected)throw new OperationCanceledException("Installation abgebrochen.");
      }
      Engine.RequireSpace(latest.Size);
      if(Process.GetProcessesByName("RetailGame").Length>0)
       throw new InvalidOperationException("Bitte laufendes Portloria zuerst beenden.");
      Directory.CreateDirectory(Path.GetDirectoryName(Engine.Game));
      string archive=Engine.Game+".v"+latest.Version+".download.partial";
      UpdateProgress("Spielversion "+latest.Version+" wird geladen ...",3);
      Engine.Download(latest,archive,delegate(int p,string m){UpdateProgress(m,3+(int)(p*.73));});
      UpdateProgress("Pruefe SHA-256 ...",77);
      try{Engine.Verify(latest,archive);}
      catch(InvalidDataException){
       try{if(File.Exists(archive))File.Delete(archive);}catch{}
       throw;
      }
      UpdateProgress("Installiere Portloria ...",80);
      Engine.Install(latest,archive,delegate(int p,string m){UpdateProgress(m,80+(int)(p*.2));});
      try{if(File.Exists(archive))File.Delete(archive);}catch{}
     }
     UI(delegate{
      busy=false;version.Text="Installiert: "+Engine.LocalVersion()+" · Testkanal";
      status.Text=required?"Version "+latest.Version+" installiert.":"Portloria ist aktuell.";
      progress.Value=100;play.Text="SPIELEN";play.Enabled=Engine.IsGameInstalled();
     });
    }catch(Exception ex){
     try{
      Directory.CreateDirectory(Engine.Home);
      File.AppendAllText(Path.Combine(Engine.Home,"launcher-errors.log"),
       DateTime.UtcNow.ToString("o")+" "+ex.GetType().Name+": "+ex.Message+Environment.NewLine);
     }catch{}
     UI(delegate{
      busy=false;status.Text="Update nicht moeglich: "+ex.Message;
      play.Text=Engine.IsGameInstalled()?"OFFLINE SPIELEN":"ERNEUT VERSUCHEN";
      play.Enabled=true;
     });
    }
   });
  }
  private void StartGame(){
   try{
    Process.Start(new ProcessStartInfo{FileName=Engine.Exe,WorkingDirectory=Path.GetDirectoryName(Engine.Exe),UseShellExecute=true});
    Close();
   }catch(Exception ex){MessageBox.Show(this,ex.Message,"Portloria",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
 internal static class Program{
  [STAThread] static int Main(string[] args){
#if TEST_ONLY
   if(args.Length>0&&args[0]=="--selftest")return SelfTests.Run();
#endif
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   Application.Run(new Launcher());
   return 0;
  }
 }
}
