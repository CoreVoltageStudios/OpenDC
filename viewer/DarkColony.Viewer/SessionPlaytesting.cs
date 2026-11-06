using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool godMode;
 readonly HashSet<string> godModeRuns=new();
 Label? godModeIndicator;
 void ApplySessionPlaytesting(){
  if(nativeWorld==null||mission==null)return;
  nativeWorld.GodMode=godMode&&!IsNetworkMatch;
  if(godMode&&!IsNetworkMatch)godModeRuns.Add(mission.RunId);
 }
 void UpdateGodModeIndicator(){
  if(!ready)return;
  if(godModeIndicator==null&&godMode){
   godModeIndicator=new Label{Text="GOD MODE",MouseFilter=Control.MouseFilterEnum.Stop,ZIndex=4096};
   godModeIndicator.AddThemeColorOverride("font_color",new Color("bdd747"));
   godModeIndicator.AddThemeFontSizeOverride("font_size",16);
   godModeIndicator.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=new Color("0b1010"),ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=3,ContentMarginBottom=3});
   AddChild(godModeIndicator);
  }
  if(godModeIndicator!=null){godModeIndicator.Visible=godMode;godModeIndicator.Position=new((GetViewportRect().Size.X-godModeIndicator.Size.X)/2,8);}
 }
 static Vector2 KeyboardPan(Vector2 direction,double delta,float zoom)=>direction.LimitLength()*600*(float)Math.Max(delta,0)/zoom;
 void ProcessKeyboardCamera(double delta){
  if(!GetWindow().HasFocus()||MissionModal||focusedUi.Length>0||dragging||minimapDragging||uiPointerOwned||GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)return;
  var direction=new Vector2((Input.IsKeyPressed(Key.Right)?1:0)-(Input.IsKeyPressed(Key.Left)?1:0),(Input.IsKeyPressed(Key.Down)?1:0)-(Input.IsKeyPressed(Key.Up)?1:0));
  camera+=KeyboardPan(direction,delta,zoom);ClampMissionCamera();
 }
 void DrawPlaytestingOptions(){
  missionHits.Clear();DrawRect(OverlayRect,new Color(0,0,0,.65f));
  var r=new Rect2((OverlayWidth-460)/2,(UiSize.Y-300)/2,460,300);UiFrame(r);float x=r.Position.X+24,y=r.Position.Y+36;
  UiLabel("PLAYTESTING",x,y,24,uiGreen);
  UiButton("god-mode","God mode",new(x,y+24,412,44),"Protect your units and buildings and access all implemented missions for this session.",enabled:!IsNetworkMatch,active:godMode&&!IsNetworkMatch);
  UiLabel(godMode?"ON":"OFF",x+350,y+53,16,godMode?uiGreen:uiDim);
  UiWrap(IsNetworkMatch?"God mode is unavailable during multiplayer.":"Session only. Protects your forces and opens all implemented missions. Completions from these missions do not write campaign progress.",x,y+102,412,17,uiDim,4);
  UiButton("close","Back   Esc",new(x,r.End.Y-64,412,40),"Return to Options");
 }
 void DrawCompactMissionMenu(){
  missionHits.Clear();DrawRect(OverlayRect,new Color(0,0,0,.6f));
  var r=new Rect2((OverlayWidth-440)/2,(UiSize.Y-64-480)/2,440,480);UiFrame(r);float x=r.Position.X+24,y=r.Position.Y+32;
  UiLabel(BattleStatus.ToUpperInvariant(),x,y,15,OdcUiTokens.Signal);UiWrap(campaignMission.Title,x,y+28,392,22,uiText,2);
  UiLabel(missionBriefing?"Briefing":mission.AtOutcome?"Mission complete":"",x,y+76,14,uiDim);
  y+=94;UiButton("close","Return to battle",new(x,y,392,46),"Close this menu.",active:true);UiLabel("Esc",x+350,y+29,14,uiDim);y+=62;
  foreach(var row in new[]{new[]{("objectives","Objectives","J"),(IsWar?"diplomacy":"controls",IsWar?"Diplomacy":"Controls","")},new[]{("save","Save game",""),("load","Load game","")}}){
   for(int i=0;i<2;i++){var b=row[i];UiButton(b.Item1,b.Item2,new(x+i*202,y,190,40),IsNetworkMatch&&b.Item1 is "save" or "load"?"Save and load are unavailable in this multiplayer milestone.":b.Item2,(!(IsNetworkMatch&&b.Item1 is "save" or "load")&&(b.Item1!="save"||CanSaveMission)));if(b.Item3.Length>0)UiLabel(b.Item3,x+i*202+165,y+26,14,uiDim);}y+=48;
  }
  UiButton("options","Options",new(x,y,392,40),"Display, audio, controls and playtesting");UiLabel("O",x+365,y+26,14,uiDim);y+=64;
  DrawLine(new Vector2(x,y-12)*ui,new Vector2(x+392,y-12)*ui,new Color("303534"),ui);
  if(IsNetworkMatch){UiButton("network-pause",network!.Match!.Paused?"Resume battle":"Pause battle (host)",new(x,y,392,38),"Only the host may pause or resume; menus do not pause.",network.Host);y+=46;UiButton("war-surrender","Surrender",new(x,y,190,38),"Forfeit control; AI continues with your remaining forces");UiButton("network-leave","Leave match",new(x+202,y,190,38),"Leave; the host ends the session, clients transfer to AI");return;}
  UiButton("restart","Restart mission",new(x,y,392,38),"Restart after confirmation");UiLabel("R",x+365,y+25,14,uiDim);y+=46;
  UiButton(IsWar?"war-surrender":"main-menu",IsWar?"Surrender":"Main menu",new(x,y,190,38),"Leave this mission");UiButton("quit","Quit",new(x+202,y,190,38),"Exit to desktop");
 }
}
