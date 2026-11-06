using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyInterference(){
  paused=true;
  void Advance(int ticks){for(int i=0;i<ticks;i++){RememberNativePositions();nativeWorld.Step();DispatchMissionSounds();}nativeAccumulator=0;}
  // Accelerated setup, followed by real-clock work/production. Original scenario unmodified.
  Advance(240);await NativeCapture("01-opening");Check(development.OpeningDelivered,"Original M2 opening delivered in accelerated setup");
  var miner=nativeWorld.Entities.Single(e=>e.Owner==0&&e.Type==14);camera=NativePosition(miner);ClampMissionCamera();
  await NativeMouse(MouseButton.Left,UnitHitRect(miner).GetCenter());Check(NativeSelected().Any(e=>e.Slot==miner.Slot),"Brozaar selected through Godot-dispatched ordinary click");
  await ClickNativeCell(MouseButton.Right,new(4,80));Advance(300);
  Check(miner.Type==48&&nativeWorld.Events.Any(e=>e.Kind=="harvest"),"Ordinary right-click reaches vent and harvests");
  await NativeCapture("02-harvest");
  // Controlled funds only to cover all roster/build states without waiting through a mission.
  nativeWorld.Teams[0].Money=12000;developmentNotice="CAPTURE FIXTURE: 12,000 Petra-7 for construction coverage; original scenario unchanged.";
  await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  async Task Buy(int id){int kind=nativeData.Scenario.Dependencies.Single(d=>d.Id==id).Kind;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await NativeMouse(MouseButton.Left,missionHits.Single(h=>h.Id=="tab:"+kind).Rect.GetCenter());await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);var h=missionHits.Single(h=>h.Id=="product:"+id);Check(h.Enabled,"Product "+id+" available");await NativeMouse(MouseButton.Left,h.Rect.GetCenter());}
  await Buy(23);await Buy(16);paused=false;
  await ToSignal(GetTree().CreateTimer(12),SceneTreeTimer.SignalName.Timeout);paused=true;
  Check(nativeWorld.Events.Any(e=>e.Kind=="production-complete")&&!nativeWorld.Teams[0].Cities[3].InitializationPending,"Real-clock warrior output and breeder construction finish");
  await NativeCapture("03-production");await Buy(17);await Buy(28);paused=false;
  await ToSignal(GetTree().CreateTimer(13),SceneTreeTimer.SignalName.Timeout);paused=true;
  await Buy(25);Advance(45);await NativeCapture("04-expanded-hive");
  var slom=nativeWorld.Entities.Single(e=>e.Owner==0&&e.Type==44);camera=NativePosition(slom);ClampMissionCamera();
  await NativeMouse(MouseButton.Left,UnitHitRect(slom).GetCenter());await ClickNativeCell(MouseButton.Right,new(18,66));Advance(240);await Send(Key.D);paused=false;
  await ToSignal(GetTree().CreateTimer(4),SceneTreeTimer.SignalName.Timeout);paused=true;Check(slom.Type==46,"Slom walks and deploys through Godot controls");await NativeCapture("05-slom");
  // Explicit combat/HP fixture, not an extra original encounter. Exercises the
  // new stationary Firestorm trajectory/trail, Sy Demon fire and deployed mine.
  var turret=nativeWorld.Spawn(41,1,new(23,66),"fixture/render-firestorm");var demon=nativeWorld.Spawn(10,0,new(21,66),"fixture/render-sy-demon");turret.Health=demon.Health=10000;
  nativeWorld.RefreshSight();turret.PendingOrder=new(turret.Slot,turret.Incarnation,OriginalOrder.Attack,Target:demon.Slot,Issuer:1);nativeWorld.Command(new(demon.Slot,demon.Incarnation,OriginalOrder.Attack,Target:turret.Slot));
  camera=NativePosition(demon);ClampMissionCamera();developmentNotice="COMBAT FIXTURE: placed Firestorm / Sy Demon with extended HP for animation review.";paused=false;
  await ToSignal(GetTree().CreateTimer(5),SceneTreeTimer.SignalName.Timeout);paused=true;
  Check(nativeWorld.Events.Any(e=>e.Kind=="launch"&&e.Slot==turret.Slot)&&nativeWorld.Events.Any(e=>e.Kind=="launch"&&e.Slot==demon.Slot),"Firestorm and Sy Demon fire through authoritative combat");await NativeCapture("05b-combat");
  nativeWorld.Retire(turret);nativeWorld.Retire(demon);Advance(120);Check(!nativeWorld.Missiles.Any(m=>m.Weapon is 34 or 43),"Firestorm shots and trails retire after firing stops");
  long cacheTick=nativeWorld.Tick;int previousSampling=nativeSampling;
  await Send(Key.C);Check(nativeSampling!=previousSampling&&nativeWorld.Tick==cacheTick,"Artwork switch releases pose cache without advancing paused simulation");
  await Send(Key.C);Check(nativeSampling==previousSampling&&nativePoseFrames.Values.All(p=>p.Count<=8),"Original artwork preference restored; cached entity poses remain bounded");
  var at=MiniMapRect.Position+MiniMapRect.Size*new Vector2(.7f,.65f);await NativeMouse(MouseButton.Left,at);Check(camera.X>nativeData.Scenario.Map.Width*16,"Minimap navigates east without world order");
  await Send(Key.R);Check(nativeWorld.Tick==0&&nativeWorld.Teams[0].Money==0&&nativeWorld.Teams[0].Production.All(q=>q.Types.Count==0)&&nativeWorld.Session.Sources.All(s=>s.Initialized==0),"Restart isolates queues, money, sources and script consumption");
  paused=true;await NativeCapture("06-restart");
  File.WriteAllText(Path.Combine(output,"m2-rendered-checks.json"),JsonSerializer.Serialize(new{checks,limits="Godot-dispatched input; accelerated setup and explicit resource fixture; real-clock build intervals; not a complete mission",gaps=nativePresentationGaps},new JsonSerializerOptions{WriteIndented=true}));
  if(nativePresentationGaps.Count>0)throw new InvalidOperationException(string.Join(";",nativePresentationGaps));GD.Print("INTERFERENCE_M2_CAPTURE_PASS");
 }
}
