using Godot;
using DarkColony.Original;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 DarkColony.Diagnostics.LegacyRoute? legacyRoute;
 async Task VerifyLegacyMission(){
  missionHarnessManual=true;AdvanceInterference(240);await MissionCapture("01-opening");
  Check(nativeWorld.Live[0,8]==7&&nativeWorld.Live[0,9]==1&&nativeWorld.Live[2,83]==1,"A03 source roster and opening reinforcement delivered");
  var miner=nativeWorld.Entities.First(e=>e.Living&&e.Owner==0&&e.Type==14);
  await NativeMouse(MouseButton.Left,UnitHitRect(miner).GetCenter());await ClickNativeCell(MouseButton.Right,new(64,11));AdvanceInterference(450);
  Check(miner.Type==48,"Brozaar harvests A03 source vent through ordinary frontend orders");
  var xeno=nativeWorld.Entities.Single(e=>e.Living&&e.Owner==0&&e.Type==9);
  camera=NativePosition(xeno);ClampMissionCamera();await NativeMouse(MouseButton.Left,UnitHitRect(xeno).GetCenter());await MissionCapture("02-xenowort-selected");
  await Send(Key.D);missionHarnessManual=false;
  long deployEnd=nativeWorld.Tick+120,lastDeploy=-1;while(nativeWorld.Tick<deployEnd){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(nativeWorld.Tick/12!=lastDeploy){lastDeploy=nativeWorld.Tick/12;await MissionCapture($"deploy-{nativeWorld.Tick:D5}");}}
  missionHarnessManual=true;AdvanceInterference(80);Check(xeno.Type==42,"Deploy D completes original Xenowort transformation");await MissionCapture("03-xenowort-deployed");
  // Explicit construction/production fixture; not evidence for earned-resource balance.
  nativeWorld.Teams[0].Money=15000;nativeSelection.Clear();
  async Task Buy(int id){await UiClick("tab:"+nativeData.Scenario.Dependencies.Single(d=>d.Id==id).Kind);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);for(int page=0;!missionHits.Any(h=>h.Id=="product:"+id)&&page<8;page++){await UiClick("products-next");await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);}await UiClick("product:"+id);AdvanceInterference(400);}
  foreach(int id in new[]{16,17,19})await Buy(id);
  await MissionCapture("04-genetics-upgrade");await Buy(22);
  Check(nativeWorld.Entities.Any(e=>e.Type==9&&e.SourceIdentity.StartsWith("production/")),"Original production artwork creates mobile Xenowort");await MissionCapture("05-new-forces");
  await VerifyLegacyCityCache();
  var restored=OriginalMission.Restore(nativeData,mission.Snapshot());InstallMission(nativeData,restored,false);missionBriefing=false;await MissionCapture("06-restored");
  await Send(Key.R);Check(nativeWorld.Tick==0&&missionBriefing&&nativeWorld.Teams[0].Money==3500,"A03 restart resets source state");await MissionCapture("07-restart");missionHarnessManual=false;
 }
 async Task VerifyLegacyCityCache(){
  // Isolated visual fixture, paid operations with explicit funds and AI suspended.
  nativeWorld.Teams[1].Ai=0;nativeWorld.Teams[1].Money=20000;
  foreach(int id in new[]{2,3,5}){if(nativeWorld.DependencyState(1,id)==1){Check(nativeWorld.RequestProduction(1,id,ProductionIssuer.Strategy).Accepted,"Human city variant fixture purchase");AdvanceInterference(420);}}
  var city=nativeWorld.Teams[1].Cities[2].Entity!;Check(city.Type==19,"Native Robotics II city rendered");
  nativeOmniscient=true;camera=NativePosition(city);ClampMissionCamera();city.Body=new();city.Body.Start(nativeData.Clips[19].Stand,0);city.Blood=new();city.Overlay=new();ClearNativeArtBodies();
  var metrics=new List<object>();for(int cycle=0;cycle<2;cycle++){int built=animationBodyBuilds;long bytes=GC.GetAllocatedBytesForCurrentThread();ulong start=Time.GetTicksUsec();int ticks=0;
   do{NativeParts(city,NativePosition(city),city.Height/8f);city.Body.Tick(city.Angle);ticks++;}while((city.Body.Frame!=0||city.Body.Delay!=0)&&ticks<500);
   metrics.Add(new{cycle,ticks,builds=animationBodyBuilds-built,allocated=GC.GetAllocatedBytesForCurrentThread()-bytes,usec=Time.GetTicksUsec()-start});File.WriteAllText(Path.Combine(output,"city-cache.json"),JsonSerializer.Serialize(new{metrics,frames=city.Body.Set!.Direction(city.Angle).Last-city.Body.Set!.Direction(city.Angle).First+1,city.Slot,cache=nativePoseFrames[city.Slot].Count}));if(cycle==1)Check(animationBodyBuilds==built,"Warm Robotics II loop reuses all city frames");}
  await MissionCapture("city-loop-reviewed");File.WriteAllText(Path.Combine(output,"city-cache.json"),JsonSerializer.Serialize(metrics));nativeOmniscient=false;
 }
 async Task VerifyLegacyPalette(){
  missionHarnessManual=true;AdvanceInterference(240);camera=new(54*32,(nativeData.Scenario.Map.Height-20)*32);zoom=1.5f;ClampMissionCamera();
  string TerrainHash(){var t=PrepareNativeTerrain(TerrainSelectors.LowSelector(nativeWorld.Coefficient)).Ground;if(t is CanvasTexture c)t=c.DiffuseTexture;using var image=t.GetImage();return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.GetData()));}
  string BodyHash()=>string.Join(";",NativeBodyColors(2).Select(c=>c.ToString()));
  var initial=TerrainHash();var bodies=BodyHash();int coefficient=nativeWorld.Coefficient;var cameraAt=camera;
  Check(nativeData.Scenario.Header.PaletteName=="JUNGLE.GIF"&&nativePalette.Source.SequenceEqual(nativeData.Read("JUNGLE.GIF")),"ANCONA selects SCN JUNGLE.GIF without colour compensation");
  await MissionCapture("palette-fresh");
  // Recreate the former configuration at the identical tick/camera, not image editing.
  void PaletteDiagnostic(string file){ClearNativeArtBodies();nativeBodyColors.Clear();miniTeamColors.Clear();miniBase=miniPixels=miniVisibility=Array.Empty<byte>();miniSelector=-1;foreach(var t in nativeTerrain.Values){DisposeNativeTexture(t.Ground);DisposeNativeTexture(t.Foreground);}nativeTerrain.Clear();nativePalette=GameColors.ReadGifPalette(nativeData.Read(file));}
  PaletteDiagnostic("DESERT.GIF");await MissionCapture("palette-before-desert-configuration");PaletteDiagnostic(nativeData.Scenario.Header.PaletteName);await MissionCapture("palette-after-jungle");
  // Exact controlled A02 success fixture exercises the real Continue transition.
  var a02=PersistenceData("SCENARIO/ALIEN/ALIEN02.SCN");InstallMission(a02,new OriginalMission(a02,0),false);BodyHash();
  Check(nativeData.Scenario.Header.PaletteName=="DESERT.GIF","A02 keeps desert palette");
  nativeWorld.Deaths[1,86]=6;AdvanceInterference(24);mission.SetClock(mission.Outcome!.Deadline+1);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);await UiClick("continue");
  Check(missionBriefing&&mission.CampaignIndex==2,"A02 Continue enters A03 briefing");await MissionCapture("palette-campaign-briefing");await Send(Key.Enter);AdvanceInterference(240);camera=cameraAt;zoom=1.5f;ClampMissionCamera();
  Check(nativeWorld.Coefficient==coefficient&&TerrainHash()==initial&&BodyHash()==bodies,"Campaign transition rebuilds terrain, body and minimap palette state");await MissionCapture("palette-campaign");
  await Send(Key.Escape);await UiClick("save");await UiClick("save-slot:0");await UiClick("close");await Send(Key.Escape);await UiClick("main-menu");await UiClick("abandon-confirm");Check(mainMenu&&nativeTerrain.Count==0&&nativeBodyColors.Count==0,"Return releases palette-derived resources");await UiClick("resume-save");
  Check(mission.CampaignIndex==2&&nativeWorld.Coefficient==coefficient&&TerrainHash()==initial&&BodyHash()==bodies,"Menu Continue restore preserves ANCONA colours and lighting");await MissionCapture("palette-restored");
  File.WriteAllText(Path.Combine(output,"palette-proof.json"),JsonSerializer.Serialize(new{palette=nativeData.Scenario.Header.PaletteName,coefficient,terrainHash=initial,bodyHash=bodies,mode="Frozen equal tick240; actual A02 outcome fixture + Continue, active save and main-menu load; no tint or selector edits"},new JsonSerializerOptions{WriteIndented=true}));
  await Send(Key.R);await Send(Key.Enter);AdvanceInterference(240);camera=cameraAt;zoom=1.5f;Check(TerrainHash()==initial,"Restart colours repeat");await MissionCapture("palette-restart");
 }
 async Task RunLegacyMission(){
  fastGame=true;legacyRoute=new();long captured=-1;int goal=-2;ulong started=Time.GetTicksMsec();
  while(!mission.AtOutcome&&nativeWorld.Tick<40000){
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(missionFault!=null)throw new InvalidOperationException(missionFault);
   var leader=nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Owner==0&&nativeWorld.IsCommander(e));
   if(leader!=null&&nativeWorld.Tick%16==0){camera=NativePosition(leader);ClampMissionCamera();}
   if(nativeWorld.Tick/2000!=captured||legacyRoute.Goal!=goal){captured=nativeWorld.Tick/2000;goal=legacyRoute.Goal;await MissionCapture($"run-{nativeWorld.Tick:D5}-goal{goal}");GD.Print($"LEGACY_PROGRESS tick={nativeWorld.Tick} goal={goal} troops={nativeWorld.Live[0,8]} cryo={nativeWorld.Live[2,83]}");}
  }
  var route=legacyRoute;legacyRoute=null;
  File.WriteAllText(Path.Combine(output,"end-to-end.json"),JsonSerializer.Serialize(new{mode="Rendered normal-clock Fast150%; ordinary authoritative orders/purchases; no state injection",elapsedMs=Time.GetTicksMsec()-started,nativeWorld.Tick,mission.Outcome,route.Actions,events=nativeWorld.Events,missionEvents=mission.Events.Where(e=>e.Kind!="normal-scan"),audio=missionAudioAudit},new JsonSerializerOptions{WriteIndented=true}));
  Check(mission.Outcome is {Success:true}&&nativeWorld.Live[2,83]==1,"A03 discovery protected and human outpost destroyed in complete rendered run");
  await ToSignal(GetTree().CreateTimer(12),SceneTreeTimer.SignalName.Timeout);await MissionCapture("90-rewards");await UiClick("continue");Check(missionNext,"A04 remains unavailable");await MissionCapture("91-campaign-boundary");
 }
}
