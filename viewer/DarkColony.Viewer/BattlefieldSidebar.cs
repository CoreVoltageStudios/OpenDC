using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 void DrawBattlefieldSidebar(){
  missionHits.Clear();var s=UiSize;float x=s.X-336;
  DrawRect(Px(new(x,0,336,s.Y)),new Color("101110"));UiMetal(new(x-8,0,6,s.Y),false,true);
  bool compact=s.Y<980;var selected=NativeSelected().ToArray();
  if(!compact)UiLabel(IsCouncil?"COUNCIL WARS":"DARK COLONY",x+20,20,12,uiDim);
  float titleY=compact?26:45;int titleSize=22;
  while(titleSize>17&&UiFont.GetStringSize(campaignMission.Title.ToUpperInvariant(),fontSize:titleSize).X>254)titleSize--;
  UiLabel(campaignMission.Title.ToUpperInvariant(),x+20,titleY,titleSize);UiLabel(campaignMission.Region.ToUpperInvariant(),x+20,titleY+21,13,uiGreen);
  var dial=new Vector2(x+302,compact?23:28)*ui;DrawCircle(dial,14*ui,new Color("55585b"));DrawCircle(dial,10*ui,new Color("191b19"));
  float angle=(nativeWorld.Phase+Math.Clamp(nativeWorld.DayCounter/(float)Math.Max(1,nativeData.Scenario.Header.Period+1),0,1))*MathF.PI-MathF.PI/2;DrawLine(dial,dial+new Vector2(MathF.Cos(angle),MathF.Sin(angle))*8*ui,uiText,2*ui);
  UiLabel(nativeWorld.Phase==1?"NIGHT":"DAY",x+284,compact?53:63,12,uiDim);DrawMissionMinimap();
  float y=RelayMapTop+SidebarMapHeight+8;
  if(!compact){UiLabel("Your units    Selected    View",x+26,y+13,12,uiDim);y+=22;}
  float summary=compact?(s.Y<800&&VisibleUnitControls().Count()>4?76:86):124;UiFrame(new(x+20,y,296,summary));
  if(selected.Length==0){UiLabel("No units selected",x+30,y+27,19,uiDim);UiWrap("Click a unit or drag a box on the battlefield.",x+30,y+51,270,14,uiDim,2);}
  else {
   bool single=selected.Length==1;UiLabel(single?UnitTitle(selected[0]):$"{selected.Length} units selected",x+30,y+26,19);
   int hp=selected.Sum(e=>e.Health),max=selected.Sum(e=>nativeData.Scenario.Types[e.Type].MaximumHealth);
   float ratio=Math.Clamp(hp/(float)Math.Max(1,max),0,1);DrawRect(Px(new(x+30,y+36,276,7)),new Color("262826"));DrawRect(Px(new(x+30,y+36,276*ratio,7)),ratio>.6f?new Color("9cc63c"):new Color("f2b33d"));
   for(int i=16;i<276;i+=16)DrawLine(new Vector2(x+30+i,y+36)*ui,new Vector2(x+30+i,y+43)*ui,Colors.Black,ui);
   UiLabel($"Health  {hp:N0} / {max:N0}",x+30,y+(summary<80?57:61),summary<80?12:14,uiDim);
   var commander=selected.FirstOrDefault(e=>nativeWorld.IsCommander(e));
   UiLabel(commander!=null?$"Inspire {commander.Energy}/255 · "+(commander.Energy>=32?"Ready":"Recharging"):single?(selected[0].Type==12?$"Disease energy {selected[0].Energy}/255":selected[0].Type is 49 or 50?$"Healing energy {selected[0].Energy}/255":null)??HarvesterStatus(selected[0])??(selected[0].Inspired>0?"Inspired":selected[0].State==3?"Moving":selected[0].Target>=0?"Engaging":"Ready"):"Orders apply to eligible units.",x+30,y+(summary<80?72:79),summary<80?12:14,commander!=null&&commander.Energy>=32?uiGreen:uiDim);
   if(!compact)UiLabel("Tab: commander · Ctrl+Tab: focus controls",x+30,y+106,12,uiDim);
  }
  y+=summary+6;y=DrawUnitCommands(x,y,compact)+4;
  float messageHeight=compact?24:44;UiFrame(new(x+20,y,296,messageHeight));
  string status=targetMode.Length>0?"Choose ground or a visible target. Esc cancels.":economicMission?developmentNotice:selected.Length==0?"Select units on the battlefield.":"Orders ready · Ctrl+Tab: focus controls";
  UiWrap(compact&&UiLines(status,280,12).Count>1?"Orders and status · hover for details":status,x+28,y+17,280,12,uiDim,compact?1:2);missionHits.Add(new("status",Px(new(x+20,y,296,messageHeight)),status,false));y+=messageHeight+6;
  if(economicMission||interferenceDev)DrawSidebarEconomy(x,y,s.Y-120);else DrawRelayGroup(x,y,s.Y-120);
  float objectiveY=s.Y-104;
  UiLabel("MISSION",x+20,objectiveY,14,uiDim);
  UiLabel(IsTraining?TrainingObjective:IsWar?"Eliminate opposing forces":IsCouncil?CouncilObjective:IsHuman?HumanObjective:campaignMission?.Index==14?"Destroy all three human bases":campaignMission?.Index==13?"Reach the chamber; protect commander":campaignMission?.Index is 11 or 12?"Destroy both human bases":campaignMission?.Index==10?"Steal Petra-7; destroy human base":campaignMission?.Index==9?"Build hive; destroy the human base":campaignMission?.Index==8?"Find the chamber; keep commander alive":campaignMission?.Index==7?"Pursue Tick; protect your commander":campaignMission?.Index==6?"Destroy human bases; protect Mind Hive":campaignMission?.Index==5?"Destroy the Citadel; protect your hive":campaignMission?.Index==4?"Find and extract Commander Tick":campaignMission?.Index==3?"Steal energy; destroy human base":campaignMission?.Index==2?"Protect discovery; destroy outpost":economicMission||interferenceDev?"Destroy communication relays":"Destroy atmosphere contaminators",x+20,objectiveY+24,15);
  UiLabel(IsTraining?TrainingStatus:IsWar?WarStatus:IsCouncil?CouncilStatus:IsHuman?HumanObjectiveStatus:campaignMission?.Index==14?(nativeWorld.Entities.Any(e=>e.Living&&e.Owner==LocalTeam&&e.Type==105)?"Portalis active; protect your hive":mission.Blocks[18]!.Remaining>0?"Hold for the Portalis arrival":"Portalis lost; protect your hive"):campaignMission?.Index==13?"Explore the underground passages":campaignMission?.Index is 11 or 12?"Protect and develop your hive":campaignMission?.Index==10?(mission.Blocks[13]!.Remaining==0?"Hive income restored":"Rescue Gorrems; spot human miners"):campaignMission?.Index==9?(nativeWorld.Teams[LocalTeam].Cities[0].Health>0?"Hive established":"Establish the new hive promptly"):campaignMission?.Index==8?(mission.Blocks[3]!.Remaining>0?"Scan complete; reach the exit":mission.Blocks[2]!.Remaining==0?"Scanning ancient knowledge":"Explore and recover the lost teams"):campaignMission?.Index==7?(mission.Blocks[9]!.Remaining==0?"Develop hive; destroy human base":nativeWorld.Teams[LocalTeam].Allies[6]!=0?"Follow Tick through the valley":"Stop Tick beyond the minefield"):campaignMission?.Index==6?(nativeWorld.Teams[LocalTeam].Allies[1]!=0?"Stratus alliance active":"Stratus alliance ended"):campaignMission?.Index==5?(nativeWorld.CanDiscoverArtifacts(LocalTeam)?"Neural Hive · artifact detection active":"Develop your hive and research"):campaignMission?.Index==4?(nativeWorld.Session.ScriptValues[0,1]>0?"Extraction underway":mission.Blocks[17]!.Remaining==0?"Tick located; reach the rendezvous":"Infiltrate Area 51"):campaignMission?.Index==3?(mission.Blocks[16]!.Remaining==0?"Energy depot secured":"Infiltrate the energy depot"):campaignMission?.Index==2?(mission.Blocks[8]!.Remaining==0?"Cryo-chamber discovered":"Locate Ancona's secret"):economicMission||interferenceDev?$"{nativeWorld.Deaths[1,86]} / 6 destroyed":$"{nativeWorld.Deaths[1,82]} / 11 destroyed",x+20,objectiveY+44,14,uiGreen);
  if(interferenceDev){UiButton("dev-pause",paused?"Resume":"Pause",new(x+20,s.Y-49,144,38),"Diagnostic M2 pause",active:paused);UiButton("dev-restart","Restart R",new(x+172,s.Y-49,144,38),"Restart M2 inspection");}
  else {
  UiButton("objectives","Objectives   J",new(x+20,s.Y-49,144,38),"Read your orders.");UiButton("menu","Menu   Esc",new(x+172,s.Y-49,144,38),IsNetworkMatch?"Local menu; network battle continues.":"Pause the battle. Music continues.");
  }
  UiFrame(new(0,s.Y-64,x,64));UiButton("history","≡",new(8,s.Y-54,38,44),"Mission message history.");
  string message=interferenceDev?"INCOMPLETE M2: "+developmentNotice:mission.CurrentMessage is {} m?InstructionText(m.TextId).Replace('\n',' '):IsHuman?"TRANSMISSION LINK ESTABLISHED":"MIND LINK ESTABLISHED";
  UiWrap(message,62,s.Y-39,x-80,17,uiGreen,2);
  if(missionMusicDiagnostic!=null)UiLabel(missionMusicDiagnostic,20,28,16,new Color("f1bb65"));
  if(developerUi){UiFrame(new(14,12,520,100));UiLabel($"DEVELOPER · tick {nativeWorld.Tick} · seed {nativeSeed}",26,39,16);UiLabel($"F3 sight: {nativeOmniscient} · V ranges: {nativeRange}",26,66,15,uiDim);UiLabel($"F6 artwork: {nativeSampling} / F7 contour fog: {nativeFineFog}",26,94,15,uiDim);}
  if(!MissionModal){DrawMissionCursor();var hover=(relayPointerHelp?null:missionHits.LastOrDefault(h=>h.Id==focusedUi))??missionHits.LastOrDefault(h=>h.Rect.HasPoint(missionPointer));string help=hover?.Help??(ResourceAt(missionPointer) is {} vent?VentHelp(vent):UiHover(missionPointer) is {} e?$"{UnitTitle(e)} · {(e.Owner==LocalTeam?"Friendly":e.Owner<8&&nativeWorld.Relations[LocalTeam,e.Owner]?"Allied":"Enemy")} · {e.Health}/{nativeData.Scenario.Types[e.Type].MaximumHealth}":targetMode.Length>0?$"{(targetMode=="waypoints"?"WAYPOINTS: click last again to start":targetMode=="disease"?"DISEASE":targetMode=="move"?"MOVE ONLY":"MOVE & ATTACK")} — choose a destination or target. Esc cancels.":"");
   if(help.Length>0)ShowRelayTooltip(help,hover?.Rect??new Rect2(missionPointer,Vector2.One));}

  if(missionPanel.Length>0)DrawMissionPanel();
 }
}
