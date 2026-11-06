namespace DarkColony.Simulation;

// Logical map units are tiles, X right / Z up. No asset, pixel or engine dependency.
public readonly record struct Point(double X,double Z) {
 public double Distance(Point p)=>Math.Sqrt((X-p.X)*(X-p.X)+(Z-p.Z)*(Z-p.Z));
 public bool Finite=>double.IsFinite(X)&&double.IsFinite(Z);
}
public readonly record struct Cell(int X,int Z) { public Point Center=>new(X+.5,Z+.5); }
public enum Facing { North,NorthEast,East,SouthEast,South,SouthWest,West,NorthWest }
public enum OrderKind { Move,Stop }
public enum OrderResult { None,Moving,Arrived,Stopped,OutsideMap,Blocked,Unreachable,UnknownUnit }
public sealed record MovementSettings(int TickRate=60,double TilesPerSecond=1.5,double Radius=.18,double ArrivalTolerance=.000001) {
 public void Validate(){if(TickRate<1||TickRate>240||!double.IsFinite(TilesPerSecond)||TilesPerSecond<=0||TilesPerSecond>20||Radius<0||Radius>=.5||!double.IsFinite(Radius)||!double.IsFinite(ArrivalTolerance)||ArrivalTolerance<0||ArrivalTolerance>.001)throw new ArgumentException("Unsupported movement settings");}
}
public sealed record UnitSnapshot(int Id,Point Position,Facing Facing,bool Moving,long MovingTicks,Point? Destination,Point[] Route,int RouteIndex);
public sealed record Command(long Tick,long Sequence,int UnitId,OrderKind Kind,Point Destination);
public sealed record CommandReceipt(long Tick,long Sequence,OrderResult Result,Point Requested,Point? Accepted,int Expanded);

