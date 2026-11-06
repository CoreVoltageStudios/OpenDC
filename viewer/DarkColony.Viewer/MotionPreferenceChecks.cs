using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyMotionPreference(){
  string phase=OS.GetCmdlineUserArgs().First(a=>a.StartsWith("--motion-phase="))[15..];bool expected=phase=="enabled";
  Check(reducedMotion==expected,"Saved OpenDC motion preference is applied on arrival");
  await UiClick("options");await WarFrames(3);await MenuCapture("motion-"+phase);
  if(phase=="fresh"||phase=="enabled"){
   await UiClick("reduce-motion");Check(reducedMotion!=expected,"Options changes OpenDC motion preference");
   using var d=JsonDocument.Parse(File.ReadAllText(artworkPreferencePath));Check(d.RootElement.GetProperty("reducedMotion").GetBoolean()==!expected,"Explicit choice persists");
  }
  File.WriteAllText(Path.Combine(output,"motion-preference.json"),JsonSerializer.Serialize(new{phase,onArrival=expected,after=reducedMotion}));
 }
}
