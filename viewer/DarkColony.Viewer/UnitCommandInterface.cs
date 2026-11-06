using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 sealed record UnitControl(string Id,string Label,Key Key,int Art,string Help,OriginalOrder? Order=null);
 // MAINE unit menu:150/33/35/141/140/37 -> MAINBUT62/63/65/121/69/74.
 // Retract uses the original generic Deploy artwork; its explicit label describes
 // the supported harvester reversal, not a claim of a distinct native retract icon.
 static readonly UnitControl[] unitControls={
  new("stop","Stop",Key.S,62,"Stop selected commandable units.",OriginalOrder.Stop),
  new("move","Move only",Key.M,63,"Move without engaging. Choose ground; Esc or right-click cancels."),
  new("attack","Move & attack",Key.A,65,"Choose ground to move and engage, or a visible enemy for an explicit attack."),
  new("waypoints","Waypoints",Key.W,63,"Place up to eight waypoints; click the last again to start a repeating route. S stops. Single-player only."),
  new("inspire","Inspire",Key.I,121,"Commander: requires 32 energy; inspires nearby troops. Other selected units keep their orders.",OriginalOrder.Inspire),
  new("deploy","Deploy mine",Key.D,69,"Deploy selected Spider or Slom mines at their current positions. Requires a clear deployment site.",OriginalOrder.Deploy),
  new("turret","Deploy turret",Key.D,74,"Deploy selected Firestorms or Xenoworts permanently as defensive turrets.",OriginalOrder.Deploy),
  new("activate","Activate",Key.D,74,"Lunatek: consume this artifact to drive nearby units of either side berserk temporarily. Ownership does not change.",OriginalOrder.Deploy),
  new("solaris","Solaris",Key.D,74,"Activate Solaris: consume the artifact in a powerful ground blast. Nearby friendly units can also be hurt.",OriginalOrder.Deploy),
  new("maktor","Maktor",Key.D,74,"Activate Maktor: consume the artifact to pull nearby mobile ground and flying units of either side into the center.",OriginalOrder.Deploy),
  new("kaox","Kaox",Key.D,74,"Activate Kaox: release five moving balls that damage ground occupants of either side, then expire.",OriginalOrder.Deploy),
  new("tektaara","Capture wildlife",Key.D,74,"Tektaara: consume this artifact to take control of nearby neutral wildlife, subject to the army limit. Existing unit orders are preserved.",OriginalOrder.Deploy),
  new("siphon","Steal Petra-7",Key.D,74,"Deploy Gorrem or S.A.R.G.E. to siphon half the income of another owner’s visible deployed miner. Scouts can provide sight. One siphoner per miner; no target causes automatic retraction.",OriginalOrder.Deploy),
  new("retract","Retract",Key.D,74,"Retract selected harvesters or resource siphoners. Other selected units keep their orders.",OriginalOrder.Deploy),
  new("disease","Disease",Key.None,73,"Gorrem: requires weapon tier II and full energy (255). Choose ground. Spores damage ground units of either side and block passage temporarily. Other selected units keep their orders.")
 };
 bool UnitRecipient(NativeEntity e,string id)=>e.Slot>=120&&e.Type is not (47 or 48)&&id=="stop" || id switch {
  "waypoints"=>!IsNetworkMatch&&UnitRecipient(e,"move"),
  "move"=>e.Slot>=120&&e.Type is not (47 or 48)&&nativeData.Scenario.Types[e.Type].Fields[2]>0,
  "attack"=>e.Slot>=120&&e.Type is not (47 or 48)&&nativeWorld.Weapon(e.Type,e.Owner)!=null,
  "inspire"=>nativeWorld.IsCommander(e)&&e.Energy>=32,
  "disease"=>nativeWorld.CanDisease(e),
  "turret"=>e.Type is 1 or 9&&nativeWorld.CanDeploy(e),
  "deploy"=>e.Type is 43 or 44&&nativeWorld.CanDeploy(e),
  "activate"=>e.Type==65&&nativeWorld.CanDeploy(e),
  "solaris"=>e.Type==63&&nativeWorld.CanDeploy(e),
  "maktor"=>e.Type==64&&nativeWorld.CanDeploy(e),
  "kaox"=>e.Type==66&&nativeWorld.CanDeploy(e),
  "tektaara"=>e.Type==67&&nativeWorld.CanDeploy(e),
  "siphon"=>e.Type is 4 or 12&&nativeWorld.CanDeploy(e),
  "retract"=>e.Type is 47 or 48 or 77 or 78&&nativeWorld.CanDeploy(e),_=>false};
 bool UnitEnabled(string id)=>NativeSelected().Any(e=>UnitRecipient(e,id));
 IEnumerable<UnitControl> VisibleUnitControls()=>unitControls.Where(c=>c.Id is "stop" or "move" or "attack" or "inspire"||c.Id=="waypoints"&&!IsNetworkMatch||NativeSelected().Any(e=>c.Id switch{
  "solaris"=>e.Type==63,"maktor"=>e.Type==64,"kaox"=>e.Type==66,"siphon"=>e.Type is 4 or 12,"disease"=>e.Type==12,"tektaara"=>e.Type==67,"activate"=>e.Type==65,"turret"=>e.Type is 1 or 9,"deploy"=>e.Type is 43 or 44,"retract"=>e.Type is 47 or 48 or 77 or 78,_=>false}));
 float DrawUnitCommands(float x,float y,bool compact){
  var controls=VisibleUnitControls().ToArray();float height=compact?(UiSize.Y<800&&controls.Length>4?38:44):50;
  for(int i=0;i<controls.Length;i++){
   var c=controls[i];bool ability=i>=4;int cols=2;
   float width=(296-(cols-1)*4)/cols;int column=i%2,row=i/2;
   var r=new Rect2(x+20+column*(width+4),y+row*(height+4),width,height);
   bool enabled=UnitEnabled(c.Id);string hint=c.Key==Key.None?"":" ["+c.Key+"]";UiButton(c.Id,"",r,c.Label+hint+"\n"+c.Help+(enabled?"":"\nNo eligible selected unit, or ability not ready."),enabled,targetMode==c.Id);
   float artWidth=ability&&cols==3?25:40;
   UiImage("MAINBUT",c.Art,new(r.Position.X+3,r.Position.Y+3,artWidth,height-(height>=44?18:6)),enabled?Colors.White:new Color(.6f,.6f,.6f));
   UiWrap(c.Label,r.Position.X+artWidth+7,r.Position.Y+(height>44?20:16),width-artWidth-10,ability&&cols==3?12:14,enabled?uiText:uiDim,2);
   if(height>=44&&c.Key!=Key.None)UiLabel(c.Key.ToString(),r.Position.X+5,r.End.Y-4,11,uiDim);
  }return y+((controls.Length+1)/2)*(height+4);
 }
 bool ActivateUnitCommand(string id){var c=unitControls.FirstOrDefault(c=>c.Id==id);if(c==null)return false;if(!UnitEnabled(id))return true;
  if(id=="waypoints"){BeginWaypoints();return true;}
  if(c.Order is {} order){if(IsNetworkMatch)QueueNetworkUnits(NativeSelected().Where(e=>UnitRecipient(e,id)),order);else foreach(var e in NativeSelected().Where(e=>UnitRecipient(e,id)).OrderBy(e=>e.Slot))nativeWorld.Command(new(e.Slot,e.Incarnation,order,Issuer:LocalTeam));if(order!=OriginalOrder.Inspire)QueueMissionVoice("ACK");targetMode="";nativeAttackMove=false;}
  else {targetMode=id;nativeAttackMove=id=="attack";}QueueRedraw();return true;
 }
 bool UnitCommandKey(Key key){var commands=unitControls.Where(c=>c.Key!=Key.None&&c.Key==key).ToArray();foreach(var c in commands)ActivateUnitCommand(c.Id);return commands.Length>0;}
 bool UnitTargetInput(InputEventMouseButton b){
  if(!b.Pressed||b.ButtonIndex!=MouseButton.Left||!(targetMode.Length>0||nativeAttackMove))return false;
  var cell=NativeCell((b.Position-NativeOffset)/zoom);if(nativeWorld.Navigation.Inside(cell)){if(targetMode=="waypoints"){AddPlayerWaypoint(cell);return true;}if(targetMode=="disease"){UiOrder(OriginalOrder.Disease,cell);return true;}var e=UiHover(b.Position);if(targetMode!="move"&&e!=null&&!nativeWorld.Relations[LocalTeam,e.Owner])UiOrder(OriginalOrder.Attack,target:e.Slot);else UiOrder(targetMode=="move"?OriginalOrder.MoveOnly:OriginalOrder.MoveAttack,ResourceAt(b.Position)?.Cell??cell);}return true;
 }
}
