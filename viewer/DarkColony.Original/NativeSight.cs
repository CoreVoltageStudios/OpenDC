using DarkColony.Assets;
namespace DarkColony.Original;

public sealed class NativeSight {
 readonly MapAsset map;readonly NativeReference reference;
 public bool[][] Visible {get;}
 public bool[][] Explored {get;}
 public NativeSight(MapAsset map,NativeReference reference) {
  this.map=map;this.reference=reference;
  Visible=Enumerable.Range(0,8).Select(_=>new bool[map.Width*map.Height]).ToArray();
  Explored=Visible.Select(a=>new bool[a.Length]).ToArray();
 }
 public void ClearCurrent(){foreach(var a in Visible)Array.Clear(a);}
 // 412577..4125df: movement marks both old and newly reserved cells between refreshes.
 public void MarkCurrent(int team,Cell p) {
  if(team>=0&&team<8&&p.X>=0&&p.Z>=0&&p.X<map.Width&&p.Z<map.Height)Visible[team][p.Z*map.Width+p.X]=true;
 }
 public void Reveal(int team,Cell origin,int radius,bool localViewer)=>Reveal(team,origin,radius,localViewer?1<<team:0);
 public void Reveal(int team,Cell origin,int radius,int exploringTeams) {
  if(team<0||team>7||radius<1||radius>10)throw new ArgumentOutOfRangeException();
  var stack=new Stack<(SightNode Node,int Depth)>();stack.Push((reference.Sight[radius],0));
  while(stack.TryPop(out var item)) {
   int x=origin.X+item.Node.Offset.X,z=origin.Z+item.Node.Offset.Z;
   if(x<0||z<0||x>=map.Width||z>=map.Height)continue;
   int at=z*map.Width+x,a=map.Cells[at].Attributes;
   // 44aa24 -> 448d1c/44a070: the viewer's vision mask controls the persistent
   // explored bit, including occluding boundary cells. History is not erased
   // when a link is removed, nor copied retroactively when one is enabled.
   for(int viewer=0;viewer<8;viewer++)if((exploringTeams&(1<<viewer))!=0)Explored[viewer][at]=true;
   if((a&0x100)==0||item.Depth<2)Visible[team][at]=true;
   if((a&0x80)!=0||(a&0x200)==0)foreach(var child in item.Node.Children)stack.Push((child,item.Depth+1));
  }
 }
 public bool At(int team,Cell p)=>team>=0&&team<8&&p.X>=0&&p.Z>=0&&p.X<map.Width&&p.Z<map.Height&&Visible[team][p.Z*map.Width+p.X];
}
