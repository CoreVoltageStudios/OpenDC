namespace DarkColony.Assets;

// Selected mode1/2 branches ->469d30/46a198 ->468ebb/4790fe (masked variants too).
// Original RLE coverage, not a generated ellipse. Canonical world-space projection
// avoids the old viewport-clipping-dependent starting shear. Alpha modernizes
// the native565 destination>>1; color-channel quantization is not reproduced.
public sealed record ShadowProjection(Raster Pixels,int X,int Y,int Extra);
public static class OriginalShadow {
 public static (int X,int Y,int Width,int Height) Bounds(SpriteCell sprite,FrameCell cell) {
  int height=sprite.Height+(sprite.Height*40>>8);
  return(cell.X+(cell.UnknownWords[3]!=0?1:sprite.OffsetX)-height/2,cell.Y-height,sprite.Width+(height-1)/2,height);
 }
 public static ShadowProjection Project(SpriteCell sprite,FrameCell cell) {
  var coverage=sprite.Coverage??throw new NotSupportedException("Shadow requires original RLE coverage");
  bool mirrored=cell.UnknownWords[3]!=0;
  int extra=sprite.Height*40>>8;var bounds=Bounds(sprite,cell);int height=bounds.Height,width=bounds.Width;
  var rgba=new byte[width*height*4];int source=0,accumulator=0;bool repeated=false;
  for(int y=0;y<height;y++) {
   for(int x=0;x<sprite.Width;x++)if(coverage[source*sprite.Width+x]!=0)rgba[(y*width+(mirrored?sprite.Width-1-x:x)+y/2)*4+3]=128;
   if(repeated){source++;repeated=false;}
   else {accumulator+=40;if(accumulator>=256){accumulator-=256;repeated=true;}else source++;}
  }
  //46a227 begins at anchorX+W, then4724ac writes at that pixel and walks left.
  //The mirrored branch does not add sprite.OffsetX: its leftmost pixel is anchorX+1.
  return new(new(width,height,rgba),bounds.X,bounds.Y,extra);
 }
 public static Placement BodyPlacement(FrameCell cell,SpriteCell sprite)=>new(cell.X+(cell.UnknownWords[3]!=0?1:sprite.OffsetX),cell.Y-sprite.Height,sprite.Width,sprite.Height);
 public static Raster Mirror(Raster source) {
  var rgba=new byte[source.Rgba.Length];for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)Array.Copy(source.Rgba,(y*source.Width+x)*4,rgba,(y*source.Width+source.Width-1-x)*4,4);
  return new(source.Width,source.Height,rgba);
 }
 public static Raster Mask(ShadowProjection shadow,TerrainOcclusion terrain,int anchorX,int groundY,int bottomY) {
  var raster=new Raster(shadow.Pixels.Width,shadow.Pixels.Height,(byte[])shadow.Pixels.Rgba.Clone());
  for(int y=0;y<raster.Height;y++)for(int x=0;x<raster.Width;x++) {
   if(raster.Rgba[(y*raster.Width+x)*4+3]==0)continue;
   int px=anchorX+shadow.X+x,py=groundY+shadow.Y+y;
   //469500 uses unexpanded sprite H;468b51 decrements on every projected row.
   //Thus cutoff is shifted upward by Extra, independently of altitude.
   if(terrain.HiddenWithCutoffOffset(px,py,bottomY,-shadow.Extra))raster.Rgba[(y*raster.Width+x)*4+3]=0;
  }
  return raster;
 }
}
