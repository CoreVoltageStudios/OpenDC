using Godot;
using DarkColony.Assets;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 // Explicit diagnostic world: original unit stats/art, flat source-tile ground.
 // It does not run mission scripts or change production map/path/gameplay data.
 async Task VerifyFacingFixtures() {
  missionHarnessManual=true;nativeOmniscient=true;
  var original=nativeData;var s=original.Scenario;int count=s.Map.Width*s.Map.Height;
  var empty=Enumerable.Repeat(-1,count).ToArray();var floor=s.Map.At(23,25).BackgroundId;
  var flat=s with {Map=s.Map with{Cells=Enumerable.Repeat(new MapCell(floor,0,0),count).ToArray()},
   Path=new(s.Map.Width,s.Map.Height,new byte[65536],Enumerable.Repeat((byte)1,count).ToArray(),Array.Empty<byte>()),
   Trips=new(s.Map.Width,s.Map.Height,new byte[count],Array.Empty<byte>()),Entities=Array.Empty<ScenarioEntity>(),NextDynamicId=152,
   GroundOccupancy=empty,AirOccupancy=empty,OtherOccupancy=empty,Teams=s.Teams.Select(t=>t with{Ai=0}).ToArray()};
  nativeData=new(original.DataRoot,flat);nativeOcclusion=new(flat.Map,flat.Terrain);
  void TerrainClear(){foreach(var t in nativeTerrain.Values){DisposeNativeTexture(t.Ground);DisposeNativeTexture(t.Foreground);}nativeTerrain.Clear();}
  TerrainClear();foreach(int selector in Enumerable.Range(0,8))PrepareNativeTerrain(selector);
  using var trace=new StreamWriter(Path.Combine(output,"facing-steps.ndjson"));
  var failures=new List<string>();int moving=0,shots=0;zoom=4;
  // Literal artwork labels independently inspected in the raw-byte atlas.
  var directions=new[]{("E",1,0,0,12),("NE",1,1,32,10),("N",0,1,64,8),("NW",-1,1,96,6),("W",-1,0,128,4),("SW",-1,-1,160,2),("S",0,-1,192,0),("SE",1,-1,224,14)};
  NativeEntity New(int type) {
   ClearNativeBodies();nativeWorld=new(nativeData,0);nativeSelection.Clear();
   var e=nativeWorld.Spawn(type,0,new(30,30),"facing-fixture/"+type);e.Angle=192;
   nativeSelection.Add((e.Slot,e.Incarnation));camera=NativePosition(e);nativeWorld.RefreshSight();return e;
  }
  void Step(NativeEntity e,string label,int? expectedSuffix=null) {
   int x=e.X,z=e.Z,angle=e.Angle,frame=e.Body.Frame,delay=e.Body.Delay;var before=e.Body.Set?.Name;
   var oldIds=nativeWorld.Missiles.Select(m=>m.Id).ToHashSet();nativeWorld.Step();camera=NativePosition(e)+new Vector2(0,-35);
   var d=e.Body.Set?.Direction(e.Angle);var pose=e.Body.Pose(e.Angle);int dx=e.X-x,dz=e.Z-z;
   if(dx!=0||dz!=0){moving++;if(expectedSuffix.HasValue&&d?.Name!=nativeData.Scenario.Types[e.Type].Sprite+"MOVE"+expectedSuffix)failures.Add(label+" moving "+d?.Name);}
   int launched=nativeWorld.Events.Count(a=>a.Tick==nativeWorld.Tick&&a.Kind=="launch"&&a.Slot==e.Slot);shots+=launched;
   if(launched>0&&expectedSuffix.HasValue&&d!=null&&!d.Name.EndsWith(expectedSuffix.Value.ToString()))failures.Add(label+" firing "+d.Name);
   trace.WriteLine(JsonSerializer.Serialize(new{stage=animationStage,label,nativeWorld.Tick,e.Identity,e.Type,e.X,e.Z,worldDelta=new{dx,dz},screenDelta=new{x=dx/8d,y=-dz/8d},e.Angle,beforeAngle=angle,beforeSet=before,beforeFrame=frame,beforeDelay=delay,sequence=d?.Name,e.Body.Frame,e.Body.Delay,e.Body.Mode,e.Target,e.DestinationX,e.DestinationZ,actions=e.Actions.Select(a=>new{a.Kind,a.Angle,a.Vx,a.Vz,a.Word,a.Destination}),cells=pose?.Cells.Select(c=>new{c.Bank,c.CellId,c.X,c.Y,mirror=c.UnknownWords[3]!=0}),launched,newMissiles=nativeWorld.Missiles.Where(m=>!oldIds.Contains(m.Id)).Select(m=>new{m.Id,m.X,m.Z,m.Vx,m.Vz,m.Angle,m.Delay,m.State,m.OriginX,m.OriginZ,m.Target,m.LaunchTick})}));
  }
  foreach(int type in missionSuite=="facing-observe"?new[]{8}:new[]{0,8,69,73}) {
   foreach(var (label,dx,dz,angle,suffix) in directions) {
    var e=New(type);animationStage=$"facing-walk-{type}-{label}";
    nativeWorld.Command(new(e.Slot,e.Incarnation,OriginalOrder.MoveOnly,(30+dx*12)*256+128,(30+dz*12)*256+128));
    await AnimationFixtureTicks(78,()=>Step(e,label,suffix));
    await MissionCapture(animationStage);
   }
   foreach(var (label,dx,dz,angle,suffix) in directions) {
    var e=New(type);var t=nativeWorld.Spawn(89,1,new(30+dx*2,30+dz*2),"facing-fixture/target");
    animationStage=$"facing-fire-{type}-{label}";nativeWorld.RefreshSight();nativeWorld.Command(new(e.Slot,e.Incarnation,OriginalOrder.Attack,Target:t.Slot));
    await AnimationFixtureTicks(62,()=>Step(e,label,suffix));
    Check(nativeWorld.Events.Count(a=>a.Kind=="launch")>=2,"stationary firing repeatedly "+type+" "+label);
    Check(e.X==30*256+128&&e.Z==30*256+128,"stationary firing retains position "+type+" "+label);
    await MissionCapture(animationStage);
   }
   {
    var e=New(type);animationStage=$"facing-transitions-{type}";
    void Move(int x,int z)=>nativeWorld.Command(new(e.Slot,e.Incarnation,OriginalOrder.MoveOnly,x*256+128,z*256+128));
    Move(42,30);await AnimationFixtureTicks(24,()=>Step(e,"start E"));
    Move(42,30);await AnimationFixtureTicks(12,()=>Step(e,"repeat E"));
    Move(30,42);await AnimationFixtureTicks(32,()=>Step(e,"replace N/NW segment"));
    Move(30,18);await AnimationFixtureTicks(35,()=>Step(e,"reverse S"));
    nativeWorld.Command(new(e.Slot,e.Incarnation,OriginalOrder.Stop));await AnimationFixtureTicks(28,()=>Step(e,"stop"));
    var t=nativeWorld.Spawn(89,1,new(e.Cell.X+7,e.Cell.Z),"facing-fixture/east-target");nativeWorld.RefreshSight();
    nativeWorld.Command(new(e.Slot,e.Incarnation,OriginalOrder.Attack,Target:t.Slot));await AnimationFixtureTicks(90,()=>Step(e,"move to attack E"));
    var other=nativeWorld.Spawn(89,1,new(e.Cell.X,e.Cell.Z+2),"facing-fixture/north-target");nativeWorld.RefreshSight();
    nativeWorld.Command(new(e.Slot,e.Incarnation,OriginalOrder.Attack,Target:other.Slot));await AnimationFixtureTicks(65,()=>Step(e,"switch target N"));
    Check(nativeWorld.Events.Any(a=>a.Kind=="launch"&&a.Other==other.Slot),"target switch reaches new target "+type);
    await MissionCapture(animationStage);
   }
  }
  File.WriteAllText(Path.Combine(output,"facing-checks.json"),JsonSerializer.Serialize(new{classification="explicit flat diagnostic world, actual native commands and renderer; not mission",moving,shots,passed=failures.Count==0,failures=failures.Distinct().ToArray()},new JsonSerializerOptions{WriteIndented=true}));
  nativeData=original;nativeOcclusion=new(s.Map,s.Terrain);TerrainClear();foreach(int selector in Enumerable.Range(0,8))PrepareNativeTerrain(selector);
  await Send(Key.R);missionHarnessManual=false;animationStage="facing-fixtures-restarted";await MissionCapture(animationStage);
  if(missionSuite!="facing-observe")Check(failures.Count==0,"independent artwork headings for all moving/firing samples");
 }
}
