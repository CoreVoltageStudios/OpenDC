using Godot;
using DarkColony.Original;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Net.NetworkInformation;
using System.Net.Sockets;
namespace DarkColony.Viewer;

public sealed record WarPeer(int Id,int Slot,string Name,bool Ready=false,bool Loaded=false);
public sealed record WarPacket(string Kind){
 public int Protocol {get;init;}=WarNetworkSession.Protocol;
 public string Build {get;init;}=""; public string Name {get;init;}=""; public string Hash {get;init;}=""; public string Message {get;init;}="";
 public long Sequence {get;init;} public long Frame {get;init;} public long Tick {get;init;}
 public int Slot {get;init;}=-1; public int Revision {get;init;}
 public bool Flag {get;init;}
 public WarConfiguration? Setup {get;init;}
 public WarSlot? Player {get;init;}
 public WarPeer[]? Players {get;init;}
 public WarCommand? Command {get;init;}
 public WarCommand[]? Commands {get;init;}
}

// ENet reliable ordered messages; no scene replication, assets or profile writes.
// All callbacks and simulation steps run on the owning Godot thread. Loading is
// isolated background work; the lobby remains responsive until the barrier opens.
public sealed class WarNetworkSession : IDisposable {
 public const int Protocol=1,DefaultPort=27960,MaxPacket=32768;
 public static string Build=>typeof(OriginalMission).Module.ModuleVersionId.ToString("N")+"/"+typeof(WarNetworkSession).Module.ModuleVersionId.ToString("N")+"/"+typeof(DarkColony.Assets.OriginalScenarioImport).Module.ModuleVersionId.ToString("N");
 static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=20};
 public ENetMultiplayerPeer Peer {get;}=new();
 public bool Host {get;}
 public int Id=>Peer.GetUniqueId();
 public int LocalSlot=>Players.FirstOrDefault(p=>p.Id==Id)?.Slot??-1;
 public WarConfiguration Setup {get;private set;}
 public WarPeer[] Players {get;private set;}=Array.Empty<WarPeer>();
 public string Phase {get;private set;}="connecting"; public string Notice {get;private set;}="Connecting…";
 public int Revision {get;private set;} public int Port {get;}
 public SynchronizedWar? Match {get;private set;}
 public long Frame {get;private set;} public long SentBytes {get;private set;} public long ReceivedBytes {get;private set;} public long Rejected {get;private set;}
 internal long DiagnosticFastForwardUntil=0;
 internal bool DiagnosticHoldSimulation;
 internal bool DiagnosticHoldLoading;
 internal long DiagnosticFrameLimit=long.MaxValue;
 internal string? DiagnosticBuildOverride,DiagnosticContentOverride;
 public bool AllAcknowledged=>Players.Where(p=>p.Id!=1).All(p=>acks.GetValueOrDefault(p.Id)>=Frame);
 public int QueueDepth=>pending.Count+frames.Count;
 public int MaximumQueue {get;private set;}
 public double WorstHashMs {get;private set;}
 public double WorstStepMs {get;private set;}
 byte[]? checkedState;long checkedFrame;
 public event Action? Changed,MatchReady;
 public event Action<string>? Log;
 readonly string root,evidence,name;
 readonly HashSet<int> connectedPeers=new();
 readonly Dictionary<int,long> sequences=new(),acks=new();
 readonly Dictionary<int,ulong> seen=new(),ackProgress=new();
 readonly Dictionary<long,string> hashes=new();
 readonly List<WarCommand> pending=new();
 readonly Queue<WarPacket> frames=new();
 readonly Queue<WarPacket> history=new();
 Task<SynchronizedWar>? loading;
 ulong started,lastHost,now,departureNoticeUntil;long sequence;double accumulator;
 bool helloSent,disposed;string loadedHash="";
 public WarNetworkSession(string dataRoot,string evidencePath,string playerName,bool host,string address="",int port=DefaultPort,WarConfiguration? setup=null){
  root=dataRoot;evidence=evidencePath;name=CleanName(playerName);Host=host;Port=port;
  Setup=(setup??WarConfiguration.Default()) with{Multiplayer=true};
  Setup.Slots[0]=Setup.Slots[0] with{Name=name,Controller=WarController.Local};
  if(setup==null)Setup.Slots[1]=Setup.Slots[1] with{Controller=WarController.Remote,Name="Open slot"};
  started=lastHost=Time.GetTicksMsec();
  Peer.PeerConnected+=Connected;Peer.PeerDisconnected+=Disconnected;
  Peer.TransferMode=MultiplayerPeer.TransferModeEnum.Reliable;Peer.TransferChannel=0;
  Error error;
  if(host){Peer.SetBindIP("*");error=Peer.CreateServer(port,7,1);}
  else error=Peer.CreateClient(address.Trim(),port,1);
  if(error!=Error.Ok){Peer.PeerConnected-=Connected;Peer.PeerDisconnected-=Disconnected;Peer.Dispose();throw new IOException("Cannot "+(host?"host":"join")+" on UDP port "+port+": "+error);}
  if(host){Phase="lobby";Notice="Waiting for players. Share a reachable LAN or VPN address.";Players=new[]{new WarPeer(1,0,name)};}
 }
 public static string CleanName(string? value){value=value?.Trim();if(string.IsNullOrEmpty(value)||value.Length>16||value.Any(char.IsControl))throw new InvalidDataException("Use a name of 1–16 printable characters.");return value;}
 public static string[] Addresses()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
  .SelectMany(n=>n.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork).Select(a=>n.Name+": "+a.Address)).ToArray();
 void Connected(long id){if(disposed||Phase is "closed" or "leaving")return;connectedPeers.Add((int)id);seen[(int)id]=Time.GetTicksMsec();}
 void Disconnected(long id){
  connectedPeers.Remove((int)id);seen.Remove((int)id);disconnectAt.Remove((int)id);
  if(disposed||Phase is "closed" or "leaving")return;
  if(!Host){if(id!=1)return;Fail("The host left or the connection was lost. The match has ended.");return;}
  RemovePlayer((int)id,false,"Connection lost; AI takes over.");
 }
 void Send(int target,WarPacket packet){if(disposed||Phase=="closed"||Host&&target!=1&&!connectedPeers.Contains(target))return;var bytes=JsonSerializer.SerializeToUtf8Bytes(packet,Json);if(bytes.Length>MaxPacket)throw new InvalidDataException("Network message exceeds its bound.");Peer.SetTargetPeer(target);var e=Peer.PutPacket(bytes);if(e!=Error.Ok)throw new IOException("Network send failed: "+e);SentBytes+=bytes.Length;}
 void Broadcast(WarPacket packet){foreach(var p in Players)if(p.Id!=1)Send(p.Id,packet);}
 void Publish(){if(Host)Broadcast(new("lobby"){Setup=Setup,Players=Players,Revision=Revision});Changed?.Invoke();}
 public void Configure(WarConfiguration config){
  if(!Host||Phase!="lobby")return;
  config=config.Copy() with{Multiplayer=true};config.Validate(root);
  int capacity=WarCatalog.Find(root,config.MapId).Capacity;
  foreach(var p in Players){if(p.Slot>=capacity)throw new InvalidDataException("A connected player is outside this map's capacity. Choose a larger map or let that player leave.");
   if(config.Slots[p.Slot].Controller!=(p.Id==1?WarController.Local:WarController.Remote))throw new InvalidDataException("Connected player roles cannot be replaced with AI.");}
  
  Setup=config;ResetReady();Publish();
 }
 void ResetReady(){Revision++;Players=Players.Select(p=>p with{Ready=false,Loaded=false,Name=Setup.Slots[p.Slot].Name}).ToArray();}
 public void EditPlayer(WarSlot player){if(Phase!="lobby")return;if(Host){var c=Setup.Copy();c.Slots[LocalSlot]=player with{Controller=WarController.Local};Configure(c);}else Send(1,new("player"){Player=player,Revision=Revision});}
 public void Ready(bool value){if(Phase!="lobby")return;if(Host){Players=Players.Select(p=>p.Id==1?p with{Ready=value}:p).ToArray();Publish();}else Send(1,new("ready"){Flag=value,Revision=Revision});}
 public bool CanStart=>Host&&Phase=="lobby"&&Players.Length>=2&&Players.All(p=>p.Ready)&&Setup.Slots.Select((s,i)=>(s,i)).Where(p=>p.s.Controller==WarController.Remote).All(p=>Players.Any(x=>x.Slot==p.i));
 public void Start(){if(!CanStart)throw new InvalidOperationException("At least two connected players must be ready.");Setup.Validate(root);Peer.RefuseNewConnections=true;Broadcast(new("load"){Setup=Setup,Revision=Revision});BeginLoad(Setup);}
 void BeginLoad(WarConfiguration setup){Setup=setup.Copy();Phase="loading";Notice="Loading the agreed map and checking content…";started=Time.GetTicksMsec();int slot=LocalSlot;string contentRoot=root;var configuration=Setup.Copy();loading=Task.Run(()=>new SynchronizedWar(contentRoot,configuration,slot));Changed?.Invoke();}
 public void Submit(WarCommand command){
  if(Phase!="match"||Match==null||Match.Mission.AtOutcome)return;
  int owner=Array.IndexOf(Match.Mission.War!.Starts,LocalSlot);
  command=command with{Owner=owner};
  if(Host)AcceptCommand(1,new("command"){Command=command,Sequence=++sequence});
  else Send(1,new("command"){Command=command,Sequence=++sequence});
 }
 public void Timing(bool speed,bool value){if(!Host||Phase!="match"||Match==null)return;pending.RemoveAll(c=>c.Kind==(speed?"speed":"pause"));pending.Add(new(speed?"speed":"pause",Match.Mission.War!.LocalTeam,Enabled:value));}
 public void Surrender(){if(Phase!="match")return;if(Host)QueueSurrender(1);else Send(1,new("surrender"){Sequence=++sequence});}
 void QueueSurrender(int peer){var p=Players.FirstOrDefault(p=>p.Id==peer);if(p==null||Match==null)return;int owner=Array.IndexOf(Match.Mission.War!.Starts,p.Slot);if(!Match.Surrendered[owner]&&!pending.Any(c=>c.Kind=="surrender"&&c.Owner==owner))pending.Add(new("surrender",owner));}
 void AcceptCommand(int peer,WarPacket packet){
  if(packet.Sequence<=sequences.GetValueOrDefault(peer)){Rejected++;return;}sequences[peer]=packet.Sequence;
  var player=Players.FirstOrDefault(p=>p.Id==peer);var c=packet.Command;
  if(Phase!="match"||Match==null||player==null||c==null||c.Owner!=Array.IndexOf(Match.Mission.War!.Starts,player.Slot)||c.Kind is "depart" or "surrender" or "speed" or "pause"||pending.Count>=64||!Match.Valid(c)){Rejected++;return;}
  // Cap per-peer contributions between host frames; no unbounded command queue.
  if(pending.Count(x=>x.Owner==c.Owner)>=8){Rejected++;return;}pending.Add(c);
 }
 void RemovePlayer(int id,bool surrender,string reason){
  var player=Players.FirstOrDefault(p=>p.Id==id);if(player==null)return;
  if(Phase=="match"&&Match!=null)pending.Add(new(surrender?"surrender":"depart",Array.IndexOf(Match.Mission.War!.Starts,player.Slot)));
  else if(Phase=="loading"){Fail("A player left while loading. Return to Multiplayer and host again.");return;}
  Players=Players.Where(p=>p.Id!=id).ToArray();seen.Remove(id);acks.Remove(id);ackProgress.Remove(id);sequences.Remove(id);remoteContent.Remove(id);
  if(Phase=="lobby"){Setup.Slots[player.Slot]=Setup.Slots[player.Slot] with{Controller=WarController.None};ResetReady();Publish();}
  Notice=player.Name+": "+(Phase=="lobby"?"Left the lobby; slot is inactive.":reason);departureNoticeUntil=Time.GetTicksMsec()+5000;if(Phase=="match")Broadcast(new("notice"){Message=Notice});Log?.Invoke(Notice);Changed?.Invoke();
 }
 void Receive(int from,WarPacket p){
  // Retired sessions may drain only their leave acknowledgement. Late load/begin
  // packets cannot install a match or mutate the next session's UI.
  if(disposed||Phase=="closed")return;
  if(Phase=="leaving"){if(!Host&&from==1&&p.Kind is "bye" or "error")Phase="closed";return;}
  if(Host&&disconnectAt.ContainsKey(from))return;
  if(p.Kind==null||p.Kind.Length>24||p.Name==null||p.Name.Length>16||p.Build==null||p.Build.Length>128||p.Hash==null||p.Hash.Length>128||p.Message==null||p.Message.Length>1024)throw new InvalidDataException("Invalid packet fields");
  if(p.Protocol!=Protocol){RejectPeer(from,"Incompatible multiplayer protocol.");return;}
  if(!Host&&from!=1)return;
  seen[from]=now;if(!Host)lastHost=now;
  if(Host){
   if(p.Kind=="hello"){
    if(Phase!="lobby"||Players.Any(x=>x.Id==from)){RejectPeer(from,"This match has already started.");return;}
    if(p.Build!=Build){RejectPeer(from,"Different OpenDC build. Both players need the same source build.");return;}
    string joined=CleanName(p.Name);int capacity=WarCatalog.Find(root,Setup.MapId).Capacity;
    int slot=Enumerable.Range(0,capacity).FirstOrDefault(i=>Setup.Slots[i].Controller==WarController.Remote&&!Players.Any(x=>x.Slot==i),-1);
    if(slot<0){RejectPeer(from,"This map has no free player slot.");return;}
    Setup.Slots[slot]=Setup.Slots[slot] with{Controller=WarController.Remote,Name=joined};
    Players=Players.Append(new WarPeer(from,slot,joined)).ToArray();ResetReady();Publish();return;
   }
   var player=Players.FirstOrDefault(x=>x.Id==from);if(player==null){RejectPeer(from,"Join handshake required.");return;}
   if(p.Kind=="player"&&Phase=="lobby"&&p.Player!=null&&p.Revision==Revision){var c=Setup.Copy();c.Slots[player.Slot]=p.Player with{Controller=WarController.Remote,Name=CleanName(p.Player.Name)};try{Configure(c);}catch(InvalidDataException e){Send(from,new("notice"){Message=e.Message});}return;}
   if(p.Kind=="ready"&&Phase=="lobby"&&p.Revision==Revision){Players=Players.Select(x=>x.Id==from?x with{Ready=p.Flag}:x).ToArray();Publish();return;}
   if(p.Kind=="loaded"&&Phase=="loading"){if(p.Hash.Length!=64){RejectPeer(from,"Invalid content fingerprint.");return;}remoteContent[from]=p.Hash;Players=Players.Select(x=>x.Id==from?x with{Loaded=true}:x).ToArray();Changed?.Invoke();return;}
   if(p.Kind=="command"){AcceptCommand(from,p);return;}
   if(p.Kind=="surrender"&&Phase=="match"){if(p.Sequence>sequences.GetValueOrDefault(from)){sequences[from]=p.Sequence;QueueSurrender(from);}return;}
   if(p.Kind=="ack"&&Phase=="match"){
    if(p.Frame<acks.GetValueOrDefault(from)||p.Frame>Frame){Rejected++;return;}if(p.Frame>acks.GetValueOrDefault(from))ackProgress[from]=now;acks[from]=p.Frame;
    if(p.Hash.Length>0){if(p.Hash.Length!=64||!hashes.TryGetValue(p.Frame,out var hash)){Rejected++;return;}if(hash!=p.Hash)Desync(p.Frame,from,p.Hash);}
    return;
   }
   if(p.Kind=="leave"){Send(from,new("bye"));RemovePlayer(from,false,"Left the match; AI takes over.");disconnectAt[from]=now+500;return;}
   if(p.Kind=="ping"){Send(from,new("pong"));return;}
  }else{
   if(p.Kind=="lobby"&&Phase is "connecting" or "lobby"){
    if(p.Setup==null||p.Players==null||p.Players.Length>8||p.Players.Select(x=>x.Id).Distinct().Count()!=p.Players.Length||p.Players.All(x=>x.Id!=Id))throw new InvalidDataException("Invalid lobby assignment");
    p.Setup.Validate(root);Setup=p.Setup;Players=p.Players;Revision=p.Revision;Phase="lobby";Notice="Choose your player settings, then mark ready.";Changed?.Invoke();return;
   }
   if(p.Kind=="load"&&Phase=="lobby"&&p.Setup!=null&&p.Revision==Revision){BeginLoad(p.Setup);return;}
   if(p.Kind=="begin"&&Phase=="loading"&&Match!=null){StartClock();return;}
   if(p.Kind=="frame"&&Phase=="match"){
    if(p.Frame!=Frame+frames.Count+1||p.Commands==null||p.Commands.Length>64||frames.Count>=8)throw new InvalidDataException("Invalid or excessive simulation batch");frames.Enqueue(p);return;
   }
   if(p.Kind=="error"){if(p.Message.StartsWith("Synchronization mismatch"))SaveMismatch();Fail(p.Message,false);return;}
   if(p.Kind=="bye"){Phase="closed";return;}
   if(p.Kind=="pong")return;
   if(p.Kind=="notice"){Notice=p.Message;Changed?.Invoke();return;}
  }
  Rejected++;
 }
 readonly Dictionary<int,string> remoteContent=new();
 ulong lastPing;
 ulong leavingAt;
 readonly Dictionary<int,ulong> disconnectAt=new();
 public void Leave(){if(disposed||Phase is "closed" or "leaving")return;
  if(Peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Disconnected){Phase="closed";return;}
  if(Host)Broadcast(new("error"){Message="The host ended the session."});else if(helloSent)Send(1,new("leave"));
  Phase="leaving";leavingAt=Time.GetTicksMsec();Peer.Poll();
 }
 void StartClock(){Phase="match";Notice="Connected";accumulator=0;Frame=0;acks.Clear();ackProgress.Clear();foreach(var p in Players)ackProgress[p.Id]=Time.GetTicksMsec();sequences.Clear();sequence=0;hashes.Clear();frames.Clear();MatchReady?.Invoke();Changed?.Invoke();}
 public void Poll(double delta){
  if(disposed||Phase=="closed")return;
  try{
   now=Time.GetTicksMsec();if(!TransportActive())return;Peer.Poll();if(disposed||Phase=="closed"||!TransportActive())return;
   if(!Host&&Phase=="connecting"&&!helloSent&&Peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected){helloSent=true;Send(1,new("hello"){Build=DiagnosticBuildOverride??Build,Name=name});}
   int count=0;while(Peer.GetAvailablePacketCount()>0&&count++<128){int from=Peer.GetPacketPeer();var bytes=Peer.GetPacket();ReceivedBytes+=bytes.Length;if(bytes.Length>MaxPacket){RejectPeer(from,"Oversized network message.");continue;}try{var p=JsonSerializer.Deserialize<WarPacket>(bytes,Json)??throw new InvalidDataException("Empty packet");Receive(from,p);}catch(Exception e) when(e is JsonException or InvalidDataException or ArgumentException){RejectPeer(from,"Invalid message: "+e.Message);}}
   if(Phase=="closed")return;
   foreach(var id in disconnectAt.Keys.ToArray())if(now>=disconnectAt[id]){disconnectAt.Remove(id);DisconnectTransport(id);}
   if(Phase=="leaving"){if(now>=leavingAt+1000)Phase="closed";return;}
   if(!Host&&helloSent&&Peer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected&&now-lastPing>1000){lastPing=now;Send(1,new("ping"));}
   if(Phase=="connecting"&&now-started>12000){Fail("No response. Check the address, UDP port, VPN connection and firewall permissions.");return;}
   if(!Host&&Phase!="connecting"&&now-lastHost>20000){Fail("The host stopped responding. The session has ended.");return;}
   if(Host)foreach(var p in Players.Where(p=>p.Id!=1&&now>seen.GetValueOrDefault(p.Id,now)&&now-seen.GetValueOrDefault(p.Id,now)>20000).ToArray()){RemovePlayer(p.Id,false,"Timed out; AI takes over.");DisconnectTransport(p.Id);}
   if(Host&&Phase=="match"&&Match?.Mission.AtOutcome==false)foreach(var p in Players.Where(p=>p.Id!=1&&Frame>acks.GetValueOrDefault(p.Id)&&now>ackProgress.GetValueOrDefault(p.Id,now)&&now-ackProgress.GetValueOrDefault(p.Id,now)>20000).ToArray()){Send(p.Id,new("error"){Message="Your simulation stopped advancing for 20 seconds. AI has taken over."});RemovePlayer(p.Id,false,"Simulation timed out; AI takes over.");disconnectAt[p.Id]=now+500;}
   if(Phase=="loading"&&!DiagnosticHoldLoading&&loading?.IsCompleted==true){Match=loading.GetAwaiter().GetResult();loading=null;loadedHash=DiagnosticContentOverride??Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(SaveCodec.Content(Match.Mission.World.Data)+Match.Mission.World.Data.Reference.ContentHash)));_ = Match.CanonicalBytes();Players=Players.Select(p=>p.Id==Id?p with{Loaded=true}:p).ToArray();if(!Host)Send(1,new("loaded"){Hash=loadedHash});Changed?.Invoke();}
   if(Phase=="loading"){
    if(DiagnosticHoldLoading)return;
    if(now>started&&now-started>90000){Fail("Loading timed out. No match was started.");return;}
    if(Host&&Match!=null&&Players.All(p=>p.Loaded)){
     if(Players.Any(p=>p.Id!=1&&remoteContent.GetValueOrDefault(p.Id)!=loadedHash)){Fail("Simulation content differs between players. Use matching original content and conversions; no assets were transferred.");return;}
     Broadcast(new("begin"));StartClock();
    }return;
   }
   if(Phase!="match"||Match==null||DiagnosticHoldSimulation)return;
   if(Match.Mission.World.Tick<DiagnosticFastForwardUntil)delta=.25;
   accumulator=Math.Min(accumulator+Math.Min(delta,.25),.3);double step=Match.Fast ? .044 : .066;
   int steps=0;while(accumulator>=step&&steps++<4){
    if(Host){
     if(Match.Mission.AtOutcome||Frame>=DiagnosticFrameLimit)break;
     long slow=Players.Where(p=>p.Id!=1).Select(p=>acks.GetValueOrDefault(p.Id)).DefaultIfEmpty(Frame).Min();
     if(Frame-slow>=4){Notice="Waiting for a player…";break;}
     if(now>=departureNoticeUntil)Notice=Match.Paused?"Host paused the battle":"Connected";
     int take=0,bytes=512;while(take<pending.Count&&take<64){int size=JsonSerializer.SerializeToUtf8Bytes(pending[take],Json).Length;if(bytes+size>MaxPacket-512)break;bytes+=size;take++;}var batch=new WarPacket("frame"){Frame=Frame+1,Commands=pending.Take(take).ToArray()};pending.RemoveRange(0,take);Broadcast(batch);Execute(batch);
    }else{if(frames.Count==0)break;Execute(frames.Dequeue());}
    accumulator-=step;
   }
   MaximumQueue=Math.Max(MaximumQueue,QueueDepth);
  }catch(Exception e){Fail("Multiplayer stopped: "+e.Message);}
 }
 bool TransportActive(){
  // ENet can become inactive while draining a graceful leave (especially when
  // loading aborts both ends). Polling an inactive peer emits an engine error.
  if(Peer.GetConnectionStatus()!=MultiplayerPeer.ConnectionStatus.Disconnected)return true;
  if(Phase=="leaving")Phase="closed";
  else Fail(Host?"The network session closed. Return to Multiplayer to host again.":helloSent?"The host left or the connection was lost. The match has ended.":"Unable to connect. Check the address, UDP port, VPN connection and firewall permissions.",false);
  return false;
 }
 void Execute(WarPacket batch){
  long stepStart=System.Diagnostics.Stopwatch.GetTimestamp();
  foreach(var c in batch.Commands!)Match!.Apply(c,c.Kind is "depart" or "surrender" or "pause" or "speed");
  Match!.Step();Frame=batch.Frame;history.Enqueue(batch);if(history.Count>256)history.Dequeue();
  WorstStepMs=Math.Max(WorstStepMs,System.Diagnostics.Stopwatch.GetElapsedTime(stepStart).TotalMilliseconds);
  string hash="";if(Frame%128==0||Match.Mission.AtOutcome){long begin=System.Diagnostics.Stopwatch.GetTimestamp();checkedState=Match.CanonicalBytes();checkedFrame=Frame;hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(checkedState));WorstHashMs=Math.Max(WorstHashMs,System.Diagnostics.Stopwatch.GetElapsedTime(begin).TotalMilliseconds);hashes[Frame]=hash;if(hashes.Count>8)hashes.Remove(hashes.Keys.Min());Log?.Invoke($"SYNC frame={Frame} tick={Match.Mission.World.Tick} hash={hash}");}
  if(!Host)Send(1,new("ack"){Frame=Frame,Tick=Match.Mission.World.Tick,Hash=hash});
 }
 void Desync(long frame,int peer,string received){
  SaveMismatch();
  Fail($"Synchronization mismatch at checked frame {frame} (player {peer}). Match stopped; diagnostics retained. Expected {hashes[frame]}, received {received}.");
 }
 void SaveMismatch(){Directory.CreateDirectory(evidence);if(checkedState!=null)File.WriteAllBytes(Path.Combine(evidence,$"desync-{checkedFrame}-{Id}.json"),checkedState);File.WriteAllText(Path.Combine(evidence,$"commands-{checkedFrame}-{Id}.json"),JsonSerializer.Serialize(history));}
 void RejectPeer(int id,string message){Rejected++;if(Host){Send(id,new("error"){Message=message});RemovePlayer(id,false,"Invalid network message; AI takes over.");disconnectAt[id]=Time.GetTicksMsec()+500;}else Fail(message,false);}
 public void Fail(string message,bool broadcast=true){if(Phase=="closed")return;Notice=message;if(Host&&broadcast)Broadcast(new("error"){Message=message});Phase="closed";Log?.Invoke(message);Changed?.Invoke();}
 void DisconnectTransport(int id){
  // PeerDisconnected may have beaten the grace timer. Force removes a still-live
  // peer without emitting that local signal, so release transport bookkeeping here.
  disconnectAt.Remove(id);if(connectedPeers.Remove(id))Peer.DisconnectPeer(id,true);
  seen.Remove(id);
 }
 public void Dispose(){if(disposed)return;try{if(Phase is not ("closed" or "leaving"))Leave();}catch{}disposed=true;Phase="closed";
  Peer.PeerConnected-=Connected;Peer.PeerDisconnected-=Disconnected;Peer.Close();Peer.Dispose();
  // Construction is bounded managed work. Detach its result and observe faults;
  // never wait on the UI thread or deliver completion to a retired session.
  if(loading!=null)_=loading.ContinueWith(t=>{_ = t.Exception;},TaskContinuationOptions.OnlyOnFaulted);loading=null;
  Match=null;Players=Array.Empty<WarPeer>();frames.Clear();pending.Clear();history.Clear();connectedPeers.Clear();disconnectAt.Clear();seen.Clear();acks.Clear();ackProgress.Clear();sequences.Clear();remoteContent.Clear();hashes.Clear();checkedState=null;
  Changed=null;MatchReady=null;Log=null;
 }
}

