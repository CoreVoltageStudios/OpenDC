using DarkColony.Assets;

// Procedural artwork only: these tests contain no original map/sprite data.
public static class TerrainDepthChecks {
 public static void Run(){
  int checks=0;void Assert(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
  const int width=64,height=128;var pixels=new byte[width*height];
  void Rect(int x,int y,int w,int h){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)pixels[yy*width+xx]=1;}
  Rect(8,3,6,40);Rect(8,53,6,40);Rect(35,12,4,4);
  for(int y=0;y<=40;y++)pixels[y*width+20+y%2]=1; // 8-connected at top edge.
  pixels[20*width+10]=0; // A transparent hole remains transparent.
  var cells=new MapCell[8];var tiles=new List<TerrainTile>();
  for(int row=0;row<4;row++)for(int col=0;col<2;col++){
   ushort id=(ushort)(row*2+col+1);var indices=new byte[1024];
   for(int y=0;y<32;y++)for(int x=0;x<32;x++)indices[y*32+x]=pixels[(row*32+y)*width+col*32+x];
   tiles.Add(new(id,indices));cells[(3-row)*2+col]=new(0,id,4);
  }
  var terrain=new TerrainAsset(0,new byte[768],tiles.ToArray(),Array.Empty<byte>());
  var map=new MapAsset(2,4,cells,Array.Empty<byte>());var mask=new TerrainOcclusion(map,terrain);
  Assert(mask.Hidden(10,10,100),"Legacy cutoff reproduces the old in-front masking");
  Assert(mask.HiddenForGround(10,10,100,42),"Behind/on last opaque row still occludes");
  Assert(!mask.HiddenForGround(10,10,100,43),"First row past the complete base releases the body");
  Assert(!mask.HiddenForGround(10,10,110,43),"FIN bottom changes cannot restore a passed object");
  Assert(mask.HiddenForGround(10,60,100,43),"A separate lower object still occludes");
  Assert(!mask.HiddenForGround(10,60,100,93),"Lower object releases independently");
  Assert(!mask.HiddenForGround(10,20,100,0),"Foreground hole preserved");
  Assert(mask.HiddenForGround(36,13,100,100),"Small detached details keep the conservative native mask");
  Assert(mask.HiddenForGround(20,10,100,40)&&!mask.HiddenForGround(20,10,100,41),"Diagonal connections and top-edge component traversal");
  Assert(!mask.HiddenForGround(-1,10,100,0)&&!mask.HiddenForGround(10,height,100,0),"Map bounds remain safe");
  var body=new VisualAsset("synthetic",new Raster(1,1,new byte[]{255,255,255,255}),1,1,1,1,0,33);
  Assert(mask.ApplyGroundBody(body,10,43,100).Rgba[3]==255,"Body uses ground anchor rather than FIN-adjusted bottom");
  var shadowPixels=new byte[4*80];for(int i=3;i<shadowPixels.Length;i+=4)shadowPixels[i]=128;
  var shadow=OriginalShadow.Mask(new(new(1,80,shadowPixels),0,-40,0),mask,10,50,100);
  Assert(shadow.Rgba[3]==128&&shadow.Rgba[50*4+3]==0,"Shadow agrees with passed upper/still-near lower object");
  foreach(int bottom in new[]{32,64,100,127})for(int y=0;y<height;y++)for(int x=0;x<width;x++)
   if(mask.HiddenForGround(x,y,bottom,50))Assert(mask.MayMask(x,y,1,1,bottom),"Conservative fast path must never discard an occluder");
  var flipped=new TerrainOcclusion(map with{Cells=cells.Select(c=>c with{Attributes=(ushort)(c.Attributes|0x40)}).ToArray()},terrain);
  Assert(flipped.HiddenForGround(21,10,100,42)&&!flipped.HiddenForGround(21,10,100,43),"Mirrored foreground retains the same base depth");
  var again=new TerrainOcclusion(map,terrain);Assert(again.ApplyGroundBody(body,10,43,100).Rgba.SequenceEqual(mask.ApplyGroundBody(body,10,43,100).Rgba),"New map state is deterministic");
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)mask.HiddenForGround(10,10,100,43);
  Assert(GC.GetAllocatedBytesForCurrentThread()==before,"Depth lookup allocates nothing per pixel");
  Console.WriteLine($"TERRAIN_DEPTH_PASS checks={checks}");
 }
}
