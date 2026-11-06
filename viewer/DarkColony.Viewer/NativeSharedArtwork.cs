using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 // Original indexed pixels/palette/mirroring define these immutable textures.
 // World position is excluded ONLY after conservative terrain-mask rejection.
 readonly record struct SharedArtKey(string Bank,int Cell,int Color,bool Mirror,int Kind);
 readonly Dictionary<SharedArtKey,Texture2D> nativeSharedArt=new();
 readonly HashSet<ulong> nativeSharedTextureIds=new();
 long nativeSharedBytes;int sharedArtHits,sharedArtMisses;
 bool nativeShareUnmasked=true;
 bool SharedArt(SharedArtKey key,Func<Texture2D> build,int bytes,out Texture2D texture){
  if(nativeSharedArt.TryGetValue(key,out texture!)){sharedArtHits++;return true;}
  // Bound retained presentation memory; full cache falls back to existing ownership.
  if(nativeSharedBytes+bytes>128*1024*1024)return false;
  texture=build();nativeSharedArt.Add(key,texture);nativeSharedTextureIds.Add(texture.GetInstanceId());nativeSharedBytes+=bytes;sharedArtMisses++;return true;
 }
 void ClearSharedArt(){
  nativeSharedTextureIds.Clear();foreach(var t in nativeSharedArt.Values)DisposeNativeTexture(t);
  nativeSharedArt.Clear();nativeSharedBytes=0;sharedArtHits=sharedArtMisses=0;
 }
}
