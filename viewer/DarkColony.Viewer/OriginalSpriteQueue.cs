using Godot;
using DarkColony.Assets;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly record struct NativePart(Texture2D Texture,Vector2 Offset,int Ordinal,ushort Layer,int SortX);
 readonly record struct NativeDrawItem(Texture2D Texture,Vector2 At,long Key,int Submission,long Slot,int Ordinal,ushort Layer);
 readonly List<NativeDrawItem> nativeSpriteQueue=new(1024);
 void QueueNativePart(Texture2D texture,Vector2 at,long key,long slot,int ordinal,ushort layer)=>
  nativeSpriteQueue.Add(new(texture,at,key,nativeSpriteQueue.Count,slot,ordinal,layer));
 void FlushNativeSprites(){
  // Stable submission ties keep a cell's shadow immediately before its body.
  nativeSpriteQueue.Sort(static(a,b)=>{int c=b.Key.CompareTo(a.Key);return c!=0?c:a.Submission.CompareTo(b.Submission);});
  foreach(var p in nativeSpriteQueue)DrawNativeArtwork(p.Texture,p.At);
 }
}
