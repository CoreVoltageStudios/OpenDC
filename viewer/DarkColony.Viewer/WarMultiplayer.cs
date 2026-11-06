using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 WarNetworkSession? network;
 Control? multiplayerRoot;Label? networkHud;
 Label networkMessage=null!,networkLobbyText=null!;
 Button? networkReady;
 bool networkDirty,networkBinding;
 bool IsNetworkMatch=>network?.Match!=null&&!mainMenu;
 bool MultiplayerVisible=>multiplayerRoot?.Visible==true;
 readonly Label[] networkStates=new Label[8];
 readonly List<OptionButton> warOptionsControls=new();
 HBoxContainer? warColumns;
 readonly List<WarNetworkSession> retiredNetworks=new();
 void CloseNetwork(){warNameDrafts.Clear();if(network!=null){fastGame=ArtworkPreference.LoadFast(artworkPreferencePath);network.Leave();retiredNetworks.Add(network);}network=null;networkHud?.Hide();multiplayerRoot?.Hide();}
 void OpenMultiplayer(string message=""){
  HideWarSetup();missionPanel="";focusedUi="";ReleasePointerBoundary();multiplayerRoot?.QueueFree();
  // Match WarSetup: scaled UI glyphs/icons must not inherit the world's nearest filter.
  multiplayerRoot=new Control{Name="Multiplayer",ZIndex=160,TextureFilter=TextureFilterEnum.Linear,MouseFilter=Control.MouseFilterEnum.Stop,Theme=WarTheme()};AddChild(multiplayerRoot);
  var panel=new PanelContainer();multiplayerRoot.AddChild(panel);panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
  var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,FollowFocus=true};panel.AddChild(scroll);var rows=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(rows);rows.AddThemeConstantOverride("separation",16);rows.AddChild(WarLabel("Multiplayer",40));
  var intro=WarLabel("Host a battle or join by LAN / virtual-LAN address.\nEvery player needs the same OpenDC build and original content.");intro.AutowrapMode=TextServer.AutowrapMode.WordSmart;rows.AddChild(intro);
  rows.AddChild(WarLabel("Player name"));var name=new LineEdit{Text=System.Environment.UserName.Length>16?System.Environment.UserName[..16]:System.Environment.UserName,MaxLength=16};rows.AddChild(name);
  var cards=new HBoxContainer();cards.AddThemeConstantOverride("separation",24);rows.AddChild(cards);
  var hosting=WarSection(cards,"Host a battle",false);var hostingText=WarLabel("Choose the map, players and match options. Share a reachable LAN or virtual-LAN address.");hostingText.AutowrapMode=TextServer.AutowrapMode.WordSmart;hosting.AddChild(hostingText);var host=new Button{Text="Host battle",CustomMinimumSize=new(260,46)};hosting.AddChild(host);
  var joining=WarSection(cards,"Join by address",false);var addressRow=new HBoxContainer();joining.AddChild(addressRow);var address=new LineEdit{PlaceholderText="Host LAN / VPN address",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(260,40)};addressRow.AddChild(address);
  var port=new SpinBox{MinValue=1024,MaxValue=65535,Value=WarNetworkSession.DefaultPort,CustomMinimumSize=new(112,40)};addressRow.AddChild(port);var join=new Button{Text="Join by address",CustomMinimumSize=new(180,46)};joining.AddChild(join);
  networkMessage=WarLabel(message);networkMessage.AutowrapMode=TextServer.AutowrapMode.WordSmart;networkMessage.SizeFlagsVertical=Control.SizeFlags.ExpandFill;networkMessage.AddThemeColorOverride("font_color",new Color("edbf68"));rows.AddChild(networkMessage);
  var help=WarLabel("UDP port "+WarNetworkSession.DefaultPort+" by default. Allow OpenDC through your private-network firewall if prompted. On Hamachi, join using the host's Hamachi IPv4 address. No port or firewall rules are changed by OpenDC.",14);help.AutowrapMode=TextServer.AutowrapMode.WordSmart;var helpToggle=new Button{Text="Connection help ▾"};rows.AddChild(helpToggle);help.Visible=false;helpToggle.Pressed+=()=>help.Visible=!help.Visible;rows.AddChild(help);
  var back=new Button{Text="Back   Esc",CustomMinimumSize=new(180,42)};rows.AddChild(back);back.Pressed+=()=>{CloseNetwork();focusedUi="multiplayer";};
  void Connect(bool hosting){try{string playerName=WarNetworkSession.CleanName(name.Text);if(!hosting&&string.IsNullOrWhiteSpace(address.Text))throw new InvalidDataException("Enter the host's LAN or VPN address.");
   CloseNetwork();network=new(Path.Combine(root,"private/DC"),Path.Combine(PersistenceRoot,"network-diagnostics"),playerName,hosting,address.Text,(int)port.Value);
   name.Text=playerName;name.Editable=false;
   var owner=network;owner.Changed+=()=>{if(ReferenceEquals(network,owner))networkDirty=true;};owner.Log+=s=>GD.Print(s);owner.MatchReady+=()=>{if(ReferenceEquals(network,owner))InstallNetworkMatch();};
   if(hosting){warSetup=network.Setup.Copy();OpenWarSetup();networkDirty=true;}else{multiplayerRoot!.Show();host.Disabled=join.Disabled=true;networkMessage.Text="Connecting… Back cancels this attempt.";}
  }catch(Exception e){multiplayerRoot!.Show();networkMessage.Text=e.Message;host.Disabled=join.Disabled=false;name.Editable=true;}}
  host.Pressed+=()=>Connect(true);join.Pressed+=()=>Connect(false);name.GrabFocus();QueueRedraw();
 }
 void LayoutMultiplayer(){if(!MultiplayerVisible)return;float width=Math.Min(1140,UiSize.X-96),height=Math.Min(700,UiSize.Y-96);multiplayerRoot!.Scale=Vector2.One*ui;multiplayerRoot.Size=new(width,height);multiplayerRoot.Position=(GetViewportRect().Size-multiplayerRoot.Size*ui)/2;}
 void InstallNetworkMatch(){
  var next=network!.Match!.Mission;multiplayerRoot?.Hide();godMode=false;InstallMission(next.World.Data,next,false);firstStrike=true;
  GD.Print($"NETWORK_MATCH peer={network.Id} slot={network.LocalSlot} owner={LocalTeam} map={network.Setup.MapId}");
 }
 void ProcessNetwork(double delta){
  for(int i=retiredNetworks.Count-1;i>=0;i--){var old=retiredNetworks[i];old.Poll(delta);if(old.Phase=="closed"){old.Dispose();retiredNetworks.RemoveAt(i);}}
  if(network==null)return;network.Poll(delta);
  if(network.Phase=="closed"){
   string reason=network.Notice;ReturnMainMenu();OpenMultiplayer(reason);return;
  }
  if(networkDirty&&network.Phase=="lobby"){
   networkDirty=false;networkBinding=true;
   try{warSetup=network.Setup.Copy();RestoreWarNameDrafts();if(!warSetupVisible){multiplayerRoot?.Hide();OpenWarSetup();}else{BindWarPlayers();FilterWarMaps();}BindNetworkLobby();}finally{networkBinding=false;}
  }
  if(network.Phase=="loading"){networkLobbyText.Text=network.Notice+"  "+network.Players.Count(p=>p.Loaded)+" / "+network.Players.Length;warStart.Disabled=true;if(networkReady!=null)networkReady.Disabled=true;warRoot!.MouseFilter=Control.MouseFilterEnum.Stop;SetNetworkControls(false);}
  if(IsNetworkMatch){
   fastGame=network.Match!.Fast;nativeWorld.GodMode=false;DispatchMissionSounds();DispatchAmbient();DispatchMessageSound();
   networkHud??=new Label{MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=4000};if(networkHud.GetParent()==null){networkHud.AddThemeFontSizeOverride("font_size",14);networkHud.AddThemeColorOverride("font_color",uiGreen);AddChild(networkHud);}
   networkHud.Show();networkHud.Position=new(16,6);networkHud.Text=network.Match.Surrendered[LocalTeam]?"SURRENDERED · AI controls your forces · Leave from the menu":network.Notice+(network.Host?" · Host":"");
  }
 }
 void SetNetworkControls(bool lobby){
  if(network==null)return;bool host=network.Host&&lobby;
  warMapList.MouseFilter=host?Control.MouseFilterEnum.Stop:Control.MouseFilterEnum.Ignore;warSearch.Editable=host;warCapacity.Disabled=!host;
  foreach(var c in warOptionsControls)c.Disabled=!host;
  for(int i=0;i<8;i++){
   bool connected=network.Players.Any(p=>p.Slot==i);bool editable=lobby&&(host&&!connected||i==network.LocalSlot)&&i<WarCatalog.Find(Path.Combine(root,"private/DC"),warSetup.MapId).Capacity;
   warRoles[i].Disabled=!host||connected||i>=WarCatalog.Find(Path.Combine(root,"private/DC"),warSetup.MapId).Capacity;
   warRaces[i].Disabled=warColors[i].Disabled=warTeams[i].Disabled=!editable;warNames[i].Editable=editable&&!connected;
   warNames[i].TooltipText=connected?"Player names are fixed for this session. Choose a name before hosting or joining.":"";
  }
 }
 void BindNetworkLobby(){if(network==null)return;SetNetworkControls(network.Phase=="lobby");
  for(int i=0;i<8;i++){var p=network.Players.FirstOrDefault(p=>p.Slot==i);networkStates[i].Text=p==null?warSetup.Slots[i].Controller==WarController.None?"Inactive":warSetup.Slots[i].Controller==WarController.Remote?"Open slot":"Computer":(p.Id==1?"Host":"Player")+(p.Id==network.Id?" · You":"")+"\n"+(p.Loaded?"Loaded":p.Ready?"Ready":"Not ready");}
  int[] values={warSetup.Options.Storage,warSetup.Options.Artifacts,warSetup.Options.Erupting?1:0,warSetup.Options.Renewable?1:0,warSetup.Options.Quantity-1,warSetup.Options.Flow-1,warSetup.Options.CommanderRank};for(int i=0;i<warOptionsControls.Count;i++)warOptionsControls[i].Select(values[i]);
  networkLobbyText.Text=network.Host?"Host · UDP "+network.Port+" · "+string.Join("   |   ",WarNetworkSession.Addresses()):"Connected · UDP "+network.Port+" · Choose your settings and mark ready";
  networkReady!.Text=network.Players.FirstOrDefault(p=>p.Id==network.Id)?.Ready==true?"Not ready":"Ready";networkReady.Disabled=warNameDrafts.Count>0;
  warStart.Visible=network.Host;warStart.Disabled=!network.CanStart||warNameDrafts.Count>0;
  warNotice.Text=network.Notice+"\n"+(network.Host?"Open slots accept connections. Every open slot must join or become AI / inactive before starting. Changes reset readiness.":"The host controls the map and match options.");
  if(warNameDrafts.Count>0)warNotice.Text="Finish editing the player name (1 to 16 printable characters) before marking ready or starting.";
 }
 // An incomplete host-owned name is a local draft, not a session update. Keep it
 // through incoming player/ready updates until it validates or its slot disappears.
 readonly Dictionary<int,string> warNameDrafts=new();
 void RestoreWarNameDrafts(){
  if(network?.Host!=true){warNameDrafts.Clear();return;}
  int capacity=WarCatalog.Find(Path.Combine(root,"private/DC"),warSetup.MapId).Capacity;
  foreach(var entry in warNameDrafts.ToArray()){
   if(entry.Key>=capacity||network.Players.Any(p=>p.Slot==entry.Key)){warNameDrafts.Remove(entry.Key);warSetup.Slots[entry.Key]=warSetup.Slots[entry.Key] with{Name=network.Setup.Slots[entry.Key].Name};}
   else warSetup.Slots[entry.Key]=warSetup.Slots[entry.Key] with{Name=entry.Value};
  }
 }
 void NetworkSetupChanged(){
  if(network==null||networkBinding||warBinding||network.Phase!="lobby")return;
  try{if(network.Host){RestoreWarNameDrafts();network.Configure(warSetup);warNameDrafts.Clear();}
   else network.EditPlayer(warSetup.Slots[network.LocalSlot]);}
  catch(Exception e){warNotice.Text=e.Message;warStart.Disabled=true;if(networkReady!=null)networkReady.Disabled=true;}
 }
 bool NetworkUiAction(string id){
  if(id=="multiplayer"){OpenMultiplayer();return true;}
  if(!IsNetworkMatch)return false;
  if(id is "save" or "load" or "restart" or "god-mode"||id.StartsWith("load-slot:")||id.StartsWith("save-slot:")){developmentNotice="Unavailable in a live network match.";return true;}
  if(id is "speed-classic" or "speed-fast"){if(network!.Host)network.Timing(true,id=="speed-fast");return true;}
  if(id=="network-pause"){network!.Timing(false,!network.Match!.Paused);return true;}
  if(id=="war-setup"){ReturnMainMenu();OpenMultiplayer();return true;}
  if(id=="network-leave"){AskAbandon("main-menu");return true;}
  return false;
 }
 void QueueNetworkUnits(IEnumerable<NativeEntity> units,OriginalOrder order,Cell? cell=null,int target=-1,bool group=false){
  var refs=units.OrderBy(e=>e.Slot).Take(150).Select(e=>new WarUnit(e.Slot,e.Incarnation)).ToArray();if(refs.Length==0)return;
  network!.Submit(new(group?"group":"units",LocalTeam,refs,order,cell.HasValue?cell.Value.X*256+128:0,cell.HasValue?cell.Value.Z*256+128:0,target,target<0?0:nativeWorld.Slots[target]?.Incarnation??0));
 }
}
