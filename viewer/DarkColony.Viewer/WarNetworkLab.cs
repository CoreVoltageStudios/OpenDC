using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;

// Explicit headless diagnostic entry. Uses the product ENet session and commands.
public partial class WarNetworkLab : Node {
 WarNetworkSession session=null!;string folder="",scenario="";int count,limit;bool host,finished,diverged,leaving,probed;
 ulong start,lastMemory;long lastOrder=-1;int readyRevision=-1;StreamWriter log=null!;
 public override void _Ready(){try{
  var args=OS.GetCmdlineUserArgs();string Arg(string name,string fallback)=>args.FirstOrDefault(a=>a.StartsWith("--"+name+"="))?[(name.Length+3)..]??fallback;
  host=Arg("role","host")=="host";count=int.Parse(Arg("peers","2"));limit=int.Parse(Arg("ticks","1024"));scenario=Arg("case","normal");
  folder=Path.GetFullPath(Arg("output","generated/multiplayer/lab"));Directory.CreateDirectory(folder);log=new StreamWriter(Path.Combine(folder,"network.log")){AutoFlush=true};start=Time.GetTicksMsec();
  string root=args.Contains("--development-content")?Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"),"../..","private/DC")):Path.Combine(new DarkColony.ContentImport.ContentStore(Arg("content-store",DarkColony.ContentImport.ContentStore.DefaultPath)).ActiveRoot??throw new InvalidDataException("Imported content required"),"private/DC");
  var config=WarConfiguration.Default(Arg("map","D4PLAY04"),7) with{Multiplayer=true,Options=new(3,3,true,true,8,8,2)};
  int capacity=WarCatalog.Find(root,config.MapId).Capacity;for(int i=1;i<capacity;i++)config.Slots[i]=config.Slots[i] with{Controller=i<count?WarController.Remote:WarController.Ai};
  if(scenario.StartsWith("economy"))config=config with{Options=new()};
  if(scenario=="economy"){config.Slots[0]=config.Slots[0] with{Team=0};config.Slots[1]=config.Slots[1] with{Team=0};for(int i=2;i<capacity;i++)config.Slots[i]=config.Slots[i] with{Team=2};}
  session=new(root,folder,Arg("name",host?"Host":"Client"),host,Arg("address","127.0.0.1"),int.Parse(Arg("port","27960")),config);
  if(!host&&scenario=="build-mismatch")session.DiagnosticBuildOverride="intentional-incompatible-build";
  if(!host&&scenario=="content-mismatch")session.DiagnosticContentOverride=new string('0',64);
  session.MatchReady+=PrepareMechanics;session.DiagnosticFrameLimit=limit;if(scenario=="soak")session.DiagnosticFastForwardUntil=16000;session.Log+=s=>{log.WriteLine(s);GD.Print(s);};
 }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}}
 public override void _Process(double delta){if(finished||session==null)return;try{
  session.Poll(scenario is "realtime" or "soak"?delta:.25);
  if(scenario=="soak"&&Time.GetTicksMsec()-lastMemory>=15000){lastMemory=Time.GetTicksMsec();using var process=System.Diagnostics.Process.GetCurrentProcess();log.WriteLine("SOAK "+JsonSerializer.Serialize(new{ms=lastMemory-start,session.Frame,tick=session.Match?.Mission.World.Tick,session.QueueDepth,session.MaximumQueue,privateBytes=process.PrivateMemorySize64,managedBytes=GC.GetTotalMemory(false),gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2)}));}
  if(session.Phase=="lobby"&&session.Players.Length==count){
   if(scenario=="invalid-player"&&!host){if(!probed){probed=true;session.Peer.SetTargetPeer(1);session.Peer.PutPacket(JsonSerializer.SerializeToUtf8Bytes(new WarPacket("player"){Player=session.Setup.Slots[session.LocalSlot] with{Name=null!},Revision=session.Revision}));}}
   else{if(readyRevision!=session.Revision){readyRevision=session.Revision;session.Ready(true);}if(host&&session.CanStart){if(scenario=="lobby-reset"&&!probed){probed=true;var changed=session.Setup.Copy();var invalid=changed.Copy();invalid.Slots[1]=invalid.Slots[1] with{Color=invalid.Slots[0].Color};try{session.Configure(invalid);throw new Exception("Duplicate colour accepted");}catch(InvalidDataException){}int old=session.Revision;session.Configure(changed with{Options=changed.Options with{Flow=9}});if(session.Revision<=old||session.Players.Any(p=>p.Ready))throw new Exception("Readiness did not reset");log.WriteLine("LOBBY_CHANGE_RESET_PASS");}else session.Start();}}
  }
  if(session.Phase=="match"&&session.Match is {} match){
   var w=match.Mission.World;long tick=w.Tick;int owner=w.Session.LocalTeam;
   if(scenario=="mechanics"){MechanicsOrders(match);}
   else if(scenario.StartsWith("economy")||scenario is "arena" or "soak"){if(tick/200!=lastOrder){lastOrder=tick/200;EconomyOrders(match);}}
   else if(tick/32!=lastOrder){lastOrder=tick/32;
    var units=w.Entities.Where(e=>e.Living&&e.Owner==owner&&e.Slot>=152).Take(150).ToArray();
    if(units.Length>0){var c=units[0].Cell;session.Submit(new("group",owner,units.Select(e=>new WarUnit(e.Slot,e.Incarnation)).ToArray(),OriginalOrder.MoveAttack,Math.Clamp(c.X+3,1,w.Data.Scenario.Map.Width-2)*256+128,Math.Clamp(c.Z+2,1,w.Data.Scenario.Map.Height-2)*256+128));}
    var d=w.ProductionCatalog(owner).FirstOrDefault(d=>w.DependencyState(owner,d.Id)==1&&d.Cost<=w.Teams[owner].Money);if(d!=null)session.Submit(new("production",owner,Value:d.Id));
   }
   if(scenario is "oversized" or "protocol"&&!host&&tick>=256&&!probed){probed=true;session.Peer.SetTargetPeer(1);session.Peer.PutPacket(scenario=="oversized"?new byte[WarNetworkSession.MaxPacket+1]:JsonSerializer.SerializeToUtf8Bytes(new WarPacket("ping"){Protocol=999}));}
   if(scenario=="stalled"&&!host&&tick>=256)session.DiagnosticHoldSimulation=true;
   if(scenario=="diverge"&&!host&&tick>=256&&!diverged){w.Teams[owner].Money++;diverged=true;}
   if(scenario=="client-leave"&&!host&&session.LocalSlot==1&&tick>=256&&!leaving){leaving=true;session.Leave();}
   if(scenario=="host-leave"&&host&&tick>=256&&!leaving){leaving=true;session.Leave();}
   if(scenario=="surrender"&&!host&&tick>=256&&!probed){probed=true;session.Surrender();}
   if(scenario=="timing"&&host&&session.Frame>=128&&!probed){probed=true;session.Timing(false,true);}
   if(scenario=="timing"&&host&&session.Frame>=192&&!diverged){diverged=true;session.Timing(false,false);session.Timing(true,true);}
   if(scenario=="outcome"&&tick>=256&&!probed){probed=true;int other=Array.IndexOf(match.Mission.War!.Starts,host?1:0);session.Submit(new("alliance",owner,Value:other,Enabled:true));}
   if(scenario=="invalid"&&!host&&tick>=256&&!probed){probed=true;var unit=w.Entities.First(e=>e.Owner==owner&&e.Living);foreach(var c in new[]{new WarCommand("depart",owner),new WarCommand("units",owner,new[]{new WarUnit(unit.Slot,unit.Incarnation+1)}),new WarCommand("units",(owner+1)%8,new[]{new WarUnit(unit.Slot,unit.Incarnation)}),new WarCommand("production",owner,Value:int.MaxValue)}){session.Peer.SetTargetPeer(1);session.Peer.PutPacket(JsonSerializer.SerializeToUtf8Bytes(new WarPacket("command"){Sequence=900000,Command=c}));}}
   if((session.Frame>=limit||match.Mission.AtOutcome)&&(!host||session.AllAcknowledged)){Finish(0);return;}
  }
  if(session.Phase=="closed"){log.WriteLine(session.Notice);Finish(leaving||scenario is "diverge" or "host-leave" or "build-mismatch" or "content-mismatch" or "oversized" or "protocol" or "stalled"||scenario=="invalid-player"&&!host?0:1);return;}
  if(host&&(scenario is "build-mismatch" or "invalid-player")&&Time.GetTicksMsec()-start>5000){Finish(session.Rejected>0?0:1);return;}
  ulong timeout=scenario is "soak" or "realtime"?(ulong)Math.Max(180000,limit*75L+30000):180000;
  if(Time.GetTicksMsec()-start>timeout){log.WriteLine("TIMEOUT "+session.Phase+" "+session.Notice);Finish(1);}
 }catch(Exception e){log.WriteLine(e);Finish(1);}}
 void EconomyOrders(SynchronizedWar match){
  var w=match.Mission.World;int owner=w.Session.LocalTeam;var force=w.Entities.Where(e=>e.Owner==owner&&e.Living&&e.Slot>=152).ToArray();
  var workers=force.Where(e=>e.Type is 6 or 14).ToArray();foreach(var worker in workers.Take(2)){var vent=w.Entities.Where(e=>e.Type==40&&e.Living&&e.ResourceFlow>0&&!w.Entities.Any(h=>h.Living&&h.Type is 47 or 48&&h.Cell==e.Cell)).OrderBy(e=>Math.Abs(e.Cell.X-worker.Cell.X)+Math.Abs(e.Cell.Z-worker.Cell.Z)).FirstOrDefault();if(vent!=null)session.Submit(new("group",owner,new[]{new WarUnit(worker.Slot,worker.Incarnation)},OriginalOrder.MoveOnly,vent.X,vent.Z));}
  var all=w.ProductionCatalog(owner).Where(d=>w.DependencyState(owner,d.Id)==1&&d.Cost<=w.Teams[owner].Money).ToArray();int race=w.Teams[owner].Race;bool hasWorker=force.Any(e=>e.Type is 6 or 14 or 47 or 48);var product=(!hasWorker?all.FirstOrDefault(d=>d.Kind==1&&d.Subject==(race==0?6:14)):null)??(force.Length<16?all.FirstOrDefault(d=>d.Kind==1&&d.Subject==(race==0?0:8)):null)??all.FirstOrDefault(d=>d.Kind==0)??all.FirstOrDefault(d=>d.Kind==2)??all.FirstOrDefault(d=>d.Kind==1&&d.Subject==(race==0?0:8));if(product!=null)session.Submit(new("production",owner,Value:product.Id));
  var army=force.Where(e=>e.Type is not (6 or 14 or 47 or 48)).ToArray();var leaders=army.Where(e=>w.IsCommander(e)&&e.Energy>=32).ToArray();if(leaders.Length>0)session.Submit(new("units",owner,leaders.Select(e=>new WarUnit(e.Slot,e.Incarnation)).ToArray(),OriginalOrder.Inspire));
  if(army.Length==0)return;var enemy=w.Entities.Where(e=>e.Living&&e.Owner<8&&!w.Relations[owner,e.Owner]&&w.Visible(owner,e.Cell)).OrderBy(e=>Math.Abs(e.Cell.X-army[0].Cell.X)+Math.Abs(e.Cell.Z-army[0].Cell.Z)).FirstOrDefault();
  if(enemy!=null)session.Submit(new("units",owner,army.Select(e=>new WarUnit(e.Slot,e.Incarnation)).Take(150).ToArray(),OriginalOrder.Attack,Target:enemy.Slot,TargetIncarnation:enemy.Incarnation));
  else if(scenario=="arena"){var target=w.Data.Scenario.Entities.First(e=>e.Owner<8&&e.Owner!=owner&&e.Type is 69 or 73);session.Submit(new("group",owner,army.Select(e=>new WarUnit(e.Slot,e.Incarnation)).Take(150).ToArray(),OriginalOrder.MoveAttack,target.X256,target.Z256));}
  else if(w.Tick>4000&&w.Tick%1200<200){int rival=Enumerable.Range(0,8).FirstOrDefault(t=>match.Mission.War!.Slot(t)!=null&&!match.Mission.War.Eliminated[t]&&!w.Relations[owner,t],-1);if(rival>=0){var b=w.Teams[rival].Base;session.Submit(new("group",owner,army.Select(e=>new WarUnit(e.Slot,e.Incarnation)).Take(150).ToArray(),OriginalOrder.MoveAttack,(b[0]+4)*256+128,(b[1]+4)*256+128));}}
 }
 void Finish(int code){if(finished)return;finished=true;var m=session.Match;
  if(m!=null)try{File.WriteAllText(Path.Combine(folder,"final.json"),m.Canonical().ToJsonString());}catch(Exception e){log.WriteLine("FINAL_STATE_ERROR "+e);code=1;}
  File.WriteAllText(Path.Combine(folder,"receipt.json"),JsonSerializer.Serialize(new{code,session.Phase,session.Notice,session.Frame,tick=m?.Mission.World.Tick,outcome=m?.Mission.Outcome,godMode=m?.Mission.World.GodMode,departed=m?.Departed,surrendered=m?.Surrendered,session.SentBytes,session.ReceivedBytes,session.Rejected,session.MaximumQueue,session.WorstHashMs,session.WorstStepMs,milliseconds=Time.GetTicksMsec()-start,events=m?.Mission.World.Events.GroupBy(e=>e.Kind).ToDictionary(g=>g.Key,g=>g.Count()),privateBytes=System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64}));
  session.Dispose();log.Dispose();GetTree().Quit(code);
 }
}
