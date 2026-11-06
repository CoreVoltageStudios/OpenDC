using Godot;
using DarkColony.Original;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyMenuPause(){
  var evidence=new List<object>();
  foreach(string scenario in new[]{"SCENARIO/ALIEN/ALIEN02.SCN","SCENARIO/HUMAN/HUMAN05.SCN","SCENARIO/AEROGEN/AERO01.SCN","SCENARIO/COUNCIL/COUN01.SCN","SCENARIO/TEST/HTRAIN3.SCN","SCENARIO/TEST/ATRAIN7.SCN","WAR"}){
   var next=scenario=="WAR"?OriginalMission.StartWar(Path.Combine(root,"private/DC"),WarConfiguration.Default("D4PLAY04",7)):new OriginalMission(PersistenceData(scenario),7);var data=next.World.Data;InstallMission(data,next,false);missionHarnessManual=true;AdvanceInterference(scenario.Contains("HTRAIN3")?1400:320);missionHarnessManual=false;ProcessFirstStrike(0);OpenMissionPanel("menu");ProcessFirstStrike(0);
   var before=mission.Snapshot();long tick=nativeWorld.Tick;
   foreach(string panel in new[]{"menu","options","playtesting","controls","objectives","history","save","load","overwrite","abandon"}){
    GD.Print("PAUSE_PANEL "+scenario+" "+panel);OpenMissionPanel(panel);for(int i=0;i<300;i++)ProcessFirstStrike(.1);await WarFrames(1);
    Check(nativeWorld.Tick==tick&&JsonNode.DeepEquals(before,mission.Snapshot()),"No authoritative change through 30s menu "+panel+scenario);
   }
   await NativeMouse(MouseButton.Right,NativeArea.GetCenter());Check(JsonNode.DeepEquals(before,mission.Snapshot()),"Menu rejects world pointer commands");
   if(IsWar){OpenMissionPanel("diplomacy");ActivateMissionUi("war-alliance:1");Check(JsonNode.DeepEquals(before,mission.Snapshot()),"Diplomacy stages changes until resume");ActivateMissionUi("war-alliance:1");pendingDiplomacy.Clear();}
   await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);Check(JsonNode.DeepEquals(before,mission.Snapshot()),"Actual rendered clock pause");
   OwnLocalPause(LocalPauseReason.Fault,true);CloseRelayPanel();Check(SinglePlayerPaused,"Closing menu does not release another owner");OwnLocalPause(LocalPauseReason.Fault,false);
   OpenMissionPanel("save");SaveMissionSlot(0);Check(JsonNode.DeepEquals(before,mission.Snapshot()),"Paused save unchanged");CloseRelayPanel();ProcessFirstStrike(30);Check(nativeWorld.Tick==tick,"Resume admits no accumulated time");
   GD.Print("PAUSE_RESUME "+scenario);var expected=OriginalMission.Restore(data,before);for(int i=0;i<30;i++)ProcessFirstStrike(.02);
   while(expected.World.Tick<nativeWorld.Tick){long previous=expected.World.Tick;expected.SetClock((expected.World.Tick+1)*66);expected.Step();Check(expected.World.Tick>previous,"Expected continuation advances "+scenario);}
   Check(JsonNode.DeepEquals(expected.Snapshot(),mission.Snapshot()),"Paused/resumed exact matching-tick continuation "+scenario);
   long after=nativeWorld.Tick;Check(after>tick&&after-tick<=14,"Normal speed without burst");
   LoadMissionSlot(0);ProcessFirstStrike(0);Check(!SinglePlayerPaused&&nativeWorld.Tick==tick,"Load excludes temporary UI pause");ResetFirstStrike();Check((IsWar||missionBriefing)&&nativeWorld.Tick==0,"Restart clears old reasons");if(!IsWar)StartFirstStrike();ProcessFirstStrike(0);Check(!SinglePlayerPaused,"Battle starts after retry");
   if(IsWar){OpenMissionPanel("diplomacy");bool old=nativeWorld.Teams[LocalTeam].Allies[1]!=0;ActivateMissionUi("war-alliance:1");var staged=mission.Snapshot();SaveMissionSlot(0);Check(JsonNode.DeepEquals(staged,mission.Snapshot()),"Staged diplomacy does not enter save");CloseRelayPanel();Check((nativeWorld.Teams[LocalTeam].Allies[1]!=0)!=old&&pendingDiplomacy.Count==0,"Staged diplomacy applies exactly on resume");}
   OpenMissionPanel("menu");await MissionCapture("paused-"+Path.GetFileNameWithoutExtension(scenario));evidence.Add(new{scenario,tick,after,panels=10,syntheticPausedSeconds=300,exact=true});ReturnMainMenu();
  }
  File.WriteAllText(Path.Combine(output,"menu-pause.json"),System.Text.Json.JsonSerializer.Serialize(evidence));missionHarnessManual=true;GD.Print("MENU_PAUSE_PASS");
 }
}
