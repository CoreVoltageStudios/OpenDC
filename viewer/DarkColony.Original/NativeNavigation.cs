using DarkColony.Assets;
namespace DarkColony.Original;

public sealed record NativeRoute(Cell[] Cells,int Expansions,bool Found);
// 4435e8/444714/444890/444c7c: reverse directional bucket search; no prototype A*.
public sealed class NativeNavigation {
 readonly PathAsset map;readonly NativeReference reference;
 // Search scratch only: excluded from save/hash state, discarded with this world.
 // Reusing these map-sized arrays removes repeated LOH allocations. Reset every
 // search, retaining the exact bucket insertion/tie order and public result arrays.
 sealed class SearchMemory {
  internal readonly bool[] Families=new bool[256],Closed;
  internal readonly int[] Scores,Parents;
  internal readonly LinkedList<int>[] Buckets=Enumerable.Range(0,256).Select(_=>new LinkedList<int>()).ToArray();
  internal readonly LinkedListNode<int>?[] Links;
  internal SearchMemory(int size){Scores=new int[size];Parents=new int[size];Closed=new bool[size];Links=new LinkedListNode<int>?[size];}
  internal void Reset(){Array.Clear(Families);Array.Fill(Scores,-1);Array.Clear(Parents);Array.Clear(Closed);foreach(var b in Buckets)b.Clear();Array.Clear(Links);}
 }
 SearchMemory? searchMemory;bool searching;
 static readonly Cell[] SearchDirections={new(1,0),new(1,1),new(1,-1),new(-1,0),new(-1,-1),new(-1,1),new(0,1),new(0,-1)};
 public long Generation {get;private set;}
 public NativeNavigation(PathAsset map,NativeReference reference){this.map=map;this.reference=reference;}
 internal void Restore(long generation)=>Generation=generation;
 public void WorldUpdate()=>Generation+=2;
 public bool Inside(Cell p)=>p.X>=0&&p.Z>=0&&p.X<map.Width&&p.Z<map.Height;
 public bool Ground(Cell p)=>Inside(p)&&map.At(p.X,p.Z) is not (0 or 255);
 public NativeRoute Find(Cell source,Cell destination,Func<Cell,bool>? occupied=null,bool detour=false,bool flying=false) {
  if(flying?(!Inside(source)||!Inside(destination)):(!Ground(source)||!Ground(destination)))return new(Array.Empty<Cell>(),0,false);
  if(flying&&!detour){
   //444978 builds predecessors from destination: excess major axis, then diagonals.
   Generation++;var reverse=new List<Cell>{destination};var p=destination;
   int sx=Math.Sign(source.X-p.X),sz=Math.Sign(source.Z-p.Z),dx=Math.Abs(source.X-p.X),dz=Math.Abs(source.Z-p.Z);
   while(dx>dz){p=new(p.X+sx,p.Z);reverse.Add(p);dx--;}
   while(dz>dx){p=new(p.X,p.Z+sz);reverse.Add(p);dz--;}
   while(dx-->0){p=new(p.X+sx,p.Z+sz);reverse.Add(p);}reverse.Reverse();return new(reverse.ToArray(),0,true);
  }
  // Occupancy callbacks in the game do not recurse; retain safe behavior for
  // a reentrant caller without sharing its active scratch state.
  bool outer=searching;var memory=outer?new SearchMemory(map.Width*map.Height):searchMemory??=new SearchMemory(map.Width*map.Height);
  searching=true;try{return FindGround(source,destination,occupied,detour,flying,memory);}finally{searching=outer;}
 }
 NativeRoute FindGround(Cell source,Cell destination,Func<Cell,bool>? occupied,bool detour,bool flying,SearchMemory memory){
  Generation++;memory.Reset();var families=memory.Families;
  if(detour){Array.Fill(families,true);families[0]=families[255]=false;}
  else {
   int f=map.At(destination.X,destination.Z),goal=map.At(source.X,source.Z);
   for(int n=0;n<256;n++) {
    if(f is 0 or 255||families[f])throw new InvalidDataException("PTH family chain is broken or cyclic");
    families[f]=true;if(f==goal)break;f=map.Table[f*256+goal];
    if(n==255)throw new InvalidDataException("PTH family chain did not terminate");
   }
  }
  int size=map.Width*map.Height;var scores=memory.Scores;var parents=memory.Parents;var closed=memory.Closed;
  var buckets=memory.Buckets;var links=memory.Links;int bucket=0,priority=0,expanded=0,queued=0;
  int At(Cell p)=>p.Z*map.Width+p.X;
  Cell CellAt(int i)=>new(i%map.Width,i/map.Width);
  bool Allowed(Cell p)=>Inside(p)&&(flying||families[map.At(p.X,p.Z)])&&(!detour||p==source||occupied==null||!occupied(p));
  void Expand(int id) {
   var p=CellAt(id);int row=Math.Sign(source.X-p.X)+1+(Math.Sign(source.Z-p.Z)+1)*3;
   foreach(var d in SearchDirections) {
    var q=new Cell(p.X+d.X,p.Z+d.Z);if(!Allowed(q))continue;int target=At(q);
    if(closed[target]||(d.X!=0&&d.Z!=0&&!Allowed(new(p.X+d.X,p.Z))&&!Allowed(new(p.X,p.Z+d.Z))))continue;
    int score=scores[id]+reference.PathCosts[row*9+(d.Z+1)*3+d.X+1];
    if(scores[target]>=0&&scores[target]<score)continue;
    if(links[target]!=null){links[target]!.List!.Remove(links[target]!);queued--;}
    parents[target]=id;scores[target]=score;int b=(score-priority+bucket)&255;
    links[target]=buckets[b].AddFirst(target);queued++;
   }
   closed[id]=true;
  }
  int start=At(destination),end=At(source);scores[start]=0;parents[start]=start;Expand(start);
  if(start==end)return new(new[]{source},0,true);
  while(queued>0&&(!detour||expanded<256)) {
   while(buckets[bucket].Count==0){bucket=(bucket+1)&255;priority++;}
   int id=buckets[bucket].First!.Value;Expand(id);buckets[bucket].Remove(links[id]!);links[id]=null;queued--;expanded++;
   if(id==end) {
    var route=new List<Cell>{source};
    while(id!=start){id=parents[id];route.Add(CellAt(id));if(route.Count>size)throw new InvalidDataException("Native route predecessor cycle");}
    return new(route.ToArray(),expanded,true);
   }
  }
  return new(Array.Empty<Cell>(),expanded,false);
 }
}
