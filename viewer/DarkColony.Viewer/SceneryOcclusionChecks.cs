using Godot;using DarkColony.Assets;using DarkColony.Original;using System.Text.Json;
namespace DarkColony.Viewer;public partial class Viewer {
 async Task VerifySceneryOcclusion(){
  bool behind=OS.GetCmdlineUserArgs().Contains("--scenery-behind"),captureFrames=!OS.GetCmdlineUserArgs().Contains("--scenery-no-capture");
  foreach(string mapId in behind?new[]{"D4PLAY09"}:new[]{"D4PLAY09","J4PLAY01"}){
  StartWarMatch(WarConfiguration.Default(mapId,7));missionHarnessManual=true;nativeOmniscient=true;zoom=3;var w=nativeWorld;var occlusion=nativeOcclusion??throw new InvalidOperationException("Missing map foreground");var map=nativeData.Scenario.Map;
  foreach(var e in w.Entities.ToArray())w.Retire(e);
  var start=behind?new Cell(46,14):mapId=="D4PLAY09"?new Cell(45,12):new Cell(33,29);var unit=w.Spawn(0,LocalTeam,start,"diagnostic/reported-spire");if(mapId=="D4PLAY09"&&!behind){unit.X=1466*8;unit.Z=map.Height*256-3176*8-1;}unit.Angle=192;camera=mapId=="D4PLAY09"?new(1466,3145):new(36*32+16,(map.Height-1-29)*32-16);w.RefreshSight();nativeSelection.Clear();nativeSelection.Add((unit.Slot,unit.Incarnation));
  await UiCapture(mapId+"-anchor");var trace=new List<object>();int maskedCells=0;Directory.CreateDirectory(Path.Combine(output,mapId));
  for(int i=0;i<120;i++){
   if(i==0)NativeCommand(OriginalOrder.MoveOnly,mapId=="D4PLAY09"?new(47,12):new(39,29));if(i==60)NativeCommand(OriginalOrder.MoveOnly,behind?start:mapId=="D4PLAY09"?new(44,12):start);w.Step();
   if(mapId=="D4PLAY09")Check(w.Navigation.Ground(unit.Cell),"Spire path remains walkable at step "+i);
   var at=NativeRenderPosition(unit).Body;var cells=new List<object>();
   foreach(var c in unit.Body.Pose(unit.Angle)!.Cells.Where(c=>c.UnknownWords[2] is 0 or 1)){
    var sprite=nativeData.Sprite(c.Bank).Cells[c.CellId];var placement=OriginalShadow.BodyPlacement(c,sprite);var raster=GameColors.Body(sprite,NativeBodyColors(w.Teams[LocalTeam].Color));if(c.UnknownWords[3]!=0)raster=OriginalShadow.Mirror(raster);
    var visual=new VisualAsset("diagnostic",raster,sprite.Width,sprite.Height,sprite.Width,sprite.Height,-placement.X,-placement.Y);var masked=occlusion.ApplyGroundBody(visual,(int)at.X,(int)at.Y,(int)at.Y+c.Y);
    int hidden=Enumerable.Range(0,sprite.Width*sprite.Height).Count(p=>raster.Rgba[p*4+3]!=0&&masked.Rgba[p*4+3]==0);if(hidden>0)maskedCells++;
    if(mapId=="D4PLAY09"&&!behind&&i==0)Check(hidden==0,"Reported in-front pose is no longer hidden by the passed spire");
    bool may=occlusion.MayMask((int)at.X+placement.X,(int)at.Y+placement.Y,sprite.Width,sprite.Height,(int)at.Y+c.Y);Check(may||hidden==0,"Foreground cache rejection preserves mask at step "+i);
    var body=NativeParts(unit,at,0).Last();var tex=(CanvasTexture)body.Texture;using var actual=tex.DiffuseTexture.GetImage();var expected=ArtworkSampling.Prepare(masked,1,true);
    Check(actual.GetData().SequenceEqual(expected.Rgba),"Rendered body equals full foreground mask at step "+i);
    cells.Add(new{c.Bank,c.CellId,c.X,c.Y,layer=c.UnknownWords[0],hidden,may,cutoff=occlusion.Cutoff((int)at.X,(int)at.Y+c.Y)});
   }
   var canonical=mission.Snapshot();canonical.Remove("runId");var state=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical.ToJsonString())));var events=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(w.Events))));trace.Add(new{i,w.Tick,unit.X,unit.Z,unit.Angle,state,events,ground=w.Navigation.Ground(unit.Cell),cell=unit.Cell,cells});
   if(i%2==0){QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);if(captureFrames){using var image=GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(output,mapId,(i/2).ToString("D4")+".png"));}}
  }
  if(behind||mapId=="J4PLAY01")Check(maskedCells>0,"Behind scenery still masks the moving body");
  File.WriteAllText(Path.Combine(output,mapId+"-trace.json"),JsonSerializer.Serialize(trace));ReturnMainMenu();await WarFrames();Check(nativeOcclusion==null,"Returning to the menu releases map-specific foreground depth");
  }
 }
}
