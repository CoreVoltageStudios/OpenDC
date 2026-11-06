using Godot;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
// Godot-dispatched reproduction of malformed player settings through ordinary Surface/Relay
// controls and the [ ] UI-scale shortcuts. The caller prepares --artwork-preferences.
public partial class Viewer {
 async Task SettingsStep(string name,Func<Task> action){
  GD.Print("SETTINGS_STEP "+name);await action();
  for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Check(failureLogger.FirstError==null&&missionFault==null&&missionFaultCount==0,name+": no engine error or mission halt");
 }
 string[] PreservedSettings()=>Directory.GetFiles(Path.GetDirectoryName(artworkPreferencePath)!,Path.GetFileNameWithoutExtension(artworkPreferencePath)+".malformed-*"+Path.GetExtension(artworkPreferencePath));
 JsonObject StoredSettings()=>JsonNode.Parse(File.ReadAllText(artworkPreferencePath)) as JsonObject??throw new InvalidDataException("Settings are not an object");
 async Task VerifyMalformedSettings(){
  byte[] malformed=File.ReadAllBytes(artworkPreferencePath);
  Check(PreservedSettings().Length==0&&artworkPreferenceDiagnostic!=null,"Malformed fixture loads defaults with a diagnostic");
  Check(fastGame&&!enhancedVideo&&nativeSampling==1,"Malformed read path recovers usable defaults");
  Check(File.ReadAllBytes(artworkPreferencePath).SequenceEqual(malformed),"Reading does not rewrite malformed settings");
  await SettingsStep("open options",()=>UiClick("options"));await MenuCapture("settings-malformed-options");
  float before=ui;await SettingsStep("interface scale +",()=>UiClick("ui+"));
  Check(ui>before,"Interface scale change applies");
  var preserved=PreservedSettings();Check(preserved.Length==1&&File.ReadAllBytes(preserved[0]).SequenceEqual(malformed),"Malformed input preserved byte-for-byte for diagnosis");
  Check(Math.Abs(StoredSettings()["interfaceScale"]!.GetValue<float>()-ui)<.001f,"Recovered settings file stores the new scale");
  await MenuCapture("settings-malformed-recovered");
  foreach(string id in new[]{"speed-classic","art-crisp","edge-toggle","edge+","sfx-","music-"})await SettingsStep(id,()=>UiClick(id));
  var stored=StoredSettings();
  Check(stored["gameSpeed"]!.GetValue<string>()=="Classic"&&stored["video"]!.GetValue<string>()=="Original"&&stored["worldArtwork"]!.GetValue<string>()=="Crisp"&&!stored["edgeScrolling"]!.GetValue<bool>(),"Speed, video, artwork and edge settings persist after recovery");
  Check(Math.Abs(stored["effectsVolume"]!.GetValue<float>()-sfxVolume)<.001f&&Math.Abs(stored["musicVolume"]!.GetValue<float>()-musicVolume)<.001f&&Math.Abs(stored["edgeScrollSpeed"]!.GetValue<float>()-edgeSpeed)<.001f,"Volumes and edge speed persist after recovery");
  await SettingsStep("close options",()=>UiClick("close"));
  before=ui;await SettingsStep("main menu ] shortcut",()=>RelayKey(Key.Bracketright));await SettingsStep("main menu [ shortcut",()=>RelayKey(Key.Bracketleft));await SettingsStep("main menu [ shortcut again",()=>RelayKey(Key.Bracketleft));
  Check(ui<before&&Math.Abs(StoredSettings()["interfaceScale"]!.GetValue<float>()-ui)<.001f,"Main-menu UI-scale shortcuts apply and persist");
  Check(PreservedSettings().Length==1,"Repeated saves keep one preserved copy, no repeated recovery");
  await SettingsStep("campaign",()=>UiClick("campaign"));await SettingsStep("start First Strike",()=>UiClick("campaign-start:0"));await SettingsStep("begin",()=>Send(Key.Enter));
  Check(!mainMenu&&!missionBriefing&&mission!=null,"Mission entered after settings recovery");
  before=ui;await SettingsStep("mission ] shortcut",()=>RelayKey(Key.Bracketright));Check(ui>before&&Math.Abs(StoredSettings()["interfaceScale"]!.GetValue<float>()-ui)<.001f,"Mission UI-scale shortcut applies and persists");
  await MenuCapture("settings-malformed-mission");
  File.WriteAllText(Path.Combine(output,"settings-final.json"),File.ReadAllText(artworkPreferencePath));
 }
 // Pre-fix reproduction of the in-mission shortcut path without touching Options first.
 async Task VerifyMalformedSettingShortcuts(){
  byte[] malformed=File.ReadAllBytes(artworkPreferencePath);
  await SettingsStep("campaign",()=>UiClick("campaign"));await SettingsStep("start First Strike",()=>UiClick("campaign-start:0"));await SettingsStep("begin",()=>Send(Key.Enter));
  long tick=nativeWorld.Tick;missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);
  float before=ui;await SettingsStep("mission ] shortcut",()=>RelayKey(Key.Bracketright));Check(ui>before,"] increases interface scale in mission");
  await SettingsStep("mission [ shortcut",()=>RelayKey(Key.Bracketleft));
  await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;
  Check(nativeWorld.Tick>tick&&missionFault==null,"Mission keeps advancing after UI-scale shortcuts");
  Check(Math.Abs(ui-before)<.001f&&Math.Abs(StoredSettings()["interfaceScale"]!.GetValue<float>()-ui)<.001f,"[ restores the scale and the stored value follows");
  var preserved=PreservedSettings();Check(preserved.Length==1&&File.ReadAllBytes(preserved[0]).SequenceEqual(malformed),"Malformed input preserved byte-for-byte");
  await MenuCapture("settings-shortcuts-mission");
 }
 // Fresh process over the recovered file: valid settings load normally, no diagnostic.
 async Task VerifyRecoveredSettingsReload(){
  var stored=StoredSettings();
  Check(artworkPreferenceDiagnostic==null,"Recovered settings load without diagnostic");
  Check(!fastGame&&!enhancedVideo&&nativeSampling==0&&!edgeScrolling,"Fresh process restores speed, video, artwork and edge settings");
  Check(Math.Abs(stored["interfaceScale"]!.GetValue<float>()-ui)<.001f&&Math.Abs(stored["effectsVolume"]!.GetValue<float>()-sfxVolume)<.001f&&Math.Abs(stored["musicVolume"]!.GetValue<float>()-musicVolume)<.001f&&Math.Abs(stored["edgeScrollSpeed"]!.GetValue<float>()-edgeSpeed)<.001f,"Fresh process restores scale, volumes and edge speed");
  await SettingsStep("open options",()=>UiClick("options"));await SettingsStep("speed-fast",()=>UiClick("speed-fast"));
  Check(StoredSettings()["gameSpeed"]!.GetValue<string>()=="Fast"&&PreservedSettings().Length==1,"Valid settings keep saving normally without new recovery copies");
  await MenuCapture("settings-reload-options");
 }
}
