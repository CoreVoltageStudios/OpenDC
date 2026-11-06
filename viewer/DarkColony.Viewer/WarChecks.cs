using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task WarFrames(int count=3){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 async Task WarControlClick(Control control){var p=NativeWindowPoint(control.GetGlobalRect().GetCenter());Input.ParseInputEvent(new InputEventMouseMotion{Position=p});await WarFrames(1);Input.ParseInputEvent(new InputEventMouseButton{Position=p,ButtonIndex=MouseButton.Left,Pressed=true});await WarFrames(1);Input.ParseInputEvent(new InputEventMouseButton{Position=p,ButtonIndex=MouseButton.Left,Pressed=false});await WarFrames();}
 async Task VerifyWarInterface(){
  missionHarnessManual=true;
  if(!mainMenu)ReturnMainMenu();await WarFrames();await UiClick("war-setup");await WarFrames();
  Check(warSetupVisible&&warMaps.Length==56,"War setup enters from actual main-menu action");
  warSearch.Text="zzzz-no-map";FilterWarMaps();await WarFrames();Check(warMapList.ItemCount==0&&warDetails.Text.StartsWith("No maps"),"Empty filter feedback");warSearch.Text="";FilterWarMaps();
  SelectWarMap(warMaps.Single(m=>m.Id=="D8PLAY10"));
  for(int i=1;i<8;i++)warSetup.Slots[i]=warSetup.Slots[i] with{Controller=i%2==0?WarController.AiPlus:WarController.Ai};BindWarPlayers();ValidateWarSetup();
  for(int i=0;i<120&&warPreview.Texture==null;i++)await WarFrames(1);
  await MenuCapture("war-setup-eight");
  maximumAspect=16;ApplyAspectLimit();await WarFrames();await MenuCapture("war-setup-centered");ui=1.5f;await WarFrames();await MenuCapture("war-setup-centered-enlarged");Check(warStart.GetGlobalRect().End.Y<=GetViewportRect().Size.Y,"Centered enlarged Start remains reachable");maximumAspect=0;ApplyAspectLimit();ui=GetViewportRect().Size.Y/1080;await WarFrames();
  float prior=ui;ui=1.5f;await WarFrames();await MenuCapture("war-setup-enlarged");Check(warStart.GetGlobalRect().End.Y<=GetViewportRect().Size.Y,"Enlarged footer remains reachable");ui=prior;
  warSetup.Slots[1]=warSetup.Slots[1] with{Color=warSetup.Slots[0].Color};BindWarPlayers();ValidateWarSetup();Check(warStart.Disabled,"Colour conflicts prevent launch");await MenuCapture("war-colour-conflict");warSetup.Slots[1]=warSetup.Slots[1] with{Color=1};BindWarPlayers();ValidateWarSetup();
  SelectWarMap(warMaps.Single(m=>m.Id=="D2PLAY01"));Check(warSetup.Slots.Skip(2).All(s=>s.Controller==WarController.None),"Smaller map explicitly deactivates excess slots");
  await WarFrames();warStartAfter=0;await WarControlClick(warStart);Check(IsWar&&!mainMenu&&!missionBriefing,"Retained Start battle button enters War directly");
  Check(!godMode&&nativeWorld.GodMode==false,"Ordinary War starts God OFF");
  var original=mission.War!.Configuration;int local=LocalTeam;Check(nativeWorld.Teams[local].Race==original.Slots.Single(s=>s.Controller==WarController.Local).Race,"HUD local owner follows shuffled assignment");
  for(int i=0;i<300;i++){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}
  await Send(Key.Tab);Check(NativeSelected().All(e=>e.Owner==local)&&NativeSelected().Any(),"Commander selection uses assigned local team");await Send(Key.S);
  await MissionCapture("war-human-hud");OpenMissionPanel("diplomacy");await WarFrames();await MenuCapture("war-diplomacy");await Send(Key.Escape);
  SaveMissionSlot(0);var snap=mission.Snapshot();Check(File.Exists(SavePath(0)),"War manual save written");ReturnMainMenu();await WarFrames();Check(continueSlot==0&&saveCampaigns[0]=="Single-player War","Continue identifies War save");await MenuCapture("war-continue");await UiClick("resume-save");Check(IsWar&&JsonNode.DeepEquals(snap,mission.Snapshot()),"Continue restores War without reinitializing or reshuffling");
  ResetFirstStrike();Check(nativeWorld.Tick==0&&JsonSerializer.Serialize(mission.War!.Configuration)==JsonSerializer.Serialize(original),"Restart repeats exact setup and seed");
  OpenMissionPanel("menu");await WarFrames();await UiClick("war-surrender");Check(missionPanel=="abandon","Surrender asks before ending match");await UiClick("abandon-confirm");await WarFrames();Check(mission.AtOutcome&&mission.War!.Surrendered&&!mission.Outcome!.Success,"Confirmed surrender reaches defeat");await MissionCapture("war-surrender-result");
  await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);Check(mainMenu&&warSetupVisible,"Default result action returns to setup");
  SelectWarMap(warMaps.Single(m=>m.Id=="A2PLAY01"));warSetup.Slots[0]=warSetup.Slots[0] with{Race=1};BindWarPlayers();ValidateWarSetup();await MenuCapture("war-cityless-setup");
  StartWarMatch(warSetup with{Seed=7});Check(IsWar&&!IsHuman&&!economicMission,"Taar cityless map enters with no production");for(int i=0;i<16;i++){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();}await Send(Key.Tab);await MissionCapture("war-taar-cityless");
  maximumAspect=16;ApplyAspectLimit();ui=1.5f;await WarFrames();await MissionCapture("war-centered-enlarged");
  Check(!File.Exists(Path.Combine(PersistenceRoot,"campaign-v1.json"))&&!File.Exists(Path.Combine(PersistenceRoot,"human-campaign-v1.json")),"War activity writes no campaign progress");
  OpenMissionPanel("playtesting");await WarFrames();await UiClick("god-mode");Check(godMode&&nativeWorld.GodMode,"War Options retains session God mode");
  var hit=typeof(OriginalWorld).GetMethod("ApplyDamage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
  var own=nativeWorld.Entities.First(e=>e.Living&&e.Owner==LocalTeam);var enemy=nativeWorld.Entities.First(e=>e.Living&&e.Owner<8&&e.Owner!=LocalTeam);int hp=own.Health;hit.Invoke(nativeWorld,new object[]{enemy,own,1});Check(own.Health==hp,"War local owner protected by existing damage path");
  var unassisted=OriginalMission.Restore(nativeData,mission.Snapshot());Check(!unassisted.World.GodMode,"War snapshot does not persist God mode");await UiClick("god-mode");Check(!godMode&&!nativeWorld.GodMode,"War God mode turns off");hit.Invoke(nativeWorld,new object[]{enemy,own,1});Check(own.Health==hp-1,"War normal damage restored");CloseRelayPanel();
  var observer=SaveCodec.Open(Path.Combine(root,"generated/war-implementation/transitions-final/local-observer-save.json"))["payload"]!.AsObject();var observerConfig=observer["war"]!["Configuration"]!.Deserialize<WarConfiguration>()!;var observerData=observerConfig.Load(Path.Combine(root,"private/DC"));InstallMission(observerData,OriginalMission.Restore(observerData,observer),false);SaveMissionSlot(2);ReturnMainMenu();LoadMissionSlot(2);Check(IsWar&&mission.War!.Eliminated[LocalTeam]&&nativeWorld.Entities.All(e=>e.Owner!=LocalTeam),"Eliminated observer save loads without a local living unit");await MissionCapture("war-observer-restored");
  ReturnMainMenu();await WarFrames();Check(nativeWorld==null&&nativeTerrain.Count==0&&missionMusic==null&&missionAudioPlayer==null,"War replacement releases world/render/audio resources");
  HumanResult(7,true);RecordCampaign();ReleaseMissionVideo();ReturnMainMenu();await WarFrames();await UiClick("campaign");await UiClick("faction:human");campaignScroll=7;await WarFrames();await UiClick("campaign-start:7");Check(IsHuman&&mission.CampaignIndex==7&&missionBriefing,"Controlled earned profile opens Human8 through campaign after War");
  HumanResult(8,true);RecordCampaign();ReleaseMissionVideo();ContinueCampaign();Check(IsHuman&&mission.CampaignIndex==8&&missionBriefing,"Controlled Human8 ->9 routing preserved");HumanResult(9,true);RecordCampaign();ReleaseMissionVideo();ContinueCampaign();Check(IsHuman&&mission.CampaignIndex==9&&missionBriefing,"Controlled Human9 ->10 routing preserved");await MissionCapture("war-regression-human9-to10");ReturnMainMenu();
  File.WriteAllText(Path.Combine(output,"war-ui-checks.json"),JsonSerializer.Serialize(checks));
 }
 async Task VerifyWarOrdinary(){
  missionHarnessManual=true;godMode=false;warDiagnosticSeed=7;
  if(!mainMenu)ReturnMainMenu();await WarFrames();await UiClick("war-setup");await WarFrames();
  warSetup=WarConfiguration.Default("A2PLAY01",7);SelectWarMap(warMaps.Single(m=>m.Id=="A2PLAY01"));await WarFrames();warStartAfter=0;await WarControlClick(warStart);
  Check(IsWar&&nativeWorld.Seed==7&&!nativeWorld.GodMode,"Ordinary route starts through setup with isolated reproducible seed and God OFF");
  int local=LocalTeam;var destination=nativeWorld.Entities.First(e=>e.Owner<8&&e.Owner!=local&&nativeWorld.IsCommander(e)).Cell;int orders=0;
  while(!mission.AtOutcome&&nativeWorld.Tick<24000){
   if(nativeWorld.Tick%300==0){
    var force=nativeWorld.Entities.Where(e=>e.Living&&e.Owner==local&&e.Slot>=120).ToArray();
    void Select(NativeEntity e){nativeSelection.Clear();nativeSelection.Add((e.Slot,e.Incarnation));}
    foreach(var e in force.Where(nativeWorld.IsCommander))if(e.Energy>=32){Select(e);NativeCommand(OriginalOrder.Inspire);orders++;}
    var visible=nativeWorld.Entities.Where(e=>e.Living&&e.Owner<8&&!nativeWorld.Relations[local,e.Owner]&&nativeWorld.Visible(local,e.Cell)).ToArray();
    if(visible.Length>0){foreach(var unit in force){var target=visible.OrderBy(e=>Math.Abs(unit.Cell.X-e.Cell.X)+Math.Abs(unit.Cell.Z-e.Cell.Z)).First();Select(unit);NativeCommand(OriginalOrder.Attack,target:target.Slot);orders++;}}
    else if(nativeWorld.Tick%1200==0){nativeSelection.Clear();foreach(var e in force)nativeSelection.Add((e.Slot,e.Incarnation));NativeCommand(OriginalOrder.MoveAttack,destination);orders++;}
   }
   mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();
   if(nativeWorld.Tick%60==0){var focus=nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Owner==local&&nativeWorld.IsCommander(e));if(focus!=null){camera=NativePosition(focus);ClampMissionCamera();}await WarFrames(1);}
   if(nativeWorld.Tick==1800)await MissionCapture("war-ordinary-combat");
  }
  await WarFrames();Check(mission.Outcome?.Success==true&&nativeWorld.Tick==4420,"Ordinary Human arena victory matches the command-only headless route");
  Check(!File.Exists(CampaignPath),"Ordinary War victory writes no campaign credit");await MissionCapture("war-ordinary-victory");SaveCodec.Write(Path.Combine(output,"war-ordinary-final.json"),mission.Snapshot());
  File.WriteAllText(Path.Combine(output,"war-ordinary-rendered.json"),JsonSerializer.Serialize(new{nativeWorld.Tick,mission.Outcome,orders,godMode,classification="Actual setup button and existing Viewer command dispatch; accelerated deterministic simulation, no injected economy/entities/outcomes. Not a human Windows-input victory."}));
  await ToSignal(GetTree().CreateTimer(.35),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);Check(mainMenu&&warSetupVisible,"Victory Enter returns to setup");
 }
}
