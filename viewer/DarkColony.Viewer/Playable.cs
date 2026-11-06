using Godot;
using DarkColony.Assets;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool playMode,laneMode,selected,showPath=true,showNavigation,captureDriven,realtimeCheck;int receiptCount,playCaptureIndex;
 MovementSimulation simulation=null!;NavigationGrid navigation=null!;MovementSettings movementSettings=null!;FixedStepClock simClock=null!;
 TrooperClips clips=null!;JsonElement movementConfig;long commandSequence;int animationFps=8;
 string feedback="Left-click the trooper to select it.";Vector2? rejectedPoint;long rejectedUntil;
 Rect2 PlayInfo=>new(GetViewportRect().Size.X-274*ui,158*ui,250*ui,192*ui);
 Vector2 ToPixel(Point p)=>new((float)(p.X*32),(float)((visuals.Map.Height-p.Z)*32));
 Point ToLogical(Vector2 pixel)=>new(pixel.X/32d,visuals.Map.Height-pixel.Y/32d);
 Vector2 BodyAnchor(Point p) {
  var a=OriginalCoordinates.Project((int)Math.Floor(p.X*256),(int)Math.Floor(p.Z*256),0,visuals.Map.Height);return new(a.X,a.Y);
 }
 TrooperPose PlayPose {get{var u=simulation.Unit;return clips.Resolve((int)u.Facing,u.Moving,u.MovingTicks*animationFps/movementSettings.TickRate);}}
 void PreparePlay(string[] args) {
  movementConfig=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"fixtures/movement-settings.json"))).RootElement.Clone();
  visuals=new OriginalVisuals(Path.Combine(root,"private/DC"),movementConfig.GetProperty("map").GetString()!,movementConfig.GetProperty("terrain").GetString()!);
  movementSettings=new(movementConfig.GetProperty("tickRate").GetInt32(),movementConfig.GetProperty("tilesPerSecond").GetDouble(),movementConfig.GetProperty("radius").GetDouble(),movementConfig.GetProperty("arrivalTolerance").GetDouble());
  animationFps=movementConfig.GetProperty("animationFramesPerSecond").GetInt32();clips=new(visuals.Animation,visuals.Sprites,visuals.Palette);
  File.WriteAllText(Path.Combine(output,"play-input.jsonl"),"");
  realtimeCheck=args.Contains("--realtime-check");navigation=BuildPlayNavigation();ResetPlay();camera=ToPixel(simulation.Unit.Position)+new Vector2(100,0);zoom=3;bounds=false;paused=false;captureDriven=capturing&&!realtimeCheck;
  GetWindow().Title="OpenDC | Movement Prototype";
 }
 NavigationGrid BuildPlayNavigation() {
  int w=visuals.Map.Width,h=visuals.Map.Height;var allowed=new bool[w*h];var b=movementConfig.GetProperty("laneFileBounds");
  if(!laneMode) {
   string relative=Path.ChangeExtension(movementConfig.GetProperty("map").GetString()!,".PTH");
   var source=File.ReadAllBytes(Path.Combine(root,"private/DC",relative));var path=PathAsset.Decode(source,w,h);var ground=GroundNavigation.From(visuals.Map,path);
   File.WriteAllBytes(Path.Combine(output,"navigation.bin"),ground.Allowed.Select(v=>(byte)(v?1:0)).ToArray());
   File.WriteAllText(Path.Combine(output,"navigation.json"),JsonSerializer.Serialize(new{relative,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source)),w,h,ground.Blocked,ground.AdditionalPathBlocked,semantics="static ground PTH family!=0; runtime occupancy omitted",footprint="prototype axis-aligned square"}));
   return new(w,h,ground.Allowed);
  }
  // Phase2 fixture is an explicit bounded open-ground lane; no inferred map collision.
  for(int row=b[1].GetInt32();row<b[3].GetInt32();row++)for(int x=b[0].GetInt32();x<b[2].GetInt32();x++)allowed[(h-1-row)*w+x]=true;
  return new(w,h,allowed);
 }
 void ResetPlay() {
  var spawn=movementConfig.GetProperty("spawn");simulation=new(navigation,new(spawn[0].GetDouble(),spawn[1].GetDouble()),movementSettings);simClock=new(movementSettings.TickRate);
  commandSequence=0;receiptCount=0;selected=false;rejectedPoint=null;feedback="Left-click the trooper to select it.";unit=BodyAnchor(simulation.Unit.Position);
 }
 void PlayTick() {
  simulation.Step();unit=BodyAnchor(simulation.Unit.Position);
  while(receiptCount<simulation.Receipts.Count) {
   var r=simulation.Receipts[receiptCount++];feedback=r.Result switch {
    OrderResult.Moving=>$"Moving to cell {r.Accepted!.Value.X-.5:0}, {r.Accepted.Value.Z-.5:0}.",OrderResult.Stopped=>"Stopped.",
    OrderResult.Blocked=>"Blocked terrain: order rejected.",OrderResult.Unreachable=>"No route: order rejected.",OrderResult.OutsideMap=>"Outside map: order rejected.",_=>r.Result.ToString()};
   if(r.Result is OrderResult.Blocked or OrderResult.Unreachable or OrderResult.OutsideMap){rejectedPoint=ToPixel(r.Requested);rejectedUntil=simulation.Tick+180;}
  }
  if(simulation.Status==OrderResult.Arrived&&!feedback.StartsWith("Blocked")&&!feedback.StartsWith("No route")&&!feedback.StartsWith("Outside"))feedback="Arrived. Right-click for another destination.";
 }
 void ProcessPlay(double delta) {
  if(squadMode){ProcessSquad(delta);return;}
  if(!captureDriven&&!paused)simClock.Advance(Math.Min(delta,1),PlayTick);QueueRedraw();
 }
 void RecordInput(string action) {
  inputEvents++;var record=new{tick=simulation.Tick,action,selected,sequence=commandSequence,camera=new[]{camera.X,camera.Y},zoom,ui};
  if(inputEvents<=4096)File.AppendAllText(Path.Combine(output,"play-input.jsonl"),JsonSerializer.Serialize(record)+System.Environment.NewLine);
  else if(inputEvents==4097)File.AppendAllText(Path.Combine(output,"play-input.jsonl"),"{\"logLimit\":4096}"+System.Environment.NewLine);
 }
 void PlayOrder(OrderKind kind,Point target) {
  if(!selected){feedback="Select the trooper before issuing an order.";RecordInput("order-without-selection");return;}
  simulation.Enqueue(new(simulation.Tick+1,++commandSequence,MovementSimulation.TrooperId,kind,target));feedback=kind==OrderKind.Stop?"Stopping...":"Move order queued.";RecordInput(kind.ToString());
 }
 bool IsPlayUi(Vector2 p)=>!Area.HasPoint(p)||PlayInfo.HasPoint(p);
 void PlayAction(string action) {
  if(action=="stop")PlayOrder(OrderKind.Stop,new());
  else if(action=="path")showPath=!showPath;
  else if(action=="nav")showNavigation=!showNavigation;
  else if(action=="focus"){camera=ToPixel(simulation.Unit.Position);ClampCamera();}
  else if(action=="pause"){paused=!paused;feedback=paused?"Simulation paused.":"Simulation resumed.";}
  else if(action=="ui-")ui=Math.Max(.75f,ui-.125f);
  else if(action=="ui+")ui=Math.Min(1.125f,ui+.125f);
  else if(action=="grid")grid=!grid;
  else if(action=="bounds")bounds=!bounds;
  RecordInput("button:"+action);QueueRedraw();
 }
 void PlayInput(InputEvent e) {
  if(squadMode){SquadInput(e);return;}
  if(e is InputEventKey k&&k.Pressed&&!k.Echo) {
   if(k.Keycode==Key.X)PlayOrder(OrderKind.Stop,new());
   else if(k.Keycode==Key.Escape){selected=false;feedback="Selection cleared.";RecordInput("deselect-key");}
   else if(k.Keycode==Key.F12)CapturePlay("manual");
   else if(k.Keycode==Key.Q)GetTree().Quit();
   else if(k.Keycode==Key.Space)PlayAction("pause");
   else if(k.Keycode==Key.O)PlayAction("path");else if(k.Keycode==Key.N)PlayAction("nav");else if(k.Keycode==Key.G)PlayAction("grid");else if(k.Keycode==Key.B)PlayAction("bounds");else if(k.Keycode==Key.F)PlayAction("focus");
   else if(k.Keycode is Key.Equal or Key.Plus or Key.KpAdd){Zoom(1.25f,Area.GetCenter());RecordInput("zoom-key");}
   else if(k.Keycode is Key.Minus or Key.KpSubtract){Zoom(.8f,Area.GetCenter());RecordInput("zoom-key");}
   else {var d=k.Keycode switch{Key.W or Key.Up=>new Vector2(0,-64),Key.S or Key.Down=>new Vector2(0,64),Key.A or Key.Left=>new Vector2(-64,0),Key.D or Key.Right=>new Vector2(64,0),_=>Vector2.Zero};camera+=d;ClampCamera();RecordInput("pan-key");}
  }
  if(e is InputEventMouseButton m) {
   if(m.ButtonIndex==MouseButton.Middle){if(!m.Pressed||!IsPlayUi(m.Position))dragging=m.Pressed;RecordInput("pan-button");}
   if(m.Pressed) {
    if(IsPlayUi(m.Position)) {
     if(m.ButtonIndex==MouseButton.Left){var b=buttons.FirstOrDefault(b=>b.Rect.HasPoint(m.Position));if(b.Action!=null)PlayAction(b.Action);}
     RecordInput("ui-consumed");return;
    }
    if(m.ButtonIndex==MouseButton.Left) {
     var v=PlayPose.Visual;var p=World(m.Position);var anchor=BodyAnchor(simulation.Unit.Position);
     selected=new Rect2(anchor-new Vector2(v.OriginX,v.OriginY),new(v.LogicalWidth,v.LogicalHeight)).Grow(3).HasPoint(p)||p.DistanceTo(ToPixel(simulation.Unit.Position))<12;
     feedback=selected?"Trooper selected. Right-click to move; X to stop.":"Selection cleared.";RecordInput(selected?"select":"deselect-ground");
    } else if(m.ButtonIndex==MouseButton.Right)PlayOrder(OrderKind.Move,ToLogical(World(m.Position)));
    else if(m.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){Zoom(m.ButtonIndex==MouseButton.WheelUp?1.25f:.8f,m.Position);RecordInput("zoom-wheel");}
   }
  }
  if(e is InputEventMouseMotion motion&&dragging){camera-=motion.Relative/zoom;ClampCamera();RecordInput("pan-drag");}
  QueueRedraw();
 }
 void DrawPlay() {
  if(squadMode){DrawSquad();return;}
  ClampCamera();var size=GetViewportRect().Size;DrawRect(new(Vector2.Zero,size),new Color("091310"));DrawSetTransform(Offset,0,Vector2.One*zoom);
  DrawAsset("map/background",Vector2.Zero);DrawAsset("map/foreground",Vector2.Zero);
  if(showNavigation)for(int row=0;row<visuals.Map.Height;row++)for(int x=0;x<visuals.Map.Width;x++)if(!navigation.Walkable(new(x,visuals.Map.Height-1-row)))DrawRect(new(x*32,row*32,32,32),new Color(1,.22f,.15f,.22f));
  if(grid){for(int x=0;x<=visuals.Map.Width;x++)DrawLine(new(x*32,0),new(x*32,MapHeight),new Color(1,1,1,.18f),1/zoom);for(int y=0;y<=visuals.Map.Height;y++)DrawLine(new(0,y*32),new(MapWidth,y*32),new Color(1,1,1,.18f),1/zoom);}
  var u=simulation.Unit;var center=ToPixel(u.Position);
  if(showPath&&u.Moving){var p=center;foreach(var point in u.Route.Skip(u.RouteIndex)){var q=ToPixel(point);DrawLine(p,q,new Color("8bc9cf"),1.5f/zoom);p=q;}}
  if(u.Destination is Point dest){var p=ToPixel(dest);DrawCircle(p,5,new Color("e6ca80"),false,1.5f/zoom);DrawLine(p-new Vector2(8,0),p+new Vector2(8,0),accent,1/zoom);DrawLine(p-new Vector2(0,8),p+new Vector2(0,8),accent,1/zoom);}
  if(rejectedPoint is Vector2 reject&&simulation.Tick<rejectedUntil){DrawLine(reject-new Vector2(7,7),reject+new Vector2(7,7),new Color("fa7769"),2/zoom);DrawLine(reject-new Vector2(7,-7),reject+new Vector2(7,-7),new Color("fa7769"),2/zoom);}
  if(selected)DrawArc(center,12,0,Mathf.Tau,36,accent,2/zoom);
  var pose=PlayPose;var v=pose.Visual;var anchor=BodyAnchor(u.Position);string key=$"play:{v.Id}:{anchor.X}:{anchor.Y}";
  if(key!=bodyKey){var raster=visuals.Occlusion.ApplyGroundBody(v,(int)anchor.X,(int)anchor.Y,(int)anchor.Y+pose.BottomOffset);bodyTexture?.Dispose();bodyTexture=FromRaster(raster);bodyKey=key;}
  DrawTextureRect(bodyTexture!,new(anchor-new Vector2(v.OriginX,v.OriginY),new Vector2(v.LogicalWidth,v.LogicalHeight)),false);
  if(bounds){DrawRect(new(anchor-new Vector2(v.OriginX,v.OriginY),new(v.LogicalWidth,v.LogicalHeight)),new Color("f0b97b"),false,1/zoom);float r=(float)movementSettings.Radius*32;DrawRect(new(center-Vector2.One*r,Vector2.One*r*2),new Color("85def0"),false,1/zoom);}
  DrawSetTransform(Vector2.Zero);DrawPlayHud(size);
 }
 void DrawPlayHud(Vector2 size) {
  var u=simulation.Unit;buttons.Clear();DrawRect(new(0,0,size.X,140*ui),panel);
  Text("OpenDC",new(24,34),26,accent);Text("HUMAN04 / MOVEMENT PROTOTYPE"+(laneMode?" / OPEN LANE":""),new(244,32),18,muted);
  Text($"{(paused?"PAUSED":u.Moving?"MOVING":"IDLE")}   {u.Facing}   WORLD {zoom:0.00}x   UI {ui:0.00}x   TICK {simulation.Tick}",new(24,64),18,ink);
  float x=24;void Button(string action,string label,float width){var r=new Rect2(new Vector2(x,82)*ui,new Vector2(width,36)*ui);buttons.Add((r,action,label));DrawRect(r,new Color("263b32"));Text(label,new(x+10,106),16,ink);x+=width+8;}
  Button("stop","Stop [X]",104);Button("pause",paused?"Resume [Space]":"Pause [Space]",152);Button("focus","Focus [F]",108);Button("path",$"Path {(showPath?"ON":"off")} [O]",132);Button("nav",$"Blocked {(showNavigation?"ON":"off")} [N]",168);Button("grid","Grid [G]",100);Button("bounds","Bounds [B]",128);Button("ui-","UI -",62);Button("ui+","UI +",62);
  DrawRect(PlayInfo,new Color(.05f,.09f,.08f,.96f));float right=size.X/ui-274;
  Text("SECURITY TROOPER / 1",new(right+14,185),18,accent);Text(selected?"SELECTED":"NOT SELECTED",new(right+14,216),17,ink);
  Text($"X {u.Position.X:0.00}  Z {u.Position.Z:0.00}",new(right+14,246),17,muted);Text("Right-click: destination",new(right+14,277),16,ink);Text("X: stop / Esc: deselect",new(right+14,308),16,ink);Text("Q: quit",new(right+14,335),15,muted);
  float y=size.Y/ui-76;DrawRect(new(0,y*ui,size.X,76*ui),panel);Text(feedback,new(24,y+24),19,ink);
  Text($"Middle drag / WASD: pan   Wheel: zoom   Speed {movementSettings.TilesPerSecond:0.0} tiles/s; animation {animationFps} FPS: prototype values. Terrain bypass; shadow omitted.",new(24,y+51),16,new Color("e6bc84"));
 }
 object PlayState(string name) {
  var u=simulation.Unit;var p=PlayPose;return new{name,mode=laneMode?"lane":"navigation",simulation.Tick,unit=u,simulation.Status,selected,paused,settings=movementSettings,animationFps,
   pose=new{p.Sequence,p.FinFrame,p.SpriteCell,p.BottomOffset,origin=new[]{p.Visual.OriginX,p.Visual.OriginY},size=new[]{p.Visual.LogicalWidth,p.Visual.LogicalHeight}},
   anchor=new[]{BodyAnchor(u.Position).X,BodyAnchor(u.Position).Y},camera=new[]{camera.X,camera.Y},zoom,ui,viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},area=new[]{Area.Position.X,Area.Position.Y,Area.Size.X,Area.Size.Y},feedback,showPath,showNavigation,selectedInputEvents=inputEvents,receipts=simulation.Receipts};
 }
 async Task SavePlay(string label) {
  string name=$"movement-{playCaptureIndex++:D4}";QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
  using var img=GetViewport().GetTexture().GetImage();if(img.SavePng(Path.Combine(output,name+".png"))!=Error.Ok)throw new IOException("Capture failed");
  File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(PlayState(label),new JsonSerializerOptions{WriteIndented=true}));
 }
 async void CapturePlay(string label){try{await SavePlay(label);}catch(Exception e){GD.PushError(e.ToString());}}
 async Task MousePlay(MouseButton button,Vector2 position) {
  // ParseInputEvent mouse positions enter in native window coordinates, before content scaling.
  var native=(Vector2)GetWindow().Size;var viewport=GetViewportRect().Size;float scale=Math.Min(native.X/viewport.X,native.Y/viewport.Y);
  position=(native-viewport*scale)/2+position*scale;
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=button,Position=position,Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Input.ParseInputEvent(new InputEventMouseButton{ButtonIndex=button,Position=position,Pressed=false});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
 }
 Vector2 Screen(Point point)=>Offset+ToPixel(point)*zoom;
 async Task AdvancePlay(int ticks,string label) {for(int i=0;i<ticks;i++){PlayTick();if((i+1)%(laneMode?10:5)==0)await SavePlay(label);}}
 public async void MovementSuite() {
  try {
   for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   if(combatMode){await CombatSuite();return;}
   if(squadMode){await SquadSuite();return;}
   if(realtimeCheck){await RealtimeSuite();return;}
   if(!laneMode){await NavigationSuite();return;}
   await MousePlay(MouseButton.Right,Screen(new(40.5,73.5)));Check(commandSequence==0&&!simulation.Unit.Moving,"right-click without selection ignored");
   await MousePlay(MouseButton.Left,Screen(simulation.Unit.Position));Check(selected,"left-click selects through Godot input");await SavePlay("selected");
   await MousePlay(MouseButton.Right,new Vector2(30,30));Check(commandSequence==0&&selected,"right-click UI does not command/deselect");
   await MousePlay(MouseButton.Right,Screen(new(40.5,73.5)));await AdvancePlay(240,"open-lane");Check(simulation.Unit.Position==new Point(40.5,73.5)&&!simulation.Unit.Moving,"open-lane arrival");await SavePlay("arrived-idle");
   await MousePlay(MouseButton.Right,Screen(new(34.5,75.5)));await AdvancePlay(60,"diagonal");
   var before=simulation.Unit.Position;await Send(Key.X);PlayTick();Check(simulation.Unit.Position==before&&!simulation.Unit.Moving,"input stop without teleport");await SavePlay("stopped");
   await MousePlay(MouseButton.Left,Screen(new(33.5,74.5)));Check(!selected,"empty-ground deselection");
   await MousePlay(MouseButton.Left,Screen(simulation.Unit.Position));Check(selected,"reselection");
   File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));File.WriteAllText(Path.Combine(output,"final-play-state.json"),JsonSerializer.Serialize(PlayState("final")));
   GD.Print("MOVEMENT_LANE_PASS");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());GetTree().Quit(1);}
 }
}
