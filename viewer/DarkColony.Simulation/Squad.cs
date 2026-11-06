namespace DarkColony.Simulation;

public enum TravelState { Idle,Moving,Waiting,Arrived,Stopped,Blocked }
public enum SquadOrderResult { Accepted,Stopped,NoUnits,UnknownUnit,OutsideMap,BlockedTerrain,InsufficientSpace }
public sealed record SquadSettings(int DestinationRadius=2,int ReplanInterval=120,int MaxReplans=2,int WaitBudget=600) {
 public void Validate(){if(DestinationRadius<0||DestinationRadius>4||ReplanInterval<1||MaxReplans<0||MaxReplans>4||WaitBudget<1||WaitBudget>3600)throw new ArgumentException("Squad settings outside prototype bounds");}
}
public sealed record UnitSpawn(int Id,Point Position);
public sealed record SquadOrder(long Tick,long Sequence,int[] Units,OrderKind Kind,Point Target);
public sealed record Assignment(int Id,Point Destination);
public sealed record SquadReceipt(long Tick,long Sequence,SquadOrderResult Result,int[] Units,Assignment[] Assignments);
public sealed record SquadUnit(int Id,Point Position,Facing Facing,TravelState State,bool Moving,long MovingTicks,Point? Destination,Point[] Route,int RouteIndex,int WaitTicks,int Replans,Point? ReservedFrom,Point? ReservedTo,long OrderSequence);

