using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCouncilDirect(){
  missionHarnessManual=true;Check(IsCouncil&&missionBriefing,"Direct expansion entry");
  await MissionCapture("direct-briefing");await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(320);await MissionCapture("direct-opening-before-tab");Check(nativeWorld.Entities.Any(e=>e.Living&&e.Owner==0&&NativePosition(e).DistanceTo(camera)<500),"Opening camera frames local force without selecting commander");SaveMissionSlot(0);LoadMissionSlot(0);
  Check(IsCouncil&&nativeWorld.Tick==320,"Direct expansion save/load");
  int directIndex=mission.CampaignIndex;string directCampaign=mission.CampaignId;
  string fixture=directIndex==0?"generated/council-wars-first/checks/"+mission.CampaignId+"-success.json":(directIndex==7?"generated/council-wars-final/checks/":directIndex>=3?"generated/council-wars-middle/checks/":"generated/council-wars-next/checks/")+mission.CampaignId+"-"+(directIndex+1)+"-success.json";
  var snap=SaveCodec.Open(Path.Combine(root,fixture))["payload"]!.AsObject();
  InstallMission(nativeData,OriginalMission.Restore(nativeData,snap),false);await WarFrames();Check(missionVideo!=null,"Original outcome playback starts");
  var movie=missionVideo!;long peakVideo=movie.RetainedVideoBytes;int starvations=0;ulong start=Time.GetTicksMsec();while(missionVideo!=null&&Time.GetTicksMsec()-start<90000){peakVideo=Math.Max(peakVideo,movie.RetainedVideoBytes);starvations=Math.Max(starvations,movie.BufferStarvations);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
  File.WriteAllText(Path.Combine(output,"movie-playback.json"),JsonSerializer.Serialize(new{directCampaign,directIndex,movie.Duration,peakVideo,starvations,workers=IndexedMovieFrames.ActiveWorkers,elapsed=(Time.GetTicksMsec()-start)/1000d}));
  Check(missionVideo==null&&rewardsVisible,"Original outcome reaches natural end and releases player");await MissionCapture("direct-natural-result");
  await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);
  if(directIndex+1<OriginalCampaign.SupportedCount(directCampaign))Check(missionBriefing&&mission.CampaignIndex==directIndex+1,"Direct outcome continues within implemented expansion missions");
  else if(directIndex==OriginalCampaign.CatalogCount(directCampaign)-1)Check(mainMenu&&studioIntro==null,"Direct final outcome returns to main menu");
  else Check(mainMenu&&missionPanel=="campaign","Direct last implemented outcome returns to campaign");
  File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("COUNCIL_DIRECT_PASS");
 }
 async Task VerifyCouncilPerformance(){
  missionHarnessManual=true;DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);Engine.MaxFps=60;
  RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(),true);perfFrames.EnsureCapacity(6000);var rows=new List<object>();
  ui=1;maximumAspect=0;ApplyAspectLimit();SetNativeSampling(1);godMode=false;
  foreach(bool fast in new[]{false,true}){
   bool final=missionSuite=="council-final-performance";bool middle=missionSuite=="council-middle-performance";bool next=missionSuite=="council-next-performance";var data=PersistenceData(final?"SCENARIO/AEROGEN/AERO08.SCN":middle?"SCENARIO/AEROGEN/AERO06.SCN":next?"SCENARIO/COUNCIL/COUN02.SCN":"SCENARIO/COUNCIL/COUN01.SCN");var snapshot=SaveCodec.Open(Path.Combine(root,final?"generated/council-wars-final/checks/cw-human-8-developed-4800.json":middle?"generated/council-wars-middle/checks/cw-human-6-developed-24000.json":next?"generated/council-wars-next/checks/cw-taar-2-developed-4800.json":"generated/council-wars-first/checks/cw-taar-developed-12000.json"))["payload"]!.AsObject();
   InstallMission(data,OriginalMission.Restore(data,snapshot),false);missionHarnessManual=true;missionBriefing=false;fastGame=fast;ApplySessionPlaytesting();missionPointer=new(-100,-100);
   var center=NativePosition(nativeWorld.Entities.First(e=>e.Owner==0&&e.Type is 16 or 28));camera=center;zoom=1.5f;ClampMissionCamera();await UiCapture("developed-"+fast);
   ClearNativeArtBodies();perfFrames.Clear();perfLastSample=0;nativeAccumulator=0;var samples=new List<object>();int bin=-1;ulong began=Time.GetTicksUsec();
   perfActive=true;missionHarnessManual=false;
   while((Time.GetTicksUsec()-began)/1e6<22&&!mission.AtOutcome){
    double elapsed=(Time.GetTicksUsec()-began)/1e6;perfStage=elapsed<3?"cold":"sustained";
    camera=center+new Vector2((float)Math.Sin(elapsed*.45)*400,(float)Math.Sin(elapsed*.21)*140);zoom=1.5f+(float)Math.Sin(elapsed*.3)*.3f;ClampMissionCamera();
    if((int)(elapsed/5)>bin){bin=(int)(elapsed/5);using var p=Process.GetCurrentProcess();samples.Add(new{elapsed,privateBytes=p.PrivateMemorySize64,managedBytes=GC.GetTotalMemory(false),resources=PresentationResources()});}
    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(missionFault!=null)throw new InvalidOperationException(missionFault);
   }
   perfActive=false;missionHarnessManual=true;
   File.WriteAllText(Path.Combine(output,"frames-"+(fast?"fast":"classic")+".json"),JsonSerializer.Serialize(perfFrames));
   rows.Add(new{fast,viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},ui,artwork="Classic",cap=60,vsync="Enabled",initial=final?4800:middle?24000:next?4800:12000,final=nativeWorld.Tick,units=nativeWorld.Entities.Count(),visible=nativeDrawn.Count,samples,resources=PresentationResources()});
  }
  ReturnMainMenu();await WarFrames();DrainRetiredMissionAudio();Check(nativeTerrain.Count==0&&nativeSharedArt.Count==0&&nativePoseFrames.Count==0&&missionMusic==null&&nativeWorld==null,"Expansion replacement clears world, terrain, shared sprites and music");
  File.WriteAllText(Path.Combine(output,"performance.json"),JsonSerializer.Serialize(new{classification="22-second normal-clock source-hosted optimized render per speed; no readback in measured interval; ordinary developed state; not a long soak or external PresentMon",gpu=RenderingServer.GetVideoAdapterName(),rows,after=PresentationResources()},new JsonSerializerOptions{WriteIndented=true}));
  GD.Print("COUNCIL_PERFORMANCE_PASS");
 }
}
