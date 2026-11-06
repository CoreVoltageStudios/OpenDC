using Godot;
using DarkColony.Assets;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool renderingMotion;
 async Task VerifyRenderingSampling() {
  missionHarnessManual=true;
  void Advance(int n){for(int i=0;i<n;i++){mission.SetClock(mission.Milliseconds+66);mission.Step();}}
  Advance(180);zoom=2;await Send(Key.Tab);
  foreach(float z in new[]{1.25f,2f,3f})foreach(int mode in new[]{0,1,2}){zoom=z;SetNativeSampling(mode);await UiCapture($"sampling-{z}-{mode}");}
  var commander=NativeSelected().Single();var enemy=nativeWorld.Spawn(0,1,new(commander.Cell.X+3,commander.Cell.Z),"diagnostic/filter/enemy");nativeWorld.RefreshSight();NativeCommand(OriginalOrder.Attack,target:enemy.Slot);
  for(int i=0;i<90;i++){Advance(1);if(nativeWorld.Missiles.Any(m=>m.Delay==0))break;}
  zoom=3;foreach(int mode in new[]{0,1,2}){SetNativeSampling(mode);await UiCapture($"sampling-combat-{mode}");}
 }
 async Task VerifyRenderingPresentation() {
  missionHarnessManual=true;
  void Advance(int n){for(int i=0;i<n;i++){RememberNativePositions();mission.SetClock(mission.Milliseconds+66);mission.Step();DispatchMissionSounds();}nativeAccumulator=0;}
  async Task Capture(string name){animationStage=name;var proof=VisualStateProof();await UiCapture(name);Check(proof==VisualStateProof(),name+": drawing preserves world / sight / RNG");Check(nativeDrawn.All(id=>NativeVisible(nativeWorld.Slots[id.Slot]!)),name+": no hidden entity submitted");File.WriteAllText(Path.Combine(output,name+"-render.json"),JsonSerializer.Serialize(new{proof,camera=new[]{camera.X,camera.Y},zoom,ui,knowledge=nativeFogState,drawn=nativeDrawn.Select(id=>new{slot=id.Slot,incarnation=id.Incarnation}).ToArray()}));}
  var trace=new List<object>();
  async Task Motion(string name,int ticks,bool pan=false){animationStage=name;renderingMotion=true;long allocated=GC.GetTotalAllocatedBytes();var timer=System.Diagnostics.Stopwatch.StartNew();
   for(int t=0;t<ticks;t++){Advance(1);for(int sub=0;sub<3;sub++){nativeAccumulator=(sub+1)*.022;if(pan)camera.X+=.35f;await ToSignal(GetTree().CreateTimer(.022),SceneTreeTimer.SignalName.Timeout);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);trace.Add(new{stage=name,t,sub,wall=Time.GetTicksUsec(),nativeWorld.Tick,camera=new[]{camera.X,camera.Y},zoom,ui,drawn=nativeDrawn.Select(id=>{var e=nativeWorld.Slots[id.Slot]!;var p=NativeRenderPosition(e);return new{e.Identity,e.X,e.Z,e.Angle,e.State,sequence=e.Body.Set?.Direction(e.Angle)?.Name,e.Body.Frame,e.Body.Delay,position=new[]{p.Body.X,p.Body.Y}};}).ToArray()});}}
   trace.Add(new{stage=name,elapsed=timer.Elapsed.TotalSeconds,allocatedBytes=GC.GetTotalAllocatedBytes()-allocated});renderingMotion=false;nativeAccumulator=0;
  }
  Advance(180);await SelectWholeSquad();zoom=2;await Send(Key.Tab);await Capture("01-stationary");
  await SelectWholeSquad();zoom=2;await Send(Key.Tab);NativeCommand(OriginalOrder.MoveOnly,new(28,20));await Motion("moving-fog",70);await Capture("02-moving");
  zoom=1.25f;await Capture("03-zoom-out");zoom=3;await Capture("04-zoom-in");zoom=2;await Motion("pan",25,true);await Capture("05-panned");
  await Send(Key.R);await Send(Key.Enter);Advance(180);await SelectWholeSquad();zoom=2;await Send(Key.Tab);var commander=NativeSelected().Single();var enemy=nativeWorld.Spawn(0,1,new(commander.Cell.X+3,commander.Cell.Z),"diagnostic/render/enemy");nativeWorld.RefreshSight();
  var cent=nativeWorld.Entities.First(e=>e.Type==89&&e.Owner==0);var centCell=cent.Cell;nativeSelection.Add((cent.Slot,cent.Incarnation));NativeCommand(OriginalOrder.Attack,target:enemy.Slot);await Motion("combat",90);await Capture("06-combat");Check(cent.Cell==centCell,"stationary CENT remains fixed during explicit attack");Check(nativeWorld.Events.Any(e=>e.Kind=="launch"),"combat fixture launches actual weapons");
  nativeWorld.Sight.ClearCurrent();await Capture("07-hidden");Check(!nativeDrawn.Contains((enemy.Slot,enemy.Incarnation)),"hidden combat target suppressed");
  foreach(var a in nativeWorld.Sight.Explored)Array.Clear(a);var map=nativeData.Scenario.Map;
  for(int x=0;x<15;x++){nativeWorld.Sight.Explored[0][x]=true;if(x<10)nativeWorld.Sight.Visible[0][x]=true;}
  camera=new(7*32,map.Height*32-16);zoom=2;await Capture("08-narrow-edge");
  await Send(Key.R);Check(nativeFogState.All(s=>s==0),"restart rebuilds empty fog without stale explored cells");await Capture("09-restart");
  // Explicit foreground rack: original map/art/masks, no mission progression.
  await Send(Key.Enter);foreach(var e in nativeWorld.Entities.ToArray())nativeWorld.Retire(e);
  var subject=nativeWorld.Spawn(8,0,new(23,25),"diagnostic/render/occlusion");subject.Angle=192;nativeOmniscient=true;
  var cell=subject.Body.Pose(subject.Angle)!.Cells.First(c=>c.UnknownWords[2] is 0 or 1);
  var sprite=nativeData.Sprite(cell.Bank).Cells[cell.CellId];var placement=OriginalShadow.BodyPlacement(cell,sprite);
  var raw=GameColors.Body(sprite,GameColors.Table(nativePalette,0,16,ColorBank.Body));var visual=new VisualAsset("rack",raw,raw.Width,raw.Height,raw.Width,raw.Height,-placement.X,-placement.Y);
  int covered=Enumerable.Range(0,raw.Width*raw.Height).Count(p=>raw.Rgba[p*4+3]!=0);bool found=false;
  foreach(var point in Enumerable.Range(0,map.Width*map.Height).Select(i=>new Cell(i%map.Width,i/map.Width)).Where(c=>map.At(c.X,c.Z).ForegroundId!=0).OrderBy(c=>Math.Abs(c.X-23)+Math.Abs(c.Z-25))) {
   int x=point.X*32+16,y=(map.Height-1-point.Z)*32+24;var masked=nativeOcclusion.ApplyGroundBody(visual,x,y,y+cell.Y);int left=Enumerable.Range(0,raw.Width*raw.Height).Count(p=>masked.Rgba[p*4+3]!=0);
   if(left<covered*.25||left>covered*.8)continue;subject.X=x*8;subject.Z=map.Height*256-y*8-1;camera=new(x,y-25);found=true;break;
  }
  Check(found,"original-map foreground rack has partial body occlusion");zoom=3;nativeSelection.Clear();nativeSelection.Add((subject.Slot,subject.Incarnation));
  SetNativeSampling(0);await Capture("10-foreground-nearest");SetNativeSampling(1);await Capture("11-foreground-filtered");
  animationStage="foreground-animation";subject.Body.Start(nativeData.Clips[8].Move,0);for(int i=0;i<35;i++){subject.Body.Tick(subject.Angle);await ToSignal(GetTree().CreateTimer(.066),SceneTreeTimer.SignalName.Timeout);}
  await Capture("12-foreground-motion");
  // Repeated diagnostic toggles must not alter authoritative phases or state.
  string stable=VisualStateProof();SetNativeSampling(0);nativeFineFog=false;ClearNativeFog();await Capture("13-comparison-toggle");SetNativeSampling(1);nativeFineFog=true;ClearNativeFog();Check(stable==VisualStateProof(),"diagnostic comparison preserves simulation and animation phases");
  File.WriteAllText(Path.Combine(output,"render-trace.json"),JsonSerializer.Serialize(trace));
 }
}
