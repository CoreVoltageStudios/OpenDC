using Godot;
using DarkColony.Assets;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool miniLastOmniscient;int miniComparison;double miniBuildMilliseconds;long miniBuildBytes;int miniBuilds,miniUploads;
 byte[] miniPixels=Array.Empty<byte>(),miniBase=Array.Empty<byte>(),miniVisibility=Array.Empty<byte>();
 void UpdateClearMinimap(int selector){
  var map=nativeData.Scenario.Map;const int samples=4;int width=map.Width*samples,height=map.Height*samples;
  bool rebuild=miniBase.Length==0||selector!=miniSelector;
  if(!rebuild&&miniLastOmniscient==nativeOmniscient&&miniTexture!=null&&nativeWorld.Tick>=miniTick&&nativeWorld.Tick-miniTick<4)return;
  miniTick=nativeWorld.Tick;miniLastOmniscient=nativeOmniscient;
  if(rebuild){
   var watch=System.Diagnostics.Stopwatch.StartNew();long allocated=GC.GetAllocatedBytesForCurrentThread();
   miniSelector=selector;
   if(miniBase.Length==0){miniBase=new byte[width*height*4];miniPixels=new byte[miniBase.Length];miniVisibility=new byte[map.Width*map.Height];}
   Array.Fill(miniVisibility,(byte)255);
   var palette=GameColors.Table(nativePalette,selector,16,ColorBank.Terrain);var tiles=nativeData.Scenario.Terrain.ByEditorId;
   // Repeated ground/foreground combinations share the same exact source-area average.
   var colors=new Dictionary<(int,int,int),byte[]>();
   for(int z=0;z<map.Height;z++)for(int x=0;x<map.Width;x++){
    var c=map.At(x,z);var key=((int)c.BackgroundId,(int)c.ForegroundId,c.Attributes&0x60);
    if(!colors.TryGetValue(key,out var block)){
     block=new byte[64];var bg=tiles[c.BackgroundId].Indices;var fg=c.ForegroundId!=0?tiles[c.ForegroundId].Indices:null;
     for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++){
      int red=0,green=0,blue=0;
      // Average actual 8x8 texels, compositing transparency and mirroring before reducing.
      for(int py=sy*8;py<(sy+1)*8;py++)for(int px=sx*8;px<(sx+1)*8;px++){
       int color=bg[py*32+((c.Attributes&0x20)!=0?31-px:px)];
       if(fg!=null){int f=fg[py*32+((c.Attributes&0x40)!=0?31-px:px)];if(f!=0)color=f;}
       var rgb=palette[color];red+=rgb.R;green+=rgb.G;blue+=rgb.B;
      }
      int i=(sy*4+sx)*4;block[i]=(byte)(red/64);block[i+1]=(byte)(green/64);block[i+2]=(byte)(blue/64);block[i+3]=255;
     }
     colors.Add(key,block);
    }
    for(int sy=0;sy<4;sy++)Buffer.BlockCopy(block,sy*16,miniBase,(((map.Height-1-z)*4+sy)*width+x*4)*4,16);
   }
   miniBuildMilliseconds=watch.Elapsed.TotalMilliseconds;miniBuildBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;miniBuilds++;
  }
  bool changed=rebuild;
  for(int z=0;z<map.Height;z++)for(int x=0;x<map.Width;x++){
   int cell=z*map.Width+x;byte state=(byte)(nativeOmniscient||nativeWorld.Visible(LocalTeam,new(x,z))?2:nativeWorld.Sight.Explored[LocalTeam][cell]?1:0);
   if(miniVisibility[cell]==state)continue;miniVisibility[cell]=state;changed=true;
   for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++){int i=(((map.Height-1-z)*4+sy)*width+x*4+sx)*4;for(int channel=0;channel<3;channel++)miniPixels[i+channel]=state==0?(byte)0:state==1?(byte)(miniBase[i+channel]*.42):miniBase[i+channel];miniPixels[i+3]=255;}
  }
  if(!changed)return;
  using var image=Image.CreateFromData(width,height,false,Image.Format.Rgba8,miniPixels);
  if(miniTexture==null){miniTexture=ImageTexture.CreateFromImage(image);miniCanvas=new(){DiffuseTexture=miniTexture,TextureFilter=TextureFilterEnum.Linear};}else miniTexture.Update(image);miniUploads++;
 }
}
