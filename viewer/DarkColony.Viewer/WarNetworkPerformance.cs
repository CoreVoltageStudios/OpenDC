using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Diagnostics;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyNetworkPerformance(){
  if(IsolatedPlayerData==null)throw new InvalidOperationException("Isolated benchmark profile required");
  DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);Engine.MaxFps=60;
  OpenMultiplayer();await WarFrames();var button=multiplayerRoot!.FindChildren("*","Button",true,false).Cast<Button>().Single(b=>b.Text=="Host battle");await WarControlClick(button);
  SelectWarMap(warMaps.Single(m=>m.Id=="D8PLAY10"));for(int i=2;i<8;i++)warSetup.Slots[i]=warSetup.Slots[i] with{Controller=WarController.Ai};ValidateWarSetup();GD.Print("NETWORK_UI_LISTENING");
  ulong deadline=Time.GetTicksMsec()+30000;while(network!.Players.Length<2&&Time.GetTicksMsec()<deadline)await WarFrames(1);Check(network!.Players.Length==2,"Benchmark connects real ENet client");
  network.Ready(true);while(!network.CanStart&&Time.GetTicksMsec()<deadline)await WarFrames(1);network.DiagnosticFastForwardUntil=16000;network.Start();
  while(!IsNetworkMatch||nativeWorld.Tick<16000)await WarFrames(1);
  ui=1;maximumAspect=0;ApplyAspectLimit();SetNativeSampling(1);nativeOmniscient=true; // Presentation-only stress coverage, never network visibility.
  var units=nativeWorld.Entities.Where(e=>e.Living&&e.Owner<8&&e.Slot>=152).ToArray();var center=NativePosition(units.OrderByDescending(e=>units.Count(o=>Math.Abs(e.Cell.X-o.Cell.X)+Math.Abs(e.Cell.Z-o.Cell.Z)<12)).First());camera=center;zoom=1.5f;ClampMissionCamera();
  var metrics=new List<object>();
  foreach(bool fast in new[]{false,true}){
   network.Timing(true,fast);while(network.Match!.Fast!=fast)await WarFrames(1);await MissionCapture("network-developed-"+(fast?"fast":"classic"));
   RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(),true);var drawnCounts=new List<int>();perfFrames.Clear();perfFrames.EnsureCapacity(4000);perfLastSample=0;perfBuilds=0;ulong began=Time.GetTicksUsec();perfActive=true;
   using var process=Process.GetCurrentProcess();long memory=process.PrivateMemorySize64;string stage=fast?"fast":"classic";
   while((Time.GetTicksUsec()-began)/1e6<25&&!mission.AtOutcome){double t=(Time.GetTicksUsec()-began)/1e6;perfStage=stage+(t<3?"-cold":"-sustained");camera=center+new Vector2((float)Math.Sin(t*.45)*380,(float)Math.Sin(t*.21)*150);zoom=1.5f+(float)Math.Sin(t*.3)*.3f;ClampMissionCamera();await WarFrames(1);drawnCounts.Add(nativeDrawn.Count);}
   perfActive=false;process.Refresh();using(var writer=new StreamWriter(Path.Combine(output,"network-perf-"+stage+".csv"))){writer.WriteLine("stage,tick,frame_ms,process_ms,simulation_ms,draw_ms,art_ms,fog_ms,hud_ms,builds,render_cpu_ms,gpu_ms,allocated,gen0,gen1,gen2");foreach(var f in perfFrames)writer.WriteLine(FormattableString.Invariant($"{f.Stage},{f.Tick},{f.FrameMs:F4},{f.ProcessMs:F4},{f.SimulationMs:F4},{f.DrawMs:F4},{f.ArtMs:F4},{f.FogMs:F4},{f.HudMs:F4},{f.Builds},{f.RenderCpuMs:F4},{f.GpuMs:F4},{f.Allocated},{f.Gen0},{f.Gen1},{f.Gen2}"));}
   metrics.Add(new{stage,viewport=GetViewportRect().Size.ToString(),nativeWorld.Tick,units=nativeWorld.Entities.Count(),visible=nativeDrawn.Count,minDrawn=drawnCounts.Min(),maxDrawn=drawnCounts.Max(),meanDrawn=drawnCounts.Average(),frames=perfFrames.Count,startBytes=memory,endBytes=process.PrivateMemorySize64,network.WorstHashMs,network.WorstStepMs,network.MaximumQueue,network.SentBytes,network.ReceivedBytes});
  }
  ReturnMainMenu();for(int i=0;i<120&&retiredNetworks.Count>0;i++)await WarFrames(1);await WarFrames(8);
  Check(nativeWorld==null&&nativeTerrain.Count==0&&nativeSharedBytes==0&&missionMusic==null&&retiredNetworks.Count==0,"Network replacement releases mission and transport resources");
  File.WriteAllText(Path.Combine(output,"network-performance.json"),JsonSerializer.Serialize(new{classification="Optimized source, real ENet localhost client, eight occupied slots, normal clock after accelerated command-only development to tick16000; presentation-only omniscience; 25s per speed; no external PresentMon",gpu=RenderingServer.GetVideoAdapterName(),ui=1,artwork="Classic",cap=60,metrics},new JsonSerializerOptions{WriteIndented=true}));
 }
}
