using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyDepartureUi(){
  Check(IsolatedPlayerData!=null,"Departure UI isolated");bool host=OS.GetCmdlineUserArgs().Contains("--network-role=host");
  string shared=OS.GetCmdlineUserArgs().Single(a=>a.StartsWith("--departure-shared="))[19..];Directory.CreateDirectory(shared);
  for(int cycle=0;cycle<3;cycle++){
   ReturnMainMenu();await UiClick("multiplayer");await WarFrames(4);var fields=multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().ToArray();fields[0].Text=host?"Departure host":"Departure client";fields[1].Text="127.0.0.1";multiplayerRoot.FindChildren("*","SpinBox",true,false).Cast<SpinBox>().Single().Value=27975;
   await WarControlClick(multiplayerRoot.FindChildren("*","Button",true,false).Cast<Button>().Single(b=>b.Text==(host?"Host battle":"Join by address")));
   network!.DiagnosticHoldLoading=cycle==2;network.DiagnosticFastForwardUntil=1100;
   await WaitLandfall(()=>network?.Players.Length==2);await WarFrames(4);await WarControlClick(networkReady!);
   if(host){await WaitLandfall(()=>network!.CanStart);warStartAfter=0;await WarControlClick(warStart);}
   if(cycle==2){await WaitLandfall(()=>network?.Phase=="loading");if(!host){CloseNetwork();ReturnMainMenu();OpenMultiplayer();}else{await WaitLandfall(()=>network==null);Check(networkMessage.Text.Contains("loading"),"Loading abort reason");}}
   else{
    await WaitLandfall(()=>IsNetworkMatch);await WaitLandfall(()=>network!.Frame>=256);OpenMissionPanel("menu");await WarFrames(3);await MenuCapture("cycle"+cycle+"-menu");
    bool leave=cycle==0?!host:host;
    if(leave){ActivateMissionUi("network-leave");await WarFrames(3);await MenuCapture("cycle"+cycle+"-confirmation");ActivateMissionUi("abandon-confirm");OpenMultiplayer();}
    else if(host){await WaitLandfall(()=>network!.Frame>=1024);Check(network!.Match!.Departed.Any(x=>x),"Client takeover continues rendered match");await MenuCapture("after-takeover");CloseNetwork();ReturnMainMenu();OpenMultiplayer();}
    else {await WaitLandfall(()=>network==null);Check(networkMessage.Text.Contains("host"),"Host ended message");}
   }
   await WaitLandfall(()=>retiredNetworks.Count==0);Check(mainMenu&&MultiplayerVisible,"Usable Multiplayer return");await MenuCapture("cycle"+cycle+"-return");
   File.WriteAllText(Path.Combine(shared,$"{cycle}-{host}"),"done");await WaitLandfall(()=>File.Exists(Path.Combine(shared,$"{cycle}-{!host}")));
   await Send(Key.Escape);await WarFrames(3);Check(mainMenu&&!MultiplayerVisible&&focusedUi=="multiplayer","Escape to main menu");
   Check(failureLogger.FirstError==null&&missionFaultCount==0,"No engine error or halt");
  }
  File.WriteAllText(Path.Combine(output,"departure-ui-pass.txt"),"Real ENet independent peers; Godot-dispatched buttons/keys. Client takeover, host end, loading abort, 3 fresh UI sessions, return paths, no engine errors. Not physical Windows input.\n");
 }
}
