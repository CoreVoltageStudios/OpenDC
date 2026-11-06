using Godot;
using DarkColony.Assets;
namespace DarkColony.Viewer;
public partial class Viewer {
 // 0 original nearest; 1 ordinary bilinear; 2 terrain bilinear / crisp sprites.
 int nativeSampling=1;
 const bool enhancedVideo=false; // Legacy diagnostic assertions: shipping playback is original-only.
 string artworkPreferencePath="";string? artworkPreferenceDiagnostic;
 void PrepareArtworkPreference(string[] args){artworkPreferencePath=args.FirstOrDefault(a=>a.StartsWith("--artwork-preferences="))?[22..]??(capturing?Path.Combine(output,"player-settings.json"):Path.Combine(PlayerDataRoot,"player-settings.json"));nativeSampling=ArtworkPreference.Load(artworkPreferencePath,out artworkPreferenceDiagnostic)==ArtworkStyle.Crisp?0:1;LoadPresentationPreferences();}
 void ChooseArtwork(ArtworkStyle style){SetNativeSampling(style==ArtworkStyle.Crisp?0:1);SaveSettings(p=>ArtworkPreference.Save(p,style),"Artwork changed, but the preference could not be saved.");}
 readonly Dictionary<ulong,(int Scale,int Pad)> nativeSamplingLayout=new();
 readonly Dictionary<string,Texture2D> nativeMissileTextures=new();
 void ClearNativeMissiles(){foreach(var texture in nativeMissileTextures.Values)DisposeNativeTexture(texture);nativeMissileTextures.Clear();}
 readonly Dictionary<int,Rgb8[]> nativeBodyColors=new();
 Rgb8[] NativeBodyColors(int selector){if(!nativeBodyColors.TryGetValue(selector,out var colors))nativeBodyColors[selector]=colors=GameColors.Table(nativePalette,selector,16,ColorBank.Body);return colors;}
 Texture2D NativeArtwork(Raster raster,bool terrain=false) {
  if(!firstStrike&&!originalStateMode&&!interferenceDev)return OriginalTexture(raster);
  int scale=!terrain&&nativeSampling==2?2:1,pad=terrain||nativeSampling==0?0:1;
  var pixels=nativeSampling==0||terrain?raster:ArtworkSampling.Prepare(raster,scale,pad!=0);
  var texture=new CanvasTexture{DiffuseTexture=OriginalTexture(pixels),TextureFilter=nativeSampling==0?TextureFilterEnum.Nearest:TextureFilterEnum.Linear};
  nativeSamplingLayout[texture.GetInstanceId()]=(scale,pad);return texture;
 }
 Rect2 NativeArtworkBounds(Texture2D texture,Vector2 at)=>nativeSamplingLayout.TryGetValue(texture.GetInstanceId(),out var layout)?new(at-Vector2.One*layout.Pad,texture.GetSize()/layout.Scale):new(at,texture.GetSize());
 void DrawNativeArtwork(Texture2D texture,Vector2 at)=>DrawTextureRect(texture,NativeArtworkBounds(texture,at),false);
 void SetNativeSampling(int mode) {
  if(nativeSampling==mode)return;nativeSampling=mode;
  // Sampling changes invalidate only derived art, not fog or interpolation history.
  ClearNativeArtBodies();
  ClearNativeMissiles();
  foreach(var pair in nativeTerrain.Values)foreach(var t in new[]{pair.Ground,pair.Foreground})if(t is CanvasTexture c)c.TextureFilter=mode==0?TextureFilterEnum.Nearest:TextureFilterEnum.Linear;
 }
}
