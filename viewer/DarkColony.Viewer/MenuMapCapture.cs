using Godot;
using DarkColony.Original;
using System.Text.Json;
namespace DarkColony.Viewer;
// Opt-in rendered menu documentation only. Never runs during ordinary play.
// Uses isolated profiles supplied by the launcher; outcome fixtures stay under generated/.
public partial class Viewer {
 async Task CaptureMenuMap(){
  missionHarnessManual=true;ui=1;maximumAspect=0;ApplyAspectLimit();
  var records=new List<object>();
  async Task Shot(string name,string parent,string route,bool fixture=false){
   missionPointer=new(-100,-100);await WarFrames(8);QueueRedraw();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
   using var im=GetViewport().GetTexture().GetImage();im.SavePng(Path.Combine(output,name+".png"));
   records.Add(new{name,parent,route,fixture,ui,aspect=maximumAspect,width=im.GetWidth(),height=im.GetHeight(),panel=missionPanel,actions=missionHits.Select(h=>h.Id).ToArray()});
   File.WriteAllText(Path.Combine(output,"menu-map.json"),JsonSerializer.Serialize(records));
  }
  if(missionSuite=="menu-map-details"){
   var d=PersistenceData("SCENARIO/HUMAN/HUMAN01.SCN");
   var s=SaveCodec.Open(Path.Combine(root,"generated/human15/h01/controlled-success.json"))["payload"]!.AsObject();
   // Explicit presentation-only failure fixture, not an ordinary defeat or script check.
   var o=s["outcome"]!.AsObject();o["Success"]=false;o["Winner"]=1;o["TextPath"]="SCENARIO/HUMAN/HUMAN01.002";o["NextIndex"]=0;
   s["resultProfile"]!["CampaignIndex"]=0;InstallMission(d,OriginalMission.Restore(d,s),false);
   await WarFrames(10);ReleaseMissionVideo();BeginRewards();await ToSignal(GetTree().CreateTimer(2.5),SceneTreeTimer.SignalName.Timeout);
   await Shot("base-defeat","Battle","Controlled outcome presentation fixture; original failure text",true);ReturnMainMenu();
   foreach(string faction in new[]{"HUMAN","ALIEN"}){
    d=PersistenceData($"SCENARIO/{faction}/{faction}02.SCN");InstallMission(d,new OriginalMission(d,7),false);AdvanceInterference(250);
    await Shot(faction.ToLowerInvariant()+"-production","Battle","Mission 2 production category");
    focusedUi=missionHits.First(h=>h.Id.StartsWith("product:")).Id;relayPointerHelp=false;
    await Shot(faction.ToLowerInvariant()+"-production-tooltip","Battle","Keyboard focus on first production tile");
    Check(relayTooltip?.Visible==true,"Production tooltip visible for capture");ReturnMainMenu();
   }
   OpenWarSetup();await WarFrames(30);Input.ParseInputEvent(new InputEventMouseMotion{Position=warTeams[0].GetGlobalRect().GetCenter()});
   await ToSignal(GetTree().CreateTimer(1.5),SceneTreeTimer.SignalName.Timeout);await Shot("war-team-tooltip","War setup","Mouse hover on local team control");HideWarSetup();return;
  }
  if(missionSuite=="menu-map-finals"){
   foreach(var pair in new[]{(1,true),(15,true)}){
    HumanResult(pair.Item1,pair.Item2);await WarFrames(10);ReleaseMissionVideo();BeginRewards();await ToSignal(GetTree().CreateTimer(2.5),SceneTreeTimer.SignalName.Timeout);await Shot(pair.Item1==15?"human-campaign-completion":pair.Item2?"base-victory":"base-defeat","Battle","Controlled outcome; original movie skipped",true);ReturnMainMenu();
   }
   var d=PersistenceData("SCENARIO/ALIEN/ALIEN15.SCN");var s=SaveCodec.Open(Path.Combine(root,"generated/final-evacuation/mission-final/success.json"))["payload"]!.AsObject();InstallMission(d,OriginalMission.Restore(d,s),false);await WarFrames(10);ReleaseMissionVideo();BeginRewards();await ToSignal(GetTree().CreateTimer(2.5),SceneTreeTimer.SignalName.Timeout);await Shot("taar-campaign-completion","Ending","Controlled final Taar outcome; movie skipped",true);ReturnMainMenu();
   OpenWarSetup();await WarFrames(30);string[] labels={"storage","artifacts","erupting","renewable","quantity","flow","rank"};for(int i=0;i<warOptionsControls.Count;i++){if(warOptionsControls[i] is OptionButton choice){choice.ShowPopup();await Shot("war-option-"+labels[i],"War setup","Open match option");choice.GetPopup().Hide();}}
   HideWarSetup();StartWarMatch(WarConfiguration.Default("A2PLAY01",7));AdvanceInterference(32);SaveMissionSlot(1);OpenMissionPanel("save");await Shot("war-save","Battle","Esc > Save Game; isolated War save");ReturnMainMenu();OpenMissionPanel("load");surfaceSave=1;await Shot("war-load","Main menu","Load Game > War slot");missionPanel="";
   return;
  }
  if(missionSuite=="menu-map-extra"){
   SelectedCampaign="human";RefreshPersistenceCatalog();OpenMissionPanel("campaign");godMode=true;await Shot("god-unlocked-missions","Campaign","Session God mode on");godMode=false;
   var d=PersistenceData("SCENARIO/ALIEN/ALIEN04.SCN");InstallMission(d,new OriginalMission(d,7),true);RevealBriefing();await ToSignal(GetTree().CreateTimer(1.5),SceneTreeTimer.SignalName.Timeout);await Shot("long-title-briefing","Campaign","Operation Breakthrough; original long briefing",true);ReturnMainMenu();
   OpenMissionPanel("options");await WarFrames();var h=missionHits.First(x=>x.Id=="aspect:16");missionPointer=Px(h.Rect).GetCenter();await WarFrames(40);await MenuCapture("options-aspect-tooltip");
   Directory.CreateDirectory(Path.GetDirectoryName(SavePath(3))!);File.WriteAllText(SavePath(3),"invalid isolated fixture");RefreshPersistenceCatalog();OpenMissionPanel("load");await Shot("load-unreadable","Main menu","Load Game with damaged isolated save",true);LoadMissionSlot(3);await Shot("save-error","Load Game","Select unreadable save",true);missionPanel="";
   OpenMultiplayer("Different OpenDC build. Both players need the same source build.");await Shot("network-compatibility-error","Multiplayer","Recorded protocol error presented through real error UI",true);CloseNetwork();
   OpenMultiplayer();await WarFrames();var fields=multiplayerRoot!.FindChildren("*","LineEdit",true,false).Cast<LineEdit>().ToArray();fields[1].Text="127.0.0.1";multiplayerRoot.FindChildren("*","SpinBox",true,false).Cast<SpinBox>().Single().Value=28093;await WarControlClick(multiplayerRoot.FindChildren("*","Button",true,false).Cast<Button>().Single(x=>x.Text=="Join by address"));await Shot("network-connecting","Multiplayer","Actual unanswered loopback connection");ulong until=Time.GetTicksMsec()+18000;while(network!=null&&Time.GetTicksMsec()<until)await WarFrames(1);await Shot("network-connection-error","Connecting","Actual loopback timeout");CloseNetwork();return;
  }
  await Shot("main-no-continue","Launch","Studio intro > Main menu");
  foreach(string panel in new[]{"load","options","controls","playtesting","quit-confirm"}){OpenMissionPanel(panel);await Shot("main-"+panel,"Main menu",panel=="controls"||panel=="playtesting"?"Options > "+panel:panel);missionPanel="";}
  foreach(string campaign in new[]{"taar","human","cw-taar","cw-human"}){
   SelectedCampaign=campaign;RefreshPersistenceCatalog();OpenMissionPanel("campaign");campaignScroll=0;await Shot(campaign+"-selection","Main menu","Campaign > edition > faction");
   if(!IsCouncil){campaignScroll=14;await Shot(campaign+"-last-page","Campaign","More missions / Page Down");campaignScroll=0;}
   ActivateMissionUi("campaign-start:0");await WarFrames();await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);await Shot(campaign+"-brief-revealing","Campaign","First mission > briefing");
   RevealBriefing();await Shot(campaign+"-brief-complete","Briefing","Space / Reveal all");StartFirstStrike();AdvanceInterference(320);
   foreach(string panel in new[]{"menu","objectives","controls","options","playtesting","history","save","load"}){OpenMissionPanel(panel);await Shot(campaign+"-"+panel,"Battle","Esc > "+panel);}
   SaveMissionSlot(0);await Shot(campaign+"-saved","Save","Choose empty slot1");ActivateMissionUi("save-slot:0");await Shot(campaign+"-overwrite","Save","Choose occupied slot1");missionPanel="";
   ActivateMissionUi("restart");await Shot(campaign+"-restart","In-game menu","Restart > confirmation");missionPanel="";
   AskAbandon("main-menu");await Shot(campaign+"-leave","In-game menu","Main menu > confirmation");missionPanel="";
   ReturnMainMenu();await WarFrames();await Shot(campaign+"-continue","Main menu","Saved mission available");OpenMissionPanel("load");await Shot(campaign+"-load-populated","Main menu","Load Game");missionPanel="";
  }
  foreach(string faction in new[]{"cw-human","cw-taar"})foreach(bool win in new[]{false,true}){
   var d=PersistenceData(OriginalCampaign.Catalog(OriginalContent.Root(root,faction),faction)[0].Scenario);var s=SaveCodec.Open(Path.Combine(root,"generated/council-wars-first/checks/"+faction+(win?"-success.json":"-failure.json")))["payload"]!.AsObject();InstallMission(d,OriginalMission.Restore(d,s),false);await WarFrames();ReleaseMissionVideo();BeginRewards();await Shot(faction+(win?"-victory":"-defeat"),"Battle","Controlled outcome fixture; movie skipped",true);ReturnMainMenu();
  }
  HumanResult(1,true);await WarFrames(10);ReleaseMissionVideo();BeginRewards();await Shot("base-victory","Battle","Controlled Human success",true);ReturnMainMenu();HumanResult(15,true);await WarFrames(10);ReleaseMissionVideo();BeginRewards();await Shot("human-campaign-completion","Ending","Controlled final Human outcome; movie skipped",true);ReturnMainMenu();
  var td=PersistenceData("SCENARIO/ALIEN/ALIEN15.SCN");var ts=SaveCodec.Open(Path.Combine(root,"generated/final-evacuation/mission-final/success.json"))["payload"]!.AsObject();InstallMission(td,OriginalMission.Restore(td,ts),false);await WarFrames(10);ReleaseMissionVideo();BeginRewards();await ToSignal(GetTree().CreateTimer(2.5),SceneTreeTimer.SignalName.Timeout);await Shot("taar-campaign-completion","Ending","Controlled final Taar outcome; movie skipped",true);ReturnMainMenu();
  OpenWarSetup();await WarFrames(60);await Shot("war-setup","Main menu","Single-player War");
  foreach(var pair in new[]{("controller",warRoles[1]),("faction",warRaces[0]),("colour",warColors[0]),("team",warTeams[0]),("capacity",warCapacity),("rank",warRank!)}){pair.Item2.ShowPopup();await Shot("war-dropdown-"+pair.Item1,"War setup","Open "+pair.Item1);pair.Item2.GetPopup().Hide();}
  warSearch.Text="no-matching-map";FilterWarMaps();await Shot("war-empty-search","War setup","Search without matches");warSearch.Text="";FilterWarMaps();
  SelectWarMap(warMaps.Single(m=>m.Id=="D8PLAY10"));for(int i=1;i<8;i++)warSetup.Slots[i]=warSetup.Slots[i] with{Controller=WarController.Ai};BindWarPlayers();ValidateWarSetup();await WarFrames(60);await Shot("war-eight","War setup","Eight-slot map; AI opponents");
  warSetup.Slots[1]=warSetup.Slots[1] with{Color=warSetup.Slots[0].Color};BindWarPlayers();ValidateWarSetup();await Shot("war-colour-conflict","War setup","Duplicate colour prevents start");
  StartWarMatch(WarConfiguration.Default("A2PLAY01",7));AdvanceInterference(32);foreach(string panel in new[]{"menu","objectives","diplomacy"}){OpenMissionPanel(panel);await Shot("war-"+panel,"War battle","Esc > "+panel);}
  ActivateMissionUi("war-surrender");await Shot("war-surrender-confirm","War menu","Surrender");ActivateMissionUi("abandon-confirm");await WarFrames();await Shot("war-defeat","War battle","Confirmed surrender");ReturnMainMenu();
  OpenMultiplayer();await Shot("multiplayer-host-join","Main menu","Multiplayer");CloseNetwork();
  foreach(var config in new[]{(0,1f),(16,1f),(16,1.5f)}){maximumAspect=config.Item1;ui=config.Item2;ApplyAspectLimit();await WarFrames();string tag=$"layout-{maximumAspect}-{ui.ToString(System.Globalization.CultureInfo.InvariantCulture)}";await Shot(tag+"-main","Main menu","Display variant");OpenMissionPanel("campaign");await Shot(tag+"-campaign","Main menu","Campaign display variant");OpenMissionPanel("options");await Shot(tag+"-options","Main menu","Options display variant");missionPanel="";OpenWarSetup();await WarFrames(30);await Shot(tag+"-war","Main menu","War display variant");HideWarSetup();OpenMultiplayer();await Shot(tag+"-multiplayer","Main menu","Multiplayer display variant");CloseNetwork();}
  GD.Print("MENU_MAP_PASS");
 }
}

