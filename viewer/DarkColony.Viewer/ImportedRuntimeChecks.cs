using Godot;
using DarkColony.Original;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyImportedRuntime(){
  Check(File.Exists(Path.Combine(root,"content-manifest.json")),"Imported root required; no development fallback");
  missionHarnessManual=true;var records=new List<object>();
  foreach(string edition in missionSuite=="imported-content-return"?Array.Empty<string>():new[]{"base","council","training"})foreach(string faction in new[]{"human","taar"}){
   await SessionClick("campaign");await SessionClick("edition:"+edition);
   if(edition=="council"&&!councilAvailable){await MenuCapture("optional-council-unavailable");ReturnMainMenu();continue;}
   await SessionClick("faction:"+faction);await MenuCapture(edition+"-"+faction+"-selection");await SessionClick("campaign-start:0");Check(missionBriefing&&!nativeWorld.GodMode,"Normal first-mission entry "+edition+faction);
   await ToSignal(GetTree().CreateTimer(1.2),SceneTreeTimer.SignalName.Timeout);await MissionCapture(edition+"-"+faction+"-revealing");await Send(Key.Space);Check(missionBriefing&&!BriefingRevealing&&BriefingTransmissionComplete,"Reveal all does not enter battle");await MissionCapture(edition+"-"+faction+"-briefing");await Send(Key.Enter);AdvanceInterference(320);await MissionCapture(edition+"-"+faction+"-gameplay");
   SaveMissionSlot(0);var saved=mission.Snapshot();records.Add(new{edition,faction,tick=nativeWorld.Tick,voices=missionSoundPaths.Count,music=missionMusic!=null});ReturnMainMenu();await SessionClick("resume-save");Check(JsonNode.DeepEquals(saved,mission.Snapshot()),"Imported Continue restores exact state");ResetFirstStrike();Check(missionBriefing&&nativeWorld.Tick==0,"Imported restart");ReturnMainMenu();
  }
  OpenMissionPanel("options");await SessionClick("game-content");Check(contentSetup!=null&&!ready,"Game content reachable from main-menu Options");await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);using(var im=GetViewport().GetTexture().GetImage())im.SavePng(Path.Combine(output,"options-game-content.png"));OpenImportedMenu();ulong wait=Time.GetTicksMsec();while(!ready&&Time.GetTicksMsec()-wait<60000)await WarFrames(1);Check(mainMenu&&ready&&contentSetup==null,"Return from content does not replay studio intro: "+(contentSetup==null?"closed":contentStatus.Text));
  File.WriteAllText(Path.Combine(output,"imported-runtime.json"),System.Text.Json.JsonSerializer.Serialize(records));GD.Print("IMPORTED_RUNTIME_PASS");
 }
}
