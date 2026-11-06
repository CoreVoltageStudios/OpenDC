using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 async Task VerifyCampaignCatalog(){
  foreach(string faction in new[]{"taar","human"}){
   var catalog=OriginalCampaign.Catalog(nativeData.DataRoot,faction);
   Check(catalog.Length==15&&catalog.All(m=>m.Implemented),"Fifteen implemented records: "+faction);
   SelectedCampaign=faction;RefreshPersistenceCatalog();OpenMissionPanel("campaign");campaignScroll=0;await MenuCapture("catalog-"+faction+"-top");
   Check(missionHits.Any(h=>h.Id=="campaign-start:0"&&h.Enabled)&&!missionHits.Any(h=>h.Id.StartsWith("campaign-start:")&&h.Id!="campaign-start:0"&&h.Enabled),"Fresh campaign unlocks only first: "+faction);
   campaignScroll=14;await MenuCapture("catalog-"+faction+"-bottom");Check(missionHits.Any(h=>h.Id=="campaign-start:14"&&!h.Enabled)&&!missionHits.Any(h=>h.Id=="campaign-start:15"),"Last mission locked, no mission16: "+faction);CloseRelayPanel();
  }
  await SessionClick("options");await SessionClick("playtesting");await SessionClick("god-mode");CloseRelayPanel();CloseRelayPanel();
  OpenMissionPanel("campaign");campaignScroll=8;await MenuCapture("catalog-human9-god");await SessionClick("campaign-start:8");Check(IsHuman&&mission.CampaignIndex==8&&godMode,"Main menu temporary Extermination entry");
  await Send(Key.Space);await Send(Key.Enter);AdvanceInterference(240);SaveMissionSlot(0);CloseRelayPanel();long tick=nativeWorld.Tick;
  ReturnMainMenu();await SessionClick("resume-save");Check(IsHuman&&mission.CampaignIndex==8&&nativeWorld.Tick==tick,"Main-menu Continue returns to exact Human9 state");await MenuCapture("catalog-human9-continue");
  ReturnMainMenu();Check(mission==null&&nativeWorld==null&&nativeTerrain.Count==0&&missionMusic==null,"Return to menu releases mission and media");await MenuCapture("catalog-final-menu");
 }
}
