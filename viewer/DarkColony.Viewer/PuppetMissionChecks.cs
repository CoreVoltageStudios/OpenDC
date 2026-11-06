using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyPuppetMission(){
  missionHarnessManual=true;AdvanceInterference(1800);ResetMissionCamera();
  Check(economicMission&&mission.CampaignIndex==5&&!godMode,"Direct A06 entry uses economy and normal damage");
  Check(nativeWorld.ArtifactContents.Values.Sum(c=>c.Count)==23&&nativeWorld.Entities.Count(e=>e.Type==65)==3,"Buried artifacts remain separate after native opening");
  Check(nativeWorld.Entities.Where(e=>e.Type==37).All(e=>!NativeVisible(e)),"No artifact artwork before Neural Hive");
  await MissionCapture("01-opening-hive");
  var commander=nativeWorld.Entities.First(e=>e.Owner==0&&nativeWorld.IsCommander(e));nativeSelection.Add((commander.Slot,commander.Incarnation));camera=NativePosition(commander);ClampMissionCamera();
  await MissionCapture("02-northern-reinforcements");
  await Send(Key.J);await MissionCapture("03-objectives");await Send(Key.Escape);
  SaveMissionSlot(0);CloseRelayPanel();long saved=nativeWorld.Tick;ReturnMainMenu();await SessionClick("resume-save");Check(nativeWorld.Tick==saved&&mission.CampaignIndex==5,"Surface Continue restores A06");
  // Controlled shared-system fixture, already cross-process verified by PuppetChecks.
  var snapshot=SaveCodec.Open(Path.Combine(root,"generated/puppet-masters/shared-v2/excavation-save.json"))["payload"]!.AsObject();
  InstallMission(nativeData,OriginalMission.Restore(nativeData,snapshot),false);missionHarnessManual=true;
  var site=nativeWorld.Entities.First(e=>e.Type==37&&nativeWorld.ArtifactWorker(e)!=null);var worker=nativeWorld.ArtifactWorker(site)!;
  camera=NativePosition(worker);zoom=2;ClampMissionCamera();nativeSelection.Add((worker.Slot,worker.Incarnation));
  Check(NativeVisible(site),"Neural Hive and current sight expose the excavation site");
  SyncArtifactAudio();Check(artifactAudio.Values.Any(v=>v.Playing&&v.Sound==95),"Original excavation audio loop plays");
  await UiCapture("04-excavation");
  var sequence=new List<object>();missionHarnessManual=false;
  for(int i=0;i<60;i++){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);sequence.Add(new{nativeWorld.Tick,worker.X,worker.Z,worker.Angle,clip=worker.Body.Set?.Name,worker.Body.Frame,worker.Body.Delay,progress=site.Actions[0].Word});if(i%3==0){using var frame=GetViewport().GetTexture().GetImage();frame.SavePng(Path.Combine(output,$"excavation-frame-{i:D3}.png"));}}
  missionHarnessManual=true;File.WriteAllText(Path.Combine(output,"excavation-sequence.json"),JsonSerializer.Serialize(sequence));
  Check(sequence.Count==60&&worker.Body.Set?.Name=="SLUGFUNK","Consecutive rendered excavation frames retain original FUNK loop");
  while(!nativeWorld.Entities.Any(e=>e.Type==65&&e.Owner==0))AdvanceInterference(1);
  var artifact=nativeWorld.Entities.First(e=>e.Type==65&&e.Owner==0);nativeSelection.Clear();nativeSelection.Add((artifact.Slot,artifact.Incarnation));
  await UiCapture("05-lunatek");Check(missionHits.Any(h=>h.Id=="activate"&&h.Enabled),"Lunatek has explicit usable Activate action");
  await SessionClick("activate");missionHarnessManual=false;
  var activation=new List<object>();
  for(int i=0;i<80;i++){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);activation.Add(new{nativeWorld.Tick,artifact.Type,artifact.Health,artifact.X,artifact.Z,artifact.Angle,clip=artifact.Body.Set?.Name,artifact.Body.Frame});if(i%4==0){using var frame=GetViewport().GetTexture().GetImage();frame.SavePng(Path.Combine(output,$"activation-frame-{i:D3}.png"));}}
  missionHarnessManual=true;File.WriteAllText(Path.Combine(output,"activation-sequence.json"),JsonSerializer.Serialize(activation));
  AdvanceInterference(100);Check(!artifact.Living&&nativeWorld.Events.Count(e=>e.Kind=="lunatek-activated")==1,"UI activation consumes Lunatek once");
  // Mixed-ability layout fixture: no mission completion claim.
  foreach(int type in new[]{73,44,9,48,65}){var e=nativeWorld.Spawn(type,0,new(site.Cell.X+3,site.Cell.Z),"ui/mixed/"+type);if(type==73)e.Energy=255;nativeSelection.Add((e.Slot,e.Incarnation));}
  foreach(int aspect in new[]{0,16,21})foreach(float scale in new[]{.75f,1f,1.5f}){
   maximumAspect=aspect;ApplyAspectLimit();ui=scale;camera=NativePosition(worker);ClampMissionCamera();
   await UiCapture($"06-mixed-{aspect}-{scale}");RelayLayout($"A06 mixed {aspect}/{scale}");
   Check(missionHits.Where(h=>h.Id.StartsWith("product:")||h.Id.StartsWith("products-")).All(h=>h.Rect.End.Y<=(UiSize.Y-120)*ui),"Production rows and scroll controls clear objective text");
   Check(new[]{"inspire","deploy","turret","retract","activate"}.All(id=>missionHits.Any(h=>h.Id==id)),"Every applicable mixed ability exposed");
   OpenMissionPanel("menu");await UiCapture($"07-menu-{aspect}-{scale}");RelayLayout("A06 menu");CloseRelayPanel();
  }
  maximumAspect=16;ApplyAspectLimit();ui=GetViewportRect().Size.Y/1080;nativeSelection.Clear();nativeSelection.Add((worker.Slot,worker.Incarnation));
  focusedUi="product:20";relayPointerHelp=false;developmentTab=0;await UiCapture("08-product-tooltip");
  Check(relayTooltip is {Visible:true}&&GetViewportRect().Encloses(relayTooltip.GetGlobalRect()),"Rendered tooltip contained in active game area");
  foreach(string name in new[]{"dtalk2","atran6"}){
   var movie=new OriginalMovie();movie.Load(Path.Combine(root,"generated/original-video/v1",name+".odcv"));AddChild(movie);movie.Size=GetViewportRect().Size;movie.Play();await ToSignal(GetTree().CreateTimer(.6),SceneTreeTimer.SignalName.Timeout);Check(movie.Frame>0&&!movie.Enhanced,"Original A06 movie playback "+name);await UiCapture("09-movie-"+name);RemoveChild(movie);movie.Free();
  }
  // Restart discards artifact progress/loops and restores exact buried inventory.
  ResetFirstStrike();Check(nativeWorld.ArtifactContents.Values.Sum(c=>c.Count)==23&&artifactAudio.Count==0,"Restart restores sites and clears audio loops");await MissionCapture("10-restart-briefing");
 }
}
