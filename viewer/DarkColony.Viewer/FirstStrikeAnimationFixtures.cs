using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 int animationFixtureTick;
 async Task AnimationFixtureTicks(int count,Action step) {
  double elapsed=0;int done=0;
  while(done<count){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);elapsed+=GetProcessDeltaTime();while(elapsed>.066&&done<count){elapsed-=.066;step();done++;animationFixtureTick++;}QueueRedraw();}
 }
 async Task VerifyAnimationFixtures() {
  // Explicit source-animation rack on original map: not mission execution/outcomes.
  missionHarnessManual=true;nativeOmniscient=true;
  foreach(var e in nativeWorld.Entities.ToArray())nativeWorld.Retire(e);
  var cast=new[]{0,8,69,73,82,89,92,93}.Select((type,i)=>nativeWorld.Spawn(type,0,new(21+i%4*4,25+i/4*5),"animation-fixture/"+type)).ToArray();
  foreach(var e in cast)e.Height=0;
  camera=new(28*32,(nativeData.Scenario.Map.Height-28)*32);zoom=2.4f;
  foreach(int direction in Enumerable.Range(0,16)) {
   animationStage=$"fixture-walk-direction-{direction:D2}";
   foreach(var e in cast){e.Angle=direction*16;e.Body.Start(nativeData.Clips[e.Type].Stand,2);e.Body.Start(nativeData.Clips[e.Type].Move,0);for(int i=0;i<Array.IndexOf(cast,e)%4;i++)e.Body.Tick(e.Angle);}
   await AnimationFixtureTicks(52,()=>{foreach(var e in cast)e.Body.Tick(e.Angle);});
   Check(cast.All(e=>e.Body.Mode==0),"rendered looping cast direction"+direction);
  }
  animationStage="fixture-idle-fire-idle";
  foreach(var e in cast)e.Body.Start(nativeData.Clips[e.Type].Stand,0);
  await AnimationFixtureTicks(20,()=>{foreach(var e in cast)e.Body.Tick(e.Angle);});
  for(int variant=0;variant<cast.Max(e=>nativeData.Clips[e.Type].Fire.Length);variant++) {
   animationStage="fixture-fire-variant-"+variant;
   foreach(var e in cast){var fire=nativeData.Clips[e.Type].Fire;e.Body.Start(fire[Math.Min(variant,fire.Length-1)],1);}
   await AnimationFixtureTicks(55,()=>{foreach(var e in cast){e.Body.Tick(e.Angle);if(e.Body.Mode==2)e.Body.Start(nativeData.Clips[e.Type].Stand,0);}});
  }
  for(int variant=0;variant<7;variant++) {
   animationStage="fixture-blood-variant-"+variant;
   foreach(var e in cast){var sets=nativeData.Clips[e.Type].Blood;if(sets.Length>variant)e.Blood.Start(sets[variant],1);}
   await AnimationFixtureTicks(100,()=>{foreach(var e in cast){e.Body.Tick(e.Angle);e.Blood.Tick(e.Angle);}});
   Check(cast.All(e=>e.Blood.Mode==2),"blood one-shot retirement variant"+variant);
  }
  animationStage="fixture-impact";
  var impact=new NativeMissile{Id=999,Shooter=cast[2].Slot,Weapon=62,X=cast[2].X,Z=cast[2].Z,State=2};impact.Animation.Start(nativeData.MissileImpacts[62]!,1);nativeWorld.Missiles.Add(impact);
  await AnimationFixtureTicks(25,()=>{impact.Animation.Tick(0);if(impact.Animation.Mode==2)nativeWorld.Missiles.Remove(impact);});
  Check(!nativeWorld.Missiles.Contains(impact),"rendered original SMOK impact completes and retires");
  for(int variant=0;variant<cast.Max(e=>nativeData.Clips[e.Type].Die.Length);variant++) {
   animationStage="fixture-death-variant-"+variant+"-and-critical-hold";
   foreach(var e in cast){var clips=nativeData.Clips[e.Type];e.Body.Start(nativeWorld.IsCommander(e)?clips.Critical:clips.Die[Math.Min(variant,clips.Die.Length-1)],nativeWorld.IsCommander(e)?3:1);}
   int duration=cast.Max(e=>{var set=e.Body.Set!;var d=set.Direction(e.Angle);return Enumerable.Range(d.First+1,d.Last-d.First).Sum(i=>NativeAnimation.DurationTicks(set.Asset.Frames[i].Duration))+2;});
   await AnimationFixtureTicks(duration,()=>{foreach(var e in cast)e.Body.Tick(e.Angle);});
  }
  Check(cast.All(e=>nativeWorld.IsCommander(e)?e.Body.Mode==3:e.Body.Mode==2),"death completes while commander critical pose holds");
  Check(cast.Where(e=>!nativeWorld.IsCommander(e)).All(e=>nativeBodies[e.Slot].Parts.Count==0),"completed bodies and attachments submit no textures");
  animationStage="fixture-hidden-progress-reveal";
  foreach(var e in cast){e.Owner=1;e.Body.Start(nativeData.Clips[e.Type].Move,0);}
  nativeOmniscient=false;nativeWorld.Sight.ClearCurrent();await AnimationFixtureTicks(17,()=>{foreach(var e in cast)e.Body.Tick(e.Angle);});
  Check(cast.All(e=>!nativeDrawn.Contains((e.Slot,e.Incarnation))),"hidden entities submit no body/effects/shadows");
  var before=cast.Select(e=>(e.Body.Frame,e.Body.Delay)).ToArray();nativeOmniscient=true;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
  Check(cast.Select(e=>(e.Body.Frame,e.Body.Delay)).SequenceEqual(before),"visibility change does not rebind or restart phase");
  await AnimationFixtureTicks(30,()=>{foreach(var e in cast)e.Body.Tick(e.Angle);});
  animationStage="fixture-slot-reuse";var old=cast[0];int slot=old.Slot,inc=old.Incarnation;nativeWorld.Retire(old);var reused=nativeWorld.Spawn(8,0,new(21,25),"animation-fixture/reused");
  Check(reused.Slot==slot&&reused.Incarnation==inc+1&&reused.Blood.Mode==2,"rendered reuse identity fresh");
  reused.Body.Start(nativeData.Clips[8].Move,0);await AnimationFixtureTicks(32,()=>reused.Body.Tick(0));
  await MissionCapture("fixture-reused");
  await Send(Key.R);Check(nativeWorld.Tick==0&&nativeWorld.Entities.All(e=>!e.SourceIdentity.StartsWith("animation-fixture/")&&e.Body.Frame==0&&e.Body.Delay==0&&e.Blood.Mode==2)&&nativeBodies.Keys.All(k=>nativeWorld.Slots[k] is {State:not 0}),"restart restores fresh original phases and only current render instances");missionHarnessManual=false;animationStage="fixture-restarted";
  await MissionCapture("fixture-restarted");
 }
}
