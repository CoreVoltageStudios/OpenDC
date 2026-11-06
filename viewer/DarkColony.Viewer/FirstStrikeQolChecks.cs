using Godot;
using DarkColony.Original;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyQol(){
  missionHarnessManual=true;var timing=new List<object>();
  if(missionSuite!="qol-visual"){
  string settings=Path.Combine(output,"qol-settings-fixture.json");File.WriteAllText(settings,"{\"worldArtwork\":\"Crisp\",\"edgeScrolling\":false,\"edgeScrollSpeed\":1.75}");
  Check(ArtworkPreference.LoadFast(settings),"old settings without speed default to Fast");ArtworkPreference.SaveFast(settings,false);
  Check(!ArtworkPreference.LoadFast(settings)&&ArtworkPreference.Load(settings,out _)==ArtworkStyle.Crisp&&ArtworkPreference.LoadNavigation(settings,out _)==(false,1.75f),"speed save preserves artwork and navigation preferences");
  Check(AdmittedMissionTime(20,true)==.1*1.5&&AdmittedMissionTime(-1,true)==0,"stall time capped before speed multiplication");
  foreach(bool fast in new[]{false,true}){
   fastGame=fast;long start=nativeWorld.Tick;double began=Time.GetTicksMsec();long music=missionMusic!.Frame;
   missionHarnessManual=false;await ToSignal(GetTree().CreateTimer(5),SceneTreeTimer.SignalName.Timeout);missionHarnessManual=true;
   double elapsed=(Time.GetTicksMsec()-began)/1000;long ticks=nativeWorld.Tick-start;
   Check(Math.Abs(ticks-elapsed*(fast?1.5:1)/.066)<3,"measured normal-time simulation rate "+fast);
   Check(Math.Abs((missionMusic.Frame-music)/44100d-elapsed)<.3,"music playback remains normal speed "+fast);
   timing.Add(new{fast,elapsed,ticks,musicFrames=missionMusic.Frame-music});
  }
  // Equivalent tick scheduled inputs, independent frame-rate/speed batching.
  string Proof(double speed){var m=new OriginalMission(nativeData,0);double acc=0;while(m.World.Tick<1500){acc+=1d/120*speed;while(acc>.066&&m.World.Tick<1500){acc-=.066;m.SetClock((m.World.Tick+1)*66);m.Step();if(m.World.Tick%120==0)foreach(var e in m.World.Entities.Where(e=>e.Living&&e.Owner==0&&e.Type is 8 or 73))m.World.Command(new(e.Slot,e.Incarnation,OriginalOrder.MoveAttack,7*256+128,47*256+128));}}return JsonSerializer.Serialize(new{m.World.Tick,m.World.Phase,m.World.DayCounter,m.World.Random.Draws,m.Events,nativeEvents=m.World.Events,entities=m.World.Entities.Select(e=>new{e.Slot,e.Type,e.Owner,e.X,e.Z,e.Health,e.State,frame=e.Body.Frame})});}
  var classic=Proof(1);var fastProof=Proof(1.5);Check(classic==fastProof,"equivalent tick state including combat, animation, RNG and scripts");
  File.WriteAllText(Path.Combine(output,"qol-equivalence.json"),JsonSerializer.Serialize(new{ticks=1500,classic=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(classic))),fast=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fastProof)))}));
  // Isolated day/night clock boundary, accelerated ticks, not a mission playthrough.
  string DayProof(double speed){var w=new OriginalWorld(nativeData,0);double acc=0;int end=nativeData.Scenario.Header.Period+2;while(w.Tick<end){acc+=1d/120*speed;while(acc>.066&&w.Tick<end){acc-=.066;w.Step();}}return JsonSerializer.Serialize(new{w.Tick,w.Phase,w.DayCounter,w.Coefficient});}
  Check(DayProof(1)==DayProof(1.5),"day/night crossing identical at equivalent ticks");
  }
  await SelectWholeSquad();zoom=2;nativeOmniscient=true;
  foreach(float scale in new[]{1f,1.25f,1.5f}){
   ui=scale;var savedCamera=camera;
   foreach(int variant in new[]{1,2,0}){miniComparison=variant;await UiCapture($"qol-minimap-{scale:0.##}-variant-{variant}");}
   int orders=nativeWorld.Events.Count(e=>e.Kind=="order");
   foreach(var point in new[]{MiniMapRect.Position+Vector2.One,MiniMapRect.End-Vector2.One,MiniMapRect.GetCenter()}){
    await NativeMouse(MouseButton.Left,point);var expected=(point-MiniMapRect.Position)/MiniMapRect.Size*new Vector2(nativeData.Scenario.Map.Width*32,nativeData.Scenario.Map.Height*32);
    var half=NativeArea.Size/(2*zoom);expected.X=Math.Clamp(expected.X,half.X,nativeData.Scenario.Map.Width*32-half.X);expected.Y=Math.Clamp(expected.Y,half.Y,nativeData.Scenario.Map.Height*32-half.Y);
    GD.Print($"QOL_MINIMAP_INPUT scale={scale} point={point} expected={expected} camera={camera} pointer={missionPointer} rect={MiniMapRect}");
    Check(camera.DistanceTo(expected)<1,"minimap navigation/clamping at scale "+scale);
   }
   var start=MiniMapRect.Position+Vector2.One;var end=MiniMapRect.GetCenter();
   Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=NativeWindowPoint(start),Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   Input.ParseInputEvent(new InputEventMouseMotion{Position=NativeWindowPoint(MiniMapRect.End-Vector2.One),ButtonMask=MouseButtonMask.Left});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Position=NativeWindowPoint(end),Pressed=false});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   Check(camera.DistanceTo(new Vector2(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16))<1&&!minimapDragging,"minimap release uses actual endpoint after intervening motion "+scale);
   Check(nativeWorld.Events.Count(e=>e.Kind=="order")==orders,"minimap navigation does not issue world commands "+scale);camera=savedCamera;
  }
  miniLegacyCanvas?.Dispose();miniLegacyImage?.Dispose();miniLegacyCanvas=null;miniLegacyImage=null;
  int builds=miniBuilds,uploads=miniUploads;for(int i=0;i<20;i++)UpdateClearMinimap(miniSelector);
  Check(miniBuilds==builds&&miniUploads==uploads,"unchanged map reuses cached texture without uploads");
  File.WriteAllText(Path.Combine(output,"qol-minimap-performance.json"),JsonSerializer.Serialize(new{miniBuildMilliseconds,miniBuildBytes,miniBuilds,miniUploads,width=miniTexture!.GetWidth(),height=miniTexture.GetHeight()}));
  nativeOmniscient=false;await UiCapture("qol-fog");
  await Send(Key.O);await UiClick("speed-classic");Check(!ArtworkPreference.LoadFast(artworkPreferencePath),"Classic preference persisted");await UiClick("speed-fast");Check(ArtworkPreference.LoadFast(artworkPreferencePath),"Fast preference persisted");await UiCapture("qol-options");await Send(Key.Escape);
  File.WriteAllText(Path.Combine(output,"qol-timing.json"),JsonSerializer.Serialize(timing));
  if(missionSuite=="qol-visual")return;
  await VerifyQolSwitching();
  fastGame=true;await Send(Key.R);await Send(Key.Enter);await VerifyReinforcementRoute();
 }

 async Task VerifyQolSwitching(){
  await Send(Key.R);await Send(Key.Enter);missionHarnessManual=true;
  while(nativeWorld.Tick<180){mission.SetClock((nativeWorld.Tick+1)*66);mission.Step();DispatchMissionSounds();}
  await Send(Key.Tab);var mover=NativeSelected().Single();zoom=2;
  var enemy=nativeWorld.Spawn(0,1,new(mover.Cell.X+4,mover.Cell.Z),"diagnostic/qol-target");nativeWorld.RefreshSight();
  var trace=new List<object>();var recorder=new AudioEffectRecord{Format=AudioStreamWav.FormatEnum.Format16Bits};AudioServer.AddBusEffect(0,recorder);recorder.SetRecordingActive(true);
  foreach(bool fighting in new[]{false,true}){
   if(fighting)UiOrder(OriginalOrder.Attack,target:enemy.Slot);else UiOrder(OriginalOrder.MoveOnly,new(mover.Cell.X+3,mover.Cell.Z));
   foreach(bool fast in new[]{false,true,false,true}){
    double phase=nativeAccumulator;int frame=mover.Body.Frame;long tick=nativeWorld.Tick;
    ActivateMissionUi(fast?"speed-fast":"speed-classic");
    Check(nativeAccumulator==phase&&mover.Body.Frame==frame&&nativeWorld.Tick==tick,"switch does not reset interpolation/animation or skip ticks");
    missionHarnessManual=false;var until=Time.GetTicksMsec()+700;
    while(Time.GetTicksMsec()<until){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);trace.Add(new{wall=Time.GetTicksMsec(),fast,fighting,nativeWorld.Tick,mover.X,mover.Z,mover.State,frame=mover.Body.Frame,mover.Body.Delay,sequence=mover.Body.Set?.Name,alpha=nativeAccumulator/.066,confirmation=orderAnimation.Frame,cursor=cursorAnimation.Frame,drawn=nativeDrawn.Contains((mover.Slot,mover.Incarnation)),music=missionMusic!.Frame});}
    missionHarnessManual=true;
   }
  }
  Check(nativeWorld.Events.Any(e=>e.Kind=="launch"&&e.Tick>=180),"speed switching fixture includes real firing");
  Check(trace.Count>50,"continuous drawn motion trace collected across switches");
  recorder.SetRecordingActive(false);using var audio=recorder.GetRecording();audio.SaveToWav(Path.Combine(output,"qol-switching-mixer.wav"));AudioServer.RemoveBusEffect(0,AudioServer.GetBusEffectCount(0)-1);recorder.Dispose();
  File.WriteAllText(Path.Combine(output,"qol-switching-trace.json"),JsonSerializer.Serialize(trace));
  await UiCapture("qol-switching-combat");
 }
 // Diagnostic-only retained old center-texel path, for matched before/after comparisons.
 ImageTexture? miniLegacyImage;CanvasTexture? miniLegacyCanvas;
 void DrawMinimapComparison(Rect2 rect,int selector){
  if(miniLegacyImage==null){
   var map=nativeData.Scenario.Map;var pixels=new byte[map.Width*map.Height*4];var palette=GameColors.Table(nativePalette,selector,16,ColorBank.Terrain);var tiles=nativeData.Scenario.Terrain.ByEditorId;
   for(int z=0;z<map.Height;z++)for(int x=0;x<map.Width;x++){var c=map.At(x,z);int index=tiles[c.BackgroundId].Indices[16*32+((c.Attributes&0x20)!=0?15:16)];if(c.ForegroundId!=0){int f=tiles[c.ForegroundId].Indices[16*32+((c.Attributes&0x40)!=0?15:16)];if(f!=0)index=f;}var rgb=palette[index];int i=((map.Height-1-z)*map.Width+x)*4;pixels[i]=(byte)rgb.R;pixels[i+1]=(byte)rgb.G;pixels[i+2]=(byte)rgb.B;pixels[i+3]=255;}
   using var im=Image.CreateFromData(map.Width,map.Height,false,Image.Format.Rgba8,pixels);miniLegacyImage=ImageTexture.CreateFromImage(im);miniLegacyCanvas=new(){DiffuseTexture=miniLegacyImage};
  }
  miniLegacyCanvas!.TextureFilter=miniComparison==1?TextureFilterEnum.Nearest:TextureFilterEnum.Linear;DrawTextureRect(miniLegacyCanvas,rect,false);
 }
}
