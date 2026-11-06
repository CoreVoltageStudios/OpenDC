using DarkColony.Assets;
namespace DarkColony.Original;

// AI3, exact DC16 44c190. Run-owned state, stable entity incarnations, no presentation clock.
// The implemented task variants are those exercised by A02. Other original variants
// fail explicitly at their decision gate instead of silently becoming generic RTS AI.
public sealed class NativeStrategyState {
 public NativeStrategyZone[] Zones=Enumerable.Range(0,256).Select(_=>new NativeStrategyZone()).ToArray();
 public NativeStrategyMemory?[] Memory=new NativeStrategyMemory?[800];
 public NativeStrategyAgent?[] Agents=new NativeStrategyAgent?[800];
 public NativeStrategyGroup[][] Tasks=Enumerable.Range(0,4).Select(_=>Enumerable.Range(0,16).Select(_=>new NativeStrategyGroup()).ToArray()).ToArray();
 public long Updates; public int ProductionRule=-1; public string ProductionResult="";
 public bool FirstAssignment=true;
 // 44c07c defaults;44c2a4 messages1..5 change roles0,2,3,4,5.
 [System.Text.Json.Serialization.JsonRequired] public int[] ProductionWeights={1,2,1,2,4,2,10000,4,10000};
}
public sealed class NativeStrategyZone {
 public Cell Center;public int Distance=255;public int[] Neighbors=Array.Empty<int>();
 public int AirOwner=-1,AirPower,GroundOwner=-1,GroundPower,FlyingOwner=-1,FlyingPower,Unarmed;
 public int Flags;
}
public sealed record NativeStrategyMemory(Cell Position,int Type,int Owner);
public sealed class NativeStrategyAgent {
 public int Slot,Incarnation,Task,Group,Status,Zone,Stuck;public Cell Last;
}
public sealed class NativeStrategyGroup {
 public bool Active;public int CurrentZone,TargetZone,Mode,Cursor;public int[] Route=Array.Empty<int>();
 public List<int> Members=new();public int[] Counts=new int[9];
}
public sealed partial class OriginalWorld {
 public void StrategyMessage(int team,int[] message){
  if(team is <0 or >=8||Teams[team].Ai!=3||message.Length!=2||message[0] is <1 or >5)
   throw new NotSupportedException("Unestablished AI message");
  var state=Teams[team].Strategy??=InitializeStrategy3(team);
  int role=message[0]==1?0:message[0];
  state.ProductionWeights[role]=unchecked((short)message[1]);
  Emit("strategy-configuration",null,team,message[0]);
 }
 static int StrategyRole(int type)=>type<16?type%8:type is 49 or 50?7:type is 41 or 42?1:8; //456570
 int StrategyZone(Cell cell)=>Navigation.Inside(cell)?Data.Scenario.Path.At(cell.X,cell.Z):0;
 NativeStrategyState InitializeStrategy3(int team) {
  var state=new NativeStrategyState();var path=Data.Scenario.Path;
  var t=Teams[team];var start=new Cell(t.Base[0],t.Base[1]);if(start.X==0||start.Z==0)start=new(t.Rally[0],t.Rally[1]);
  if(!Navigation.Ground(start))start=NearestGround(start,false);int first=StrategyZone(start);
  if(first==0)throw new InvalidDataException("AI3 start zone is zero");
  //442ecc builds CLIST2 from distinct next hops in target-index order, including self.
  for(int z=1;z<255;z++)state.Zones[z].Neighbors=Enumerable.Range(1,254).Select(i=>(int)path.Table[z*256+i]).Where(i=>i!=0).Distinct().ToArray();
  var dist=Enumerable.Repeat(256,256).ToArray();dist[first]=0;
  while(true){int best=-1;for(int z=1;z<255;z++)if(dist[z]<256&&(best<0||dist[z]<dist[best]))best=z;if(best<0)break;
   int d=dist[best];state.Zones[best].Distance=d;dist[best]=257;
   foreach(int n in state.Zones[best].Neighbors)if(dist[n]!=257&&d+1<dist[n])dist[n]=d+1;
  }
  var sx=new int[256];var sz=new int[256];var count=new int[256];
  for(int z=0;z<path.Height;z++)for(int x=0;x<path.Width;x++){int f=path.At(x,z);sx[f]+=x;sz[f]+=z;count[f]++;}
  for(int f=1;f<255;f++)if(count[f]>0){var center=new Cell(sx[f]/count[f],sz[f]/count[f]);bool found=false;
   for(int r=0;r<100&&!found;r++)for(int z=center.Z-r;z<=center.Z+r&&!found;z++)for(int x=center.X-r;x<=center.X+r;x++)
    if(Navigation.Inside(new(x,z))&&path.At(x,z)==f){state.Zones[f].Center=new(x,z);found=true;break;}
   if(!found)throw new InvalidDataException("AI3 zone centroid search failed");
  }
  int initial=Enumerable.Range(1,254).First(z=>state.Zones[z].Distance<3);
  void Group(int task,int id,int mode){var g=state.Tasks[task][id];g.Active=true;g.CurrentZone=g.TargetZone=initial;g.Route=new[]{initial};g.Mode=mode;}
  Group(0,0,0);Group(1,0,0);Group(2,0,2);Group(2,1,2);Group(3,0,0);
  Emit("strategy-initialized",null,team,3);return state;
 }
 void StepStrategy3(int team) {
  var s=Teams[team].Strategy??=InitializeStrategy3(team);s.Updates++;
  if(s.FirstAssignment){AssignStrategy3(team,s);s.FirstAssignment=false;}
  UpdateStrategyKnowledge(team,s);
  AssignStrategy3(team,s);StrategyProduction(team,s);
  //44c190: pre/count, cleanup, goals, orders in task order. Empty mining task is
  //the original A02 case, not an invented economy for its cityless enemy team.
  for(int task=0;task<4;task++){
   CleanupStrategy(s,task);
   if(task==0)StrategyMiners(team,s);
   if(task==1){bool force=StrategyDefenseGoals(team,s);for(int i=0;i<16;i++)if(s.Tasks[1][i].Active)StrategyGroupOrders(team,s,s.Tasks[1][i],force&&i==0);}
   if(task==2){StrategyAttackGoals(team,s);foreach(var g in s.Tasks[2])if(g.Active)StrategyGroupOrders(team,s,g,false);}
   if(task==3)StrategyScouts(team,s);
  }
 }
 void AssignStrategy3(int team,NativeStrategyState s){
  var pending=new int[9];var assignment=new int[4,9];
  bool Unassigned(NativeEntity e)=>e.Slot>=120&&e.Living&&e.Owner==team&&(s.Agents[e.Slot]==null||s.Agents[e.Slot]!.Incarnation!=e.Incarnation);
  foreach(var e in Entities)if(Unassigned(e)&&e.Type is not (41 or 42))pending[StrategyRole(e.Type)]++;
  int Count(int task,int role)=>s.Tasks[task].Sum(g=>g.Counts[role]);
  assignment[0,6]=pending[6];pending[6]=0;assignment[1,1]=pending[1];pending[1]=0;
  if(DependencyState(team,10)==1||DependencyState(team,24)==1||pending[5]>0){assignment[3,5]=pending[5];pending[5]=0;}
  else if(Count(1,0)+Count(2,0)>=Count(3,0)*4){assignment[3,0]=pending[0];pending[0]=0;}
  for(int role=0;role<9;role++){
   int defense=Count(1,role),attack=Count(2,role);
   while(pending[role]-->0){if(defense*192>attack*64){assignment[2,role]++;attack++;}else{assignment[1,role]++;defense++;}}
  }
  foreach(var e in Entities)if(Unassigned(e)){
   int role=StrategyRole(e.Type),task=-1;for(int i=0;i<4;i++)if(assignment[i,role]>0){task=i;assignment[i,role]--;break;}if(task<0)continue;
   int chosen=0;
   if(task is 1 or 2){int score=int.MaxValue,total=int.MaxValue;for(int i=0;i<16;i++){
    var g=s.Tasks[task][i];if(!g.Active)continue;
    int weight=task==1?(i==0?1:2):(i+1)*(g.Mode==1?1:4),candidate=g.Counts[role]*weight,sum=g.Counts.Sum();
    if(candidate<score||candidate==score&&sum<total){chosen=i;score=candidate;total=sum;}
   }}
   var selected=s.Tasks[task][chosen];selected.Members.Insert(0,e.Slot);selected.Counts[role]++;
   s.Agents[e.Slot]=new(){Slot=e.Slot,Incarnation=e.Incarnation,Task=task,Group=chosen,Last=e.Cell};
   Emit("strategy-assigned",e,task,chosen);
  }
 }
 void CleanupStrategy(NativeStrategyState s,int task){
  foreach(var g in s.Tasks[task]){
   for(int i=g.Members.Count-1;i>=0;i--){int id=g.Members[i];var a=s.Agents[id];var e=Slots[id];
    if(e==null||!e.Living||a==null||a.Incarnation!=e.Incarnation){g.Members.RemoveAt(i);if(a!=null&&a.Task==task)s.Agents[id]=null;}
   }
   Array.Clear(g.Counts);foreach(int id in g.Members)g.Counts[StrategyRole(Slots[id]!.Type)]++;
  }
 }
 int StrategyPower(int type,int count=1){
  var t=Data.Scenario.Types[type];if(!Data.Weapons.TryGetValue(t.Fields[5],out var weapon))return 0;
  int denominator=t.Fields[10]==2?50:Data.DamageFactors[1][t.Fields[10]];
  if(denominator==0)throw new InvalidDataException("AI3 power denominator zero");
  return Data.DamageFactors[weapon.Class][1]*count*25/denominator;
 }
 void UpdateStrategyKnowledge(int team,NativeStrategyState s){
  foreach(var z in s.Zones){z.AirOwner=z.GroundOwner=z.FlyingOwner=-1;z.AirPower=z.GroundPower=z.FlyingPower=z.Unarmed=0;z.Flags&=~1;}
  for(int id=0;id<800;id++){
   var entity=Slots[id];if(entity==null||entity.Owner>=8)continue;
   bool visible=Visible(team,entity.Cell);NativeStrategyMemory? m=null;
   if(visible&&entity.State!=0){m=new(entity.Cell,entity.Type,entity.Owner);if(entity.Owner!=team)s.Memory[id]=m;}
   else if(s.Memory[id] is {} old){if(Visible(team,old.Position))s.Memory[id]=null;else m=old;}
   if(m==null||m.Owner!=team&&Relations[team,m.Owner])continue;
   var cell=m.Position;if(StrategyZone(cell)==0)cell=NearestGround(cell,false);int zone=StrategyZone(cell);var z=s.Zones[zone];var type=Data.Scenario.Types[m.Type];
   if(!Data.Weapons.TryGetValue(type.Fields[5],out var weapon)){if(m.Owner!=team&&type.Fields[10]!=8)z.Unarmed++;continue;}
   void Accumulate(ref int owner,ref int power,int amount){if(amount<=0)return;if(owner==m.Owner)power=unchecked((ushort)(power+amount));else{
    if(owner!=-1)z.Flags|=1;if(amount<power)power-=amount;else{power=unchecked((ushort)(amount-power));owner=m.Owner;}
   }}
   int p=StrategyPower(m.Type);if(type.Flying!=0)Accumulate(ref z.FlyingOwner,ref z.FlyingPower,p);
   Accumulate(ref z.GroundOwner,ref z.GroundPower,p);Accumulate(ref z.AirOwner,ref z.AirPower,Data.DamageFactors[weapon.Class][2]);
  }
  foreach(var z in s.Zones)if((z.AirOwner!=-1&&z.GroundOwner!=-1&&z.AirOwner!=z.GroundOwner)||(z.AirOwner!=-1&&z.FlyingOwner!=-1&&z.AirOwner!=z.FlyingOwner)||(z.GroundOwner!=-1&&z.FlyingOwner!=-1&&z.GroundOwner!=z.FlyingOwner))z.Flags|=1;
 }
 int[] StrategyRoute(int from,int to){
  var result=new List<int>();for(int n=0;n<256;n++){if(from==0)return Array.Empty<int>();result.Add(from);if(from==to)return result.ToArray();from=Data.Scenario.Path.Table[from*256+to];}
  throw new InvalidDataException("AI3 cyclic zone route");
 }
 int StrategyDistance(int from,int to){if(from==0||to==0)return 255;var route=StrategyRoute(from,to);return route.Length==0?255:route.Length-1;}
 void StrategySetRoute(NativeStrategyGroup g,int target){var route=StrategyRoute(g.CurrentZone,target);if(route.Length==0)throw new InvalidDataException("AI3 unreachable goal");g.Route=route;g.Cursor=g.CurrentZone==target?0:1;g.TargetZone=target;}
 bool StrategyDefenseGoals(int team,NativeStrategyState s){
  bool force=false;if((Random.Next()&15)==0){int chosen=-1,count=1;for(int i=1;i<255;i++)if(s.Zones[i].Distance<2&&Random.Next()%count==0){chosen=i;count++;}
   if(chosen<0)throw new InvalidDataException("AI3 defense has no rally zone");StrategySetRoute(s.Tasks[1][0],chosen);force=true;
  }
  return force; //Additional defense groups are requested by mining/expansion variants, absent in A02.
 }
 void StrategyOrder(NativeEntity e,OriginalOrder order,Cell destination){
  var command=new NativeCommand(e.Slot,e.Incarnation,order,destination.X*256,destination.Z*256,Issuer:e.Owner);
  bool accepted=AcceptCommand(command,true);RecordOrder(command,accepted);Emit("strategy-order",e,(int)order,accepted?1:0);
 }
 void StrategyGroupOrders(int team,NativeStrategyState s,NativeStrategyGroup g,bool force){
  bool wait=false;
  foreach(int id in g.Members){var e=Slots[id]!;var a=s.Agents[id]!;if(a.Status==0)a.Status=2;
   if(e.Cell==a.Last)a.Stuck=Math.Min(60,a.Stuck+1);else{a.Stuck=0;a.Last=e.Cell;}
   int distance=StrategyDistance(StrategyZone(e.Cell),g.CurrentZone);
   if(e.Type is 1 or 9&&distance<=2&&a.Stuck>=4){StrategyOrder(e,OriginalOrder.Deploy,e.Cell);a.Stuck=0;continue;}
   if(distance<4&&!force){if(a.Status==2)a.Status=1;}
   else{if(a.Stuck<60&&a.Status!=2)wait=true;if(a.Zone!=g.CurrentZone){a.Zone=g.CurrentZone;StrategyOrder(e,a.Status==2?OriginalOrder.MoveAttack:OriginalOrder.MoveOnly,s.Zones[a.Zone].Center);}}
  }
  if(wait)return;g.CurrentZone=g.Route[g.Cursor];if(g.CurrentZone!=g.TargetZone)g.Cursor++;
  foreach(int id in g.Members){var a=s.Agents[id]!;if(a.Zone!=g.CurrentZone){a.Zone=g.CurrentZone;StrategyOrder(Slots[id]!,OriginalOrder.MoveAttack,s.Zones[a.Zone].Center);}}
 }
 int StrategyThreat(int team,NativeStrategyState s,int from,int target,int[]? route=null){
  route??=StrategyRoute(from,target);if(route.Length==0)return -1;
  Span<bool> near=stackalloc bool[256];Span<bool> expanded=stackalloc bool[256];
  foreach(int z in route){near[z]=true;foreach(int n in s.Zones[z].Neighbors)near[n]=true;if(z==target)break;}
  near.CopyTo(expanded);for(int z=1;z<256;z++)if(near[z])foreach(int n in s.Zones[z].Neighbors)expanded[n]=true;
  int total=0;for(int z=0;z<256;z++)if(expanded[z]&&s.Zones[z].GroundOwner is >=0&&s.Zones[z].GroundOwner!=team)total+=s.Zones[z].GroundPower;return total;
 }
 void StrategyAttackGoals(int team,NativeStrategyState s){
  var defense=new int[256];int budget=0;for(int i=0;i<16;i++){var d=s.Tasks[1][i];if(d.Active){defense[d.TargetZone]=i==0?1:2;budget=Math.Max(budget,s.Zones[d.TargetZone].Distance);}}budget+=3;
  var used=new bool[256];var resting=new int[256];for(int i=0;i<16;i++)if(s.Tasks[2][i].Active){if(s.Tasks[2][i].Mode==0)used[i]=true;if(s.Tasks[2][i].Mode is 1 or 3)resting[i]++;} //Native indexes the group here, not its target.
  for(int i=0;i<16;i++){
   var g=s.Tasks[2][i];if(!g.Active)continue;var counts=new int[9];foreach(int id in g.Members)if(s.Agents[id]!.Status==1)counts[StrategyRole(Slots[id]!.Type)]++;
   int strength=0;for(int role=0;role<9;role++)strength+=StrategyPower(role,counts[role]);
   if(g.Mode is 2 or 3){int chosen=-1,best=0;
    for(int z=0;z<256;z++)if(!used[z]){
     var zone=s.Zones[z];int threat=StrategyThreat(team,s,g.CurrentZone,z),desire=0;
     if(zone.Distance<=budget&&(threat!=0||(zone.Flags&1)!=0))desire=(zone.Flags&2)==0?budget+1-zone.Distance:budget/2;
     if(zone.Unarmed!=0)desire+=budget/2;if(desire==0||strength==0||threat<0)continue;
     int score=strength*desire/Math.Max(1,threat);if(score>best){chosen=z;best=score;}
    }
    if(chosen>=0){used[chosen]=true;g.Mode=0;StrategySetRoute(g,chosen);}
    else if(g.Mode==2){g.Mode=3;int target=-1,priority=int.MaxValue,distance=int.MaxValue;
     for(int z=0;z<256;z++)if(defense[z]!=0){int d=StrategyDistance(z,g.CurrentZone);if(resting[z]<priority||resting[z]==priority&&d<distance){target=z;priority=resting[z];distance=d;}}
     if(target<0)throw new InvalidDataException("AI3 no fallback zone");StrategySetRoute(g,target);resting[target]++;
    }
   }else if(g.Mode==0){int threat=StrategyThreat(team,s,g.CurrentZone,g.TargetZone,g.Route.Skip(g.Cursor).ToArray());
    if(threat==0&&(s.Zones[i].Flags&1)==0&&s.Zones[g.TargetZone].Unarmed==0||strength*2<=threat)g.Mode=2;
   }else if(g.Mode==1)throw new NotSupportedException("AI3 forming-group variant not used by A02");
  }
 }
 void StrategyScouts(int team,NativeStrategyState s){
  foreach(int id in s.Tasks[3][0].Members){var e=Slots[id]!;var a=s.Agents[id]!;bool idle=e.Actions.Count>0&&e.Actions[0].Kind==1,choose=false;
   if(a.Status is 0 or 4)choose=idle;else if(a.Status==5){if(idle)a.Status=6;}else if(a.Status==6)choose=(Random.Next()&63)==0||e.StrategyHit;
   else throw new InvalidDataException("AI3 scout state");e.StrategyHit=false;if(!choose)continue;
   Cell destination=default;bool random=true;
   if((Random.Next()&1)==0){int best=0,target=0;var near=new bool[256];
    for(int z=1;z<256;z++){Array.Clear(near);near[z]=true;foreach(int n in s.Zones[z].Neighbors)near[n]=true;bool danger=false;
     for(int n=0;n<256&&!danger;n++)if(near[n])foreach(int n2 in s.Zones[n].Neighbors)if(s.Zones[n2].AirOwner>=0&&s.Zones[n2].AirOwner!=team){danger=true;break;}
     if(danger)continue;var zone=s.Zones[z];int value=zone.GroundOwner>=0&&zone.GroundOwner!=team?zone.GroundPower:0;
     if(zone.FlyingOwner>=0&&zone.FlyingOwner!=team)value-=zone.FlyingPower;if(zone.Unarmed!=0)value++;
     if(value>best){best=value;target=z;}
    }
    if(best>0){destination=s.Zones[target].Center;a.Status=5;random=false;}
   }
   Cell RandomGround(){for(int n=0;n<100000;n++){var c=new Cell(Random.Next()%Data.Scenario.Map.Width,Random.Next()%(Data.Scenario.Map.Height-5));if(StrategyZone(c)!=0)return c;}throw new InvalidDataException("AI3 random ground search exhausted");}
   if(random){
    if((Random.Next()&3)==0){int count=0;NativeEntity? vent=null;foreach(var candidate in Entities)if(candidate.Living&&candidate.Type==40){count++;if((Random.Next()&255)<256/count)vent=candidate;}
     if(vent==null)destination=RandomGround();else{
      destination=vent.Cell;a.Status=4;var probe=destination;
      for(int n=0;n<100000;n++){int z=StrategyZone(probe);if(z==0)break;int danger=0;
       //45a6e0 sums owner IDs here (including zero), not strength; retain that quirk.
       foreach(int near in s.Zones[z].Neighbors.Prepend(z)){int owner=s.Zones[near].FlyingOwner;if(owner>=0&&owner!=team)danger+=owner;}
       if(danger==0){destination=probe;break;}
       probe=new(Math.Clamp(probe.X+(Random.Next()&15)-8,0,Data.Scenario.Map.Width-1),Math.Clamp(probe.Z+(Random.Next()&15)-8,0,Data.Scenario.Map.Height-1));
       if(n==99999)throw new InvalidDataException("AI3 vent safety search exhausted");
      }
     }
    }else{destination=RandomGround();a.Status=4;}
   }
   StrategyOrder(e,e.Type is 5 or 13?OriginalOrder.MoveOnly:OriginalOrder.MoveAttack,destination);
  }
 }
 int[]? strategyBuildings;
 void StrategyProduction(int team,NativeStrategyState s){
  strategyBuildings??=Data.Reference.Ints(0x49903c,18);var t=Teams[team];
  var counts=new int[9];foreach(var task in s.Tasks)foreach(var g in task)for(int role=0;role<9;role++)counts[role]+=g.Counts[role];
  foreach(var q in t.Production)foreach(int type in q.Types)counts[StrategyRole(type)]++;
  //499084 policy order. A02's zero base disables construction; the subsequent
  //harvester goal has no eligible product. No artificial resources or city spawn.
  var rules=new (int Kind,int Value)[]{(0,8),(1,1),(0,0),(2,5),(0,1),(0,3),(2,10),(1,2),(2,15),(0,4),(0,2),(2,20),(0,7),(0,6),(2,30),(0,5),(2,200),(3,0)};
  for(int i=0;i<rules.Length;i++){
   var (kind,value)=rules[i];int id=-1;
   if(kind==0){id=strategyBuildings[value*2+t.Race];var dep=Data.Scenario.Dependencies.Single(d=>d.Id==id);
    if(DependencyState(team,id)!=1&&!t.Cities[dep.Subject].InitializationPending)continue;
    if(t.Cities[dep.Subject].InitializationPending){s.ProductionRule=i;s.ProductionResult="Construction pending";return;}
   }else if(kind==1){if(counts[6]>=value)continue;
    var miners=Data.Scenario.Dependencies.Where(d=>d.Kind==1&&StrategyRole(d.Subject)==6&&DependencyState(team,d.Id)==1).OrderBy(d=>d.Subject);
    s.ProductionRule=i;s.ProductionResult="No eligible harvester";foreach(var miner in miners){var result=RequestProduction(team,miner.Id,ProductionIssuer.Strategy);s.ProductionResult=result.Reason;if(!result.Accepted)return;}return;
   }else if(kind==2){int army=counts[0]+counts[2]+counts[3]+counts[4]+counts[5];if(army>=value||Entities.Count(e=>e.Owner==team&&e.Slot>=152)>=Session.MaximumArmy)continue;
    var weights=s.ProductionWeights;int best=10000;
    foreach(var d in Data.Scenario.Dependencies.Where(d=>d.Kind==1).OrderBy(d=>d.Subject)){int role=StrategyRole(d.Subject);if(role==8||DependencyState(team,d.Id)!=1)continue;int priority=role==6?10000:counts[role]*weights[role];if(priority<best){best=priority;id=d.Id;}}
   }
   s.ProductionRule=i;s.ProductionResult=id<0?"No eligible product":RequestProduction(team,id,ProductionIssuer.Strategy).Reason;return;
  }
 }
}
