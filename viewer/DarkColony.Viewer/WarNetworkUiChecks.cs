using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyNetworkTextRendering(){
  bool baseline=OS.GetCmdlineUserArgs().Contains("--render-baseline");var records=new List<object>();
  float normal=GetViewportRect().Size.Y/1080;bool wide=GetViewportRect().Size.X/GetViewportRect().Size.Y>2;
  foreach(int aspect in wide?new[]{0,16}:new[]{0})foreach(float scale in new[]{normal,1.5f}){
   CloseNetwork();HideWarSetup();maximumAspect=aspect;ApplyAspectLimit();ui=scale;await WarFrames(6);
   string tag=$"aspect-{aspect}-ui-{scale.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)}";
   await MenuCapture(tag+"-main");OpenMultiplayer();await WarFrames(6);
   var name=multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().First();name.Text="Render sample";name.CaretColumn=0;
   var port=multiplayerRoot.FindChildren("*","SpinBox",true,false).Cast<SpinBox>().Single();port.Value=27964;
   await MenuCapture(tag+"-entry");var entryBounds=multiplayerRoot.GetGlobalRect();var nameBounds=name.GetGlobalRect();
   var entryFilter=multiplayerRoot.TextureFilter;var entryFont=name.GetThemeFont("font").ResourcePath;
   if(!baseline)Check(entryFilter==TextureFilterEnum.Linear,"Host/Join has independent smooth UI filtering");
   await WarControlClick(multiplayerRoot.FindChildren("*","Button",true,false).Cast<Button>().Single(b=>b.Text=="Host battle"));await WarFrames(8);
   Check(network?.Phase=="lobby"&&warSetupVisible,"Host button still enters real ENet lobby");
   Check(warNames[0].GetThemeFont("font").ResourcePath==entryFont,"Entry and lobby use the same Barlow font");
   await MenuCapture(tag+"-lobby");
   records.Add(new{aspect,scale,viewport=GetViewportRect().Size.ToString(),viewerFilter=TextureFilter.ToString(),entryFilter=entryFilter.ToString(),lobbyFilter=warRoot!.TextureFilter.ToString(),entryFont,entryBounds=entryBounds.ToString(),nameBounds=nameBounds.ToString(),lobbyNameBounds=warNames[0].GetGlobalRect().ToString()});
   await Send(Key.Escape);ulong until=Time.GetTicksMsec()+2500;while(retiredNetworks.Count>0&&Time.GetTicksMsec()<until)await WarFrames(1);
   Check(network==null&&retiredNetworks.Count==0,"Back releases each rendering-test host");
  }
  CloseNetwork();File.WriteAllText(Path.Combine(output,"rendering.json"),System.Text.Json.JsonSerializer.Serialize(records,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
 }
 async Task VerifyNetworkErrors(){
  OpenMultiplayer();await WarFrames();var inputs=multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().ToArray();inputs[1].Text="127.0.0.1";multiplayerRoot.FindChildren("*","SpinBox",true,false).Cast<SpinBox>().Single().Value=27992;
  await WarControlClick(multiplayerRoot.FindChildren("*","Button",true,false).Cast<Button>().Single(b=>b.Text=="Join by address"));
  ulong until=Time.GetTicksMsec()+16000;while(network!=null&&Time.GetTicksMsec()<until)await WarFrames(1);
  Check(network==null&&MultiplayerVisible&&networkMessage.Text.Length>10,"Failed actual direct-IP join reports error and returns navigation");await MenuCapture("network-connection-error");
  for(int i=0;i<2;i++){
   await WarControlClick(multiplayerRoot!.FindChildren("*","Button",true,false).Cast<Button>().Single(b=>b.Text=="Host battle"));await WarFrames();
   Check(network?.Phase=="lobby","Port is reusable on repeated hosting");await Send(Key.Escape);
   // Graceful disconnect is wall-clock bounded; uncapped capture frames are not seconds.
   until=Time.GetTicksMsec()+2500;while(retiredNetworks.Count>0&&Time.GetTicksMsec()<until)await WarFrames(1);
   Check(network==null&&retiredNetworks.Count==0,"Lobby Back releases transport");OpenMultiplayer();await WarFrames();
  }
  await MenuCapture("network-repeat-host-return");CloseNetwork();
 }
 async Task VerifyNetworkInterface(){
  var args=OS.GetCmdlineUserArgs();bool host=args.Contains("--network-role=host");bool results=args.Contains("--network-results");
  await UiClick("multiplayer");await WarFrames();Check(MultiplayerVisible,"Main menu Multiplayer entry");await MenuCapture("network-entry");
  var inputs=multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().ToArray();inputs[0].Text=host?"Host-long-name16":"Client-Taar";
  inputs[1].Text="127.0.0.1";
  var port=multiplayerRoot.FindChildren("*","SpinBox",true,false).Cast<SpinBox>().Single();port.Value=27963;
  var connect=multiplayerRoot.FindChildren("*","Button",true,false).Cast<Button>().First(b=>b.Text==(host?"Host battle":"Join by address"));await WarControlClick(connect);
  if(host){SelectWarMap(warMaps.Single(m=>m.Id==(results?"A2PLAY01":"D8PLAY10")));for(int i=2;i<(results?2:8);i++)warSetup.Slots[i]=warSetup.Slots[i] with{Controller=WarController.Ai};ValidateWarSetup();GD.Print("NETWORK_UI_LISTENING");}
  ulong deadline=Time.GetTicksMsec()+30000;
  while(network?.Players.Length!=2&&Time.GetTicksMsec()<deadline)await WarFrames(1);
  Check(network?.Players.Length==2,"Two actual ENet peers join the lobby");await WarFrames(8);
  await MenuCapture("network-lobby-eight");maximumAspect=16;ApplyAspectLimit();ui=1.5f;await WarFrames(8);await MenuCapture("network-lobby-centered-enlarged");
  Check(warStart.GetGlobalRect().End.Y<=GetViewportRect().Size.Y,"Lobby footer inside active viewport");
  maximumAspect=0;ApplyAspectLimit();ui=GetViewportRect().Size.Y/1080;await WarFrames(5);
  await WarControlClick(networkReady!);await WarFrames(4);
  if(args.Contains("--menu-map-evidence"))await MenuCapture("network-ready");
  if(host){deadline=Time.GetTicksMsec()+20000;while(!network!.CanStart&&Time.GetTicksMsec()<deadline)await WarFrames(1);Check(network!.CanStart,"Host waits for all human readiness");warStartAfter=0;await WarControlClick(warStart);}
  if(args.Contains("--menu-map-evidence")&&network?.Phase=="loading")await MenuCapture("network-loading");
  deadline=Time.GetTicksMsec()+30000;while(!IsNetworkMatch&&Time.GetTicksMsec()<deadline)await WarFrames(1);
  Check(IsNetworkMatch&&!godMode&&!nativeWorld.GodMode,"Network match installs with God mode off");
  if(args.Contains("--menu-pause-evidence")){
   var receipt=new List<object>();
   foreach(string panel in new[]{"menu","options","controls","objectives","diplomacy","abandon"}){
    long before=nativeWorld.Tick;OpenMissionPanel(panel);await ToSignal(GetTree().CreateTimer(.7),SceneTreeTimer.SignalName.Timeout);
    Check(IsNetworkMatch&&!SinglePlayerPaused&&nativeWorld.Tick>before,"Local "+panel+" does not pause ENet simulation");receipt.Add(new{panel,before,after=nativeWorld.Tick});
   }
   while(network!.Frame<128)await WarFrames(1);if(host)network.Timing(false,true);
   deadline=Time.GetTicksMsec()+15000;while(!network.Match!.Paused&&Time.GetTicksMsec()<deadline)await WarFrames(1);Check(network.Match!.Paused,"Synchronized host pause arrives");long pausedTick=nativeWorld.Tick;
   CloseRelayPanel();OwnLocalPause(LocalPauseReason.Fault,true);OwnLocalPause(LocalPauseReason.Fault,false);OpenMissionPanel("options");CloseRelayPanel();
   await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);Check(network.Match.Paused&&nativeWorld.Tick==pausedTick,"Local close cannot cancel synchronized host pause");
   if(host){await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);network.Timing(false,false);}
   deadline=Time.GetTicksMsec()+15000;while((network.Match.Paused||nativeWorld.Tick<=pausedTick)&&Time.GetTicksMsec()<deadline)await WarFrames(1);Check(!network.Match.Paused&&nativeWorld.Tick>pausedTick,"Host resumes both peers");
   File.WriteAllText(Path.Combine(output,"network-menu-pause.json"),System.Text.Json.JsonSerializer.Serialize(new{host,receipt,pausedTick}));
  }
  await Send(Key.Tab);await Send(Key.S);OpenMissionPanel("menu");await WarFrames();await MenuCapture("network-menu");
  Check(!CanSaveMission,"Network saves disabled");ActivateMissionUi("restart");Check(nativeWorld.Tick>=0&&IsNetworkMatch,"Restart cannot replace one participant");
  ActivateMissionUi("god-mode");Check(!nativeWorld.GodMode,"Network God toggle is blocked");
  if(args.Contains("--menu-map-evidence")){ActivateMissionUi("network-leave");await MenuCapture("network-leave-confirm");missionPanel="";OpenMissionPanel("menu");ActivateMissionUi("war-surrender");await MenuCapture("network-surrender-confirm");missionPanel="";}
  OpenMissionPanel("options");await WarFrames();await MenuCapture("network-options");missionPanel="";
  if(host){network!.Timing(true,true);}
  while(IsNetworkMatch&&network!.Frame<300)await WarFrames(1);
  Check(IsNetworkMatch&&network!.Frame>=300,"Rendered peers advance beyond two synchronization checks");
  nativeSelection.Clear();foreach(var e in nativeWorld.Entities.Where(e=>e.Owner==LocalTeam&&e.Living&&e.Slot>=152))nativeSelection.Add((e.Slot,e.Incarnation));
  var unit=NativeSelected().First();NativeCommand(OriginalOrder.MoveOnly,new Cell(unit.Cell.X+3,unit.Cell.Z+3));
  await MissionCapture("network-battle");maximumAspect=16;ApplyAspectLimit();ui=1.5f;await WarFrames(5);await MissionCapture("network-battle-centered");
  long SubmittedSequence()=>(long)typeof(WarNetworkSession).GetField("sequence",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(network)!;
  long beforeInput=SubmittedSequence();
  await NativeMouse(MouseButton.Right,new(-20,GetViewportRect().Size.Y/2));
  await NativeMouse(MouseButton.Right,new(GetViewportRect().Size.X+20,GetViewportRect().Size.Y/2));
  Check(SubmittedSequence()==beforeInput,"Outside active viewport input submits no network order");
  OpenMissionPanel("menu");await WarFrames();await NativeMouse(MouseButton.Right,NativeArea.GetCenter());
  Check(SubmittedSequence()==beforeInput,"Active menu input submits no network order");missionPanel="";await WarFrames();
  if(results){int other=Array.IndexOf(mission.War!.Starts,host?1:0);ActivateMissionUi("war-alliance:"+other);while(IsNetworkMatch&&!mission.AtOutcome)await WarFrames(1);Check(IsNetworkMatch&&mission.AtOutcome,"Reciprocal human alliance ends cityless match through actual ordered diplomacy");await WarFrames(8);await MissionCapture("network-controlled-results");await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await Send(Key.Enter);Check(mainMenu&&MultiplayerVisible,"Results Enter leaves session and returns to Multiplayer setup");await MenuCapture("network-result-return");return;}
  if(host){while(IsNetworkMatch&&network!.Frame<400)await WarFrames(1);await MenuCapture("network-after-client-departure");}
  else {CloseNetwork();ReturnMainMenu();await WarFrames();await MenuCapture("network-left");}
 }
}
