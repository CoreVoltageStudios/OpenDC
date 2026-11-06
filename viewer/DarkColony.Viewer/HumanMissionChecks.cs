using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyHumanMission(){
  int number=campaignMission.Index+1;missionHarnessManual=true;
  Check(IsHuman&&!godMode&&economicMission==OriginalCampaign.HasEconomy(nativeData),"Human direct identity, local economy and God OFF");
  await Send(Key.Space);await Send(Key.Home);await Send(Key.Pagedown);await UiCapture("human-briefing-middle");for(int i=0;i<8;i++)await Send(Key.Pagedown);await UiCapture("human-briefing-bottom");
  Check(briefingScroll==Math.Max(0,briefingTotalLines-briefingVisibleLines),"Human full briefing reachable");
  await Send(Key.Enter);AdvanceInterference(240);ResetMissionCamera();await Send(Key.Tab);
  Check(NativeSelected().Any(e=>nativeWorld.IsCommander(e)&&e.Type is >=69 and <=72),"Tab selects owned Human commander");
  await UiCapture("human-opening");if(UnitEnabled("inspire")){await SessionClick("inspire");AdvanceInterference(4);Check(nativeWorld.Events.Any(e=>e.Kind.Contains("inspir")),"Human Inspire dispatched through sidebar");}
  await Send(Key.J);await UiCapture("human-objectives");CloseRelayPanel();
  SaveMissionSlot(0);CloseRelayPanel();long tick=nativeWorld.Tick;ReturnMainMenu();await SessionClick("resume-save");
  Check(IsHuman&&mission.CampaignIndex==number-1&&nativeWorld.Tick==tick,"Continue routes Human save exactly");
  var saved=mission.Snapshot();
  if(number==2){
   var snap=SaveCodec.Open(Path.Combine(root,"generated/human15/h02-economy/exploiter-deploying-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);
   var miner=nativeWorld.Entities.Single(e=>e.Owner==0&&e.Type==47);nativeSelection.Clear();nativeSelection.Add((miner.Slot,miner.Incarnation));camera=NativePosition(miner);ClampMissionCamera();
   for(int f=0;f<12;f++){await UiCapture("exploiter-deployment-"+f.ToString("00"));AdvanceInterference(4);}
   int income=nativeWorld.EarnedResources[0];AdvanceInterference(64);Check(nativeWorld.EarnedResources[0]>income&&miner.Type==47,"Rendered deployed Exploiter earns actual resources");await UiCapture("exploiter-harvesting");
   await SessionClick("retract");for(int i=0;i<150&&miner.Type==47;i++)AdvanceInterference(1);Check(miner.Type==6,"Human Retract sidebar dispatch");await UiCapture("exploiter-retracted");
   snap=SaveCodec.Open(Path.Combine(root,"generated/human15/h02-economy/human-producing-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);ResetMissionCamera();await UiCapture("human-production-active");
  }
  if(number==3){
   foreach(var ability in new[]{("firestorm-mobile",1,41,"turret"),("spider-mobile",43,45,"deploy")}){
    var snap=SaveCodec.Open(Path.Combine(root,"generated/human15/h03-abilities/"+ability.Item1+"-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);
    var unit=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==ability.Item2);var leader=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&nativeWorld.IsCommander(e));
    nativeSelection.Clear();nativeSelection.Add((unit.Slot,unit.Incarnation));nativeSelection.Add((leader.Slot,leader.Incarnation));camera=NativePosition(unit);ClampMissionCamera();
    await UiCapture("human-mixed-"+ability.Item1);Check(UnitEnabled("inspire")&&UnitEnabled(ability.Item4),"Human mixed selection retains distinct abilities");
    await SessionClick(ability.Item4);for(int i=0;i<10;i++){AdvanceInterference(4);await UiCapture(ability.Item1+"-animation-"+i.ToString("00"));}AdvanceInterference(150);
    Check(unit.Type==ability.Item3&&nativeWorld.IsCommander(leader),"Human ability dispatch only transforms eligible unit");await UiCapture(ability.Item1+"-complete");
   }
  }
  if(number==4){
   var snap=SaveCodec.Open(Path.Combine(root,"generated/human15/h04/stratus-extracting-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);
   // The controlled script fixture has not explored the rendezvous. This temporal
   // artwork inspection uses the diagnostic fog override; normal visibility is
   // restored by the following InstallMission. It is not player sight evidence.
   nativeOmniscient=true;
   var stratus=nativeWorld.Entities.First(e=>e.Living&&e.Owner==1&&nativeWorld.IsCommander(e));camera=NativePosition(stratus);ClampMissionCamera();
   for(int i=0;i<12;i++){await UiCapture("human-stratus-extraction-"+i.ToString("00"));AdvanceInterference(8);}Check(!stratus.Living,"Rendered allied commander extraction completes");
   snap=SaveCodec.Open(Path.Combine(root,"generated/human15/h04-aerial/human-air-artillery-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);ResetMissionCamera();
   nativeSelection.Clear();foreach(var e in nativeWorld.Entities.Where(e=>e.Living&&e.Owner==0&&e.Type is 3 or 5))nativeSelection.Add((e.Slot,e.Incarnation));
   await UiCapture("human-air-artillery");var group=NativeSelected().ToArray();var at=group[0].Cell;nativeWorld.CommandGroup(group,OriginalOrder.MoveOnly,new(at.X+5,at.Z+3));
   for(int i=0;i<12;i++){AdvanceInterference(8);await UiCapture("human-air-ground-move-"+i.ToString("00"));}
   Check(group.All(e=>e.Living)&&group.Any(e=>e.Type==5&&e.Height>0),"Rendered mixed air/ground Human movement");
   foreach(int type in new[]{3,5}){
    snap=SaveCodec.Open(Path.Combine(root,$"generated/human15/h04-weapons-valid/weapon-{type}-active-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);
    var shooter=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==type);camera=NativePosition(shooter);ClampMissionCamera();
    for(int i=0;i<12;i++){await UiCapture($"human-weapon-{type}-{i:00}");AdvanceInterference(4);}
    Check(nativeWorld.Events.Any(e=>e.Kind=="blast"&&e.Slot==shooter.Slot),"Rendered Human weapon continuation "+type);
   }
  }
  if(number==5){
   var snap=SaveCodec.Open(Path.Combine(root,"generated/human15/h05/before-betrayal-save.json"))["payload"]!.AsObject();InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);ResetMissionCamera();
   Check(nativeWorld.Teams[0].Allies[1]!=0,"Rendered H05 before betrayal");await UiCapture("human-before-betrayal");AdvanceInterference(24);Check(!nativeWorld.Relations[0,1],"Rendered H05 relations change");await UiCapture("human-after-betrayal");await Send(Key.J);await UiCapture("human-betrayal-objectives");CloseRelayPanel();
   for(int i=0;i<12;i++){AdvanceInterference(8);await UiCapture("human-betrayal-reinforcements-"+i.ToString("00"));}
  }
  if(number>=6){await VerifyLateHumanEffects(number);InstallMission(nativeData,OriginalMission.Restore(nativeData,saved),false);ResetMissionCamera();}
  foreach(float scale in (number<=5||number is 7 or 9 or 13 or 15?new[]{.75f,1f,1.5f}:new[]{1f}))foreach(int aspect in new[]{0,16}){
   ui=scale;maximumAspect=aspect;ApplyAspectLimit();await UiCapture($"human-hud-{scale}-{aspect}");RelayLayout("Human HUD");
   if(economicMission){foreach(int tab in new[]{1,0,2}){ActivateDevelopment("tab:"+tab);await UiCapture($"human-products-{tab}-{scale}-{aspect}");RelayLayout("Human products");}}
   focusedUi="inspire";relayPointerHelp=false;await UiCapture($"human-tooltip-{scale}-{aspect}");if(relayTooltip is {Visible:true})Check(GetViewportRect().Encloses(relayTooltip.GetGlobalRect()),"Human tooltip actual bounds");focusedUi="";
   OpenMissionPanel("menu");await UiCapture($"human-menu-{scale}-{aspect}");RelayLayout("Human menu");CloseRelayPanel();
  }
  ui=1;maximumAspect=0;ApplyAspectLimit();ReturnMainMenu();OpenMissionPanel("campaign");await MenuCapture("human-campaign");
  Check(SelectedCampaign=="human"&&missionHits.Any(h=>h.Id=="campaign-start:0"&&h.Enabled),"Human normal campaign selection");
  await SessionClick("faction:taar");await MenuCapture("taar-preserved");Check(SelectedCampaign=="taar"&&OriginalCampaign.SupportedTaarMissions==15,"Taar catalog retained in faction flow");
  await SessionClick("faction:human");await SessionClick("campaign-start:0");Check(IsHuman&&mission.CampaignIndex==0&&missionBriefing,"Normal Human first mission entry");
  var data=PersistenceData($"SCENARIO/HUMAN/HUMAN{number:00}.SCN");InstallMission(data,OriginalMission.Restore(data,saved),false);ResetFirstStrike();Check(IsHuman&&nativeWorld.Tick==0,"Human restart clears mission state");await UiCapture("human-restart");
  var outcome=SaveCodec.Open(Path.Combine(root,$"generated/{(number==9?"h09-war":number<=5?"human15":"human615")}/h{number:00}/controlled-success.json"))["payload"]!.AsObject();InstallMission(data,OriginalMission.Restore(data,outcome),false);
  await ToSignal(GetTree().CreateTimer(.8),SceneTreeTimer.SignalName.Timeout);Check(missionVideo is {Frame:>0,Enhanced:false},"Human original-source success movie");await UiCapture("human-success-movie");await Send(Key.Escape);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);Check(rewardsVisible,"Human movie skip enters rewards");await UiCapture("human-rewards");
  if(number==9){
   // Controlled completion records establish routing only, not ordinary victories.
   ReleaseMissionVideo();ReturnMainMenu();
   for(int n=1;n<=8;n++){HumanResult(n,true);RecordCampaign();ReleaseMissionVideo();}
   ContinueCampaign();Check(IsHuman&&mission.CampaignIndex==8&&missionBriefing,"Normal earned Human8 to Human9 continuation");await UiCapture("human8-to9");
   HumanResult(9,true);RecordCampaign();ReleaseMissionVideo();ContinueCampaign();Check(IsHuman&&mission.CampaignIndex==9&&missionBriefing,"Normal earned Human9 to Human10 continuation");await UiCapture("human9-to10");
   ReturnMainMenu();string progress=File.ReadAllText(CampaignPath);godMode=true;ApplySessionPlaytesting();OpenMissionPanel("campaign");campaignScroll=8;await MenuCapture("human9-god-catalog");await SessionClick("campaign-start:8");Check(nativeWorld.GodMode&&mission.CampaignIndex==8,"God-mode Human9 entry");
   HumanResult(9,true);RecordCampaign();ReleaseMissionVideo();Check(File.ReadAllText(CampaignPath)==progress,"God-mode H09 completion cannot change permanent progress");ReturnMainMenu();godMode=false;ApplySessionPlaytesting();
  }
 }
}
