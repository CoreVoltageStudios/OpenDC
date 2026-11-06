using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyReinforcementRoute(){
  bool switchTransport=missionSuite=="qol-transport";
  for(int pass=0;pass<(switchTransport?1:2);pass++){
   if(pass>0){await Send(Key.R);Check(mission.Blocks[4]!.Remaining==1&&nativeWorld.Transports.Count==0,"restart clears reinforcement event and transport");await Send(Key.Enter);}
   missionHarnessManual=true;
   void Orders(){
    long tick=nativeWorld.Tick;if(tick<240||tick%60!=0)return;
    nativeSelection.Clear();foreach(var e in nativeWorld.Entities.Where(e=>e.Living&&e.Owner==0&&e.Type is 8 or 73))nativeSelection.Add((e.Slot,e.Incarnation));
    var c=NativeSelected().FirstOrDefault(e=>e.Type==73);if(c!=null&&c.Energy>=32)NativeCommand(OriginalOrder.Inspire);
    bool cleared=new[]{new Cell(7,47),new Cell(5,45)}.All(cell=>!nativeWorld.Entities.Any(e=>e.Living&&e.Type==82&&e.Cell==cell));
    if(tick%300==0)NativeCommand(OriginalOrder.MoveAttack,cleared?new Cell(27,63):new Cell(7,47));
   }
   // Accelerate only the ordinary command-driven opening. Original units, damage,
   // movement, triggers and transports remain active; no scene/health edits.
   while(nativeWorld.Tick<1050){RememberNativePositions();mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();DispatchMissionSounds();Orders();}
   Check(nativeWorld.Deaths[1,82]==2,"natural western opening destroys two polluters");
   camera=new(28*32+16,(nativeData.Scenario.Map.Height-1-63)*32+16);zoom=2;ClampMissionCamera();
   await MissionCapture($"reinforce-{pass}-approach");
   missionWall=mission.Milliseconds/1000d;nativeAccumulator=0;missionHarnessManual=false;
   var trace=new List<object>();long lastOrder=-1;int switches=0;
   while(nativeWorld.Tick<1340){
    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    if(switchTransport&&switches<4&&nativeWorld.Tick>=1120+switches*30){
     long tick=nativeWorld.Tick;double residual=nativeAccumulator;int events=nativeWorld.Events.Count;
     ActivateMissionUi(switches%2==0?"speed-classic":"speed-fast");
     Check(nativeWorld.Tick==tick&&nativeAccumulator==residual&&nativeWorld.Events.Count==events,"transport speed switch preserves tick, residual and events");
     GD.Print($"QOL_TRANSPORT_SWITCH tick={tick} fast={fastGame} unloaded={nativeWorld.Events.Count(e=>e.Kind=="unloaded"&&e.Tick>1109)}");switches++;
    }
    if(nativeWorld.Tick!=lastOrder){Orders();lastOrder=nativeWorld.Tick;}
    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    trace.Add(new{nativeWorld.Tick,mission.Milliseconds,fastGame,remaining=mission.Blocks[4]!.Remaining,drawn=nativeDrawn.ToArray(),units=nativeWorld.Entities.Where(e=>e.Identity.StartsWith("TRO/4/")||e.Type==93).Select(e=>new{e.Slot,e.Incarnation,e.Owner,e.Type,e.X,e.Z,e.Height,e.State,visible=NativeVisible(e),drawn=nativeDrawn.Contains((e.Slot,e.Incarnation)),frame=e.Body.Frame,sequence=e.Body.Set?.Name})});
   }
   missionHarnessManual=true;
   if(switchTransport)Check(switches==4,"four switches during transport descent and unloading");
   var delivered=nativeWorld.Entities.Where(e=>e.Type==8&&e.Owner==0&&e.Identity.StartsWith("TRO/4/")).ToArray();
   await MissionCapture($"reinforce-{pass}-delivered");
   Check(mission.Events.Count(e=>e.Kind=="trigger"&&e.Block==4)==1,"route triggers once");
   Check(delivered.Length==4&&delivered.All(e=>nativeDrawn.Contains((e.Slot,e.Incarnation))),"all four player passengers actually drawn near Communicant");
   nativeSelection.Clear();foreach(var e in delivered)nativeSelection.Add((e.Slot,e.Incarnation));
   await Send(Key.M);await ClickNativeCell(MouseButton.Left,new(32,63));
   missionHarnessManual=false;await WaitMissionTick(nativeWorld.Tick+65);missionHarnessManual=true;
   Check(delivered.All(e=>e.X>29*256),"delivered units accept player Move input");
   await MissionCapture($"reinforce-{pass}-controlled");
   if(missionSuite=="qol"&&pass==0){
    await Send(Key.M);await ClickNativeCell(MouseButton.Left,new(27,63));missionHarnessManual=false;await WaitMissionTick(nativeWorld.Tick+120);missionHarnessManual=true;
    Check(mission.Events.Count(e=>e.Kind=="trigger"&&e.Block==4)==1&&nativeWorld.Entities.Count(e=>e.Type==8&&e.Owner==0&&e.Identity.StartsWith("TRO/4/"))==4,"Fast re-entry does not duplicate reinforcement delivery");
    await MissionCapture("reinforce-fast-reentry");
   }
   File.WriteAllText(Path.Combine(output,$"reinforcement-render-trace-{pass}.json"),JsonSerializer.Serialize(trace));
  }
 }
}
