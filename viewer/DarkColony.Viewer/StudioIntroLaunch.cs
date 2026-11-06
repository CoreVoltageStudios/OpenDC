using Godot;
using DarkColony.StudioIntro;
namespace DarkColony.Viewer;
// Hosts the reusable Core Voltage Studios intro before the main menu. The intro owns
// its drawing, sound, timing and skip input; ODC only supplies the effects volume and
// waits for it. Automated checks and --skip-intro go straight to the menu.
public partial class Viewer {
 CoreVoltageIntro? studioIntro;
 void BeginStudioIntro(){
  studioIntro=new CoreVoltageIntro{VolumeLinear=ArtworkPreference.LoadEffectsVolume(Path.Combine(PlayerDataRoot,"player-settings.json"))};
  try{studioIntro.KeepResidual=new DarkColony.ContentImport.ContentStore(ChosenContentStore()).ActiveRoot==null;}catch(IOException){studioIntro.KeepResidual=true;}
  AddChild(studioIntro);studioIntro.Begin();GD.Print("STUDIO_INTRO Core Voltage Studios");
 }
 async Task EndStudioIntro(){
  if(studioIntro==null)return;
  if(!studioIntro.Done){var done=new TaskCompletionSource();studioIntro.Finished+=()=>done.TrySetResult();await done.Task;}
  GD.Print(studioIntro.Skipped?"STUDIO_INTRO_SKIPPED":"STUDIO_INTRO_COMPLETE");
  studioIntro.QueueFree();studioIntro=null;
 }
}