public sealed class NavigationGrid {
 readonly bool[] allowed;
 public int Width {get;} public int Height {get;}
 public NavigationGrid(int width,int height,bool[] walkable) {
  if(width<1||height<1||width>256||height>256||walkable.Length!=width*height)throw new ArgumentException("Navigation dimensions");
  Width=width;Height=height;allowed=(bool[])walkable.Clone();
 }
 public bool Contains(Cell c)=>c.X>=0&&c.Z>=0&&c.X<Width&&c.Z<Height;
 public bool Walkable(Cell c)=>Contains(c)&&allowed[c.Z*Width+c.X];
 public NavigationGrid WithBlocked(IEnumerable<Cell> cells) {
  var copy=(bool[])allowed.Clone();foreach(var cell in cells)if(Contains(cell))copy[cell.Z*Width+cell.X]=false;return new(Width,Height,copy);
 }
 public Cell CellAt(Point p)=>new((int)Math.Floor(p.X),(int)Math.Floor(p.Z));
 public bool InBounds(Point p,double radius)=>p.Finite&&p.X>=radius&&p.Z>=radius&&p.X<=Width-radius&&p.Z<=Height-radius&&p.X<Width&&p.Z<Height;
 public bool Clear(Point a,Point b,double radius) {
  if(!InBounds(a,radius)||!InBounds(b,radius))return false;
  int minX=Math.Max(0,(int)Math.Floor(Math.Min(a.X,b.X)-radius)),maxX=Math.Min(Width-1,(int)Math.Floor(Math.Max(a.X,b.X)+radius));
  int minZ=Math.Max(0,(int)Math.Floor(Math.Min(a.Z,b.Z)-radius)),maxZ=Math.Min(Height-1,(int)Math.Floor(Math.Max(a.Z,b.Z)+radius));
  for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)if(!Walkable(new(x,z))&&Intersects(a,b,x-radius,z-radius,x+1+radius,z+1+radius))return false;
  return true;
 }
 static bool Intersects(Point a,Point b,double x0,double z0,double x1,double z1) {
  double lo=0,hi=1;
  bool Clip(double p,double d,double min,double max) {
   if(Math.Abs(d)<1e-15)return p>=min&&p<=max;
   double l=(min-p)/d,h=(max-p)/d;if(l>h)(l,h)=(h,l);lo=Math.Max(lo,l);hi=Math.Min(hi,h);return lo<=hi;
  }
  return Clip(a.X,b.X-a.X,x0,x1)&&Clip(a.Z,b.Z-a.Z,z0,z1);
 }
}
public sealed record RouteResult(OrderResult Result,Point[] Points,int Cost,int Expanded);
public static class RoutePlanner {
 // Remake A*, not a reproduction of DC16 routing. Stable neighbors and (f,h,serial) ordering.
 static readonly (int x,int z,int cost)[] Neighbors={(0,1,1000),(1,0,1000),(0,-1,1000),(-1,0,1000),(1,1,1414),(1,-1,1414),(-1,-1,1414),(-1,1,1414)};
 public static RouteResult Find(NavigationGrid grid,Point start,Point requested,double radius) {
  RouteResult Fail(OrderResult r,int expanded=0)=>new(r,Array.Empty<Point>(),0,expanded);
  if(!grid.InBounds(requested,0))return Fail(OrderResult.OutsideMap);
  var source=grid.CellAt(start);var target=grid.CellAt(requested);var dest=target.Center;
  if(!grid.Walkable(target)||!grid.Clear(dest,dest,radius))return Fail(OrderResult.Blocked);
  if(!grid.Walkable(source)||!grid.Clear(start,source.Center,radius))return Fail(OrderResult.Unreachable);
  int n=grid.Width*grid.Height,src=source.Z*grid.Width+source.X,end=target.Z*grid.Width+target.X,serial=0,expanded=0;
  var g=Enumerable.Repeat(int.MaxValue,n).ToArray();var parent=Enumerable.Repeat(-1,n).ToArray();var closed=new bool[n];
  var queue=new PriorityQueue<int,(int f,int h,int serial)>();
  int H(Cell c){int dx=Math.Abs(c.X-target.X),dz=Math.Abs(c.Z-target.Z);return Math.Min(dx,dz)*1414+Math.Abs(dx-dz)*1000;}
  g[src]=0;queue.Enqueue(src,(H(source),H(source),serial++));
  while(queue.TryDequeue(out int current,out _)) {
   if(closed[current])continue;closed[current]=true;expanded++;
   if(current==end) {
    var path=new List<Point>();int p=end;while(p!=src){path.Add(new Cell(p%grid.Width,p/grid.Width).Center);p=parent[p];}
    path.Add(source.Center);path.Reverse();if(start.Distance(path[0])<1e-12)path.RemoveAt(0);
    return new(OrderResult.Moving,path.ToArray(),g[end],expanded);
   }
   var c=new Cell(current%grid.Width,current/grid.Width);
   foreach(var step in Neighbors) {
    var next=new Cell(c.X+step.x,c.Z+step.z);
    if(!grid.Walkable(next))continue;
    if(step.x!=0&&step.z!=0&&(!grid.Walkable(new(c.X+step.x,c.Z))||!grid.Walkable(new(c.X,c.Z+step.z))))continue;
    if(!grid.Clear(c.Center,next.Center,radius))continue;
    int ni=next.Z*grid.Width+next.X,cost=g[current]+step.cost;if(closed[ni]||cost>=g[ni])continue;
    g[ni]=cost;parent[ni]=current;int h=H(next);queue.Enqueue(ni,(cost+h,h,serial++));
   }
  }
  return Fail(OrderResult.Unreachable,expanded);
 }
}
public sealed class MovementSimulation {
 public const int TrooperId=1;
 readonly List<Command> commands=new();readonly List<CommandReceipt> receipts=new();
 readonly NavigationGrid grid;readonly MovementSettings settings;
 Point position;Facing facing=Facing.South;Point[] route=Array.Empty<Point>();int routeIndex;Point? destination;long movingTicks;
 public long Tick {get;private set;}
 public OrderResult Status {get;private set;}=OrderResult.None;
 public IReadOnlyList<CommandReceipt> Receipts=>receipts;
 public UnitSnapshot Unit=>new(TrooperId,position,facing,routeIndex<route.Length,movingTicks,destination,(Point[])route.Clone(),routeIndex);
 public MovementSimulation(NavigationGrid grid,Point start,MovementSettings settings) {
  settings.Validate();if(!grid.Clear(start,start,settings.Radius))throw new ArgumentException("Invalid spawn footprint");this.grid=grid;this.settings=settings;position=start;
 }
 public void Enqueue(Command command) {
  if(command.Tick<=Tick||commands.Any(c=>c.Sequence==command.Sequence)||receipts.Any(c=>c.Sequence==command.Sequence))throw new ArgumentException("Commands require future tick and unique sequence");
  if(!Enum.IsDefined(command.Kind))throw new ArgumentException("Unsupported command");commands.Add(command);
 }
 public void Step() {
  Tick++;
  foreach(var c in commands.Where(c=>c.Tick==Tick).OrderBy(c=>c.Sequence).ToArray()) {
   commands.Remove(c);OrderResult result;Point? accepted=null;int expanded=0;
   if(c.UnitId!=TrooperId)result=OrderResult.UnknownUnit;
   else if(c.Kind==OrderKind.Stop){route=Array.Empty<Point>();routeIndex=0;destination=null;Status=result=OrderResult.Stopped;}
   else {
    var path=RoutePlanner.Find(grid,position,c.Destination,settings.Radius);result=path.Result;expanded=path.Expanded;
    if(result==OrderResult.Moving){bool wasMoving=routeIndex<route.Length;route=path.Points;routeIndex=0;destination=grid.CellAt(c.Destination).Center;accepted=destination;if(!wasMoving)movingTicks=0;Status=route.Length==0?OrderResult.Arrived:OrderResult.Moving;}
    // Invalid orders preserve a previously valid route and position.
   }
   receipts.Add(new(Tick,c.Sequence,result,c.Destination,accepted,expanded));
  }
  if(routeIndex>=route.Length)return;
  movingTicks++;double remaining=settings.TilesPerSecond/settings.TickRate;
  while(remaining>0&&routeIndex<route.Length) {
   Point target=route[routeIndex];double distance=position.Distance(target);
   if(distance>settings.ArrivalTolerance)facing=Direction(target.X-position.X,target.Z-position.Z);
   Point next;
   if(distance<=remaining+settings.ArrivalTolerance){next=target;remaining=Math.Max(0,remaining-distance);routeIndex++;}
   else {double fraction=remaining/distance;next=new(position.X+(target.X-position.X)*fraction,position.Z+(target.Z-position.Z)*fraction);remaining=0;}
   if(!grid.Clear(position,next,settings.Radius))throw new InvalidOperationException("Movement segment left permitted footprint");position=next;
  }
  if(routeIndex==route.Length)Status=OrderResult.Arrived;
 }
 public static Facing Direction(double dx,double dz) {
  double x=Math.Abs(dx),z=Math.Abs(dz),threshold=.4142135623730951;
  if(z<x*threshold)return dx>0?Facing.East:Facing.West;
  if(x<z*threshold)return dz>0?Facing.North:Facing.South;
  return dz>0?(dx>0?Facing.NorthEast:Facing.NorthWest):(dx>0?Facing.SouthEast:Facing.SouthWest);
 }
}
public sealed class FixedStepClock {
 readonly int rate;double seconds;
 public FixedStepClock(int tickRate){if(tickRate<1)throw new ArgumentException("Tick rate");rate=tickRate;}
 public int Advance(double elapsed,Action step) {
  if(!double.IsFinite(elapsed)||elapsed<0||elapsed>1)throw new ArgumentException("Frame elapsed must be0..1 seconds");
  seconds+=elapsed;int count=0;while(seconds+1e-12>=1d/rate){seconds-=1d/rate;step();count++;}return count;
 }
}
