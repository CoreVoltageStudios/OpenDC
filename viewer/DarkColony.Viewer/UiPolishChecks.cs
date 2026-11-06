using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyUiPolish(){
  bool before=missionSuite=="ui-polish-before";float normal=GetViewportRect().Size.Y/1080;
  foreach(float scale in new[]{.75f,normal,1.5f}.Distinct()){
   ui=scale;await MenuCapture($"menu-{scale:0.00}");
   foreach(string panel in new[]{"campaign","load","options"}){OpenMissionPanel(panel);await MenuCapture($"{panel}-{scale:0.00}");if(!before)RelayLayout(panel+scale);await Send(Key.Escape);}
  }
  if(!before){
   ui=1.5f;OpenMissionPanel("load");surfaceSave=0;saveExists[0]=true;saveTitles[0]="A deliberately long saved mission title for presentation overflow verification";
   saveDetails[0]="Presentation fixture only; no saved game is changed.";
   await MenuCapture("load-long-title");RelayLayout("Long save title");RefreshPersistenceCatalog();await Send(Key.Escape);
  }
  foreach(int n in before?new[]{4}:new[]{1,2,3,4,5}){
   var data=PersistenceData($"SCENARIO/ALIEN/ALIEN{n:00}.SCN");InstallMission(data,new OriginalMission(data,0),true);
   string original=briefing;
   foreach(float scale in new[]{normal,1.5f,.75f}.Distinct()){
    ui=scale;briefingScroll=0;await MenuCapture($"brief-{n}-{scale:0.00}-top");if(!before)RelayLayout("Briefing "+n+"/"+scale);
    await Send(Key.Pagedown);await MenuCapture($"brief-{n}-{scale:0.00}-middle");
    for(int i=0;i<8;i++)await Send(Key.Pagedown);await MenuCapture($"brief-{n}-{scale:0.00}-bottom");
    Check(briefing==original,"Original briefing unchanged "+n+"/"+scale);
    await Send(Key.Pageup);if(missionHits.Any(h=>h.Id=="brief-up"&&h.Enabled)){await UiClick("brief-up");await UiClick("brief-down");}
    await NativeMouse(MouseButton.WheelDown,FrontRect.GetCenter()*ui);
   }
   ReturnMainMenu();
  }
  var first=PersistenceData("SCENARIO/ALIEN/ALIEN01.SCN");InstallMission(first,new OriginalMission(first,0),false);AdvanceInterference(250);
  foreach(float scale in new[]{normal,1.5f}){ui=scale;foreach(string panel in new[]{"menu","objectives","options","save","controls"}){OpenMissionPanel(panel);await MenuCapture($"mission-{panel}-{scale:0.00}");if(!before)RelayLayout(panel+scale);await Send(Key.Escape);}}
  // Outcome presentation fixture, explicitly not a played victory.
  missionPanel="";nativeWorld.Deaths[1,82]=11;nativeWorld.NearbyCommanderKills[0]=10;nativeWorld.Kills[0,73]=80;AdvanceInterference(24);mission.SetClock(mission.Outcome!.Deadline+1);
  await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);
  foreach(float scale in new[]{normal,1.5f}){ui=scale;await MenuCapture($"rewards-{scale:0.00}");if(!before)RelayLayout("Rewards "+scale);}
  if(!before){campaignWritePending=true;persistenceNotice="Campaign progress could not be written.";await MenuCapture("rewards-retry");RelayLayout("Reward retry");campaignWritePending=false;}
 }
}
