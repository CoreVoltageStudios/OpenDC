using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;

// Explicit independent-process diagnostic. Files coordinate test actions only;
// configuration, commands, takeover and synchronization travel over actual ENet.
public partial class WarDepartureLab : Node {
 WarNetworkSession? session;string folder="",shared="",root="",test="";int index,port,cycle,cycles,target,revision,targetOwner=-1;long lastOrder=-1;
 bool triggered,paused,resumed,done;ulong start;StreamWriter log=null!;readonly List<WeakReference> retired=new();
 string CycleRoot=>Path.Combine(shared,"cycle"+cycle);
 string Arg(string key,string fallback)=>OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--"+key+"="))?[(key.Length+3)..]??fallback;
 public override void _Ready(){try{
  index=int.Parse(Arg("index","0"));port=int.Parse(Arg("port","27971"));cycles=int.Parse(Arg("cycles","1"));test=Arg("case","client-match-graceful");target=test.StartsWith("host")?0:1;
  shared=Path.GetFullPath(Arg("output","generated/enet-departure/check"));folder=Path.Combine(shared,"peer"+index);Directory.CreateDirectory(folder);log=new(Path.Combine(folder,"events.log")){AutoFlush=true};
  root=Path.Combine(new DarkColony.ContentImport.ContentStore(Arg("content-store","")).ActiveRoot??throw new Exception("Imported content required"),"private/DC");Begin();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
 void Begin(){
  GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();int retained=retired.Count(r=>r.IsAlive);if(retained!=0)throw new Exception("Retired sessions retained: "+retained);log.WriteLine("LIFETIME "+JsonSerializer.Serialize(new{cycle,retained,managed=GC.GetTotalMemory(false),privateBytes=System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64}));
  Directory.CreateDirectory(CycleRoot);start=Time.GetTicksMsec();triggered=paused=resumed=done=false;lastOrder=-1;revision=-1;targetOwner=-1;
  var config=WarConfiguration.Default("D4PLAY04",7) with{Multiplayer=true};for(int i=1;i<4;i++)config.Slots[i]=config.Slots[i] with{Controller=i<3?WarController.Remote:WarController.Ai};
  session=new(root,folder,"Peer"+index,index==0,"127.0.0.1",port,config){DiagnosticFrameLimit=2048,DiagnosticHoldLoading=test.Contains("loading")};
  session.Log+=s=>log.WriteLine(s);session.MatchReady+=()=>{if(test.Contains("lobby"))return;targetOwner=Array.IndexOf(session.Match!.Mission.War!.Starts,session.Players.Single(p=>p.Name=="Peer"+target).Slot);};
 }
 public override void _Process(double delta){try{
  if(done){if(cycle+1>=cycles){GetTree().Quit();return;}if(Enumerable.Range(0,3).All(i=>File.Exists(Path.Combine(CycleRoot,"done"+i)))){cycle++;Begin();}return;}
  var s=session!;s.Poll(.25);
  if(index==0&&s.Phase=="lobby"&&s.Players.Length==3)File.WriteAllText(Path.Combine(CycleRoot,"armed"),"joined");
  if(s.Phase=="lobby"&&!test.Contains("lobby")||s.Phase=="lobby"&&s.Players.Length==2&&File.Exists(Path.Combine(CycleRoot,"trigger"))){
   if(index==0&&test.Contains("lobby")&&!File.Exists(Path.Combine(CycleRoot,"lobby-released"))){if(s.Players.Any(p=>p.Name=="Peer1")||s.Players.Any(p=>p.Ready))throw new Exception("Lobby removal/readiness reset failed");File.WriteAllText(Path.Combine(CycleRoot,"lobby-released"),JsonSerializer.Serialize(s.Setup));}
   if(revision!=s.Revision){revision=s.Revision;s.Ready(true);}if(index==0&&s.CanStart)s.Start();
  }
  if(index==0&&test.Contains("pause")&&s.Frame>=96&&!paused){paused=true;s.Timing(false,true);}
  bool act=test.Contains("lobby")?s.Phase=="lobby"&&File.Exists(Path.Combine(CycleRoot,"armed")):test.Contains("loading")?s.Phase=="loading":s.Phase=="match"&&s.Frame>=256&&(!test.Contains("pause")||s.Match!.Paused);
  if(index==target&&act&&!triggered){triggered=true;File.WriteAllText(Path.Combine(CycleRoot,"trigger"),JsonSerializer.Serialize(new{index,pid=System.Environment.ProcessId,s.Frame,s.Phase}));
   if(test.EndsWith("graceful"))s.Leave();else if(test.EndsWith("stall"))s.DiagnosticHoldSimulation=true;
  }
  if(index==0&&test.Contains("pause")&&s.Match?.Departed.Any(x=>x)==true&&!resumed){if(!s.Match.Paused)throw new Exception("Departure cancelled host pause");resumed=true;s.Timing(false,false);}
  if(s.Phase=="match"&&s.Match is {} match){
   var w=match.Mission.World;int owner=w.Session.LocalTeam;
   if(w.Tick/32!=lastOrder){lastOrder=w.Tick/32;var units=w.Entities.Where(e=>e.Living&&e.Owner==owner&&e.Slot>=152).Take(150).ToArray();if(units.Length>0){var c=units[0].Cell;s.Submit(new("group",owner,units.Select(e=>new WarUnit(e.Slot,e.Incarnation)).ToArray(),OriginalOrder.MoveAttack,Math.Clamp(c.X+3,1,w.Data.Scenario.Map.Width-2)*256+128,Math.Clamp(c.Z+2,1,w.Data.Scenario.Map.Height-2)*256+128));}var d=w.ProductionCatalog(owner).FirstOrDefault(d=>w.DependencyState(owner,d.Id)==1&&d.Cost<=w.Teams[owner].Money);if(d!=null)s.Submit(new("production",owner,Value:d.Id));}
   if(s.Frame==2048&&(index!=0||s.AllAcknowledged)){if(!test.Contains("lobby")&&(!match.Departed[targetOwner]||w.Teams[targetOwner].Ai!=3))throw new Exception("Missing AI3 takeover");Finish();return;}
  }
  if(s.Phase=="closed"){if(index!=target&&!test.StartsWith("host")&&!test.Contains("loading"))throw new Exception("Survivor closed: "+s.Notice);Finish();return;}
  if(Time.GetTicksMsec()-start>65000)throw new Exception("Departure timeout: "+s.Phase+" "+s.Notice);
 }catch(Exception e){log.WriteLine(e);GD.PushError(e.ToString());session?.Dispose();GetTree().Quit(1);}}
 void Finish(){var s=session!;var m=s.Match;string stem=Path.Combine(folder,"cycle"+cycle);
  if(m!=null)File.WriteAllBytes(stem+"-state.json",m.CanonicalBytes());
  File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{test,index,cycle,s.Phase,s.Notice,s.Frame,tick=m?.Mission.World.Tick,targetOwner,departed=m?.Departed,ai=m?.Mission.World.Teams.Select(t=>t.Ai),events=m?.Mission.World.Events.GroupBy(e=>e.Kind).ToDictionary(g=>g.Key,g=>g.Count()),s.MaximumQueue,s.Rejected,milliseconds=Time.GetTicksMsec()-start}));
  s.Dispose();s.Dispose();s.Poll(.25);s.Leave();if(s.Match!=null||s.Players.Length!=0||s.QueueDepth!=0)throw new Exception("Disposed session retained state");retired.Add(new(s));session=null;done=true;File.WriteAllText(Path.Combine(CycleRoot,"done"+index),"closed");
 }
 public override void _ExitTree(){session?.Dispose();log?.Dispose();}
}

