using Godot;
using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 string SelectedCampaign="taar";
 bool IsWar=>mission?.IsWar==true&&!mainMenu;
 int LocalTeam=>nativeWorld?.Session.LocalTeam??0;
 bool IsHuman=>mainMenu?OriginalCampaign.IsHuman(SelectedCampaign):IsWar?nativeWorld.Teams[LocalTeam].Race==0:OriginalCampaign.IsHuman(campaignMission?.CampaignId??"taar");
 bool IsCouncil=>mainMenu?OriginalCampaign.IsCouncil(SelectedCampaign):!IsWar&&nativeData?.ContentId==OriginalContent.Council;
 string SelectedContentRoot=>OriginalContent.Root(root,SelectedCampaign);
 bool councilAvailable;string councilUnavailable="";
 string CouncilObjective=>(IsHuman,campaignMission?.Index??0) switch {
  (true,7)=>"Destroy the hives and Council palace",(false,7)=>"Destroy all five Human bases",
  (true,0)=>"Recover forces; destroy the three hives",(true,1)=>"Repel the counterattack; destroy three hives",(true,2)=>"Destroy both alien hives",
  (true,3)=>"Destroy both hostile hives",(true,4)=>"Destroy both alien hives",(true,5)=>"Defeat the hostile hives; follow orders",(true,6)=>"Destroy all three hostile hives",
  (false,3)=>"Destroy the medical prototypes",(false,4)=>"Protect the Council; destroy both bases",(false,5) or (false,6)=>"Protect the Council; destroy three bases",
  (false,1)=>"Destroy the Human base",(false,2)=>"Destroy sixteen droids and both bases",_=>"Destroy both Human bases"
 };
 string CouncilStatus=>(IsHuman,campaignMission?.Index??0) switch {
  (true,7)=>"Protect your base; recover the artifact",(false,7)=>"Protect your hive; strike the excavation site",
  (true,0)=>"Your commander must survive",(true,1)=>"Protect your base; follow incoming orders",(true,2)=>"Protect your base; use the camera network",
  (true,3)=>"Protect your base; follow incoming transmissions",(true,4)=>"Protect your base; recover buried artifacts",(true,5)=>nativeWorld?.Teams[0].Allies[1]==0?"Alliance ended; defeat the remaining hives":"Allied hives provide support",(true,6)=>"Protect your base; develop your forces",
  (false,3)=>"Your commander must survive",(false,4) or (false,5) or (false,6)=>"Both your hive and the Council must survive",
  (false,2)=>$"{nativeWorld?.Deaths[7,109]??0} of 16 Science Droids destroyed",_=>"Protect and develop your hive"
 };
 void DrawCouncilObjectives(float x,float y){
  UiWrap(CouncilObjective,x,y,620,26,uiGreen);
  string detail=(IsHuman,campaignMission?.Index??0) switch {
   (true,7)=>"Destroy the two alien hives and the High Council palace while protecting your base. The southern hive holds a powerful artifact. Defeat its defenders to recover it, then use it with your forces against the remaining Council strongholds.",
   (false,7)=>"Destroy all five Human bases while protecting your hive. Strike the Human excavation site to recover its artifact. The Humans are preparing to bring the same power into battle; follow the mind-link warnings and develop your forces.",
   (true,0)=>"Find the surviving teams and destroy the alien hives. Your commander must survive. There is no production under your control; protect the reinforcements you recover.",
   (true,1)=>"Protect your base and develop your forces. Destroy all three hostile hives and repel the approaching counterattack. Follow incoming transmissions; victory requires the final assault to have begun. Allied Stratus forces provide support.",
   (true,2)=>"Destroy both alien hives while protecting your base. Use the allied security cameras to survey the desert. Prepare for repeated enemy reinforcements and newly erupting Petra-7 vents.",
   (true,3)=>"Destroy the two hostile hives and protect your base. Scout the area and follow incoming transmissions. The Council commander remains separately owned; the extraction is handled by the incoming transport. Alliances may change during the mission.",
   (true,4)=>"Destroy both hostile hives while protecting your base. Develop your economy as vents erupt. Build a Research Bay to discover buried artifacts, then send an Exploiter to excavate them. Keep friendly forces clear during activation.",
   (true,5)=>"Destroy the Elite Guard hive while protecting your base. Follow incoming transmissions as the battle develops. If the other hives turn against you, defeat them too. Protect your economy and prepare for newly erupting vents.",
   (true,6)=>"Destroy all three hostile hives while protecting your base. The enemy forces share vision. Develop your economy, recover buried artifacts and prepare for changing Petra-7 supplies.",
   (false,3)=>"Destroy the Human medical prototypes and protect your commander. Explore Area 51, locate allied assistance and follow incoming transmissions. You have no hive or production in this mission.",
   (false,4)=>"Destroy the Stratus and Aerogen bases. Both your hive and the Council hive must survive. Develop your economy and recover buried artifacts. Petra-7 vents erupt as the battle develops.",
   (false,5)=>"Destroy all three hostile bases. Protect your hive and the allied Council hive. Use your shared vision and develop your forces; the enemy receives continuing support.",
   (false,6)=>"Destroy all three Human bases while protecting your hive and the Council hive. The Human forces share vision. Develop your economy and prepare for Petra-7 flow changes.",
   (false,1)=>"Destroy the Human base while protecting your hive. Gather Petra-7 and develop your forces. The allied Watchers share their vision. Your commander arrives shortly after battle begins and can be recovered once.",
   (false,2)=>"Destroy all sixteen Human Science Droids and both Human bases. Either objective may be completed first. The droids patrol in groups and are armed. Protect your hive and prepare for reinforcements as the droids are destroyed.",
   _=>"Destroy the Stratus and Aerogen bases. Protect your hive, gather Petra-7 and develop your forces. Dormant vents may erupt later. Your commander can be recovered once after falling in battle."
  };UiWrap(detail,x,y+100,620,20,uiText);
 }

 readonly string[] saveCampaigns=new string[4];
 static string HumanTypeName(int type)=>type switch {
  0=>"Security Trooper",1 or 41=>"Firestorm",2=>"Reaper",3=>"Barrager",4=>"S.A.R.G.E.",5=>"Osprey IV",6=>"Exploiter",43=>"Spider",49=>"Medi-craft",
  16=>"Exo Center",17=>"Barracks",18=>"Robot Factory",19=>"Robot Factory II",20=>"Sci-Pod",21=>"Sci-Pod II",22=>"Research Bay",_=>throw new InvalidDataException("Unlabelled Human product type "+type)
 };
 string HumanProductName(OriginalDependency d)=>d.Kind switch {
  0=>HumanTypeName(OriginalScenarioImport.CityType(0,d.Level,d.Subject)),
  1=>HumanTypeName(d.Subject),
  2=>HumanTypeName(d.Subject)+(d.Level==0?" weapon ":" armor ")+(d.RaceOrTier==1?"I":"II"),
  _=>throw new InvalidDataException("Unlabelled Human dependency "+d.Id)
 };
 string HumanObjective=>campaignMission.Index switch {
  0=>"Reactivate beacons; secure the colony",
  1=>"Guard satellites; defeat the aliens",
  2=>"Protect Aerogen; destroy the hive",
  3=>"Find Stratus commander; destroy hive",
  4=>nativeWorld.Teams[0].Allies[1]!=0?"Destroy the alien hive":"Defeat Stratus; reach extraction",
  5=>"Rescue the prisoners; find the exit",
  6=>"Protect Aerogen; destroy both hives",
  7=>"Rescue survivors; destroy both hives",
  8=>"Destroy both bases and all twelve pods",
  14=>"Destroy the remaining three alien hives",
  13=>"Explore the underground; find the artifact",
  12=>"Destroy all three alien hives",
  11=>"Intercept couriers; destroy both hives",
  10=>"Destroy the torture devices; find the room",
  9=>"Steal Petra-7; destroy the alien hive",
  _=>"Read mission objectives"
 };
 string HumanObjectiveStatus=>campaignMission.Index switch {
  0=>"Protect your commander and the colony",
  1=>"Protect the base and communications",
  2=>"Keep your base and Aerogen alive",
  3=>mission.Blocks[8]?.Remaining>0?"Commander extraction underway":"Explore the islands",
  4=>nativeWorld.Teams[0].Allies[1]!=0?"Stratus alliance active":"Stratus alliance ended",
  5=>"Your commander must survive",
  6=>"Both your base and Aerogen must survive",
  7=>"Protect your base and the survivors",
  8=>$"{nativeWorld.Deaths[4,87]} of 12 pods destroyed; protect your base",
  14=>"Protect your base; command the Esgaard",
  13=>"Your commander must survive",
  12=>"Protect your base; recover artifacts",
  11=>"Use the shared camera network",
  10=>"Your commander must survive",
  9=>"Protect your base; scout for enemy miners",
  _=>""
 };
 void DrawHumanObjectives(float x,float y){
  UiWrap(HumanObjective,x,y,620,26,uiGreen);
  string detail=campaignMission.Index switch {
   0=>"Reactivate the dropship beacons and locate the mining colony. Your commander must not be critically wounded. Secure the colony before the mission deadline.",
   1=>"Protect the communication satellites and your base. Destroy the aliens in the area so the relieving commander can arrive. Exploiters harvest active Petra-7 vents; inactive vents may erupt later.",
   2=>"Protect the Aerogen base and destroy the alien hive. Both your base and the Aerogen base must survive.",
   3=>"Locate the missing Stratus commander and reach the extraction area. Destroy the alien hive and protect your base. The Stratus commander remains under separate ownership.",
   4=>nativeWorld.Teams[0].Allies[1]!=0?"Assist Stratus in destroying the alien hive. Protect your base and follow incoming transmissions.":"Defeat the hostile Stratus base and follow the extraction instructions. If your commander has been recovered, follow the rendezvous transmission before leaving.",
   5=>"Free the imprisoned soldiers and escape the underground complex. Protect your commander and follow the passages to the exit. Some guards become hostile as you enter their sectors.",
   6=>"Protect your base and the Aerogen base. Destroy both alien hives. A Research Bay reveals artifact sites; send an Exploiter to excavate them. Solaris can damage nearby friendly units when activated.",
   7=>"Rescue the damaged surviving units and use Medi-craft to repair those under your control. Destroy both alien hives while protecting your base. Losing twelve units from the original survivor force fails the mission. A Research Bay enables artifact discovery and excavation.",
   8=>"Destroy the hostile Stratus base, the alien hive and all twelve growth pods. Protect your base. Develop your economy as Petra-7 vents erupt. A Research Bay enables discovery of buried Solaris artifacts, which an Exploiter can excavate.",
   14=>"Use the Esgaard and your Human forces to destroy all three remaining alien hives. Protect your base and develop your economy. This is the final Human campaign mission.",
   13=>"Explore the underground complex and locate the artifact. Protect your commander. Follow the transmissions when the discovery is made; this objective does not require excavation.",
   12=>"Destroy all three alien hives while protecting your base. Build a Research Bay to locate buried Solaris, Maktor and Kaox artifacts, then excavate them with an Exploiter. Keep friendly units clear during artifact activation.",
   11=>"Destroy both alien hives and protect your base. Intercept Council couriers before they call reinforcements. The camera network shares vision; a Research Bay enables Maktor excavation.",
   10=>"Destroy all seven torture devices and reach the hidden room. Your commander must survive. Explore the passages and follow incoming transmissions.",
   9=>"Use S.A.R.G.E. units to siphon Petra-7 from spotted enemy miners. Protect your base, follow incoming deliveries and destroy the alien hive. Each deployed enemy miner can have only one siphoner.",
   _=>"Follow incoming transmissions."
  };
  UiWrap(detail,x,y+100,620,20,uiText);
 }
}
