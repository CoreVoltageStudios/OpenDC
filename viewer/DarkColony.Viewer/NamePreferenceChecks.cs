using Godot;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyNamePreference(){
  var args=OS.GetCmdlineUserArgs();string phase=args.First(a=>a.StartsWith("--name-phase="))[13..];
  string expected=phase=="fresh"?"":phase=="restart"?"Copilot":"Saved Commander";
  byte[]? before=File.Exists(artworkPreferencePath)?File.ReadAllBytes(artworkPreferencePath):null;
  await UiClick("multiplayer");await WarFrames();
  var name=multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().First();
  Check(name.Text==expected,"Display name comes only from OpenDC settings; fresh profile is blank");
  Check(before==null?!File.Exists(artworkPreferencePath):before.SequenceEqual(File.ReadAllBytes(artworkPreferencePath)),"Opening does not migrate or rewrite ambiguous settings");
  await MenuCapture("name-"+phase);
  if(phase=="fresh"){
   name.GrabFocus();foreach(char c in "Copilot"){
    Input.ParseInputEvent(new InputEventKey{Keycode=(Key)char.ToUpperInvariant(c),Unicode=c,Pressed=true});
    await WarFrames(1);Input.ParseInputEvent(new InputEventKey{Keycode=(Key)char.ToUpperInvariant(c),Unicode=c,Pressed=false});await WarFrames(1);
   }
   await WarControlClick(multiplayerRoot.FindChildren("*","Button",true,false).Cast<Button>().Single(b=>b.Text=="Host battle"));await WarFrames(5);
   Check(network?.Phase=="lobby"&&network.Players[0].Name=="Copilot","Chosen name validated and used by host");
   Check(ArtworkPreference.LoadMultiplayerName(artworkPreferencePath)=="Copilot","Chosen name saved");
   CloseNetwork();HideWarSetup();OpenMultiplayer();await WarFrames();
   Check(multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().First().Text=="Copilot","Reopening preserves chosen name");
  }
  File.WriteAllText(Path.Combine(output,"name-preference.json"),JsonSerializer.Serialize(new{phase,expected,stored=ArtworkPreference.LoadMultiplayerName(artworkPreferencePath)}));
 }
}
