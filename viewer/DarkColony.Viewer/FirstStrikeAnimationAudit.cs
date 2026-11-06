using Godot;
using DarkColony.Assets;
using DarkColony.Original;
using System.Text.Json;
using System.Security.Cryptography;
namespace DarkColony.Viewer;

public partial class Viewer {
 StreamWriter? animationTrace;
 StreamWriter? animationProfile;bool animationTerrainBuilt,animationFogBuilt,animationCaptureActive;int animationBodyBuilds;
 readonly Dictionary<ulong,object> animationTextureProof=new();
 readonly List<object> animationDraws=new();
 long inspectionTraceArtTick=-1;Vector2 inspectionTraceOffset;float inspectionTraceZoom;
 ulong animationLastDraw,animationDrawStart;long animationDrawNumber;double animationDelta;
 string animationStage="ordinary-launch";
 void PrepareAnimationAudit(string[] args) {
  if(capturing||args.Contains("--animation-trace")) {animationProfile=new(Path.Combine(output,"frame-times.csv"));animationProfile.WriteLine("frame,wall_usec,delta_ms,draw_ms,tick,coefficient,terrain_build,fog_build,body_builds,capture_active,stage");}
  if(!args.Contains("--animation-trace"))return;
  animationTrace=new(Path.Combine(output,"animation-draws.ndjson"));
  File.WriteAllText(Path.Combine(output,"animation-build.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,assembly=Path.Combine(root,"viewer/DarkColony.Viewer/.godot/mono/temp/bin/Debug/DarkColony.Viewer.dll"),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"viewer/DarkColony.Viewer/.godot/mono/temp/bin/Debug/DarkColony.Viewer.dll")))),original=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"private/DC/DC16.EXE")))),classification="draw-call trace; external continuous capture required"}));
 }
 void AuditTexture(Texture2D texture,Raster raster) {
  if(animationTrace!=null)animationTextureProof[texture.GetInstanceId()]=new{raster.Width,raster.Height,sha256=Convert.ToHexString(SHA256.HashData(raster.Rgba))};
 }
 object? TextureProof(Texture2D t)=>animationTextureProof.GetValueOrDefault((t is CanvasTexture c?c.DiffuseTexture:t).GetInstanceId());
 object AnimationProof(NativeAnimation a,int angle) {
  var d=a.Set?.Direction(angle);var f=a.Pose(angle);
  return new{set=a.Set?.Name,sequence=d?.Name,a.Frame,a.Delay,a.Mode,absolute=d==null?-1:d.First+a.Frame,rawDuration=f?.Duration,cells=f?.Cells.Select(c=>new{c.Bank,c.CellId,c.X,c.Y,mirror=c.UnknownWords[3]!=0,words=c.UnknownWords})};
 }
 void BeginAnimationDraw(){if(animationTrace==null&&animationProfile==null)return;animationDrawStart=Time.GetTicksUsec();animationDraws.Clear();animationTerrainBuilt=animationFogBuilt=false;animationBodyBuilds=0;}
 void AuditEntityDraw(NativeEntity e,Vector2 at,float height,List<NativePart> parts) {
  if(animationTrace==null)return;
  animationDraws.Add(new{kind="entity",e.Identity,e.Slot,e.Incarnation,e.Type,e.Owner,e.State,e.Angle,e.X,e.Z,e.Height,action=e.Actions.LastOrDefault()?.Kind,e.DeathWord,e.Target,e.DestinationX,e.DestinationZ,actions=e.Actions.Select(a=>new{a.Kind,a.Angle,a.Vx,a.Vz,a.Word,a.Destination}),projected=OriginalCoordinates.Project(e.X,e.Z,e.Height,nativeData.Scenario.Map.Height),display=new{x=at.X,y=at.Y,height},body=AnimationProof(e.Body,e.Angle),blood=AnimationProof(e.Blood,e.Angle),overlay=AnimationProof(e.Overlay,e.Angle),parts=parts.Select(p=>new{texture=p.Texture.GetInstanceId(),x=NativeArtworkBounds(p.Texture,at+p.Offset).Position.X,y=NativeArtworkBounds(p.Texture,at+p.Offset).Position.Y,width=NativeArtworkBounds(p.Texture,at+p.Offset).Size.X,height=NativeArtworkBounds(p.Texture,at+p.Offset).Size.Y,pixels=TextureProof(p.Texture)}).ToArray()});
 }
 void AuditMissileDraw(NativeMissile m,Vector2 at,FrameCell cell,Texture2D texture,Vector2 offset) {
  if(animationTrace==null)return;
  animationDraws.Add(new{kind="missile",m.Id,m.State,m.Weapon,m.X,m.Z,m.Height,animation=AnimationProof(m.Animation,m.Angle),cell.Bank,cell.CellId,x=at.X+offset.X,y=at.Y+offset.Y,pixels=TextureProof(texture)});
 }
 void EndAnimationDraw() {
  if(animationTrace==null&&animationProfile==null)return;ulong now=Time.GetTicksUsec();
  animationProfile?.WriteLine(FormattableString.Invariant($"{animationDrawNumber},{now},{animationDelta*1000:F4},{(now-animationDrawStart)/1000d:F4},{nativeWorld.Tick},{nativeWorld.Coefficient},{animationTerrainBuilt},{animationFogBuilt},{animationBodyBuilds},{animationCaptureActive},{animationStage}"));
  if(animationDrawNumber%60==0)animationProfile?.Flush();
  if(animationTrace==null){animationDrawNumber++;return;}
  if(originalStateMode){if(inspectionTraceArtTick==inspectionArtTick&&inspectionTraceOffset==NativeOffset&&inspectionTraceZoom==zoom){animationDrawNumber++;return;}inspectionTraceArtTick=inspectionArtTick;inspectionTraceOffset=NativeOffset;inspectionTraceZoom=zoom;}
  DrawString(font,new Vector2(20,GetViewportRect().Size.Y-105),$"ANIMATION AUDIT {animationDrawNumber} / T{nativeWorld.Tick} / {animationStage}",fontSize:14,modulate:Colors.Yellow);
  animationTrace.WriteLine(JsonSerializer.Serialize(new{frame=animationDrawNumber++,wallUsec=now,intervalUsec=animationLastDraw==0?0:now-animationLastDraw,drawUsec=now-animationDrawStart,delta=animationDelta,stage=animationStage,fixtureTick=animationFixtureTick,tick=nativeWorld.Tick,clock=firstStrike?mission.Milliseconds:0,artTick=originalStateMode?inspectionArtTick:0,accumulator=nativeAccumulator,briefing=missionBriefing,manual=missionHarnessManual,zoom,offset=new{x=NativeOffset.X,y=NativeOffset.Y},draws=animationDraws,spriteOrder=nativeSpriteQueue.Select(p=>new{p.Slot,p.Ordinal,p.Layer,p.Key,p.Submission}),missiles=nativeWorld.Missiles.Select(m=>new{m.Id,m.Shooter,m.ShooterIncarnation,m.Weapon,m.X,m.Z,m.Height,m.Vx,m.Vz,m.Angle,m.Delay,m.Age,m.State,m.OriginX,m.OriginZ,m.Target,m.LaunchTick})}));
  animationLastDraw=now;if(animationDrawNumber%60==0)animationTrace.Flush();
 }
 async Task VerifyAnimationReproduction() {
  animationStage="normal-opening";await WaitMissionTick(180);
  await SelectWholeSquad();await Send(Key.Tab);zoom=2;
  animationStage="normal-movement";await ClickNativeCell(MouseButton.Right,new(28,20));await WaitMissionTick(nativeWorld.Tick+100);
  // Restart after movement to use the retained mixed-attack reproduction route.
  await Send(Key.R);await Send(Key.Enter);animationStage="normal-enemy-reveal";await VerifyMixedAttack();
  await Send(Key.Tab);zoom=2;animationStage="normal-combat";await WaitMissionTick(nativeWorld.Tick+160);
  Check(nativeWorld.Events.Any(e=>e.Kind=="launch"),"ordinary orders produce combat");
  Check(nativeWorld.Slots[152]!.Cell==new Cell(23,25),"stationary CENT keeps its cell after mixed explicit attack");
  await MissionCapture("animation-combat");
 }
}
