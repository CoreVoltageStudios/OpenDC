namespace DarkColony.Original;

public sealed class NativeTransport {
 public int Slot,Team,Reservation,Stage,Cursor,DestinationX,DestinationZ;
 public Cell Requested;
 public ushort[] Manifest=new ushort[5];
 public string Source="";
}

public sealed partial class OriginalWorld {
 public Dictionary<int,NativeTransport> Transports {get;}=new();
 public bool[,] TransportReservations {get;}=new bool[8,8];
 public NativeEntity Reinforce(int team,Cell requested,IReadOnlyList<(int Type,int Count)> manifest,string source) {
  if(team<0||team>=8||manifest.Count!=5)throw new ArgumentException("Original transport manifest/team");
  int reservation=Enumerable.Range(0,8).FirstOrDefault(i=>!TransportReservations[team,i],-1);
  if(reservation<0)throw new InvalidOperationException("Native transport reservations exhausted");
  int slot=team*15+7+reservation,type=Teams[team].Race==1?93:92;
  TransportReservations[team,reservation]=true;
  var e=NewEntity(slot,type,8,0,0,Data.Scenario.Types[type].MaximumHealth,0,source);
  e.X=unchecked((ushort)(requested.X*256+128+((Random.Next()&1)*2-1)*256));
  e.Z=unchecked((ushort)(requested.Z*256+128+((Random.Next()&1)*2-1)*256));
  var t=new NativeTransport{Slot=slot,Team=team,Reservation=reservation,Requested=requested,DestinationX=requested.X*256+128,
   DestinationZ=requested.Z*256+128,Manifest=manifest.Select(p=>(ushort)((p.Type&255)<<8|(p.Count&255))).ToArray(),Source=source};
  Transports[slot]=t;e.Actions.Add(new(21));
  QueueStep(e,t.DestinationX,t.DestinationZ,e.Cell);QueueVertical(e,false);Emit("transport-created",e,team,reservation);
  AudioCue("transport-created",e,height:65536);return e; // 4191ae initial sound height, distinct from body height.
 }
 public void ExtractCommander(int commanderTeam,int transportTeam,string source) {
  int id=CommanderIds[commanderTeam][0];if(id<0||Slots[id] is not {Living:true} e)return;
  Reinforce(transportTeam,e.Cell,new[]{(255,1),(id>>8,id&255),(0,0),(0,0),(0,0)},source);
  Emit("extraction-requested",e,transportTeam);
 }
 public void RecoverCommander(NativeEntity e) {
  // 41bb57..41bb67 sets world+0 only for "atlantis.bts" (483978); 416412 gates recovery.
  if(e.Owner>=8||Teams[e.Owner].CommanderRecoveryDisabled!=0||Data.Scenario.Header.TerrainName.Equals("ATLANTIS.BTS",StringComparison.OrdinalIgnoreCase))return;
  Reinforce(e.Owner,e.Cell,new[]{(0,0),(0,e.Slot>>8),(0,e.Slot&255),(0,0),(0,0)},"native/recovery/"+e.Identity);
 }
 void QueueVertical(NativeEntity e,bool up,int? initialHeight=null) {
  int height=initialHeight??(e.Type==93?1200:600);
  e.Actions.Add(new(22){Word=up?0:50,Mode=up?1:0,Health=height});e.Height=height+(up?0:7500);
  e.Body.Start(Data.Clips[e.Type].Stand,0);Emit(up?"transport-ascent":"transport-descent",e);
 }
 void VerticalStep(NativeEntity e,NativeAction a) {
  AudioCue("transport-volume",e); // 4183e8 updates with OLD height, before even zero-counter pop.
  int counter=a.Word;
  if(a.Mode==0){if(counter==0){Pop(e);return;}a.Word--;}
  // 41841e captures the old counter before decrement; preserve its one-update lag.
  e.Height=a.Health+6*counter*counter/2;
  if(a.Mode!=0&&++a.Word==50){Pop(e);Emit("transport-sound-stop",e);AudioCue("transport-sound-stop",e);}
 }
 bool TransportStep(NativeEntity e) {
  var t=Transports[e.Slot];
  if(t.Stage==0)t.Stage=1;
  else if(t.Stage==2) {
   if(TransportReservations[t.Team,t.Reservation])Emit("transport-release",e,t.Team,t.Reservation);
   TransportReservations[t.Team,t.Reservation]=false;e.Body.Start(Data.Clips[e.Type].Stand,2);Wait(e,60);return false;
  }
  // Unlike ordinary travel, unload handler418b31 explicitly writes the recorded anchor.
  e.X=t.DestinationX;e.Z=t.DestinationZ;
  if(t.Manifest[0]>>8==255) {
   if(t.Cursor==0){t.Cursor++;return true;}
   int id=t.Manifest[t.Cursor];var target=Slots[id];
   if(target is {Living:true}) {
    if(Math.Abs(e.X-target.X)+Math.Abs(e.Z-target.Z)>256) {
     t.DestinationX=target.X;t.DestinationZ=target.Z;QueueStep(e,target.X,target.Z,e.Cell);return false;
    }
    // 416250: noncombat State10/word1, stand mode1, no death statistic and no recovery.
    target.State=10;target.DeathWord=1;target.PendingOrder=null;target.Actions.Clear();target.Actions.Add(new(10){Word=1});
    target.Body.Start(Data.Clips[target.Type].Stand,1);ClearOccupancy(target);Emit("extracted",target,e.Slot);
   }
   if(++t.Cursor==(t.Manifest[0]&255)+1){t.Stage=2;QueueVertical(e,true);}return false;
  }
  if(t.Cursor==0&&t.Manifest[0]==0) {
   int id=((t.Manifest[1]&255)<<8)|(t.Manifest[2]&255);RecoveryArrived(id);t.Stage=2;QueueVertical(e,true);return false;
  }
  int packed=t.Manifest[t.Cursor],type=packed>>8,count=packed&255;
  if(Occupant(e.Cell,Ground)==-1) {
   var passenger=Spawn(type,t.Team,e.Cell,t.Source+"/passenger/"+t.Cursor);Emit("unloaded",passenger,e.Slot);
   t.Manifest[t.Cursor]=(ushort)(type<<8|--count);
   while(count==0) {
    if(++t.Cursor==5){t.Stage=2;QueueVertical(e,true);return false;}
    count=t.Manifest[t.Cursor]&255;
   }
  }
  var cell=NearestGround(t.Requested,true);t.DestinationX=cell.X*256+128;t.DestinationZ=cell.Z*256+128;
  QueueStep(e,t.DestinationX,t.DestinationZ,e.Cell);return false;
 }
}
