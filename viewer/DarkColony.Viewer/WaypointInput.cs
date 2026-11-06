using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly List<Cell> playerWaypoints=new(8);
 void BeginWaypoints(){playerWaypoints.Clear();targetMode="waypoints";nativeAttackMove=false;}
 void AddPlayerWaypoint(Cell point){
  if(playerWaypoints.Count>0&&playerWaypoints[^1]==point){
   foreach(var e in NativeSelected().Where(e=>UnitRecipient(e,"move")).OrderBy(e=>e.Slot))nativeWorld.CommandPatrol(e.Slot,e.Incarnation,LocalTeam,playerWaypoints);
   QueueMissionVoice("ACK");targetMode="";playerWaypoints.Clear();return;
  }
  if(playerWaypoints.Count<8)playerWaypoints.Add(point);
 }
 void DrawPlayerWaypoints(){
  if(targetMode!="waypoints")return;
  for(int i=0;i<playerWaypoints.Count;i++){
   var c=playerWaypoints[i];var p=new Vector2(c.X*32+16,(nativeData.Scenario.Map.Height-1-c.Z)*32+16)*zoom+NativeOffset;
   if(NativeArea.HasPoint(p)){DrawCircle(p,7*ui,uiGreen);Text((i+1).ToString(),p+new Vector2(9,-8)*ui,(int)(16*ui),uiGreen);}
  }
 }
}
