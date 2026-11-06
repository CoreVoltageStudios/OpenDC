using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public enum ArtworkStyle { Classic, Crisp }
public static class ArtworkPreference {
 public static ArtworkStyle Load(string path,out string? diagnostic) {
  diagnostic=null;if(!File.Exists(path))return ArtworkStyle.Classic;
  try {using var doc=JsonDocument.Parse(File.ReadAllText(path));if(!doc.RootElement.TryGetProperty("worldArtwork",out var value))return ArtworkStyle.Classic;return value.GetString() switch {"Classic"=>ArtworkStyle.Classic,"Crisp"=>ArtworkStyle.Crisp,_=>throw new FormatException("Unknown artwork preference")};}
  catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException){diagnostic="Artwork preference could not be read; using Classic.";return ArtworkStyle.Classic;}
 }
 public static string? Save(string path,ArtworkStyle style) {
  return Update(path,node=>node["worldArtwork"]=style.ToString());
 }
 public static (bool Enabled,float Speed) LoadNavigation(string path,out string? diagnostic){
  diagnostic=null;if(!File.Exists(path))return (true,1);
  try{using var doc=JsonDocument.Parse(File.ReadAllText(path));var node=doc.RootElement;
   bool enabled=!node.TryGetProperty("edgeScrolling",out var e)||e.GetBoolean();
   float speed=node.TryGetProperty("edgeScrollSpeed",out var s)?s.GetSingle():1;
   if(!float.IsFinite(speed))throw new FormatException();return(enabled,Math.Clamp(speed,.25f,2));
  }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException){diagnostic="Camera settings could not be read; using default edge scrolling.";return(true,1);}
 }
 public static string? SaveNavigation(string path,bool enabled,float speed)=>Update(path,node=>{node["edgeScrolling"]=enabled;node["edgeScrollSpeed"]=Math.Clamp(speed,.25f,2);});
 public static bool LoadFast(string path){
  try{using var d=JsonDocument.Parse(File.ReadAllText(path));return !d.RootElement.TryGetProperty("gameSpeed",out var v)||v.GetString()!="Classic";}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){return true;}
 }
 public static float LoadEffectsVolume(string path){
  try{using var d=JsonDocument.Parse(File.ReadAllText(path));return d.RootElement.TryGetProperty("effectsVolume",out var v)&&v.TryGetSingle(out float f)&&float.IsFinite(f)?Math.Clamp(f,0,1):1;}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){return 1;}
 }
 public static string? SavePresentation(string path,float scale,float effects,float music)=>Update(path,n=>{n["interfaceScale"]=scale;n["effectsVolume"]=effects;n["musicVolume"]=music;});
 public static string? SaveFast(string path,bool fast)=>Update(path,n=>n["gameSpeed"]=fast?"Fast":"Classic");
 // Legacy diagnostic compatibility only: old video preferences are ignored.
 public static bool LoadEnhancedVideo(string path)=>false;
 public static string? SaveEnhancedVideo(string path,bool enhanced)=>Update(path,n=>n["video"]=enhanced?"Enhanced":"Original");
 // Unreadable or non-object settings are never rewritten in place: their exact bytes are
 // copied beside the file (player-settings.malformed-<UTC>.json) before a fresh object is
 // written, so later changes persist again. Returns that preserved copy, if one was made.
 // Unrelated keys of a readable file survive. Storage failures still throw IOException.
 public static string LoadMultiplayerName(string path){
  try{using var d=JsonDocument.Parse(File.ReadAllText(path));return d.RootElement.TryGetProperty("multiplayerDisplayName",out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){return "";}
 }
 public static string? SaveMultiplayerName(string path,string name)=>Update(path,n=>n["multiplayerDisplayName"]=name);
 public static string? SaveReducedMotion(string path,bool reduced)=>Update(path,n=>n["reducedMotion"]=reduced);
 public static string? SaveAspect(string path,int aspect)=>Update(path,n=>n["maximumAspect"]=aspect);
 static string? Update(string path,Action<JsonObject> update){
  JsonObject node=new();string? preserved=null;
  if(File.Exists(path)){
   JsonObject? existing=null;try{existing=JsonNode.Parse(File.ReadAllText(path)) as JsonObject;}catch(JsonException){}
   if(existing!=null)node=existing;else{preserved=PreservedPath(path);File.Copy(path,preserved);}
  }
  node["version"]=2;update(node);
  DarkColony.Original.SaveCodec.AtomicWrite(path,node.ToJsonString());
  return preserved;
 }
 static string PreservedPath(string path){
  string full=Path.GetFullPath(path),stem=Path.Combine(Path.GetDirectoryName(full)!,Path.GetFileNameWithoutExtension(full)+".malformed-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture));
  for(int n=0;;n++){string candidate=stem+(n==0?"":"-"+n)+Path.GetExtension(full);if(!File.Exists(candidate))return candidate;}
 }
}
