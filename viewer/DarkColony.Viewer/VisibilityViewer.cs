using Godot;
using DarkColony.Simulation;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool explorationMode,fogCapture,omniscient;
 readonly HashSet<(int id,long tick)> observedShots=new();
 bool VisiblePoint(Point point)=>omniscient||combat.Visibility==null||combat.Visibility.CanSee(combat.PlayerTeam,point);
 bool VisibleUnit(CombatUnit unit)=>omniscient||combat.Visibility==null||(unit.Alive&&unit.Team==combat.PlayerTeam)||VisiblePoint(unit.Body.Position);
 CombatUnit[] PresentedUnits()=>combat.Units.Where(VisibleUnit).ToArray();
 string PresentedTarget(CombatUnit unit)=>unit.Target is int id&&VisibleUnit(combat.Unit(id))?$"  > {id}":"";
 bool VisibleEvent(CombatEvent e) {
  if(omniscient||combat.Visibility==null)return true;
  if(e.Kind=="Shot")return VisibleUnit(combat.Unit(e.Source));
  if(e.Kind is "Death" or "Damage")return VisiblePoint(combat.Unit(e.Target).Body.Position);
  return e.Position is Point p&&VisiblePoint(p);
 }
 void DrawFog() {
  if(combat.Visibility is not TeamVisibility visibility||omniscient)return;
  for(int z=0;z<visibility.Height;z++)for(int x=0;x<visibility.Width;x++) {
   var state=visibility.At(combat.PlayerTeam,new(x,z));if(state==TerrainKnowledge.Visible)continue;
   DrawRect(new(x*32,(visibility.Height-1-z)*32,32,32),state==TerrainKnowledge.Unexplored?new Color("080e0c"):new Color(0,0,0,.68f));
  }
 }
}
