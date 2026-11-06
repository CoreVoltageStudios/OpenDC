using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 // Two independent applications, actual ENet and the ordinary menu/control bindings.
 // Use a fresh shared --barrier directory and a separate --output/profile per peer.
 async Task VerifyNetworkNames(){
  var args=OS.GetCmdlineUserArgs();bool host=args.Contains("--network-role=host");
  string barrier=args.First(a=>a.StartsWith("--barrier="))[10..];Directory.CreateDirectory(barrier);
  void Mark(string tag)=>File.WriteAllText(Path.Combine(barrier,tag),"ready");
  async Task Until(Func<bool> condition,string label){ulong end=Time.GetTicksMsec()+30000;while(!condition()&&Time.GetTicksMsec()<end)await WarFrames(1);Check(condition(),label);}
  Task Wait(string tag)=>Until(()=>File.Exists(Path.Combine(barrier,tag)),tag);
  async Task Both(string tag){Mark(tag+(host?"-host":"-client"));await Wait(tag+(host?"-client":"-host"));}
  void Choose(OptionButton field,int value){field.Select(value);field.EmitSignal(OptionButton.SignalName.ItemSelected,(long)value);}
  void KeyStroke(Key key,uint unicode=0){Input.ParseInputEvent(new InputEventKey{Keycode=key,Unicode=unicode,Pressed=true});Input.FlushBufferedEvents();Input.ParseInputEvent(new InputEventKey{Keycode=key,Unicode=unicode,Pressed=false});Input.FlushBufferedEvents();}
  async Task Type(LineEdit field,string value,bool replace=true){field.GrabFocus();if(replace)field.SelectAll();foreach(char c in value)KeyStroke((Key)char.ToUpperInvariant(c),c);await WarFrames(6);}
  void Record(string tag,LineEdit field)=>File.AppendAllText(Path.Combine(output,"names.jsonl"),JsonSerializer.Serialize(new{tag,host,text=field.Text,caret=field.CaretColumn,selected=field.HasSelection(),from=field.HasSelection()?field.GetSelectionFromColumn():-1,to=field.HasSelection()?field.GetSelectionToColumn():-1,revision=network?.Revision,phase=network?.Phase,names=network?.Setup.Slots.Select(s=>s.Name).ToArray()})+"\n");

  if(host){
   await UiClick("war-setup");await WarFrames();var offline=warNames[0];
   Check(offline.Editable,"Single-player setup name remains editable");await Type(offline,"Copilot");offline.CaretColumn=3;
   Choose(warRaces[0],1);await WarFrames(4);Check(offline.Text=="Copilot"&&offline.CaretColumn==3,"Single-player refresh preserves name and caret");
   HideWarSetup();await WarFrames();
  }
  await UiClick("multiplayer");await WarFrames();
  var fields=multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().ToArray();var name=fields[0];fields[1].Text="127.0.0.1";
  multiplayerRoot.FindChildren("*","SpinBox",true,false).Cast<SpinBox>().Single().Value=27974;
  var connect=multiplayerRoot.FindChildren("*","Button",true,false).Cast<Button>().Single(b=>b.Text==(host?"Host battle":"Join by address"));
  name.GrabFocus();name.SelectAll();KeyStroke(Key.Backspace);await WarFrames(3);await WarControlClick(connect);
  Check(network==null&&name.Editable&&networkMessage.Text.Contains("printable"),"Empty name rejected before connection");
  await Type(name," ");await WarControlClick(connect);
  Check(network==null&&name.Editable&&networkMessage.Text.Contains("printable"),$"Invalid name stays before connection (text={name.Text}, phase={network?.Phase}, message={networkMessage.Text})");await MenuCapture("invalid-name");
  await Type(name,"Copilot");Check(name.Text=="Copilot"&&name.CaretColumn==7,"Pre-connection fast typing preserves order and caret");
  if(!host)await Wait("host-listening");await WarControlClick(connect);
  await Until(()=>network?.Phase=="lobby"&&warSetupVisible,"Lobby opens");
  if(host){SelectWarMap(warMaps.Single(m=>m.Id=="D4PLAY04"));await WarFrames(4);Choose(warRoles[2],(int)WarController.Ai);await WarFrames(6);Mark("host-listening");}
  await Until(()=>network?.Players.Length==2,"Independent peers connected");await WarFrames(6);
  Check(network!.Players.All(p=>p.Name=="Copilot"&&network.Setup.Slots[p.Slot].Name=="Copilot"),"Both peers retain accepted Copilot names");
  var local=warNames[network.LocalSlot];Check(!local.Editable,"Connected name is read-only");int edits=0;local.TextChanged+=CountEdit;
  void CountEdit(string value)=>edits++;
  local.GrabFocus();local.SelectAll();KeyStroke(Key.Backspace);await WarFrames(8);
  Check(edits==0&&local.Text=="Copilot"&&network.Phase=="lobby"&&network.Players.Length==2,"Clearing connected name cannot edit or disconnect");local.TextChanged-=CountEdit;
  Record("connected-name-locked",local);await MenuCapture("connected-name-locked");await Both("locked");

  // Direct API caller cannot turn faction/team changes into a rename either.
  if(!host)network.EditPlayer(network.Setup.Slots[network.LocalSlot] with{Name=""});
  await WarFrames(12);await Both("immutable-api");
  Check(network.Players.All(p=>p.Name=="Copilot"&&network.Setup.Slots[p.Slot].Name=="Copilot"),"EditPlayer keeps the handshake name");
  if(host){
   var ai=warNames[2];Check(ai.Editable,"Host AI setup name remains editable");
   await Type(ai,"Copilot");Check(ai.Text=="Copilot"&&ai.CaretColumn==7,"AI fast typing survives refresh");
   ai.CaretColumn=3;await Type(ai,"-",false);Check(ai.Text=="Cop-ilot"&&ai.CaretColumn==4,"Middle insertion survives refresh");
   ai.Select(3,4);await Type(ai,"X",false);Check(ai.Text=="CopXilot"&&ai.CaretColumn==4,"Selection replacement survives refresh");
   Choose(warRaces[2],1);await WarFrames(6);Check(ai.Text=="CopXilot"&&ai.CaretColumn==4,"Other controls do not reset editing position");
   // Exercise the necessary-update branch separately from identical-text binding.
   warBinding=true;ai.Select(1,4);BindWarName(ai,"LongerAI");warBinding=false;
   Check(ai.CaretColumn==4&&ai.HasSelection()&&ai.GetSelectionFromColumn()==1&&ai.GetSelectionToColumn()==4,"Necessary focused update preserves caret and selection");
   await Type(ai,"Copilot");Record("editable-fast-and-middle",ai);
   network.DiagnosticHoldIncoming=true;Mark("queue-update");await Wait("update-sent");
   await Until(()=>network.Peer.GetAvailablePacketCount()>0,"Actual incoming ENet packet queued");
   // A newer incomplete draft is deliberately not published. An older lobby
   // refresh must not replace it with the last validated name (Copilot).
   ai.GrabFocus();ai.SelectAll();KeyStroke(Key.Backspace);await WarFrames(2);
   Check(ai.Text==""&&warStart.Disabled,"Empty draft blocks start");
   await Type(ai,"   ",false);ai.CaretColumn=2;ai.Select(1,2);int oldRevision=network.Revision;
   network.DiagnosticHoldIncoming=false;
   await Until(()=>network.Revision>oldRevision&&network.Setup.Slots[1].Team==3,"Delayed client edit processed on receiving host");await WarFrames(6);
   Check(ai.Text=="   "&&ai.CaretColumn==2&&ai.HasSelection()&&ai.GetSelectionFromColumn()==1&&ai.GetSelectionToColumn()==2,"Delayed lobby refresh preserves newer draft/caret/selection");
   Check(network.Setup.Slots[2].Name=="Copilot"&&warStart.Disabled&&networkReady!.Disabled,"Incomplete draft stays local and blocks readiness");
   Record("delayed-draft-preserved",ai);await MenuCapture("delayed-draft-preserved");
   await Type(ai,"Final AI");Check(network.Setup.Slots[2].Name=="Final AI"&&warNameDrafts.Count==0&&!networkReady!.Disabled,"Completed draft publishes and permits ready");Mark("final-name");
  }else{
   await Wait("queue-update");Choose(warTeams[network.LocalSlot],3);Mark("update-sent");
   await Wait("final-name");await Until(()=>network.Setup.Slots[2].Name=="Final AI","Client receives final host AI name");
   Check(!warNames[2].Editable&&warNames[2].Text=="Final AI","Client sees authoritative AI update without editing permission");Record("received-final-name",warNames[2]);
  }
  await Both("names-complete");Check(network.Players.All(p=>p.Name=="Copilot"),"Session names still unchanged");await MenuCapture("names-complete");
  network.DiagnosticFrameLimit=128;await WarControlClick(networkReady!);
  if(host){await Until(()=>network.CanStart,"Both peers ready");warStartAfter=0;await WarControlClick(warStart);}
  await Until(()=>IsNetworkMatch&&network.Frame==128,"Match proceeds after editing checks");
  File.WriteAllBytes(Path.Combine(output,"frame128.json"),network.Match!.CanonicalBytes());
  await Both("match128");CloseNetwork();ReturnMainMenu();
 }
}
