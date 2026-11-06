using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCameraOverscroll(){
  StartWarMatch(WarConfiguration.Default("D4PLAY04",0));missionHarnessManual=true;edgeScrolling=false;nativeOmniscient=true;
  GetWindow().Size=GetViewportRect().Size.X>2000?new(1720,720):new(1280,720);
  var map=nativeData.Scenario.Map;var extent=new Vector2(map.Width*32,map.Height*32);
  var unit=nativeWorld.Entities.First(e=>e.Owner==LocalTeam&&e.Living&&nativeWorld.IsCommander(e));
  nativeSelection.Clear();nativeSelection.Add((unit.Slot,unit.Incarnation));
  var receipts=new List<object>();
  foreach(int aspect in new[]{0,16})foreach(float scale in new[]{1f,1.5f}){
   maximumAspect=aspect;ui=scale;ApplyAspectLimit();await WarFrames(3);zoom=2;
   foreach(var corner in new[]{new Vector2(16,16),new Vector2(extent.X-16,16),new Vector2(16,extent.Y-16),extent-new Vector2(16,16)}){
    camera=corner;ClampMissionCamera();await WarFrames(2);
    string tag=$"corner-{aspect}-{scale}-{(corner.X>32?1:0)}{(corner.Y>32?1:0)}";
    await MenuCapture(tag);Check(camera.DistanceTo(corner)<.01,"Map-edge cell can be centered: "+tag);
    Vector2 center=NativeArea.GetCenter(),world=(center-NativeOffset)/zoom;
    Check(world.DistanceTo(corner)<.01&&NativeCell(world)==NativeCell(corner),"World/screen/cell transform at corner");
    Check((GetViewport().GetFinalTransform().AffineInverse()*NativeWindowPoint(center)).DistanceTo(center)<.01,"Window/viewport roundtrip");
    Vector2 black=new(corner.X<32?NativeArea.Position.X+20:NativeArea.End.X-20,NativeArea.GetCenter().Y);
    Check(!nativeWorld.Navigation.Inside(NativeCell((black-NativeOffset)/zoom)),"Black sample is outside playable map");
    using(var image=GetViewport().GetTexture().GetImage()){var color=image.GetPixel((int)black.X,(int)black.Y);Check(color.R<.005&&color.G<.005&&color.B<.005,"Outside map is black");}
    var snapshot=mission.Snapshot().ToJsonString();var selection=nativeSelection.ToArray();
    await NativeMouse(MouseButton.Right,black);await NativeMouse(MouseButton.Left,black);await WarFrames(2);
    Check(snapshot==mission.Snapshot().ToJsonString()&&selection.SequenceEqual(nativeSelection),"Black-area clicks neither order nor clear selection");
    targetMode="move";await NativeMouse(MouseButton.Left,black);Check(targetMode=="move"&&snapshot==mission.Snapshot().ToJsonString(),"Targeted move ignores black area");targetMode="";
    receipts.Add(new{tag,camera=new[]{camera.X,camera.Y},world=new[]{world.X,world.Y},area=NativeArea.ToString()});
   }
   if(aspect==16&&GetWindow().Size.X>GetWindow().Size.Y*16/9f){
    var snapshot=mission.Snapshot().ToJsonString();var selection=nativeSelection.ToArray();
    // NativeMouse maps these outside-viewport coordinates into real window pillars.
    foreach(float x in new[]{-20f,GetViewportRect().Size.X+20}){
     Vector2 pillarPoint=new(x,GetViewportRect().Size.Y/2),window=NativeWindowPoint(pillarPoint);
     Check(window.X>=0&&window.X<GetWindow().Size.X,"Pillar click is inside physical window");
     await NativeMouse(MouseButton.Left,pillarPoint);await NativeMouse(MouseButton.Right,pillarPoint);
    }
    Check(snapshot==mission.Snapshot().ToJsonString()&&selection.SequenceEqual(nativeSelection),"Both display pillars reject battlefield input");
   }
  }
  camera=NativePosition(unit);ClampMissionCamera();await WarFrames(3);
  Vector2 point=NativePosition(unit)*zoom+NativeOffset;nativeSelection.Clear();await NativeMouse(MouseButton.Left,point);
  Check(NativeSelected().Any(e=>e==unit),"Unit selection still uses displayed position");
  var destination=new Cell(unit.Cell.X+1,unit.Cell.Z);Vector2 worldTarget=new(destination.X*32+16,(map.Height-1-destination.Z)*32+16);
  int orders=nativeWorld.Events.Count(e=>e.Kind=="order");await NativeMouse(MouseButton.Right,worldTarget*zoom+NativeOffset);
  Check(nativeWorld.Events.Count(e=>e.Kind=="order")>orders&&orderPoint.DistanceTo(worldTarget)<.01,"Valid battlefield click still issues mapped move order");
  File.WriteAllText(Path.Combine(output,"overscroll.json"),JsonSerializer.Serialize(receipts));
 }
}