// Conservative exclusive swept-AABB reservations. This is a remake traffic policy,
// not DC16 collision/formation reconstruction. Units never derive geometry from art.
public sealed class SquadSimulation {
 sealed class Body {
  public int Id;public Point Position;public Facing Facing=Facing.South;public TravelState State;
  public long MovingTicks,OrderSequence;public Point? Destination,ReservedFrom,ReservedTo,LimitOrigin;public double LimitRange;
  public Point[] Route=Array.Empty<Point>();public int Index,WaitTicks,Replans;public bool Moving;
  public bool Pending=>Index<Route.Length;
  public SquadUnit Snapshot=>new(Id,Position,Facing,State,Moving,MovingTicks,Destination,(Point[])Route.Clone(),Index,WaitTicks,Replans,ReservedFrom,ReservedTo,OrderSequence);
 }
 readonly SortedDictionary<int,Body> bodies=new();readonly List<SquadOrder> orders=new();readonly List<SquadReceipt> receipts=new();readonly HashSet<long> sequences=new();
 public NavigationGrid Grid {get;} public MovementSettings Movement {get;} public SquadSettings Settings {get;}
 public long Tick {get;private set;} public IReadOnlyList<SquadReceipt> Receipts=>receipts;
 public SquadUnit[] Units=>bodies.Values.Select(u=>u.Snapshot).ToArray();
 public SquadUnit Unit(int id)=>bodies[id].Snapshot;
 public SquadSimulation(NavigationGrid grid,IEnumerable<UnitSpawn> spawns,MovementSettings movement,SquadSettings? settings=null) {
  Grid=grid;Movement=movement;Settings=settings??new();movement.Validate();Settings.Validate();
  foreach(var s in spawns.OrderBy(s=>s.Id)) {
   if(s.Id<1||bodies.ContainsKey(s.Id)||!grid.Clear(s.Position,s.Position,movement.Radius)||bodies.Values.Any(u=>SweepsOverlap(s.Position,s.Position,u.Position,u.Position,movement.Radius)))throw new ArgumentException("Invalid/overlapping squad spawn");
   bodies.Add(s.Id,new(){Id=s.Id,Position=s.Position});
  }
  if(bodies.Count<1||bodies.Count>16)throw new ArgumentException("Prototype supports1..16 bodies");
 }
 public void Enqueue(SquadOrder order) {
  if(order.Tick<=Tick||!sequences.Add(order.Sequence)||!Enum.IsDefined(order.Kind))throw new ArgumentException("Future tick, unique sequence and valid command required");
  orders.Add(order with{Units=order.Units.Distinct().Order().ToArray()});
 }
 public static bool SweepsOverlap(Point a,Point b,Point c,Point d,double radius) {
  double gap=radius*2+1e-9;
  return Math.Max(Math.Min(a.X,b.X),Math.Min(c.X,d.X))<=Math.Min(Math.Max(a.X,b.X),Math.Max(c.X,d.X))+gap &&
         Math.Max(Math.Min(a.Z,b.Z),Math.Min(c.Z,d.Z))<=Math.Min(Math.Max(a.Z,b.Z),Math.Max(c.Z,d.Z))+gap;
 }
 IEnumerable<Cell> FootprintCells(Point p) {
  for(int z=(int)Math.Floor(p.Z-Movement.Radius);z<=(int)Math.Floor(p.Z+Movement.Radius);z++)
   for(int x=(int)Math.Floor(p.X-Movement.Radius);x<=(int)Math.Floor(p.X+Movement.Radius);x++)yield return new(x,z);
 }
 NavigationGrid PlanningGrid(HashSet<int> participants,int currentId,IEnumerable<Cell>? assigned=null) {
  var blocked=new HashSet<Cell>(assigned??Array.Empty<Cell>());
  foreach(var other in bodies.Values)if(other.Id!=currentId&&!participants.Contains(other.Id)) {
   if(!other.Pending)foreach(var c in FootprintCells(other.Position))blocked.Add(c);
   if(other.Destination is Point d)blocked.Add(Grid.CellAt(d));
  }
  // A neighbour can occupy part of our current coarse cell without overlapping
  // our actual footprint. Permit planning out of those starting cells; the base
  // terrain and CanReserve swept geometry still forbid every physical collision.
  foreach(var cell in FootprintCells(bodies[currentId].Position))blocked.Remove(cell);
  return Grid.WithBlocked(blocked);
 }
 void ClearRoute(Body u,TravelState state) {u.Route=Array.Empty<Point>();u.Index=0;u.ReservedFrom=u.ReservedTo=u.Destination=u.LimitOrigin=null;u.Moving=false;u.State=state;}
 // Combat is in this assembly and owns its command tick. These operations preserve
 // the same geometry/reservation invariants; none can change a unit's position.
 internal void Execute(SquadOrder order)=>ProcessOrder(order);
 internal void Halt(int id,TravelState state=TravelState.Stopped)=>ClearRoute(bodies[id],state);
 internal void Face(int id,Point target){var u=bodies[id];if(u.Position!=target)u.Facing=MovementSimulation.Direction(target.X-u.Position.X,target.Z-u.Position.Z);}
 internal void Remove(int id)=>bodies.Remove(id);
 internal bool RouteExact(int id,Point target,long sequence,bool preserveMotion=false,Point? limitOrigin=null,double limitRange=0) {
  var u=bodies[id];if(bodies.Values.Any(o=>o.Id!=id&&(SweepsOverlap(target,target,o.Position,o.Position,Movement.Radius)||o.Destination==target)))return false;
  var path=RoutePlanner.Find(PlanningGrid(new HashSet<int>(),id),u.Position,target,Movement.Radius);if(path.Result!=OrderResult.Moving)return false;
  var points=path.Points;
  if(points.Length>0&&points[^1]!=target){if(Grid.Clear(points.Length>1?points[^2]:u.Position,target,Movement.Radius))points[^1]=target;else if(Grid.Clear(points[^1],target,Movement.Radius))points=points.Append(target).ToArray();else return false;}
  if(limitOrigin is Point home&&points.Any(p=>!CombatSimulation.InRange(home,p,limitRange)))return false;
  if(preserveMotion&&points.Length>1&&Grid.Clear(u.Position,points[1],Movement.Radius))points=points.Skip(1).ToArray();
  u.Route=points;u.Index=0;u.Destination=target;u.LimitOrigin=limitOrigin;u.LimitRange=limitRange;u.ReservedFrom=u.ReservedTo=null;u.OrderSequence=sequence;u.WaitTicks=u.Replans=0;if(!preserveMotion)u.MovingTicks=0;u.Moving=false;u.State=u.Pending?TravelState.Waiting:TravelState.Arrived;return true;
 }
 void ProcessOrder(SquadOrder order) {
  void Receipt(SquadOrderResult r,Assignment[]? assignments=null)=>receipts.Add(new(order.Tick,order.Sequence,r,(int[])order.Units.Clone(),assignments??Array.Empty<Assignment>()));
  if(order.Units.Length==0){Receipt(SquadOrderResult.NoUnits);return;}
  if(order.Units.Any(id=>!bodies.ContainsKey(id))){Receipt(SquadOrderResult.UnknownUnit);return;}
  if(order.Kind==OrderKind.Stop){foreach(int id in order.Units)ClearRoute(bodies[id],TravelState.Stopped);Receipt(SquadOrderResult.Stopped);return;}
  if(!Grid.InBounds(order.Target,0)){Receipt(SquadOrderResult.OutsideMap);return;}
  Cell center=Grid.CellAt(order.Target);
  if(!Grid.Walkable(center)){Receipt(SquadOrderResult.BlockedTerrain);return;}
  var participants=order.Units.ToHashSet();var candidates=new List<Cell>();
  for(int dz=-Settings.DestinationRadius;dz<=Settings.DestinationRadius;dz++)for(int dx=-Settings.DestinationRadius;dx<=Settings.DestinationRadius;dx++) {
   var c=new Cell(center.X+dx,center.Z+dz);if(Grid.Walkable(c))candidates.Add(c);
  }
  candidates=candidates.OrderBy(c=>(c.X-center.X)*(c.X-center.X)+(c.Z-center.Z)*(c.Z-center.Z)).ThenBy(c=>c.Z).ThenBy(c=>c.X).ToList();
  var plans=new List<(Body unit,Cell destination,Point[] route)>();var assigned=new HashSet<Cell>();
  foreach(int id in order.Units) {
   var u=bodies[id];var nav=PlanningGrid(participants,id,assigned);bool found=false;
   foreach(var candidate in candidates) {
    if(assigned.Contains(candidate)||bodies.Values.Any(o=>o.Id!=id&&(SweepsOverlap(candidate.Center,candidate.Center,o.Position,o.Position,Movement.Radius)||(!participants.Contains(o.Id)&&o.Destination==candidate.Center))))continue;
    var path=RoutePlanner.Find(nav,u.Position,candidate.Center,Movement.Radius);if(path.Result!=OrderResult.Moving)continue;
    plans.Add((u,candidate,path.Points));assigned.Add(candidate);found=true;break;
   }
   if(!found){Receipt(SquadOrderResult.InsufficientSpace);return;} // Atomic rejection; old orders stay intact.
  }
  foreach(var plan in plans) {
   var u=plan.unit;u.Route=plan.route;u.Index=0;u.Destination=plan.destination.Center;u.ReservedFrom=u.ReservedTo=u.LimitOrigin=null;u.OrderSequence=order.Sequence;u.WaitTicks=u.Replans=0;u.MovingTicks=0;u.Moving=false;u.State=u.Pending?TravelState.Waiting:TravelState.Arrived;
  }
  Receipt(SquadOrderResult.Accepted,plans.Select(p=>new Assignment(p.unit.Id,p.destination.Center)).ToArray());
 }
 bool CanReserve(Body u,Point to) {
  if(u.LimitOrigin is Point home&&!CombatSimulation.InRange(home,to,u.LimitRange))return false;
  if(!Grid.Clear(u.Position,to,Movement.Radius))return false;
  foreach(var other in bodies.Values)if(other!=u) {
   if(SweepsOverlap(u.Position,to,other.Position,other.Position,Movement.Radius))return false;
   if(other.ReservedFrom is Point a&&other.ReservedTo is Point b&&SweepsOverlap(u.Position,to,a,b,Movement.Radius))return false;
  }
  return true;
 }
 void Wait(Body u) {
  u.State=TravelState.Waiting;u.Moving=false;u.WaitTicks++;
  if(u.WaitTicks>=Settings.WaitBudget){ClearRoute(u,TravelState.Blocked);return;}
  if(u.Replans<Settings.MaxReplans&&u.WaitTicks%Settings.ReplanInterval==0&&u.Destination is Point target) {
   u.Replans++;var path=RoutePlanner.Find(PlanningGrid(new HashSet<int>(),u.Id),u.Position,target,Movement.Radius);
   if(path.Result==OrderResult.Moving){var points=path.Points;if(points.Length>0&&points[^1]!=target&&Grid.Clear(points[^1],target,Movement.Radius))points=points.Append(target).ToArray();if(u.LimitOrigin is not Point home||points.All(p=>CombatSimulation.InRange(home,p,u.LimitRange))){u.Route=points;u.Index=0;if(!u.Pending)u.State=TravelState.Arrived;}}
  }
 }
 public void Step() {
  Tick++;foreach(var order in orders.Where(o=>o.Tick==Tick).OrderBy(o=>o.Sequence).ToArray()){orders.Remove(order);ProcessOrder(order);}
  foreach(var u in bodies.Values)u.Moving=false;
  foreach(var u in bodies.Values.OrderBy(u=>u.OrderSequence).ThenBy(u=>u.Id)) {
   if(!u.Pending)continue;var target=u.Route[u.Index];
   if(u.ReservedTo==null) {
    if(!CanReserve(u,target)){Wait(u);continue;}
    u.ReservedFrom=u.Position;u.ReservedTo=target;
   }
   double distance=u.Position.Distance(target),budget=Movement.TilesPerSecond/Movement.TickRate;
   if(distance>Movement.ArrivalTolerance)u.Facing=MovementSimulation.Direction(target.X-u.Position.X,target.Z-u.Position.Z);
   var next=distance<=budget+Movement.ArrivalTolerance?target:new Point(u.Position.X+(target.X-u.Position.X)*budget/distance,u.Position.Z+(target.Z-u.Position.Z)*budget/distance);
   if(!Grid.Clear(u.Position,next,Movement.Radius))throw new InvalidOperationException("Squad left static terrain");
   u.Moving=next!=u.Position;u.Position=next;if(u.Moving)u.MovingTicks++;u.State=TravelState.Moving;
   if(next==target){u.Index++;u.ReservedFrom=u.ReservedTo=null;if(!u.Pending){u.State=TravelState.Arrived;u.Moving=false;}}
  }
 }
}
