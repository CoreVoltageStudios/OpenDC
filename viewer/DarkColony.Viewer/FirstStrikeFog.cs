using Godot;
using DarkColony.Assets;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool nativeFineFog=true;
 byte[] nativeFogState=Array.Empty<byte>(),nativeFogScratch=Array.Empty<byte>(),nativeFogPixels=Array.Empty<byte>();ImageTexture? nativeFogImage;CanvasTexture? nativeFogTexture;
 void ClearNativeFog(){nativeFogState=Array.Empty<byte>();nativeFogScratch=Array.Empty<byte>();if(nativeFogTexture!=null){nativeFogTexture.DiffuseTexture=null;nativeFogTexture.Dispose();nativeFogTexture=null;}nativeFogImage?.Dispose();nativeFogImage=null;}
 void DrawNativeFog() {
  if(nativeOmniscient)return;
  var map=nativeData.Scenario.Map;
  if(nativeFogScratch.Length!=map.Width*map.Height)nativeFogScratch=new byte[map.Width*map.Height];
  var states=nativeFogScratch;
  for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++) {
   int z=map.Height-1-y;states[y*map.Width+x]=nativeWorld.Visible(LocalTeam,new(x,z))?(byte)2:nativeWorld.Sight.Explored[LocalTeam][z*map.Width+x]?(byte)1:(byte)0;
  }
  if(!nativeSoftFog){for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++){int state=states[y*map.Width+x];if(state<2)DrawRect(new(x*32,y*32,32,32),new Color(.03f,.055f,.045f,state==0?1:.68f));}return;}
  if(!states.AsSpan().SequenceEqual(nativeFogState)) {
   animationFogBuilt=true;
   Raster pixels;
   if(nativeFineFog){int length=(map.Width+1)*(map.Height+1)*4;if(nativeFogPixels.Length!=length)nativeFogPixels=new byte[length];ContourFog.Write(map.Width,map.Height,states,nativeFogPixels);pixels=new(map.Width+1,map.Height+1,nativeFogPixels);}
   else pixels=SoftFog.Build(map.Width,map.Height,states);
   using var image=Image.CreateFromData(pixels.Width,pixels.Height,false,Image.Format.Rgba8,pixels.Rgba);
   if(nativeFogImage==null){nativeFogImage=ImageTexture.CreateFromImage(image);nativeFogTexture=new(){DiffuseTexture=nativeFogImage,TextureFilter=TextureFilterEnum.Linear};}
   else nativeFogImage.Update(image);
   nativeFogScratch=nativeFogState;nativeFogState=states;
  }
  // Corner texel centres coincide with tile corners; the half-texel border is
  // outside the map. Edge samples duplicate the native nearest-cell halo.
  if(nativeFogTexture!=null)DrawTextureRect(nativeFogTexture,nativeFineFog?new(-16,-16,(map.Width+1)*32,(map.Height+1)*32):new(0,0,map.Width*32,map.Height*32),false);
 }
}
