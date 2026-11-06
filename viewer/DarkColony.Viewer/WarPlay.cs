using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 string WarStatus=>mission.War!.Eliminated[LocalTeam]?"Defeated · observing the battle":$"{Enumerable.Range(0,8).Count(t=>mission.War.Slot(t)!=null&&!mission.War.Eliminated[t])} players remain";
 void StartWarMatch(WarConfiguration setup){
  // Construct and validate everything before replacing the current session.
  var next=OriginalMission.StartWar(Path.Combine(root,"private/DC"),setup);
  warSetup=setup.Copy();InstallMission(next.World.Data,next,false);firstStrike=true;
  GD.Print($"WAR_READY map={setup.MapId} seed={setup.Seed} local={LocalTeam}");
 }
 bool WarAction(string id){
  if(id=="war-surrender"){AskAbandon("war-surrender");return true;}
  if(!IsWar)return false;
  if(id=="diplomacy"){OpenMissionPanel("diplomacy");return true;}
  if(id.StartsWith("war-alliance:")||id.StartsWith("war-vision:")){
   int other=int.Parse(id[(id.IndexOf(':')+1)..]);
   bool vision=id.StartsWith("war-vision:");bool current=vision?(mission.War!.VisionOffers[LocalTeam]&(1<<other))!=0:nativeWorld.Teams[LocalTeam].Allies[other]!=0;
   bool enabled=!pendingDiplomacy.GetValueOrDefault((other,vision),current);
   if(IsNetworkMatch)network!.Submit(new(vision?"vision":"alliance",LocalTeam,Value:other,Enabled:enabled));
   else if(SinglePlayerPaused)pendingDiplomacy[(other,vision)]=enabled;
   else mission.OfferWarRelationship(LocalTeam,other,vision,enabled);
   return true;
  }
  return false;
 }
 void BeginWarResults(){
  rewardsVisible=true;rewardMilliseconds=0;missionPanel="";resultContinueAvailable=false;resultFinal=false;
  focusedUi="war-setup";pressedUi="";uiPointerOwned=false;resultAcceptAfter=Time.GetTicksMsec()+300;
  targetMode="";nativeAttackMove=false;missionMusic?.Stop();missionMixer.StopAll();ClearMissionAudioBuffer();
 }
 void DrawWarObjectives(float x,float y){
  UiWrap("Eliminate the opposing forces.",x,y,620,26,uiGreen);
  UiWrap("The battle ends when all surviving players are allied with the first surviving player. Players are eliminated when they have no qualifying forces left; mines, deployed defensive turrets and the sixth city module alone do not keep a player in the match.",x,y+66,620,18,uiText);
  UiWrap(economicMission?"Develop your base and secure Petra-7. Artifact and vent activity follows the selected map and match options.":"This is a combat arena. Use your starting army; there are no bases or production.",x,y+225,620,18,uiText);
  UiWrap(IsNetworkMatch?"Diplomacy requires mutual offers. Surrender forfeits control; AI takes over. Leaving as host ends the session. Network save, load, restart and reconnect are unavailable.":"Diplomacy: alliance and shared sight each require mutual offers. Surrender ends your match. Restart repeats the same setup and starting positions.",x,y+316,620,17,uiDim);
  UiLabel(WarStatus,x,y+420,19,uiGreen);
 }
 void DrawWarDiplomacy(){
  missionHits.Clear();DrawRect(OverlayRect,new Color(0,0,0,.68f));
  int opponents=Enumerable.Range(0,8).Count(t=>t!=LocalTeam&&mission.War!.Slot(t)!=null);
  float width=Math.Min(900,OverlayWidth-80),height=Math.Min(210+opponents*62,UiSize.Y-40);var r=new Rect2((OverlayWidth-width)/2,(UiSize.Y-height)/2,width,height);UiFrame(r);float x=r.Position.X+24,y=r.Position.Y+36;
  UiLabel("DIPLOMACY",x,y,24,uiGreen);UiWrap("Alliance and shared sight require offers from both sides. "+BattleStatus+(IsNetworkMatch?".":". Changes apply on return to battle."),x,y+30,width-48,16,uiDim,2);y+=80;
  float row=Math.Min(62,(height-180)/Math.Max(1,opponents));int index=0;
  for(int t=0;t<8;t++){if(t==LocalTeam||mission.War!.Slot(t) is not {} slot)continue;float top=y+index++*row;bool alive=!mission.War.Eliminated[t]&&!mission.War.Eliminated[LocalTeam];
   UiLabel(slot.Name+(mission.War.Eliminated[t]?" · eliminated":""),x,top+19,17);
   bool allied=pendingDiplomacy.GetValueOrDefault((t,false),nativeWorld.Teams[LocalTeam].Allies[t]!=0),vision=pendingDiplomacy.GetValueOrDefault((t,true),(mission.War.VisionOffers[LocalTeam]&(1<<t))!=0);
   UiLabel(nativeWorld.Teams[t].Allies[LocalTeam]!=0?"Alliance offered to you":"No alliance offer",x,top+38,12,uiDim);
   float buttons=x+width-370;UiButton("war-alliance:"+t,"Offer alliance",new(buttons,top,146,38),"Your alliance offer. Both players must offer to become allies.",alive,allied);
   UiButton("war-vision:"+t,"Share sight",new(buttons+154,top,134,38),"Your sight offer. Both players must offer before sight is shared.",alive,vision);

  }
  UiButton("close","Return to battle   Esc",new(x,r.End.Y-64,width-48,40),"Close diplomacy");
 }
 void DrawWarResults(){
  missionHits.Clear();DrawSurfaceBackdrop();int players=mission.War!.Starts.Count(s=>s>=0);float width=Math.Min(980,UiSize.X-120),height=Math.Min(360+players*40,UiSize.Y-64);var r=new Rect2((UiSize.X-width)/2,(UiSize.Y-height)/2,width,height);UiFrame(r,true);float x=r.Position.X+28,y=r.Position.Y+44;
  UiLabel(IsNetworkMatch?"MULTIPLAYER WAR":"SINGLE-PLAYER WAR",x,y,16,uiDim);UiLabel(mission.Outcome!.Success&&!(IsNetworkMatch&&network!.Match!.Surrendered[LocalTeam])?"VICTORY":"DEFEAT",x,y+47,32,uiGreen);
  UiLabel(campaignMission.Title,x,y+83,23);UiLabel(TimeSpan.FromMilliseconds(mission.Milliseconds).ToString(@"hh\:mm\:ss")+(mission.War!.Surrendered?" · Surrendered":""),x,y+112,16,uiDim);
  if(godModeRuns.Contains(mission.RunId))UiLabel("God mode was used in this match",x,y+141,15,uiGreen);
  y+=182;float row=Math.Min(40,(height-320)/Math.Max(1,players));
  UiLabel("PLAYER",x,y-28,13,uiDim);UiLabel("PETRA-7",x+width*.39f,y-28,13,uiDim);UiLabel("KILLS",x+width*.51f,y-28,13,uiDim);UiLabel("LOSSES",x+width*.61f,y-28,13,uiDim);UiLabel("RESULT",x+width*.73f,y-28,13,uiDim);
  foreach(int t in Enumerable.Range(0,8)){if(mission.War.Slot(t) is not {} s)continue;
   int kills=0,losses=0;for(int type=0;type<nativeWorld.Kills.GetLength(1);type++){kills+=nativeWorld.Kills[t,type];losses+=nativeWorld.Deaths[t,type];}
   string status=(t==LocalTeam&&mission.War.Surrendered||IsNetworkMatch&&network!.Match!.Surrendered[t])?"Surrendered":mission.War.Eliminated[t]?"Eliminated":"Surviving";
   UiLabel(s.Name+(t==LocalTeam?" · You":"")+" · "+(s.Race==0?"Human":"Taar"),x,y,16);UiLabel(nativeWorld.Teams[t].Money.ToString(),x+width*.39f,y,16,uiDim);UiLabel(kills.ToString(),x+width*.51f,y,16,uiDim);UiLabel(losses.ToString(),x+width*.61f,y,16,uiDim);UiLabel(status,x+width*.73f,y,16,status=="Surviving"?uiGreen:uiDim);y+=row;
  }
  float bottom=r.End.Y-68;if(IsNetworkMatch){UiButton("main-menu","Main menu   Esc",new(x,bottom,220,42),"Leave this network session");UiButton("war-setup","Multiplayer",new(r.End.X-252,bottom,224,42),"Leave and configure another session",active:true);return;}UiButton("main-menu","Main menu   Esc",new(x,bottom,220,42),"Return to the main menu");UiButton("restart","Replay match",new(r.End.X-478,bottom,210,42),"Repeat the same map, options, seed and starting assignments");UiButton("war-setup","Return to setup",new(r.End.X-252,bottom,224,42),"Configure another match",active:true);
  if(missionPanel.Length>0)DrawMissionPanel();
 }
}
