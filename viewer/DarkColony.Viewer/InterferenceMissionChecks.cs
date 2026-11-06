using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 DarkColony.Diagnostics.InterferenceRoute? interferenceRoute;
 void AdvanceInterference(int count){for(int i=0;i<count;i++){RememberNativePositions();mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();DispatchMissionSounds();}nativeAccumulator=0;}
 async Task VerifyInterferenceMission(){
  // Labelled targeted fixtures. None of the setup below is part of normal play.
  missionHarnessManual=true;AdvanceInterference(240);await MissionCapture("01-opening");
  Check(nativeWorld.Entities.Count(e=>e.Living&&e.Owner==0&&e.Type==8)==8,"A02 original opening renders eight total warriors");
  var miner=nativeWorld.Entities.Single(e=>e.Living&&e.Owner==0&&e.Type==14);
  await NativeMouse(MouseButton.Left,UnitHitRect(miner).GetCenter());await ClickNativeCell(MouseButton.Right,new(4,80));AdvanceInterference(300);
  Check(miner.Type==48,"Normal frontend orders harvest the original vent");
  nativeWorld.Teams[0].Money=12000; // Construction-state fixture, not natural economy proof.
  await UiClick("tab:0");await UiClick("product:16");missionHarnessManual=false;
  var record=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,record);record.SetRecordingActive(true);
  await ToSignal(GetTree().CreateTimer(12),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;
  Check(nativeWorld.Teams[0].Cities[3] is {InitializationPending:false,Health:>0},"Breeder construction completes in normal-clock rendered execution");
  await UiClick("product:17");missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(13),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;
  await MissionCapture("02-completed-hive");await UiClick("tab:1");await UiClick("product:25");AdvanceInterference(100);
  Check(nativeWorld.Events.Any(e=>e.Kind=="production-complete"&&e.Value==10),"Original-art Sy Demon button produces actual controllable unit");
  var produced=nativeWorld.Entities.First(e=>e.Type==10&&e.SourceIdentity.StartsWith("production/"));await NativeMouse(MouseButton.Left,UnitHitRect(produced).GetCenter());await ClickNativeCell(MouseButton.Right,new(18,65));AdvanceInterference(90);
  Check(produced.Cell.X>=16,"Produced unit selected and commanded through frontend input");
  await MissionCapture("03-forces");await UiClick("tab:2");await MissionCapture("04-upgrades");
  foreach(float scale in new[]{.75f,1f,1.25f,1.5f}){ui=scale;await MissionCapture("scale-"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));Check(missionHits.All(h=>GetViewportRect().Encloses(h.Rect)),"A02 hitboxes inside framebuffer at scale "+scale);}
  ui=GetViewportRect().Size.Y/1080;await UiClick("objectives");await MissionCapture("05-objectives");await Send(Key.Escape);await UiClick("menu");await UiClick("options");await MissionCapture("06-options");await Send(Key.Escape);
  record.SetRecordingActive(false);record.GetRecording().SaveToWav(Path.Combine(output,"construction-mixer.wav"));AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);
  await Send(Key.R);Check(missionBriefing&&nativeWorld.Tick==0&&nativeWorld.Teams[0].Strategy==null&&nativeWorld.Teams[0].Money==0,"Restart clears A02 economy, AI and script state");await Send(Key.Enter);
  nativeWorld.Deaths[1,86]=6;nativeWorld.NearbyCommanderKills[0]=10;AdvanceInterference(24);mission.SetClock(mission.Outcome!.Deadline+1);
  await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Visible:true}&&missionMusic is {Active:false},"A02 success starts AVHD5 and stops music");await MissionCapture("07-success-movie");
  await ToSignal(GetTree().CreateTimer(18),SceneTreeTimer.SignalName.Timeout);await MissionCapture("08-rewards");Check(rewardsVisible&&mission.ResultProfile is {CampaignIndex:2,Rank:1},"Success media ends in rewards and explicit next-index/rank state");
  var a02Data=nativeData;var a02Entry=campaignEntry;await UiClick("continue");Check(mission.CampaignIndex==2&&missionBriefing,"A03 campaign destination is available");await MissionCapture("09-legacy-revealed");InstallMission(a02Data,new OriginalMission(a02Data,0,a02Entry),true);await Send(Key.Enter);
  foreach(var c in nativeWorld.Teams[0].Cities.Take(5))if(c.Entity!=null)nativeWorld.Retire(c.Entity);AdvanceInterference(24);mission.SetClock(mission.Outcome!.Deadline+1);
  await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Visible:true}&&mission.Outcome is {Success:false,NextIndex:1},"All required organs lost selects A02 failure and retains mission");await MissionCapture("10-failure-movie");
  await ToSignal(GetTree().CreateTimer(10),SceneTreeTimer.SignalName.Timeout);await MissionCapture("11-failure");await Send(Key.R);await MissionCapture("12-restart");missionHarnessManual=false;
 }
 async Task VerifyCampaignTransition(){
  missionHarnessManual=true;nativeWorld.Deaths[1,82]=11;nativeWorld.NearbyCommanderKills[0]=10;AdvanceInterference(24);mission.SetClock(mission.Outcome!.Deadline+1);
  await ToSignal(GetTree().CreateTimer(.3),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);await UiClick("continue");
  Check(economicMission&&missionBriefing&&campaignEntry is {CampaignIndex:1,Rank:1},"First Strike result continues to original A02 briefing with rank1");await MissionCapture("campaign-a02-briefing");
  await Send(Key.Enter);AdvanceInterference(240);Check(nativeWorld.Entities.Any(e=>e.Living&&e.Owner==0&&e.Type==74),"A02 opening maps requested commander73 to carried rank1 type74");await MissionCapture("campaign-a02-opening");
  await Send(Key.R);Check(campaignEntry?.Rank==1&&nativeWorld.Tick==0,"Interference retry keeps entry profile but resets mission");missionHarnessManual=false;
 }
 async Task RunInterferenceMission(){
  // Exactly the normal fixed clock at supported Fast150%. Input is an explicit
  // diagnostic player calling the same authoritative operations as UI/strategy.
  fastGame=true;interferenceRoute=new();long captured=-1;int goal=-2;ulong started=Time.GetTicksMsec();
  long allocated=GC.GetTotalAllocatedBytes();
  while(!mission.AtOutcome&&nativeWorld.Tick<45000){
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(missionFault!=null)throw new InvalidOperationException(missionFault);
   var leader=nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Owner==0&&nativeWorld.IsCommander(e));
   if(leader!=null&&nativeWorld.Tick%16==0){camera=interferenceRoute.Goal<0?new(13*32,(nativeData.Scenario.Map.Height-72)*32):NativePosition(leader);ClampMissionCamera();}
   if(nativeWorld.Tick/2000!=captured||interferenceRoute.Goal!=goal){captured=nativeWorld.Tick/2000;goal=interferenceRoute.Goal;
    await MissionCapture($"run-{nativeWorld.Tick:D5}-goal{goal}");GD.Print($"INTERFERENCE_PROGRESS tick={nativeWorld.Tick} goal={goal} relays={nativeWorld.Deaths[1,86]} warriors={nativeWorld.Live[0,8]}");
   }
  }
  var route=interferenceRoute;interferenceRoute=null;
  Check(mission.Outcome is {Success:true}&&mission.AtOutcome&&nativeWorld.Deaths[1,86]==6,"End-to-end A02 victory through original economy, AI, combat and six relay deaths");
  Check(nativeWorld.Events.Any(e=>e.Kind=="harvest")&&nativeWorld.Events.Any(e=>e.Kind=="production-complete")&&nativeWorld.Teams[1].Strategy is {Updates:>0},"Rendered run exercises harvesting, produced forces and AI3");
  await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);await MissionCapture("90-victory-media");await ToSignal(GetTree().CreateTimer(18),SceneTreeTimer.SignalName.Timeout);await MissionCapture("91-rewards");
  File.WriteAllText(Path.Combine(output,"end-to-end.json"),JsonSerializer.Serialize(new{mode="Rendered, normal real-time Fast150%; diagnostic player uses authoritative ordinary commands/purchases; no setup injection",elapsedMs=Time.GetTicksMsec()-started,allocatedBytes=GC.GetTotalAllocatedBytes()-allocated,nativeWorld.Tick,mission.Outcome,route.Actions,events=nativeWorld.Events,missionEvents=mission.Events.Where(e=>e.Kind!="normal-scan"),audio=missionAudioAudit},new JsonSerializerOptions{WriteIndented=true}));
  await Send(Key.R);Check(nativeWorld.Tick==0&&missionBriefing&&mission.Outcome==null,"Restart after complete mission clears result and state");await MissionCapture("99-restart");
 }
}
