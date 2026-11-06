namespace DarkColony.Viewer;

public partial class Viewer {
 [Flags] enum LocalPauseReason { None=0, Menu=1, Briefing=2, Frontend=4, Fault=8 }
 // Presentation ownership only: never part of OriginalMission or a saved snapshot.
 // Network timing has its own host-ordered authority and never consults this mask.
 LocalPauseReason localPauseReasons;
 readonly Dictionary<(int Other,bool Vision),bool> pendingDiplomacy=new();
 bool SinglePlayerPaused=>!IsNetworkMatch&&localPauseReasons!=LocalPauseReason.None;
 string BattleStatus=>IsNetworkMatch?(network!.Match!.Paused?"Paused by host":"Battle continues"):"Paused";
 void OwnLocalPause(LocalPauseReason reason,bool owns){
  bool before=SinglePlayerPaused;
  if(owns)localPauseReasons|=reason;else localPauseReasons&=~reason;
  if(before!=SinglePlayerPaused){nativeAccumulator=0;persistenceSkipDelta=true;}
  if(before&&!SinglePlayerPaused&&!IsNetworkMatch){
   foreach(var offer in pendingDiplomacy)mission.OfferWarRelationship(LocalTeam,offer.Key.Other,offer.Key.Vision,offer.Value);
   pendingDiplomacy.Clear();
  }
 }
 void SyncLocalPause(){
  OwnLocalPause(LocalPauseReason.Menu,missionPanel.Length>0);
  OwnLocalPause(LocalPauseReason.Briefing,missionBriefing);
  OwnLocalPause(LocalPauseReason.Frontend,mainMenu||missionNext);
  OwnLocalPause(LocalPauseReason.Fault,missionFault!=null);
 }
 void ClearLocalPause(){pendingDiplomacy.Clear();localPauseReasons=LocalPauseReason.None;nativeAccumulator=0;persistenceSkipDelta=true;}
}
