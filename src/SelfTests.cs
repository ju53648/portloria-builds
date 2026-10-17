using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PortloriaBeta {
 internal static class SelfTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("TEST FAILED: "+message);Console.WriteLine("PASS "+message);}
  static void Put(ZipArchive archive,string name,string content){
   var entry=archive.CreateEntry(name);
   using(var stream=entry.Open())
   using(var writer=new StreamWriter(stream,Encoding.UTF8))writer.Write(content);
  }
  internal static int Run(){
   string root=Path.Combine(Path.GetTempPath(),"PortloriaLauncherQA-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(root);
   Environment.SetEnvironmentVariable("PORTLORIA_TEST_HOME",Path.Combine(root,"Install"));
   try{
    string zip1=Path.Combine(root,"v1.zip");
    using(var z=ZipFile.Open(zip1,ZipArchiveMode.Create)){
     Put(z,"Windows/RetailGame.exe","test binary version 1");
     Put(z,"Windows/RetailGame/Content/Paks/data.txt","test data version 1");
    }
    Release r=new Release{Version="0.1.0",Size=new FileInfo(zip1).Length};
    using(var sha=SHA256.Create())
    using(var fs=File.OpenRead(zip1))
     r.Hash=BitConverter.ToString(sha.ComputeHash(fs)).Replace("-","").ToLowerInvariant();
    Engine.Verify(r,zip1);
    Engine.Install(r,zip1,delegate(int progress,string status){});
    Assert(File.Exists(Engine.Exe),"Initial installation is complete");
    Assert(Engine.LocalVersion()=="0.1.0","Initial version recorded");
    Assert(Engine.Newer("0.1.1","0.1.0"),"Newer version detected");
    Assert(!Engine.Newer("0.1.0","0.1.1"),"Downgrade rejected");
    string saves=Path.Combine(Engine.Game,@"Windows\RetailGame\Saved");
    Directory.CreateDirectory(saves);
    File.WriteAllText(Path.Combine(saves,"world.sav"),"original user progress");
    string zip2=Path.Combine(root,"v2.zip");
    using(var z=ZipFile.Open(zip2,ZipArchiveMode.Create)){
     Put(z,"Windows/RetailGame.exe","test binary version 2");
     Put(z,"Windows/RetailGame/Content/Paks/data.txt","test data version 2");
    }
    r.Version="0.1.1";r.Size=new FileInfo(zip2).Length;
    using(var sha=SHA256.Create())
    using(var fs=File.OpenRead(zip2))
     r.Hash=BitConverter.ToString(sha.ComputeHash(fs)).Replace("-","").ToLowerInvariant();
    Engine.Verify(r,zip2);
    Engine.Install(r,zip2,delegate(int progress,string status){});
    Assert(Engine.LocalVersion()=="0.1.1","Update version recorded");
    Assert(File.ReadAllText(Path.Combine(saves,"world.sav"))=="original user progress","User save survived update");
    Assert(File.ReadAllText(Engine.Exe).Contains("version 2"),"New game data replaced old data");
    string broken=Path.Combine(root,"malicious.zip");
    using(var z=ZipFile.Open(broken,ZipArchiveMode.Create)){Put(z,"Windows/../../outside.txt","BAD");}
    try{Engine.Extract(broken,Path.Combine(root,"MaliciousInstall"),delegate(int p,string message){});Assert(false,"Zip traversal rejected");}
    catch(InvalidDataException){Console.WriteLine("PASS Zip traversal rejected");}
    r.Hash=new string('0',64);
    try{Engine.Verify(r,zip2);Assert(false,"Wrong checksum rejected");}
    catch(InvalidDataException){Console.WriteLine("PASS Wrong checksum rejected");}
    Assert(!File.Exists(Path.Combine(root,"outside.txt")),"Extraction never wrote outside install folder");
    Console.WriteLine("ALL SELFTESTS PASSED");
    return 0;
   }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
   finally{try{Directory.Delete(root,true);}catch{}}
  }
 }
}
