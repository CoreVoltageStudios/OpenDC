using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyWarPerformance(){
  if(IsolatedPlayerData==null)throw new InvalidOperationException("War benchmark requires isolated data");
  missionHarnessManual=true;godMode=false;DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);Engine.MaxFps=60;
  var snap=SaveCodec.Open(Path.Combine(root,"generated/war-implementation/developed-fixed/D8PLAY10-developed.json"))["payload"]!.AsObject();
  var cfg=snap["war"]!["Configuration"]!.Deserialize<WarConfiguration>()!;var metrics=new List<object>();
  foreach(bool fast in new[]{false,true}){
   var data=cfg.Load(Path.Combine(root,"private/DC"));InstallMission(data,OriginalMission.Restore(data,(JsonObject)snap.DeepClone()),false);
   missionHarnessManual=true;godMode=false;ApplySessionPlaytesting();fastGame=fast;ui=1;maximumAspect=0;ApplyAspectLimit();SetNativeSampling(1);
   // Stress presentation with the existing diagnostic reveal, never authoritative
   // visibility. Native AI/forces/resources/outcomes remain the developed save's.
   nativeOmniscient=true;var units=nativeWorld!.Entities.Where(e=>e.Living&&e.Owner<8&&e.Slot>=120).ToArray();
   var center=NativePosition(units.OrderByDescending(e=>units.Count(o=>Math.Abs(o.Cell.X-e.Cell.X)+Math.Abs(o.Cell.Z-e.Cell.Z)<12)).First());camera=center;zoom=1.5f;ClampMissionCamera();
   await MissionCapture("war-developed-"+(fast?"fast":"classic"));
   RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(),true);perfFrames.Clear();perfFrames.EnsureCapacity(4000);perfLastSample=0;perfBuilds=0;nativeAccumulator=0;
   using var process=Process.GetCurrentProcess();long startBytes=process.PrivateMemorySize64;ulong began=Time.GetTicksUsec();perfActive=true;missionHarnessManual=false;
   while((Time.GetTicksUsec()-began)/1e6<23&&!mission.AtOutcome){double t=(Time.GetTicksUsec()-began)/1e6;perfStage=(fast?"fast":"classic")+(t<3?"-cold":"-sustained");camera=center+new Vector2((float)Math.Sin(t*.45)*380,(float)Math.Sin(t*.21)*150);zoom=1.5f+(float)Math.Sin(t*.3)*.3f;ClampMissionCamera();await WarFrames(1);if(missionFault!=null)throw new InvalidOperationException(missionFault);}
   perfActive=false;missionHarnessManual=true;process.Refresh();string stage=fast?"fast":"classic";
   using(var writer=new StreamWriter(Path.Combine(output,"war-perf-"+stage+".csv"))){writer.WriteLine("stage,tick,frame_ms,process_ms,simulation_ms,draw_ms,art_ms,fog_ms,hud_ms,builds,render_cpu_ms,gpu_ms,allocated,gen0,gen1,gen2");foreach(var f in perfFrames)writer.WriteLine(FormattableString.Invariant($"{f.Stage},{f.Tick},{f.FrameMs:F4},{f.ProcessMs:F4},{f.SimulationMs:F4},{f.DrawMs:F4},{f.ArtMs:F4},{f.FogMs:F4},{f.HudMs:F4},{f.Builds},{f.RenderCpuMs:F4},{f.GpuMs:F4},{f.Allocated},{f.Gen0},{f.Gen1},{f.Gen2}"));}
   metrics.Add(new{stage,viewport=GetViewportRect().Size.ToString(),nativeWorld.Tick,units=nativeWorld.Entities.Count(),visible=nativeDrawn.Count,frames=perfFrames.Count,startBytes,endBytes=process.PrivateMemorySize64,resources=PresentationResources()});
   ReturnMainMenu();await WarFrames(5);Check(nativeWorld==null&&nativeTerrain.Count==0&&nativeBodies.Count==0&&nativeSharedBytes==0&&missionMusic==null,"War benchmark replacement clears mission resources");
  }
  File.WriteAllText(Path.Combine(output,"war-performance.json"),JsonSerializer.Serialize(new{classification="Source-hosted optimized build; developed eight-player save; 23s per speed; diagnostic rendering reveal; normal simulation clock, God OFF. Monotonic Process frame interval, not external PresentMon.",ui,artwork="Classic",vsync="Enabled",cap=60,gpu=RenderingServer.GetVideoAdapterName(),metrics},new JsonSerializerOptions{WriteIndented=true}));
 }
}
