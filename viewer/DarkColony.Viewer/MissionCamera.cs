using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 void ResetMissionCamera(){
  // Initial viewpoint only; never used for authoritative positions or mission logic.
  camera=IsWar?(nativeWorld.Entities.FirstOrDefault(e=>e.Living&&e.Owner==LocalTeam) is {} unit?NativePosition(unit):new(nativeData.Scenario.Map.Width*16,nativeData.Scenario.Map.Height*16)):IsHuman||IsCouncil?new(nativeData.Scenario.Teams[0].Rally[0]*32+16,(nativeData.Scenario.Map.Height-1-nativeData.Scenario.Teams[0].Rally[1])*32+16):campaignMission.Index switch {
   13=>new(56*32+16,(nativeData.Scenario.Map.Height-1-11)*32+16),
   10=>new(117*32+16,(nativeData.Scenario.Map.Height-1-9)*32+16),
   9=>new(119*32+16,(nativeData.Scenario.Map.Height-1-6)*32+16),
   8=>new(7*32+16,(nativeData.Scenario.Map.Height-1-9)*32+16),
   7=>new(11*32+16,(nativeData.Scenario.Map.Height-1-15)*32+16),
   >=6 when nativeWorld.Teams[0].Cities[0].Entity is {} home=>NativePosition(home),
   1=>new(12*32,(nativeData.Scenario.Map.Height-74)*32),
   2=>new(54*32,(nativeData.Scenario.Map.Height-14)*32),
   3=>new(73*32,(nativeData.Scenario.Map.Height-58)*32),
   5=>new(10*32+16,(nativeData.Scenario.Map.Height-1-101)*32+16),
   4=>new(8*32+16,(nativeData.Scenario.Map.Height-1-8)*32+16),
   _=>new(22*32+16,(nativeData.Scenario.Map.Height-1-26)*32+16)
  };zoom=economicMission?1.5f:2;ClampMissionCamera();
 }
}
