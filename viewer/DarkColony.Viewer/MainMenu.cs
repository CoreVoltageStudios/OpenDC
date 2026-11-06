using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;

public partial class Viewer {
 bool mainMenu;int continueSlot=-1;string abandonAction="";
 void PrepareMainMenu(string[] args){
  PrepareEdgeScrolling(args);fastGame=ArtworkPreference.LoadFast(artworkPreferencePath);
  missionSuite=args.FirstOrDefault(a=>a.StartsWith("--mission-suite="))?[16..]??"menu";
  mainMenu=true;missionBriefing=false;RefreshPersistenceCatalog();focusedUi=continueSlot>=0?"resume-save":"campaign";GetWindow().Title="OpenDC";
 }
 void ReturnMainMenu(){
  ClearLocalPause();
  CloseNetwork();HideWarSetup();ShutdownMissionMedia();missionMixer=null!;briefingVoice=null;transportAudio.Clear();missionSoundPaths.Clear();missionAudioAudit.Clear();
  mission=null!;nativeWorld=null!;nativeOcclusion=null!;missionSounds=null!;missionRewards=null;campaignEntry=null;interferenceRoute=null;legacyRoute=null;breakthroughRoute=null;roswellRoute=null;
  nativeSelection.Clear();markerOffsets.Clear();interfaceClips.Clear();interfaceBanks.Clear();
  miniBase=miniPixels=miniVisibility=Array.Empty<byte>();miniSelector=-1;
  nativeAccumulator=missionWall=0;missionFault=null;missionNext=missionBriefing=missionOutcomeShown=campaignWritePending=false;
  // Result input guards/focus belong to that frontend, not the returned menu.
  rewardsVisible=resultContinueAvailable=resultFinal=resultBatchEnd=false;resultAcceptAfter=0;
  ResetMissionInterface();mainMenu=true;paused=true;persistenceNotice="";RefreshPersistenceCatalog();focusedUi=continueSlot>=0?"resume-save":"campaign";
  GetWindow().Title="OpenDC";QueueRedraw();
 }
 void AskAbandon(string action){
  if(!mainMenu&&(campaignWritePending||(!missionBriefing&&!mission.AtOutcome&&missionFault==null))){abandonAction=action;OpenMissionPanel("abandon");}
  else CompleteAbandon(action);
 }
 void CompleteAbandon(string action){if(action=="war-surrender"){if(IsNetworkMatch)network!.Surrender();else mission.SurrenderWar();missionPanel="";return;}if(action=="quit")RequestMissionExit(missionFaultCount>0?1:0);else if(action=="restart")ResetFirstStrike();else ReturnMainMenu();}
 bool MainMenuAction(string id){
  if(id=="war-setup"){OpenWarSetup();return true;}
  switch(id){
   case "council-introduction":BeginCouncilIntroduction();return true;
   case "resume-save":if(continueSlot>=0)LoadMissionSlot(continueSlot);return true;
   case "main-menu":AskAbandon("main-menu");return true;
   case "quit":if(mainMenu)OpenMissionPanel("quit-confirm");else AskAbandon("quit");return true;
   case "quit-confirmed":RequestMissionExit(0);return true;
   case "abandon-confirm":CompleteAbandon(abandonAction);return true;
   case "abandon-cancel":OpenMissionPanel(mission.AtOutcome?"":"menu");return true;
  }return false;
 }
 void DrawMainMenu(){landfallDrawing=true;try{DrawLandfallMain();}finally{landfallDrawing=false;}}
 void DrawLandfallMain(){
  missionHits.Clear();DrawSurfaceBackdrop();
  if(councilIntroduction!=null){councilIntroduction.Size=GetViewportRect().Size;return;}
  if(MultiplayerVisible){LayoutMultiplayer();return;}if(warSetupVisible){LayoutWarSetup();return;}
  if(missionPanel is "campaign" or "missions" or "load" or "options"){DrawSurfaceSubmenu();return;}
  float x=ReadingLeft,y=UiSize.Y<900?48:100;DrawSurfaceLogo(x,y,465,false);
  y=Math.Max(y+164,UiSize.Y-510);
  bool saved=continueSlot>=0;string hero=saved?"resume-save":"campaign";
  UiLabel(saved?"YOUR LAST TRANSMISSION":"BEGIN YOUR JOURNEY",x,y,14,OdcUiTokens.Signal);y+=22;
  UiButton(hero,saved?"Continue":"Campaign",new(x-16,y,680,76),saved?"Resume the newest valid save":"Choose a campaign or training",active:true);
  UiTextLine(saved?saveTitles[continueSlot]:"Two worlds. One colony.",new(x,y+83,650,32),24,OdcUiTokens.Secondary);
  if(saved)UiTextLine($"SLOT {continueSlot+1} · {saveDates[continueSlot]} · {saveCampaigns[continueSlot]}",new(x,y+118,650,25),14,OdcUiTokens.Muted);
  y+=160;
  foreach(var item in new[]{("campaign","Campaign"),("war-setup","Single-player War"),("multiplayer","Multiplayer")}){if(!saved&&item.Item1=="campaign")continue;UiButton(item.Item1,item.Item2,new(x-16,y,560,48),item.Item2);y+=56;}
  y=Math.Min(UiSize.Y-96,y+24);
  foreach(var item in new[]{("load","Load game"),("options","Options"),("quit","Quit")}){int i=item.Item1=="load"?0:item.Item1=="options"?1:2;UiButton(item.Item1,item.Item2,new(x-16+i*184,y,172,44),item.Item1=="load"&&!saveExists.Any(v=>v)?"No saved games yet":item.Item2,item.Item1!="load"||saveExists.Any(v=>v));}
  DrawSurfaceFooter();if(missionPanel.Length>0)DrawMissionPanel();
 }
}
