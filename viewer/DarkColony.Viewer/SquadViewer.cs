using Godot;
using DarkColony.Assets;
using DarkColony.Simulation;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool squadMode,boxActive,boxShift,occupancy;Vector2 boxStart,boxCurrent;int squadCaptureIndex,squadReceipt;
 SquadSimulation squad=null!;readonly HashSet<int> selection=new();readonly Dictionary<int,(string key,ImageTexture texture)> squadTextures=new();JsonElement squadConfig;
 Rect2 SquadInfo=>new(GetViewportRect().Size.X-316*ui,158*ui,292*ui,(combatMode?140+PresentedUnits().Length*48:290)*ui);
 bool SquadUi(Vector2 p)=>!Area.HasPoint(p)||SquadInfo.HasPoint(p);
 TrooperPose SquadPose(SquadUnit u)=>combatMode?CombatPose(u):clips.Resolve((int)u.Facing,u.Moving,u.MovingTicks*animationFps/movementSettings.TickRate);
 IEnumerable<SquadUnit> DrawOrder()=>(combatMode?combat.Units.Where(u=>VisibleUnit(u)&&(u.Alive||combat.Tick-u.DeathTick<90)).Select(u=>u.Body):squad.Units).OrderByDescending(u=>u.Position.Z).ThenBy(u=>u.Id); // Deliberate remake depth approximation.
 void PrepareSquad() {
  squadMode=true;squadConfig=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"fixtures/squad-settings.json"))).RootElement.Clone();
  var spawns=squadConfig.GetProperty("spawns").EnumerateArray().Select(s=>new UnitSpawn(s[0].GetInt32(),new(s[1].GetDouble(),s[2].GetDouble())));
  squad=new(navigation,spawns,movementSettings,new(squadConfig.GetProperty("destinationRadius").GetInt32(),squadConfig.GetProperty("replanInterval").GetInt32(),squadConfig.GetProperty("maxReplans").GetInt32(),squadConfig.GetProperty("waitBudget").GetInt32()));
  var c=squadConfig.GetProperty("camera");camera=ToPixel(new(c[0].GetDouble(),c[1].GetDouble()));zoom=2.5f;selection.Clear();feedback="Select a trooper or drag a box. Shift adds/toggles selection.";
  File.WriteAllText(Path.Combine(output,"squad-input.jsonl"),"");GetWindow().Title="OpenDC | Small Squad";
 }
 void SquadLog(string action) {
  inputEvents++;if(inputEvents<=4096)File.AppendAllText(Path.Combine(output,"squad-input.jsonl"),JsonSerializer.Serialize(new{tick=squad.Tick,action,selection=selection.Order().ToArray(),sequence=commandSequence})+System.Environment.NewLine);
 }
 void SquadTick() {
  if(combatMode){CombatTick();return;}
  squad.Step();
  while(squadReceipt<squad.Receipts.Count) {
   var r=squad.Receipts[squadReceipt++];feedback=r.Result switch{SquadOrderResult.Accepted=>$"Move accepted: {r.Assignments.Length} distinct destinations.",SquadOrderResult.Stopped=>$"Stopped {r.Units.Length} selected units.",SquadOrderResult.InsufficientSpace=>"Order rejected: insufficient reachable space near that point.",SquadOrderResult.BlockedTerrain=>"Order rejected: blocked terrain.",SquadOrderResult.OutsideMap=>"Order rejected: outside map.",_=>r.Result.ToString()};
  }
  if(squad.Units.Any(u=>u.State==TravelState.Blocked))feedback="A unit is blocked after bounded waiting. Select it and issue a new order.";
 }
 void ProcessSquad(double delta){if(!captureDriven&&!paused)simClock.Advance(Math.Min(delta,1),SquadTick);QueueRedraw();}
 void SquadCommand(OrderKind kind,Point point) {
  if(combatMode){CombatCommand(kind==OrderKind.Stop?CombatOrderKind.Stop:CombatOrderKind.Move,point);return;}
  if(selection.Count==0){feedback="Select at least one trooper first.";SquadLog("order-without-selection");return;}
  squad.Enqueue(new(squad.Tick+1,++commandSequence,selection.Order().ToArray(),kind,point));SquadLog(kind.ToString());
 }
 void SquadAction(string action) {
  switch(action) {
   case "hold":if(tacticalMode)CombatCommand(CombatOrderKind.Hold,default);break;
   case "guard":if(tacticalMode)CombatCommand(CombatOrderKind.Guard,default);break;
   case "range":showRange=!showRange;break;
   case "attack-move":if(tacticalMode){attackMoveArmed=selection.Count>0;feedback=attackMoveArmed?"Click ground for attack-move; Esc cancels.":"Select at least one friendly unit first.";}break;
   case "reset":if(combatMode)ResetCombat();break;
   case "stop":SquadCommand(OrderKind.Stop,new());break;
   case "pause":paused=!paused;break;
   case "path":showPath=!showPath;break;
   case "nav":occupancy=!occupancy;break;
   case "grid":grid=!grid;break;
   case "bounds":bounds=!bounds;break;
   case "focus":var chosen=squad.Units.Where(u=>selection.Contains(u.Id)).ToArray();if(chosen.Length==0)chosen=squad.Units.Where(u=>!combatMode||combat.Visibility==null||combat.Unit(u.Id).Team==combat.PlayerTeam).ToArray();if(chosen.Length>0)camera=ToPixel(new(chosen.Average(u=>u.Position.X),chosen.Average(u=>u.Position.Z)));ClampCamera();break;
   case "ui-":ui=Math.Max(.75f,ui-.125f);break;
   case "ui+":ui=Math.Min(1.125f,ui+.125f);break;
  }
  SquadLog("button:"+action);QueueRedraw();
 }
 int? PickSquad(Vector2 screen) {
  var world=World(screen);
  foreach(var u in DrawOrder().Reverse()) {
   if(combatMode&&!combat.Unit(u.Id).Alive)continue;
   var p=SquadPose(u);var a=BodyAnchor(u.Position);var v=p.Visual;var local=world-a+new Vector2(v.OriginX,v.OriginY);
   if(local.X<0||local.Y<0||local.X>=v.LogicalWidth||local.Y>=v.LogicalHeight)continue;
   var raster=visuals.Occlusion.ApplyGroundBody(v,(int)a.X,(int)a.Y,(int)a.Y+p.BottomOffset);
   int x=(int)(local.X*raster.Width/v.LogicalWidth),y=(int)(local.Y*raster.Height/v.LogicalHeight);
   if(raster.Rgba[(y*raster.Width+x)*4+3]!=0)return u.Id;
  }
  return squad.Units.Where(u=>(!combatMode||VisibleUnit(combat.Unit(u.Id)))&&world.DistanceTo(ToPixel(u.Position))<11).OrderBy(u=>world.DistanceSquaredTo(ToPixel(u.Position))).ThenBy(u=>u.Id).Select(u=>(int?)u.Id).FirstOrDefault();
 }
 void FinishSelection(Vector2 position) {
  if(SquadUi(position)){boxActive=false;SquadLog("selection-release-ui-cancelled");return;}
  if(boxStart.DistanceTo(position)<6) {
   int? hit=PickSquad(position);if(!boxShift)selection.Clear();if(hit is int id&&(!combatMode||combat.Unit(id).Team==combat.PlayerTeam)){if(boxShift&&!selection.Add(id))selection.Remove(id);else selection.Add(id);}
   SquadLog(boxShift?"shift-click":"single-click");
  } else {
   var rect=new Rect2(boxStart,position-boxStart).Abs();if(!boxShift)selection.Clear();foreach(var u in squad.Units)if(rect.HasPoint(Screen(u.Position))&&(!combatMode||combat.Unit(u.Id).Team==combat.PlayerTeam))selection.Add(u.Id);
   SquadLog(boxShift?"shift-box":"box-select");
  }
  boxActive=false;feedback=$"{selection.Count} selected. Right-click {(combatMode?"enemy to attack, ground to move":"to move")}; X to stop.";
 }
 void SquadInput(InputEvent e) {
  if(e is InputEventKey k&&k.Pressed&&!k.Echo) {
   if(k.Keycode==Key.X)SquadCommand(OrderKind.Stop,new());
   else if(k.Keycode==Key.H&&tacticalMode)SquadAction("hold");else if(k.Keycode==Key.J&&tacticalMode)SquadAction("guard");else if(k.Keycode==Key.V&&tacticalMode)SquadAction("range");
   else if(k.Keycode==Key.F8&&explorationMode){omniscient=!omniscient;SquadLog("omniscient:"+omniscient);}
   else if(k.Keycode==Key.R&&combatMode)SquadAction("reset");
   else if(k.Keycode==Key.Escape){if(attackMoveArmed){attackMoveArmed=false;SquadLog("cancel-attack-move");}else{selection.Clear();boxActive=false;SquadLog("deselect");}}
   else if(k.Keycode==Key.A&&tacticalMode)SquadAction("attack-move");
   else if(k.Keycode==Key.Q)GetTree().Quit();
   else if(k.Keycode==Key.F12){if(combatMode)CaptureCombat("manual");else CaptureSquad("manual");}
   else if(k.Keycode==Key.Space)SquadAction("pause");
   else if(k.Keycode==Key.O)SquadAction("path");else if(k.Keycode==Key.N)SquadAction("nav");else if(k.Keycode==Key.G)SquadAction("grid");else if(k.Keycode==Key.B)SquadAction("bounds");else if(k.Keycode==Key.F)SquadAction("focus");
   else if(k.Keycode is Key.Plus or Key.Equal or Key.KpAdd){Zoom(1.25f,Area.GetCenter());SquadLog("zoom-key");}
   else if(k.Keycode is Key.Minus or Key.KpSubtract){Zoom(.8f,Area.GetCenter());SquadLog("zoom-key");}
   else if(k.Keycode is Key.W or Key.A or Key.S or Key.D or Key.Up or Key.Left or Key.Down or Key.Right){camera+=k.Keycode switch{Key.W or Key.Up=>new Vector2(0,-64),Key.S or Key.Down=>new Vector2(0,64),Key.A or Key.Left=>new Vector2(-64,0),_=>new Vector2(64,0)};ClampCamera();SquadLog("pan-key");}
  }
  if(e is InputEventMouseButton m) {
   if(m.ButtonIndex==MouseButton.Middle){if(!m.Pressed||!SquadUi(m.Position))dragging=m.Pressed;}
   if(m.ButtonIndex==MouseButton.Left&&!m.Pressed&&boxActive)FinishSelection(m.Position);
   if(m.Pressed) {
    if(SquadUi(m.Position)){if(m.ButtonIndex==MouseButton.Left){var b=buttons.FirstOrDefault(b=>b.Rect.HasPoint(m.Position));if(b.Action!=null)SquadAction(b.Action);}SquadLog("ui-consumed");return;}
    if(m.ButtonIndex==MouseButton.Left&&attackMoveArmed){boxActive=false;CombatCommand(CombatOrderKind.AttackMove,ToLogical(World(m.Position)));}
    else if(m.ButtonIndex==MouseButton.Left){boxActive=true;boxStart=boxCurrent=m.Position;boxShift=m.ShiftPressed;}
    else if(m.ButtonIndex==MouseButton.Right){boxActive=false;int? hit=combatMode?PickSquad(m.Position):null;if(hit is int id&&combat.Unit(id).Team!=combat.PlayerTeam)CombatCommand(CombatOrderKind.Attack,default,id);else SquadCommand(OrderKind.Move,ToLogical(World(m.Position)));}
    else if(m.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown){Zoom(m.ButtonIndex==MouseButton.WheelUp?1.25f:.8f,m.Position);SquadLog("zoom-wheel");}
   }
  }
  if(e is InputEventMouseMotion motion){if(boxActive)boxCurrent=motion.Position;if(dragging){camera-=motion.Relative/zoom;ClampCamera();SquadLog("pan-drag");}}
  QueueRedraw();
 }
 void DrawSquad() {
  ClampCamera();var size=GetViewportRect().Size;DrawRect(new(Vector2.Zero,size),new Color("091310"));DrawSetTransform(Offset,0,Vector2.One*zoom);
  DrawAsset("map/background",Vector2.Zero);DrawAsset("map/foreground",Vector2.Zero);
  if(combatMode){DrawFog();DrawWeaponRange();}
  if(occupancy)for(int z=0;z<navigation.Height;z++)for(int x=0;x<navigation.Width;x++)if(!navigation.Walkable(new(x,z))&&(!combatMode||omniscient||combat.Visibility==null||combat.Visibility.At(combat.PlayerTeam,new(x,z))!=TerrainKnowledge.Unexplored))DrawRect(new(x*32,(navigation.Height-1-z)*32,32,32),new Color(1,.2f,.1f,.18f));
  if(grid){for(int x=0;x<=navigation.Width;x++)DrawLine(new(x*32,0),new(x*32,MapHeight),new Color(1,1,1,.18f),1/zoom);for(int y=0;y<=navigation.Height;y++)DrawLine(new(0,y*32),new(MapWidth,y*32),new Color(1,1,1,.18f),1/zoom);}
  foreach(var u in squad.Units) {
   if(combatMode&&combat.Visibility!=null&&!omniscient&&combat.Unit(u.Id).Team!=combat.PlayerTeam)continue;
   var center=ToPixel(u.Position);
   if(showPath&&u.RouteIndex<u.Route.Length){var last=center;foreach(var p in u.Route.Skip(u.RouteIndex)){var next=ToPixel(p);DrawLine(last,next,new Color(.55f,.8f,.82f,.65f),1/zoom);last=next;}}
   if(u.Destination is Point dest){var p=ToPixel(dest);DrawCircle(p,5,accent,false,1.5f/zoom);DrawString(font,p+new Vector2(7,3),u.Id.ToString(),HorizontalAlignment.Left,-1,Math.Max(6,(int)(14/zoom)),accent);}
   if(selection.Contains(u.Id))DrawArc(center,11,0,Mathf.Tau,36,accent,2/zoom);
   if(occupancy){float r=(float)movementSettings.Radius*32;DrawRect(new(center-Vector2.One*r,Vector2.One*r*2),new Color("94dcff"),false,1/zoom);if(u.ReservedFrom is Point a&&u.ReservedTo is Point b){var rect=new Rect2(ToPixel(a),ToPixel(b)-ToPixel(a)).Abs().Grow(r);DrawRect(rect,new Color(.4f,.7f,1,.15f));DrawRect(rect,new Color(.4f,.7f,1,.6f),false,1/zoom);}}
  }
  foreach(var u in DrawOrder()) {
   var pose=SquadPose(u);var v=pose.Visual;var anchor=BodyAnchor(u.Position);string key=$"{v.Id}:{anchor.X}:{anchor.Y}";
   if(!squadTextures.TryGetValue(u.Id,out var cached)||cached.key!=key){cached.texture?.Dispose();var pixels=visuals.Occlusion.ApplyGroundBody(v,(int)anchor.X,(int)anchor.Y,(int)anchor.Y+pose.BottomOffset);cached=(key,FromRaster(pixels));squadTextures[u.Id]=cached;}
   DrawTextureRect(cached.texture,new(anchor-new Vector2(v.OriginX,v.OriginY),new(v.LogicalWidth,v.LogicalHeight)),false,new Color(1,1,1,combatMode?CombatOpacity(u.Id):1));
   if(bounds)DrawRect(new(anchor-new Vector2(v.OriginX,v.OriginY),new(v.LogicalWidth,v.LogicalHeight)),accent,false,1/zoom);
   if(combatMode){DrawFiringEffects(u,pose,anchor);DrawCombatUnitFeedback(u,pose,anchor);}else DrawString(font,anchor-new Vector2(3,v.OriginY+4),u.Id.ToString(),HorizontalAlignment.Left,-1,Math.Max(7,(int)(16/zoom)),selection.Contains(u.Id)?accent:ink);
  }
  if(combatMode)DrawDeliveryEffects();DrawSetTransform(Vector2.Zero);if(boxActive){var r=new Rect2(boxStart,boxCurrent-boxStart).Abs();DrawRect(r,new Color(.6f,.9f,.5f,.15f));DrawRect(r,accent,false,1.5f);}
  DrawSquadHud(size);
 }
 void DrawSquadHud(Vector2 size) {
  if(combatMode){DrawCombatHud(size);return;}
  buttons.Clear();DrawRect(new(0,0,size.X,140*ui),panel);Text("OpenDC",new(24,34),26,accent);Text("HUMAN04 / SMALL SQUAD",new(244,32),18,muted);
  Text($"{selection.Count}/4 SELECTED   {(paused?"PAUSED":"ACTIVE")}   WORLD {zoom:0.00}x   UI {ui:0.00}x",new(24,64),18,ink);
  float x=24;void Button(string action,string label,float width){var r=new Rect2(new Vector2(x,82)*ui,new Vector2(width,36)*ui);buttons.Add((r,action,label));DrawRect(r,new Color("263b32"));Text(label,new(x+10,106),16,ink);x+=width+8;}
  Button("stop","Stop [X]",104);Button("pause",paused?"Resume [Space]":"Pause [Space]",152);Button("focus","Focus [F]",108);Button("path","Paths [O]",116);Button("nav","Occupancy [N]",164);Button("grid","Grid [G]",100);Button("bounds","Bounds [B]",126);Button("ui-","UI -",62);Button("ui+","UI +",62);
  DrawRect(SquadInfo,new Color(.05f,.09f,.08f,.96f));float right=size.X/ui-316;Text("SECURITY SQUAD",new(right+14,185),18,accent);
  int row=0;foreach(var u in squad.Units){float y=216+row++*47;Text($"{(selection.Contains(u.Id)?">":" ")} {u.Id}   {u.State}",new(right+14,y),17,u.State==TravelState.Blocked?new Color("fa9870"):ink);Text($"X {u.Position.X:0.0} Z {u.Position.Z:0.0}   wait {u.WaitTicks}",new(right+24,y+20),14,muted);}
  Text("Shift: add/toggle selection",new(right+14,425),15,muted);
  float bottom=size.Y/ui-76;DrawRect(new(0,bottom*ui,size.X,76*ui),panel);Text(feedback,new(24,bottom+24),18,ink);Text("Drag: select   Shift: add/toggle   Right-click: move   X: stop   Middle/WASD: pan   Wheel: zoom   Q: quit   Prototype speed/8 FPS",new(24,bottom+51),16,new Color("e6bc84"));
 }
}
