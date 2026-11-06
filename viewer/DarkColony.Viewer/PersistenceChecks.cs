using Godot;using System.Text.Json.Nodes;using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyPersistenceTemporal(){
  missionHarnessManual=true;
  var d=PersistenceData("SCENARIO/ALIEN/ALIEN02.SCN");var m=OriginalMission.Restore(d,SaveCodec.Open(Path.Combine(root,"generated/persistence/active-production/save.json"))["payload"]!.AsObject());InstallMission(d,m,false);
  camera=new(12*32,(d.Scenario.Map.Height-74)*32);zoom=2;ClampMissionCamera();nativeAccumulator=0;
  await UiCapture("temporal-before");var state=mission.Snapshot();SaveMissionSlot(0);LoadMissionSlot(0);Check(JsonNode.DeepEquals(state,mission.Snapshot()),"Loaded active economy state, FIN phases and production reservations exactly");await UiCapture("temporal-after");
  var trace=new List<object>();missionHarnessManual=false;ulong start=Time.GetTicksMsec();while(Time.GetTicksMsec()-start<4000){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);trace.Add(new{tick=nativeWorld.Tick,drawn=nativeDrawn.ToArray(),units=nativeWorld.Entities.Where(e=>e.Living&&NativeVisible(e)).Select(e=>new{e.Slot,e.Incarnation,e.X,e.Z,e.Height,body=e.Body.Set?.Name,e.Body.Frame,e.Body.Delay,overlay=e.Overlay.Set?.Name,overlayFrame=e.Overlay.Frame}).ToArray()});}
  missionHarnessManual=true;await UiCapture("temporal-continued");File.WriteAllText(Path.Combine(output,"temporal-trace.json"),System.Text.Json.JsonSerializer.Serialize(trace));
 }
 async Task VerifyPersistenceUi(bool fresh){
  missionHarnessManual=true;
  if(fresh){ReturnMainMenu();await UiClick("load");await MenuCapture("restore-fresh-slots");await UiClick("load-slot:0");Check(!missionBriefing&&nativeWorld.Tick>=300&&missionPanel=="","Fresh process loads active mission through main-menu Load Game");long tick=nativeWorld.Tick;missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;Check(nativeWorld.Tick>tick,"Restored mission continues in real time");await UiCapture("restore-continued");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("campaign");Check(CampaignProgress.Entry(CampaignPath,1)!=null,"Campaign progress survives fresh process");await MenuCapture("campaign-fresh");await UiClick("campaign-start:1");Check(economicMission&&missionBriefing&&campaignEntry?.Rank==1,"Persistent earned profile starts Interference");await UiCapture("campaign-interference");return;}
  AdvanceInterference(310);await SelectWholeSquad();await Send(Key.Escape);await UiClick("save");await UiCapture("save-slots-empty");
  var before=mission.Snapshot();await UiClick("save-slot:0");Check(File.Exists(SavePath(0))&&JsonNode.DeepEquals(before,SaveCodec.Open(SavePath(0))["payload"]!["mission"]),"Menu save captures coherent tick without world order");await UiCapture("saved-slot");
  await UiClick("save-slot:0");Check(missionPanel=="overwrite","Existing slot requires overwrite confirmation");await UiClick("save");Check(File.Exists(SavePath(0)),"Cancel keeps prior save");
  await UiClick("save-slot:0");await UiClick("overwrite-yes");Check(missionPanel=="save","Overwrite completes atomically");
  var original=File.ReadAllText(SavePath(0));File.WriteAllText(SavePath(1),"{broken");var active=mission;LoadMissionSlot(1);Check(ReferenceEquals(active,mission)&&File.ReadAllText(SavePath(0))==original,"Corrupt load keeps active mission and previous good slot");
  var snapshot=mission.Snapshot();using(var locked=new FileStream(SavePath(0),FileMode.Open,System.IO.FileAccess.Read,FileShare.None)){SaveMissionSlot(0);Check(ReferenceEquals(active,mission),"Failed replacement leaves active session intact");}Check(File.ReadAllText(SavePath(0))==original,"Failed write preserves previous file");
  missionPanel="";float scale=ui;bool speed=fastGame;int artwork=nativeSampling;await Send(Key.Escape);await UiClick("load");await UiClick("load-slot:0");Check(ui==scale&&fastGame==speed&&nativeSampling==artwork,"Loading preserves global scale/speed/artwork preferences");Check(nativeWorld.Tick==before["world"]!["tick"]!.GetValue<long>(),"Loading does not advance elapsed outside time");await UiCapture("restored-menu");
  // Controlled A01 outcome, separate from a complete mission playthrough.
  var d=PersistenceData("SCENARIO/ALIEN/ALIEN01.SCN");var win=new OriginalMission(d,0);win.World.Deaths[1,82]=11;win.World.NearbyCommanderKills[0]=10;for(int i=0;i<24;i++){win.SetClock((win.World.Tick+1)*66);win.Step();}win.SetClock(win.Outcome!.Deadline+1);
  Check(CampaignProgress.Complete(CampaignPath,win),"First completion records native result");string progress=File.ReadAllText(CampaignPath);Check(!CampaignProgress.Complete(CampaignPath,win)&&File.ReadAllText(CampaignPath)==progress,"Repeated outcome is idempotent");
  InstallMission(d,win,false);missionOutcomeShown=true;
  using(var locked=new FileStream(CampaignPath,FileMode.Open,System.IO.FileAccess.Read,FileShare.None)){
   RecordCampaign();Check(campaignWritePending,"Campaign storage failure is retained for retry");await Send(Key.Escape);Check(missionPanel=="abandon"&&!mainMenu,"Outcome Escape cannot discard unwritten progress silently");await UiClick("abandon-cancel");Check(missionPanel==""&&!mainMenu,"Cancel returns to outcome for progress retry");
   await Send(Key.Enter);Check(missionPanel=="abandon","Outcome replay key protects unwritten progress");await UiClick("abandon-cancel");ContinueCampaign();Check(ReferenceEquals(mission,win)&&campaignWritePending,"Campaign Continue retains result while storage is unavailable");
  }
  RecordCampaign();Check(!campaignWritePending&&File.ReadAllText(CampaignPath)==progress,"Campaign retry is idempotent after storage recovers");
  LoadMissionSlot(0);Check(File.ReadAllText(CampaignPath)==progress,"Older save does not roll back campaign record");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");await UiClick("campaign");await MenuCapture("campaign-recorded");
 }
}
