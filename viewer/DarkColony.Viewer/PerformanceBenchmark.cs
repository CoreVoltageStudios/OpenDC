using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool perfActive;ulong perfLastSample;
 double perfProcessMs,perfSimulationMs,perfDrawMs,perfArtMs,perfFogMs,perfHudMs;
 int perfBuilds;
 string perfStage="";
 readonly record struct PerfFrame(string Stage,long Tick,double FrameMs,double ProcessMs,double SimulationMs,double DrawMs,double ArtMs,double FogMs,double HudMs,int Builds,double RenderCpuMs,double GpuMs,double SetupMs,long Allocated,int Gen0,int Gen1,int Gen2,double GcPauseMs);
 readonly List<PerfFrame> perfFrames=new();
 static double PerfElapsed(long start)=>Stopwatch.GetElapsedTime(start).TotalMilliseconds;
 void PerfSample(double delta){
  if(!perfActive)return;
  ulong now=Time.GetTicksUsec();double wall=perfLastSample==0?0:(now-perfLastSample)/1000d;perfLastSample=now;if(wall==0)return;
  var rid=GetViewport().GetViewportRid();
  perfFrames.Add(new(perfStage,nativeWorld.Tick,wall,perfProcessMs,perfSimulationMs,perfDrawMs,perfArtMs,perfFogMs,perfHudMs,perfBuilds,
   RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid),RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid),RenderingServer.GetFrameSetupTimeCpu(),
   GC.GetTotalAllocatedBytes(false),GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2),GC.GetTotalPauseDuration().TotalMilliseconds));
 }
 public async void RunPerformanceBenchmark(){
  try {
   if(IsolatedPlayerData==null)throw new InvalidOperationException("Performance benchmark requires isolated ODC_USER_DATA.");
   missionHarnessManual=true;missionBriefing=false;mainMenu=false;perfFrames.EnsureCapacity(40000);
   var args=OS.GetCmdlineUserArgs();double seconds=double.Parse(args.FirstOrDefault(a=>a.StartsWith("--perf-seconds="))?[15..]??"25",System.Globalization.CultureInfo.InvariantCulture);
   string fixtureRoot=Path.GetFullPath(args.FirstOrDefault(a=>a.StartsWith("--perf-fixture-root="))?[20..]??root);
   bool onlyFast=args.Contains("--perf-fast-only"),memoryGodMode=args.Contains("--perf-god-mode"),human=args.Contains("--perf-human");
   int onlyTick=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--perf-only-tick="))?[17..]??"-1");
   int onlyMap=int.Parse(args.FirstOrDefault(a=>a.StartsWith("--perf-only-map="))?[16..]??"0");
   DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);Engine.MaxFps=60;
   RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(),true);
   ui=1;maximumAspect=0;ApplyAspectLimit();SetNativeSampling(1);nativeOmniscient=false;
   // The project fullscreen default can override the engine command-line window
   // request in source launches. Set and verify the actual measured framebuffer.
   if(args.FirstOrDefault(a=>a.StartsWith("--perf-size=")) is {} sizeArg){
    var parts=sizeArg[12..].Split('x');var wanted=new Vector2I(int.Parse(parts[0]),int.Parse(parts[1]));
    var window=GetWindow();window.Mode=wanted==DisplayServer.ScreenGetSize()?Window.ModeEnum.Fullscreen:Window.ModeEnum.Windowed;
    if(window.Mode==Window.ModeEnum.Windowed)window.Size=wanted;
    for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    ApplyAspectLimit();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    if(GetViewportRect().Size!=(Vector2)wanted)throw new InvalidOperationException($"Requested benchmark {wanted}, actual viewport {GetViewportRect().Size}");
   }
   var fixtureFolder=Path.Combine(output,"fixtures");Directory.CreateDirectory(fixtureFolder);
   var metrics=new List<object>();
   foreach(int map in (human?(onlyMap>0?new[]{onlyMap}:new[]{2,3,4,5}):onlyMap is >=6 and <=15?new[]{onlyMap}:new[]{4,2}).Where(m=>onlyMap==0||m==onlyMap)){
    var data=PersistenceData($"SCENARIO/{(human?"HUMAN/HUMAN":"ALIEN/ALIEN")}{map:00}.SCN");
    var build=new OriginalMission(data,0);var route6=new DarkColony.Diagnostics.PuppetRoute();var route4=new DarkColony.Diagnostics.BreakthroughRoute();var route2=new DarkColony.Diagnostics.InterferenceRoute();
    foreach(int tick in (human?new[]{5000}:map==14?new[]{1000}:map>=10?new[]{6000,12000}:map==9?new[]{1812,3000}:map==8?new[]{8000,16000,24000}:map==7?new[]{14000,22000}:map==6?new[]{14000,20000}:map==4?new[]{0,3000,9016,14000}:new[]{6000}).Where(t=>onlyTick<0||t==onlyTick)){
     // Build representative states with the retained ordinary-command player only.
     // No resources, visibility, unit counts or outcomes are injected.
     while(!human&&map<7&&build.World.Tick<tick&&!build.AtOutcome){if(map==6)route6.Update(build.World);else if(map==4)route4.Update(build.World);else route2.Update(build.World);build.SetClock((build.World.Tick+1)*66);build.Step();}
     var snapshot=human?SaveCodec.Open(Path.Combine(fixtureRoot,map>5?$"generated/human615/h{map:00}-ordinary/ordinary-{tick}.json":$"generated/human15/h{map:00}-route/ordinary-{tick}.json"))["payload"]!.AsObject():map>=10?SaveCodec.Open(Path.Combine(fixtureRoot,"generated",LateFolder(map),"ordinary-route",$"route-{tick}-save.json"))["payload"]!.AsObject():map==9?SaveCodec.Open(Path.Combine(fixtureRoot,"generated/buried-treasure/developed-combat",tick==1812?"developed-cast.json":$"developed-{tick}.json"))["payload"]!.AsObject():map==8?SaveCodec.Open(Path.Combine(fixtureRoot,$"generated/screamer/ordinary-route-corrected/route-{tick}-save.json"))["payload"]!.AsObject():map==7?SaveCodec.Open(Path.Combine(fixtureRoot,$"generated/civil-war/ordinary-route/route-{tick}-save.json"))["payload"]!.AsObject():build.Snapshot();File.WriteAllText(Path.Combine(fixtureFolder,$"{(human?"h":"a")}{map}-{tick}.json"),snapshot.ToJsonString());
     foreach(bool fast in onlyFast?new[]{true}:new[]{false,true}){
      InstallMission(data,OriginalMission.Restore(data,(JsonObject)snapshot.DeepClone()),false);missionHarnessManual=true;missionBriefing=false;nativeSelection.Clear();missionPointer=new(-100,-100);fastGame=fast;godMode=memoryGodMode;ApplySessionPlaytesting();ui=1;
      // The ordinary route's late combat is a counterattack on the player's hive.
      // Keep it on screen instead of measuring an unexplored Citadel viewport.
      bool counterattack=!human&&map==6&&tick>=20000;
      var center=map==8?new Vector2(53*32,(data.Scenario.Map.Height-43)*32):map==7?new Vector2((tick>=22000?22:42)*32,(data.Scenario.Map.Height-(tick>=22000?32:83))*32):map==6?new Vector2(20*32,(data.Scenario.Map.Height-100)*32):map==2?new Vector2(13*32,(data.Scenario.Map.Height-72)*32):tick==0?new Vector2(85*32,(data.Scenario.Map.Height-65)*32):new Vector2(99*32,(data.Scenario.Map.Height-83)*32);
      if(map==9)center=new Vector2(60*32,(data.Scenario.Map.Height-30)*32);
      if(map>=10)center=new Vector2(data.Scenario.Teams[0].Base[0]*32,(data.Scenario.Map.Height-data.Scenario.Teams[0].Base[1])*32);
      if(map==14&&!human)center=NativePosition(nativeWorld.Entities.First(e=>e.Owner==0&&e.Type==10));
      if(human){var visibleForce=nativeWorld.Entities.Where(e=>e.Living&&e.Owner==0&&e.Slot>=120&&e.Type is >=0 and <=6).ToArray();if(visibleForce.Length>0)center=NativePosition(visibleForce.OrderByDescending(e=>visibleForce.Count(o=>Math.Abs(o.Cell.X-e.Cell.X)+Math.Abs(o.Cell.Z-e.Cell.Z)<10)).First());}
      camera=center;zoom=1.5f;ClampMissionCamera();
      await UiCapture($"perf-{(human?"h":"a")}{map}-{tick}-{fast}-start");
      if(counterattack&&nativeDrawn.Count<10)throw new InvalidOperationException("Counterattack benchmark is not framing the visible hive/attackers");
      ClearNativeArtBodies();nativeAccumulator=0;perfFrames.Clear();perfBuilds=0;
      var stage=$"{(human?"h":"a")}{map}-t{tick}-{(fast?"fast":"classic")}";long initial=nativeWorld.Tick;
      var process=Process.GetCurrentProcess();long memoryStart=process.PrivateMemorySize64;ulong began=Time.GetTicksUsec();bool ordered=false,again=false;int memoryBin=-1;var memorySamples=new List<object>();
      perfLastSample=0;perfActive=true;missionHarnessManual=false;
      while((Time.GetTicksUsec()-began)/1e6<seconds&&!mission.AtOutcome){
       double elapsed=(Time.GetTicksUsec()-began)/1e6;if((int)(elapsed/5)>memoryBin){memoryBin=(int)(elapsed/5);process.Refresh();memorySamples.Add(new{seconds=elapsed,privateBytes=process.PrivateMemorySize64,managedBytes=GC.GetTotalMemory(false),sharedBytes=nativeSharedBytes,units=nativeWorld.Entities.Count(),resources=PresentationResources()});}perfStage=stage+(elapsed<3?"-cold":"-sustained");
       camera=center+new Vector2((float)Math.Sin(elapsed*.45)*450,(float)Math.Sin(elapsed*.21)*180);zoom=1.5f+(float)Math.Sin(elapsed*.3)*.4f;ClampMissionCamera();
       if(!ordered&&nativeWorld.Tick>=initial+45){ordered=true;var group=human?nativeWorld.Entities.Where(e=>e.Living&&e.Owner==0&&e.Type is 0 or 2 or 3 or 5).ToArray():BenchmarkGroup(map);nativeWorld.CommandGroup(group,OriginalOrder.MoveAttack,human?new(nativeWorld.Teams[0].Rally[0],nativeWorld.Teams[0].Rally[1]):map==14?new(78,24):map>=10?new(nativeWorld.Teams[0].Base[0]-4,nativeWorld.Teams[0].Base[1]+6):map==9?new(57,26):map==8?new(56,45):map==7?(tick>=22000?new(20,28):new(38,78)):map==6?(counterattack?new(14,99):new(28,100)):map==4?new(94,78):new(20,72));}
       if(!again&&nativeWorld.Tick>=initial+180){again=true;var group=human?nativeWorld.Entities.Where(e=>e.Living&&e.Owner==0&&e.Type is 0 or 2 or 3 or 5).ToArray():BenchmarkGroup(map);nativeWorld.CommandGroup(group,OriginalOrder.MoveAttack,human?new(nativeWorld.Teams[0].Rally[0],nativeWorld.Teams[0].Rally[1]):map==14?new(78,24):map>=10?new(nativeWorld.Teams[0].Base[0]-4,nativeWorld.Teams[0].Base[1]+6):map==9?new(60,30):map==8?new(56,45):map==7?(tick>=22000?new(23,30):new(40,80)):map==6?(counterattack?new(18,97):new(44,103)):map==4?new(103,82):new(15,67));if(map!=9)nativeWorld.RequestProduction(0,human?9:23);}
       await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
       if(missionFault!=null)throw new InvalidOperationException(missionFault);
      }
      perfActive=false;missionHarnessManual=true;process.Refresh();
      using(var writer=new StreamWriter(Path.Combine(output,stage+".csv"))){writer.WriteLine("stage,tick,frame_ms,process_ms,simulation_ms,draw_ms,art_ms,fog_ms,hud_ms,builds,render_cpu_ms,gpu_ms,setup_ms,allocated,gen0,gen1,gen2,gc_pause_ms");foreach(var f in perfFrames)writer.WriteLine(FormattableString.Invariant($"{f.Stage},{f.Tick},{f.FrameMs:F4},{f.ProcessMs:F4},{f.SimulationMs:F4},{f.DrawMs:F4},{f.ArtMs:F4},{f.FogMs:F4},{f.HudMs:F4},{f.Builds},{f.RenderCpuMs:F4},{f.GpuMs:F4},{f.SetupMs:F4},{f.Allocated},{f.Gen0},{f.Gen1},{f.Gen2},{f.GcPauseMs:F4}"));}
      metrics.Add(new{stage,initial,final=nativeWorld.Tick,seconds,godMode,frames=perfFrames.Count,units=nativeWorld.Entities.Count(),visible=nativeDrawn.Count,missiles=nativeWorld.Missiles.Count,memoryStart,memoryEnd=process.PrivateMemorySize64,harvest=nativeWorld.Events.Count(e=>e.Kind=="harvest"),launch=nativeWorld.Events.Count(e=>e.Kind=="launch"),production=nativeWorld.Events.Count(e=>e.Kind=="production-complete"),sharedArtHits,sharedArtMisses,sharedBytes=nativeSharedBytes,memorySamples,outcome=mission.Outcome});
      await UiCapture($"perf-{(human?"h":"a")}{map}-{tick}-{fast}-end");
      File.WriteAllText(Path.Combine(output,"performance-run.json"),JsonSerializer.Serialize(new{classification="Rendered normal-clock diagnostic workload; ordinary commands on deterministic route states; no screenshot/readback inside measured intervals. CSV CPU/render metrics refer to preceding work; frame_ms is monotonic wall time between process callbacks, not external PresentMon.",build=OS.HasFeature("editor")?"source-hosted":"release-export",jitOptimizationsDisabled=typeof(OriginalWorld).Assembly.GetCustomAttributes(typeof(DebuggableAttribute),false).Cast<DebuggableAttribute>().FirstOrDefault()?.IsJITOptimizerDisabled,viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},ui,artwork="Classic",vsync="Enabled",cap=60,gpu=RenderingServer.GetVideoAdapterName(),metrics},new JsonSerializerOptions{WriteIndented=true}));
      GD.Print("PERF_STAGE_DONE "+stage);
     }
    }
   }
   if(args.Contains("--perf-lifetime"))await VerifyPresentationLifetime();
   GD.Print("PERFORMANCE_BENCHMARK_PASS");await FinishFirstStrike(0);
  }catch(Exception e){perfActive=false;File.WriteAllText(Path.Combine(output,"performance-failure.txt"),e.ToString());GD.PushError(e.ToString());await FinishFirstStrike(1);}
 }
 NativeEntity[] BenchmarkGroup(int map)=>nativeWorld.Entities.Where(e=>e.Living&&e.Owner==0&&(e.Type is 8 or 10 or 11 or 13||map==9&&e.Type==12)&&(map!=9||Math.Abs(e.Cell.X-60)<25&&Math.Abs(e.Cell.Z-30)<25)).ToArray();
}
