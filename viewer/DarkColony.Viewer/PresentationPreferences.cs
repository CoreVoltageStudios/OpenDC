using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 void LoadPresentationPreferences(){
  LoadMotionPreference();
  try{
   var n=JsonNode.Parse(File.ReadAllText(artworkPreferencePath));
   float Read(string key,float fallback,float min,float max){try{float v=n?[key]?.GetValue<float>()??fallback;return float.IsFinite(v)?Math.Clamp(v,min,max):fallback;}catch(Exception e)when(e is InvalidOperationException or FormatException or OverflowException){return fallback;}}
   maximumAspect=(int)Read("maximumAspect",0,0,21);if(maximumAspect is not (16 or 21))maximumAspect=0;
   ui=Read("interfaceScale",ui,.75f,1.5f);sfxVolume=Read("effectsVolume",1,0,1);musicVolume=Read("musicVolume",1,0,1);
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException){}
 }
 void SavePresentationPreferences()=>SaveSettings(p=>ArtworkPreference.SavePresentation(p,ui,sfxVolume,musicVolume),"Presentation settings could not be saved. Existing settings were preserved.");
 string? settingsRecoveryNotice;
 // Every Options control and UI shortcut saves through this boundary. The change always
 // applies for the session; a storage/format problem becomes one visible diagnostic and
 // one log line, never an exception that halts the mission or quits the menu.
 void SaveSettings(Func<string,string?> save,string failure){
  try{
   if(save(artworkPreferencePath) is {} preserved){settingsRecoveryNotice="Unreadable settings were kept as "+Path.GetFileName(preserved)+"; new settings saved.";Godot.GD.Print("SETTINGS_RECOVERED "+preserved);}
   artworkPreferenceDiagnostic=settingsRecoveryNotice;
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException){
   if(artworkPreferenceDiagnostic!=failure)Godot.GD.Print("SETTINGS_SAVE_FAILED "+e.GetType().Name+": "+e.Message);
   artworkPreferenceDiagnostic=failure;
  }
 }
}
