using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Security.Cryptography;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool nativeSoftFog=true,nativeShadows=true;
 readonly HashSet<(int Slot,int Incarnation)> nativeDrawn=new();
 string VisualStateProof()=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{nativeWorld.Tick,nativeWorld.Phase,nativeWorld.Coefficient,nativeWorld.Random.Index,nativeWorld.Random.Draws,nativeWorld.Ground,nativeWorld.Air,nativeWorld.Other,entities=nativeWorld.Entities.ToArray(),nativeWorld.Events,nativeWorld.Sight.Visible,nativeWorld.Sight.Explored,missionEvents=mission.Events,mission.Outcome},new JsonSerializerOptions{IncludeFields=true}))));
 async Task VerifyVisualPresentation() {
  // Accelerated and state-injected presentation fixtures, separate from full mission runs.
  missionHarnessManual=true;
  void Advance(long tick){while(nativeWorld.Tick<tick){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();DispatchMissionSounds();}}
  Advance(16);var transport=nativeWorld.Slots[7]!;
  camera=NativePosition(transport)+new Vector2(0,transport.Height/16f);zoom=.7f;
  await MissionCapture("visual-transport-high");
  Advance(45);camera=NativePosition(transport)+new Vector2(0,transport.Height/16f);zoom=1.3f;await MissionCapture("visual-transport-low");
  Advance(180);await Send(Key.Tab);zoom=2;
  nativeSoftFog=false;nativeShadows=false;ClearNativeBodies();string proof=VisualStateProof();await MissionCapture("visual-before");
  nativeSoftFog=true;nativeShadows=true;ClearNativeBodies();await MissionCapture("visual-after");
  Check(proof==VisualStateProof(),"fog/shadow rendering leaves authoritative state RNG occupancy sight and events unchanged");
  Check(nativeDrawn.All(id=>nativeWorld.Slots[id.Slot] is {} e&&NativeVisible(e)),"hidden bodies and attached shadows never submitted");
  await Send(Key.Right);await Send(Key.Plus);await MissionCapture("visual-pan-zoom");await Send(Key.Left);await Send(Key.Minus);
  await Send(Key.Tab);zoom=2;await MissionCapture("visual-camera-return");
  // Exact same paused state gives an identical mask after pan/zoom round trip.
  Check(proof==VisualStateProof(),"camera and zoom preserve authoritative state");
  int phase=nativeWorld.Phase;while(nativeWorld.Phase==phase)Advance(nativeWorld.Tick+1);Advance(nativeWorld.Tick+nativeData.Scenario.Header.Ramp);
  await Send(Key.Tab);await MissionCapture("visual-day");
  Check(nativeWorld.Phase!=phase,"actual native day transition reached in accelerated fixture");
  var own=nativeWorld.Entities.First(e=>e.Type==8&&e.Owner==0);own.State=10;own.DeathWord=149;nativeWorld.RefreshSight();await MissionCapture("visual-death-sight-fixture");
  // Narrow/map-edge mask stress. No claim these injected sight cells are original gameplay.
  nativeWorld.Sight.ClearCurrent();foreach(var explored in nativeWorld.Sight.Explored)Array.Clear(explored);
  for(int x=0;x<15;x++){nativeWorld.Sight.Explored[0][x]=true;if(x<10)nativeWorld.Sight.Visible[0][x]=true;}
  camera=new(7*32, nativeData.Scenario.Map.Height*32-16);zoom=2;
  await MissionCapture("visual-narrow-map-edge-fixture");Check(nativeDrawn.All(id=>nativeWorld.Slots[id.Slot]!.Owner==0),"no enemy or transport shadow from hidden source in narrow fixture");
  await Send(Key.R);Check(nativeFogState.All(s=>s==0)&&nativePrevious.Count==0,"restart clears fog and interpolation history before fresh sight");
  // Normal-time moving squad clips. Commands are Godot-dispatched; initial opening is accelerated.
  foreach(bool interpolation in new[]{false,true}) {
   await Send(Key.Enter);Advance(180);missionWall=mission.Milliseconds/1000d;nativeAccumulator=0;
   nativeInterpolation=interpolation;missionHarnessManual=false;
   await SelectWholeSquad();await Send(Key.Tab);zoom=2;
   await ClickNativeCell(MouseButton.Right,new(28,20));
   string label=interpolation?"motion-after":"motion-before",folder=Path.Combine(output,label);Directory.CreateDirectory(folder);
   var samples=new List<object>();
   for(int frame=0;frame<90;frame++) {
    await ToSignal(GetTree().CreateTimer(1d/30),SceneTreeTimer.SignalName.Timeout);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
    using var image=GetViewport().GetTexture().GetImage();image.Resize(960,540,Image.Interpolation.Lanczos);image.SavePng(Path.Combine(folder,$"frame-{frame:D3}.png"));
    var commander=nativeWorld.Entities.First(e=>e.Type==73&&e.Owner==0);var position=NativeRenderPosition(commander);
    samples.Add(new{frame,nativeWorld.Tick,mission.Milliseconds,commander.X,commander.Z,renderX=position.Body.X,renderY=position.Body.Y});
   }
   File.WriteAllText(Path.Combine(folder,"samples.json"),JsonSerializer.Serialize(samples));
   Check(nativeWorld.Tick>215,"normal-time clip simulation progress "+label);
   await Send(Key.R);missionHarnessManual=true;
  }
  nativeInterpolation=true;missionHarnessManual=false;
 }
}
