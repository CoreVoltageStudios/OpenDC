using Godot;
using DarkColony.Simulation;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool showRange,guardCapture;
 void DrawWeaponRange(){if(!showRange)return;int? id=selection.Order().Where(i=>combat.Unit(i).Alive).Select(i=>(int?)i).FirstOrDefault();if(id is not int unitId)return;var u=combat.Unit(unitId);DrawArc(ToPixel(u.Body.Position),(float)combat.Definition(unitId).Weapon.RangeTiles*32,0,Mathf.Tau,128,new Color(.4f,.9f,1,.8f),1.5f/zoom);if(u.Tactics?.GuardAnchor is Point home){var center=ToPixel(home);DrawCircle(center,4,new Color("ffbe70"));for(int i=0;i<32;i++)DrawArc(center,(float)combat.Guard.Distance*32,i*Mathf.Tau/32,(i+.55f)*Mathf.Tau/32,6,new Color(1,.7f,.3f,.7f),1.5f/zoom);}}
 string DefenseStatus(CombatUnit u)=>u.Tactics?.Order switch{TacticalOrder.Hold=>"Hold / "+u.Activity,TacticalOrder.Guard=>u.Tactics.ResumeBlocked?"Guard / return blocked":u.Tactics.Returning?"Guard / returning":"Guard / "+u.Activity,_=>u.Activity.ToString()};
}
