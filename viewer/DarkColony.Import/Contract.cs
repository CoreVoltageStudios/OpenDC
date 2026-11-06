using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace DarkColony.ContentImport;

public sealed record RequiredFile(string Path,long Bytes,string[] Hashes,string Origin,string Role);
public sealed record ContentContract(int Revision,string Language,string BaseEdition,string CouncilEdition,string BaseNative,string CouncilNative,Dictionary<string,string> Converters,string[] Fallbacks,string[] Optional,RequiredFile[] BaseFiles,RequiredFile[] CouncilFiles) {
 public static ContentContract Current {get;}=JsonSerializer.Deserialize<ContentContract>(Resource("content-contract.json"))!;
 internal static byte[] Resource(string suffix){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(n=>n.EndsWith(suffix,StringComparison.Ordinal)))!;using var m=new MemoryStream();s.CopyTo(m);return m.ToArray();}
 public static string Hash(string path){using var s=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(s));}
}
public enum ImportStage { LookingForSource,AwaitingConfirmation,Inspecting,Extracting,Copying,Converting,Validating,Complete,Cancelled,Failed }
public sealed record ImportProgress(ImportStage Stage,string Message,long? Completed=null,long? Total=null,string? Detail=null) {
 [System.Text.Json.Serialization.JsonIgnore] public PresentationSnapshot? Presentation {get;init;}
}
public sealed record SourceFinding(string Path,string Kind,string? Edition,string[] Missing,string[] Warnings,bool Compatible);
internal static class SafePath {
 public static string Within(string root,string relative){
  if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':')||relative.Split('/','\\').Any(p=>p is ".." or "."))throw new InvalidDataException("Unsafe source path");
  string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));if(!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Path escapes content directory");return full;
 }
 public static void NoLinks(string path){if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked files are not supported: "+path);for(var d=new DirectoryInfo(Path.GetFullPath(path));d!=null;d=d.Parent)if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked destinations are not supported: "+d.FullName);}
 public static void DeleteOwnedStage(string path){NoLinks(path);if(!Guid.TryParseExact(Path.GetFileName(path),"N",out _)||!File.Exists(Path.Combine(path,".odc-import-owned")))throw new IOException("Not an importer-owned staging directory");var pending=new Stack<string>();pending.Push(path);while(pending.Count>0)foreach(var f in Directory.EnumerateFileSystemEntries(pending.Pop())){var a=File.GetAttributes(f);if((a&FileAttributes.ReparsePoint)!=0)throw new IOException("Refusing to clean linked staging content");if((a&FileAttributes.Directory)!=0)pending.Push(f);}Directory.Delete(path,true);}
}
