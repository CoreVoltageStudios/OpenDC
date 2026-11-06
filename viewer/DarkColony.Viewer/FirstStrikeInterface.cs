using Godot;
using DarkColony.Assets;
using DarkColony.Original;
namespace DarkColony.Viewer;

// Presentation and input adapter only. All orders still enter OriginalWorld.Command.
public partial class Viewer {
 sealed record UiHit(string Id,Rect2 Rect,string Help,bool Enabled=true);
 readonly List<UiHit> missionHits=new();
 readonly Dictionary<string,SpriteAsset> interfaceBanks=new();
 readonly Dictionary<string,ImageTexture> interfaceTextures=new();
 readonly Dictionary<int,Vector2> markerOffsets=new();
 string currentMissionPanel="",pressedUi="",targetMode="",focusedUi="";
 string missionPanel { get=>currentMissionPanel; set {currentMissionPanel=value;OwnLocalPause(LocalPauseReason.Menu,value.Length>0);} }
 bool minimapDragging,uiPointerOwned,developerUi;Vector2 missionPointer;
 float briefingScroll,sfxVolume=1,musicVolume=1;int historyOffset;
 Vector2 orderPoint;NativeAnimation orderAnimation=new(),cursorAnimation=new();double orderClock,cursorClock;
 readonly Dictionary<string,OriginalClipSet> interfaceClips=new();
 ImageTexture? miniTexture;long miniTick=-100;int miniSelector=-1;
 CanvasTexture? miniCanvas;
 readonly Dictionary<int,Color> miniTeamColors=new();
 Font? interfaceFont;
 readonly Dictionary<(string,float,int,bool),List<string>> uiLineCache=new();
 Font UiFont {get {if(landfallDrawing)return LandfallFont;if(interfaceFont==null){interfaceFont=ResourceLoader.Load<FontFile>("res://Fonts/Barlow-SemiBold.ttf");interfaceFont.Fallbacks=new(){ThemeDB.FallbackFont};}return interfaceFont;}}
 readonly Color chrome=new("9caaa8"),uiGreen=new("b9d94e"),uiText=new("e6e8e1"),uiDim=new("a7aba3"),uiBlack=Colors.Black;
 Vector2 UiSize=>GetViewportRect().Size/ui;
 Rect2 MissionBattlefield=>new(0,0,Math.Max(1,GetViewportRect().Size.X-336*ui),Math.Max(1,GetViewportRect().Size.Y-64*ui));
 float RelayMapTop=>UiSize.Y<980?62:84;
 float SidebarMapHeight=>UiSize.Y<800&&VisibleUnitControls().Count()>6?Math.Clamp(UiSize.Y-656,60,94):UiSize.Y<980?94:UiSize.Y>1250?250:172;
 Rect2 MiniMapRect {get {var map=nativeData.Scenario.Map;float h=SidebarMapHeight,w=Math.Min(292,h*map.Width/map.Height);h=w*map.Height/map.Width;return new(new Vector2(UiSize.X-168-w/2,RelayMapTop+(SidebarMapHeight-h)/2)*ui,new Vector2(w,h)*ui);}}
 Rect2 FrontRect=>new(Math.Max(26,(UiSize.X-1240)/2),Math.Max(22,(UiSize.Y-900)/2),Math.Min(1240,UiSize.X-52),Math.Min(900,UiSize.Y-44));
 Rect2 Px(Rect2 r)=>new(r.Position*ui,r.Size*ui);
 SpriteAsset UiBank(string name){if(!interfaceBanks.TryGetValue(name,out var bank))interfaceBanks[name]=bank=Import.Sprite(nativeData.Read($"INTRFACE/{name}.SPR"));return bank;}
 ImageTexture UiArt(string bank,int cell){bank=bank.ToUpperInvariant();string key=$"{bank}:{cell}";if(!interfaceTextures.TryGetValue(key,out var t)){var b=bank is "CURS" or "LEFT"?nativeData.Sprite(bank):UiBank(bank);var c=b.Cells[cell];interfaceTextures[key]=t=OriginalTexture(bank=="CURS"&&c.Coverage==null?Presentation.Indexed(c.Indices,c.Width,c.Height,b.Palette6,true):OriginalRewards.Pixels(b,c));}return t;}
 void UiImage(string bank,int cell,Rect2 box,Color? tint=null){var t=UiArt(bank,cell);var scale=Math.Min(box.Size.X/t.GetWidth(),box.Size.Y/t.GetHeight());var size=t.GetSize()*scale;DrawTextureRect(t,Px(new(box.GetCenter()-size/2,size)),false,tint);}
 ImageTexture UiSkin(string name){string key="skin:"+name;if(!interfaceTextures.TryGetValue(key,out var texture)){using var image=Image.LoadFromFile(Path.Combine(root,"generated/first-strike-ui/skin",name+".png"));interfaceTextures[key]=texture=ImageTexture.CreateFromImage(image);}return texture;}
 void UiFrame(Rect2 r,bool raised=false){
  if(landfallDrawing){LandfallFrame(r);return;}
  DrawRect(Px(r),new Color(raised?"0f100f":"080908"));
  DrawRect(Px(r),new Color(raised?"66696b":"2a2c2a"),false,2*ui);
  if(raised)DrawLine(Px(r).Position,Px(r).Position+new Vector2(r.Size.X*ui,0),new Color("8f9294"),ui);

 }
 void UiLabel(string text,float x,float y,int size=17,Color? color=null){DrawString(UiFont,new Vector2(x,y)*ui,text,HorizontalAlignment.Left,-1,(int)(size*ui),color??(landfallDrawing?OdcUiTokens.Text:uiText));}
 void UiButton(string id,string label,Rect2 r,string help,bool enabled=true,bool active=false,int art=-1,bool compact=false){
  var hit=new UiHit(id,Px(r),help,enabled);missionHits.Add(hit);if(landfallDrawing){LandfallButton(hit,label,r,active,enabled,compact);return;}bool hover=hit.Rect.HasPoint(missionPointer),pressed=pressedUi==id;
  DrawRect(hit.Rect,!enabled?new Color("090b0b"):pressed?new Color("101610"):active?new Color("1a2210"):hover?new Color("242a23"):new Color("111514"));
  DrawRect(hit.Rect,active?uiGreen:hover?chrome:new Color("2a2c2a"),false,2*ui);
  if(focusedUi==id)DrawRect(hit.Rect.Grow(2*ui),uiText,false,2*ui);
  if(active||hover)DrawRect(new(hit.Rect.Position,new Vector2(3*ui,hit.Rect.Size.Y)),uiGreen);
  if(pressed)DrawRect(hit.Rect.Grow(-3*ui),uiGreen,false,ui);
  if(art>=0){UiImage("MAINBUT",art,new(r.Position.X+10,r.Position.Y+6,r.Size.X-20,r.Size.Y-31),enabled?Colors.White:new Color(.35f,.35f,.35f));UiLabel(label,r.Position.X+10,r.End.Y-10,15,enabled?uiText:uiDim);if(active)DrawRect(Px(new(r.End.X-13,r.Position.Y+9,5,5)),uiGreen);}
  else {
   int size=compact?14:mainMenu&&missionPanel.Length==0?21:17;var box=new Rect2(r.Position.X+(compact?43:14),r.Position.Y,r.Size.X-(compact?57:28),r.Size.Y);
   if(!compact&&r.Size.X<=70&&label.Length<=3){float glyph=UiFont.GetStringSize(label,fontSize:size).X;box=new(r.GetCenter().X-glyph/2,r.Position.Y,Math.Max(glyph,r.Size.X-8),r.Size.Y);}
   int split=label.IndexOf("   ",StringComparison.Ordinal);
   if(split>=0){string key=label[(split+3)..];float keyWidth=UiFont.GetStringSize(key,fontSize:12).X;UiTextLine(key,new(box.End.X-keyWidth,box.Position.Y,keyWidth,box.Size.Y),12,uiDim,true);box.Size=new(Math.Max(1,box.Size.X-keyWidth-UiGap),box.Size.Y);label=label[..split];}
   if(id.StartsWith("save-slot:")||id.StartsWith("load-slot:"))box.Size=new(box.Size.X,32);
   UiTextLine(label,box,size,enabled?uiText:uiDim);
  }
 }
 void ResetMissionInterface(){landfallNavigation.Clear();developmentTab=1;developmentNotice="Select a unit, or choose an available product.";Array.Clear(relayPages);developmentPage=0;relayCatalogSource=null;missionPanel="";pressedUi=focusedUi="";targetMode="";minimapDragging=uiPointerOwned=nativeSelecting=dragging=false;briefingScroll=0;historyOffset=0;orderAnimation=new();cursorAnimation=new();cursorClock=orderClock=0;miniTick=-100;Input.MouseMode=Input.MouseModeEnum.Visible;}
 void DisposeMissionInterface(){miniCanvas?.Dispose();miniCanvas=null;miniTexture?.Dispose();miniTexture=null;foreach(var t in interfaceTextures.Values)t.Dispose();interfaceTextures.Clear();interfaceFont?.Dispose();interfaceFont=null;}
 bool MissionModal=>mainMenu|| (interferenceDev?missionFault!=null:missionBriefing||mission.AtOutcome||missionFault!=null||missionPanel.Length>0||missionNext);
 void ClampMissionCamera(){var map=nativeData.Scenario.Map;var half=NativeArea.Size/(2*zoom);camera.X=half.X>=map.Width*16?map.Width*16:Math.Clamp(camera.X,half.X,map.Width*32-half.X);camera.Y=half.Y>=map.Height*16?map.Height*16:Math.Clamp(camera.Y,half.Y,map.Height*32-half.Y);}
 void UpdateMissionInterface(){
  ui=Math.Min(ui,Math.Max(.75f,GetViewportRect().Size.Y/720));
  ClampMissionCamera();nativeSelection.RemoveWhere(id=>nativeWorld.Slots[id.Slot] is not {Living:true} e||e.Owner!=LocalTeam||e.Incarnation!=id.Incarnation);
  if(!NativeSelected().Any()||(targetMode.Length>0&&!UnitEnabled(targetMode))){targetMode="";nativeAttackMove=false;}
  if(!MissionModal){var hovered=UiHover(missionPointer);string name=nativeSelecting?"DRAWBOX":targetMode is "attack" or "disease"||nativeAttackMove||(hovered!=null&&!nativeWorld.Relations[LocalTeam,hovered.Owner])?"ATTACK":hovered!=null?"UNITSELECT":NativeSelected().Any()?"MOVE":"DEFAULT";
   cursorAnimation.Start(UiClip("CURS",name),0);cursorClock+=animationDelta;
   // 424174/422c8c: UI animation uses the 33ms presentation service, independent of body clocks.
   while(cursorClock>.033){cursorClock-=.033;cursorAnimation.Tick(0);}
  }
  orderClock+=animationDelta;while(orderAnimation.Mode!=2&&orderClock>.066){orderClock-=.066;orderAnimation.Tick(0);}
  if(orderAnimation.Mode==2)orderClock=0;
  if(missionVideo is {Visible:true})missionVideo.Size=GetViewportRect().Size;
  bool custom=GetWindow().HasFocus()&&!edgePointerExited&&!MissionModal&&NativeArea.HasPoint(missionPointer)&&!dragging;
  Input.MouseMode=custom?Input.MouseModeEnum.Hidden:Input.MouseModeEnum.Visible;
 }
 NativeEntity? UiHover(Vector2 point){
  if(!NativeArea.HasPoint(point)||MissionModal)return null;
  return nativeWorld.Entities.Where(e=>e.Living&&e.Owner<8&&e.Type!=94&&NativeVisible(e)).Select(e=>new{Entity=e,Rect=UnitHitRect(e)}).Where(x=>x.Rect.HasPoint(point)).OrderBy(x=>x.Rect.Size.X*x.Rect.Size.Y).Select(x=>x.Entity).FirstOrDefault();
 }
 Rect2 UnitHitRect(NativeEntity e){
  var pose=NativeRenderPosition(e);var at=pose.Body*zoom+NativeOffset;var rect=new Rect2(at-new Vector2(10,28)*zoom,new Vector2(20,32)*zoom);
  var f=e.Body.Pose(e.Angle);if(f!=null)foreach(var c in f.Cells.Where(c=>c.UnknownWords[2] is 0 or 1)){var s=nativeData.Sprite(c.Bank).Cells[c.CellId];var p=OriginalShadow.BodyPlacement(c,s);rect=rect.Merge(new(at+new Vector2(p.X,p.Y)*zoom,new Vector2(s.Width,s.Height)*zoom));}
  return rect.Grow(Math.Max(2,4*ui));
 }
 string UnitTitle(NativeEntity e)=>!IsHuman&&!IsCouncil&&(campaignMission?.Index==4&&e.Owner==7||campaignMission?.Index is 5 or 6&&e.Owner==1||campaignMission?.Index==7&&e.Owner==6)&&nativeWorld.IsCommander(e)?"Commander Tick":e.Type switch{106=>"Vato",107=>"Urur",108=>"Council member",112=>"Council palace",117=>"Excavation transmitter",110=>"Snake",111=>"Horn",113=>"Medical prototype",109=>"Science Droid",97=>"Watcher",105=>"Portalis",98=>"Security camera",50=>"Zisp",49=>"Medi-craft",63=>"Solaris",64=>"Maktor",66=>"Kaox",68=>"Esgaard",65=>"Lunatek",67=>"Tektaara",37=>"Artifact site",34=>"Neural Hive",12=>"Gorrem",78=>"Gorrem / siphoning",4=>"S.A.R.G.E.",77=>"S.A.R.G.E. / siphoning",26=>"Flynt",11=>"Atril",13=>"Ortu",3=>"Barrager",5=>"Osprey",33=>"Breeder Pod II",100=>"Petra-7 tank",8=>"Gray Warrior",9=>"Xenowort",42=>"Xenowort turret",31=>"Genetics Sac II",83=>"Cryo-chamber",94=>"Mind probe",0=>"Security Trooper",10=>"Sy Demon",2=>"Reaper",6=>"Exploiter",47=>"Exploiter / harvesting",14=>"Brozaar",48=>"Brozaar / harvesting",28=>"Mind Hive",29=>"War Hive",30=>"Genetics Sac",32=>"Breeder Pod",43=>"Spider",44=>"Slom",45 or 46=>"Deployed mine",41=>"Firestorm turret",86=>"Communication Relay",73=>"Ximal Taar",1=>"Firestorm",16=>"Exo Center",17=>"Barracks",18=>"Robot Factory",19=>"Robot Factory II",20=>"Sci-Pod",21=>"Sci-Pod II",22=>"Research Bay",84=>"Dropship beacon",95=>"Inactive beacon",70=>"Captain",71=>"Major",72=>"Colonel",69=>"Lieutenant",82=>"Atmosphere Contaminator",89=>"Communicant",92=>"Dropship",93=>"Saucer",_=>nativeData.Scenario.Types[e.Type].Sprite};
 Vector2 MarkerOffset(NativeEntity e){
  if(markerOffsets.TryGetValue(e.Type,out var offset))return offset;
  // 43c1f3..43c220: STAND direction0, first frame, FIN part6 supplies the fixed marker anchor.
  var set=nativeData.Clips[e.Type].Stand;var f=set.Asset.Frames[set.Direction(0).First];var p=f.Parts[6];
  if(p.Name is not ("" or "NONAME"))offset=new(p.X,p.Y);
  else {float top=0;foreach(var d in set.Directions)foreach(var c in set.Asset.Frames[d.First].Cells){var s=nativeData.Sprite(c.Bank).Cells[c.CellId];var at=OriginalShadow.BodyPlacement(c,s);top=Math.Min(top,at.Y);}offset=new(0,top);}
  markerOffsets[e.Type]=offset;return offset;
 }
 void DrawMissionMarkings(){
  if(!interferenceDev&&(missionBriefing||mission.AtOutcome))return;var hover=UiHover(missionPointer);float scale=Math.Clamp(zoom,1.3f*ui,2.8f*ui);
  foreach(var e in nativeWorld.Entities.Where(e=>e.Living&&e.Owner<8&&e.Type!=94&&NativeVisible(e))){
   bool selected=nativeSelection.Contains((e.Slot,e.Incarnation));bool command=nativeWorld.IsCommander(e);if(!selected&&e!=hover&&!command)continue;
   var at=NativeRenderPosition(e).Body*zoom+NativeOffset+MarkerOffset(e)*zoom;if(!NativeArea.Grow(-12*ui).HasPoint(at))continue;
   int percent=e.Health*100/nativeData.Scenario.Types[e.Type].MaximumHealth,band=percent>80?0:percent>60?1:percent>40?2:percent>20?3:4;
   // 436964: bits2/4 of GAMESTAT field23 choose wide wedge/rank; inspiration selects star.
   int flags=nativeData.Scenario.Types[e.Type].Fields[23];int cell=(flags&2)!=0?5+band:(flags&4)!=0||e.Inspired!=0?10+band:band;
   if(selected||e==hover){var art=UiArt("CLIENT",cell);DrawTextureRect(art,new(at-new Vector2(art.GetWidth()/2f,art.GetHeight())*scale,art.GetSize()*scale),false);}
   if(command){int rank=(flags&0x70)>>4;var art=UiArt("CLIENT",(nativeData.Scenario.Types[e.Type].Race==0?35:47)+rank);DrawTextureRect(art,new(at-new Vector2(art.GetWidth()/2f,art.GetHeight()+6)*scale,art.GetSize()*scale),false);
    if(selected||e==hover){var energy=UiArt("CLIENT",15+Math.Min(9,e.Energy*9/32));DrawTextureRect(energy,new(at+new Vector2(-6,2)*scale,energy.GetSize()*scale),false);}}
  }
 }
 void DrawMissionMinimap(){
  var map=nativeData.Scenario.Map;var r=MiniMapRect;UiFrame(new(UiSize.X-316,RelayMapTop-2,296,SidebarMapHeight+4));
  int selector=TerrainSelectors.LowSelector(nativeWorld.Coefficient);
  UpdateClearMinimap(selector);
  if(miniComparison==0)DrawTextureRect(miniCanvas!,r,false);else DrawMinimapComparison(r,selector);
  foreach(var e in nativeWorld.Entities.Where(e=>e.Living&&e.Owner<8&&e.Type!=94&&NativeVisible(e))){var p=r.Position+new Vector2(e.X/(map.Width*256f),1-e.Z/(map.Height*256f))*r.Size;int selectorColor=nativeWorld.Teams[e.Owner].Color;if(!miniTeamColors.TryGetValue(selectorColor,out var color)){var rgb=Enumerable.Range(138,6).Select(i=>GameColors.Trace(nativePalette,i,selectorColor,16,ColorBank.Body).Display).MaxBy(c=>c.R+c.G+c.B)!;miniTeamColors[selectorColor]=color=new Color(rgb.R/255f,rgb.G/255f,rgb.B/255f);}float dot=nativeWorld.IsCommander(e)?5:3;DrawRect(new(p-Vector2.One*dot*ui/2,Vector2.One*dot*ui),e.Owner==LocalTeam?uiGreen:color);if(e.Owner==LocalTeam&&nativeSelection.Contains((e.Slot,e.Incarnation)))DrawRect(new(p-Vector2.One*(dot+3)*ui/2,Vector2.One*(dot+3)*ui),new Color("f4ffd0"),false,ui);}
  var extent=new Vector2(map.Width*32,map.Height*32);var top=(camera-NativeArea.Size/(2*zoom))/extent;var wh=NativeArea.Size/zoom/extent;
  var view=new Rect2(r.Position+top*r.Size,wh*r.Size).Intersection(r);DrawRect(view,Colors.White,false,Math.Max(1,ui));
 }
 void MoveMinimap(Vector2 point){var r=MiniMapRect;var p=(point-r.Position)/r.Size;camera=new(Math.Clamp(p.X,0,1)*nativeData.Scenario.Map.Width*32,Math.Clamp(p.Y,0,1)*nativeData.Scenario.Map.Height*32);ClampMissionCamera();}
 void DrawMissionHud()=>DrawBattlefieldSidebar();
 List<string> UiLines(string text,float width,int size){
  var key=(text,width,size,landfallDrawing);if(uiLineCache.TryGetValue(key,out var cached))return cached;
  var lines=new List<string>();foreach(var paragraph in text.Split('\n')){string line="";foreach(string word in paragraph.Split(' ',StringSplitOptions.RemoveEmptyEntries)){
   string next=line.Length==0?word:line+" "+word;if(UiFont.GetStringSize(next,fontSize:size).X>width&&line.Length>0){lines.Add(line);line=word;}else line=next;
   while(line.Length>1&&UiFont.GetStringSize(line,fontSize:size).X>width){int cut=line.Length-1;while(cut>1&&UiFont.GetStringSize(line[..cut],fontSize:size).X>width)cut--;lines.Add(line[..cut]);line=line[cut..];}
  }lines.Add(line);}if(uiLineCache.Count>256)uiLineCache.Clear();uiLineCache[key]=lines;return lines;
 }
 void UiWrap(string text,float x,float y,float width,int size,Color color,int max=100,int skip=0){foreach(var line in UiLines(text,width,size).Skip(skip).Take(max)){UiLabel(line,x,y,size,color);y+=size*1.45f;}}
 OriginalClipSet UiClip(string bank,string name){if(!interfaceClips.TryGetValue(name,out var clip)){var fin=nativeData.Animation(bank);var def=fin.Animations.Single(a=>a.Name==name);interfaceClips[name]=clip=new(name,fin,Enumerable.Repeat(def,32).ToArray());}return clip;}
 void DrawUiAnimation(NativeAnimation animation,Vector2 anchor,float scale){var pose=animation.Pose(0);if(pose==null)return;foreach(var c in pose.Cells){var t=UiArt(c.Bank,c.CellId);var cell=nativeData.Sprite(c.Bank).Cells[c.CellId];var p=OriginalShadow.BodyPlacement(c,cell);DrawTextureRect(t,new(anchor+new Vector2(p.X,p.Y)*scale,t.GetSize()*scale),false);}}
 void DrawMissionCursor(){
  DrawPlayerWaypoints();
  var p=orderPoint*zoom+NativeOffset;if(NativeArea.HasPoint(p))DrawUiAnimation(orderAnimation,p,Math.Clamp(zoom,ui,2.8f*ui));
  if(NativeArea.HasPoint(missionPointer)&&!dragging)DrawUiAnimation(cursorAnimation,missionPointer,ui);
 }
 void UiOrder(OriginalOrder order,Cell? cell=null,int target=-1){bool selected=NativeSelected().Any();NativeCommand(order,cell,target);if(selected&&(cell.HasValue||target>=0)){orderPoint=target>=0?NativePosition(nativeWorld.Slots[target]!):new Vector2(cell!.Value.X*32+16,(nativeData.Scenario.Map.Height-1-cell.Value.Z)*32+16);orderClock=0;orderAnimation=new();orderAnimation.Start(UiClip("LEFT",target>=0?"CONFIRMATACK":"CONFIRMOVE"),1);}targetMode="";nativeAttackMove=false;}
 void OpenMissionPanel(string panel){if(mainMenu&&missionPanel.Length==0)relayReturnFocus=panel=="quit-confirm"?"quit":panel;missionPanel=panel;targetMode="";nativeAttackMove=false;nativeSelecting=dragging=minimapDragging=false;pressedUi=focusedUi="";focusedUi=panel switch{"quit-confirm" or "menu" or "controls" or "objectives" or "history"=>"close","abandon"=>"abandon-cancel","options"=>"ui-",_=>""};}
 void ActivateMissionUi(string id){
  if(desktopReview)GD.Print("WINDOWS_REVIEW_ACTION "+id);
  if(LandfallAction(id))return;
  if(NetworkUiAction(id))return;
  if(id is "options" or "controls" or "playtesting" or "save" or "load" or "objectives" or "diplomacy"&&missionPanel.Length>0&&missionPanel!=id)landfallNavigation.Push((missionPanel,id));
  if(id=="game-content"){ShowContentSetup();return;}
  if(id=="playtesting"){OpenMissionPanel("playtesting");return;}
  if(id=="god-mode"){godMode=!godMode;ApplySessionPlaytesting();QueueRedraw();return;}
  if(id.StartsWith("aspect:")){maximumAspect=int.Parse(id[7..]);SaveSettings(p=>ArtworkPreference.SaveAspect(p,maximumAspect),"Display preference could not be saved.");ApplyAspectLimit();return;}
  if(id is "campaign-up" or "campaign-down"){campaignScroll+=id=="campaign-up"?-campaignRows:campaignRows;focusedUi="";QueueRedraw();return;}
  if(WarAction(id))return;
  if(MainMenuAction(id)||PersistenceAction(id)||(!mainMenu&&ActivateUnitCommand(id)))return;
  if(economicMission&&(id.StartsWith("product:")||id.StartsWith("tab:")||id.StartsWith("products-")||id.StartsWith("dev-"))){ActivateDevelopment(id);return;}
  switch(id){
   case "continue":ContinueCampaign();break;
   case "brief-reveal":RevealBriefing();break;case "begin":StartFirstStrike();break;case "skip":ReleaseMissionVideo();BeginRewards();break;
   case "restart":AskAbandon("restart");break;case "exit":AskAbandon("quit");break;
   case "menu":OpenMissionPanel("menu");break;case "objectives":OpenMissionPanel("objectives");break;case "options":OpenMissionPanel("options");break;case "history":OpenMissionPanel("history");break;case "controls":OpenMissionPanel("controls");break;case "close":CloseRelayPanel();break;

   case "ui-":ui=Math.Max(.75f,ui-.125f);break;case "ui+":ui=Math.Min(1.5f,ui+.125f);break;
   case "music-":musicVolume=Math.Max(0,musicVolume-.1f);break;case "music+":musicVolume=Math.Min(1,musicVolume+.1f);break;
   case "sfx-":sfxVolume=Math.Max(0,sfxVolume-.1f);break;case "sfx+":sfxVolume=Math.Min(1,sfxVolume+.1f);break;
   case "speed-classic":case "speed-fast":fastGame=id=="speed-fast";GD.Print("GAME_SPEED "+(fastGame?"Fast 150%":"Classic 100%"));SaveSettings(p=>ArtworkPreference.SaveFast(p,fastGame),"Game speed could not be saved.");break;
   case "edge-toggle":edgeScrolling=!edgeScrolling;SaveEdgeScrolling();break;
   case "edge-":edgeSpeed=Math.Max(.25f,edgeSpeed-.25f);SaveEdgeScrolling();break;case "edge+":edgeSpeed=Math.Min(2,edgeSpeed+.25f);SaveEdgeScrolling();break;
   case "art-classic":ChooseArtwork(ArtworkStyle.Classic);break;case "art-crisp":ChooseArtwork(ArtworkStyle.Crisp);break;
   case "brief-up":ScrollBriefing(-5);break;case "brief-down":ScrollBriefing(5);break;
   case "history-up":historyOffset=Math.Max(0,historyOffset-1);break;case "history-down":historyOffset=Math.Min(Math.Max(0,mission.Messages.Count-1),historyOffset+1);break;
  }if(id.StartsWith("ui")||id.StartsWith("sfx")||id.StartsWith("music"))SavePresentationPreferences();QueueRedraw();
 }
 bool MissionInterfaceInput(InputEvent input){
  if(input is InputEventMouse outside&&!GetViewportRect().HasPoint(outside.Position)){
   missionPointer=outside.Position;CancelCameraGesture();return true;
  }
  if(input is InputEventMouseMotion motion){missionPointer=motion.Position;if(motion.Relative!=Vector2.Zero){relayPointerHelp=true;if(MissionModal){var hover=missionHits.LastOrDefault(h=>h.Enabled&&h.Rect.HasPoint(motion.Position));if(hover!=null)focusedUi=hover.Id;}}if(minimapDragging){MoveMinimap(motion.Position);return true;}if(MissionModal||uiPointerOwned)return true;return false;}
  if(input is InputEventKey k&&k.Pressed&&!k.Echo){
   relayPointerHelp=false;
   if(rewardsVisible&&missionPanel.Length==0&&!ResultInputReady)return true;
   if(missionBriefing&&missionPanel.Length==0&&k.Keycode==Key.Space){RevealBriefing();return true;}
   if(RelayGridKey(k))return true;
   if(missionPanel=="missions"&&k.Keycode is Key.Pageup or Key.Pagedown){campaignScroll+=k.Keycode==Key.Pageup?-campaignRows:campaignRows;focusedUi="";return true;}
   if(missionPanel=="missions"&&k.Keycode is Key.Up or Key.Down&&focusedUi.StartsWith("campaign-start:")){
    int index=int.Parse(focusedUi[15..]);var list=OriginalCampaign.Catalog(SelectedContentRoot,SelectedCampaign).Where(m=>m.Implemented&&(godMode||m.Index==0||campaignEntries[m.Index]!=null)).Select(m=>m.Index).ToArray();int at=Array.IndexOf(list,index);int next=list[Math.Clamp(at+(k.Keycode==Key.Up?-1:1),0,list.Length-1)];campaignScroll=Math.Clamp(campaignScroll,Math.Max(0,next-campaignRows+1),next);focusedUi="campaign-start:"+next;return true;
   }
   if((k.Keycode is Key.Tab or Key.Up or Key.Down||(rewardsVisible&&k.Keycode is Key.Left or Key.Right))&&(MissionModal||focusedUi.Length>0||k.CtrlPressed)){var enabled=missionHits.Where(h=>h.Enabled||h.Id.StartsWith("product:")).ToArray();if(enabled.Length>0){int index=Array.FindIndex(enabled,h=>h.Id==focusedUi);focusedUi=enabled[(index+(k.ShiftPressed||k.Keycode is Key.Up or Key.Left?-1:1)+enabled.Length)%enabled.Length].Id;}return true;}
   if(k.Keycode is Key.Enter or Key.KpEnter or Key.Space&&focusedUi.Length>0){var hit=missionHits.FirstOrDefault(h=>h.Id==focusedUi&&h.Enabled);if(hit!=null)ActivateMissionUi(hit.Id);return true;}
   if(k.Keycode==Key.F8){developerUi=!developerUi;if(!developerUi){nativeOmniscient=false;nativeRange=false;}return true;}
   if(developerUi&&k.Keycode==Key.F6){SetNativeSampling(nativeSampling==0?1:0);return true;}
   if(developerUi&&k.Keycode==Key.F7){nativeFineFog=!nativeFineFog;ClearNativeFog();return true;}
   if(k.Keycode is Key.F3 or Key.V){if(!developerUi&&!capturing)return true;return false;}
   if(k.Keycode==Key.R&&!mainMenu){if(capturing)ResetFirstStrike();else AskAbandon("restart");return true;}
   if(k.Keycode==Key.Escape){if(missionPanel=="save-error"){ActivateMissionUi("error-close");return true;}if(missionPanel=="overwrite"){ActivateMissionUi("save");return true;}if(focusedUi.Length>0&&!MissionModal){focusedUi="";return true;}if(missionVideo is {Visible:true})ActivateMissionUi("skip");else if(missionPanel.Length>0)CloseRelayPanel();else if(nativeSelecting||targetMode.Length>0||nativeAttackMove){nativeSelecting=false;targetMode="";nativeAttackMove=false;}else if(mainMenu){}else if(missionBriefing||mission.AtOutcome||missionFault!=null)AskAbandon("main-menu");else OpenMissionPanel("menu");return true;}
   if(k.Keycode is Key.Enter or Key.KpEnter){if(mainMenu||missionPanel.Length>0)return true;if(missionVideo is {Visible:true})ActivateMissionUi("skip");else if(missionBriefing)StartFirstStrike();else if(mission.AtOutcome&&rewardsVisible)ActivateMissionUi(focusedUi.Length>0?focusedUi:ResultDefault);return true;}
   if(missionBriefing){if(k.Keycode==Key.Pageup)ScrollBriefing(-12);if(k.Keycode==Key.Pagedown)ScrollBriefing(12);return true;}
   if(k.Keycode is Key.Bracketleft or Key.Bracketright){ui=Math.Clamp(ui+(k.Keycode==Key.Bracketright?.125f:-.125f),.75f,1.5f);SavePresentationPreferences();return true;}
   if(MissionModal||focusedUi.Length>0)return true;
   if(k.Keycode==Key.J){OpenMissionPanel("objectives");return true;}if(k.Keycode==Key.O){OpenMissionPanel("options");return true;}
   if(UnitCommandKey(k.Keycode))return true;
  }
  if(input is InputEventMouseButton b){missionPointer=b.Position;
   if(rewardsVisible&&missionPanel.Length==0&&!ResultInputReady)return true;
   if(!MissionModal&&b.Pressed&&b.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown&&relayGrid.HasPoint(b.Position)){developmentPage=Math.Max(0,developmentPage+(b.ButtonIndex==MouseButton.WheelDown?1:-1));return true;}
   if(b.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp&&b.Pressed&&MissionModal){int d=b.ButtonIndex==MouseButton.WheelDown?3:-3;if(rewardsVisible)resultScroll+=d;if(missionBriefing)ScrollBriefing(d);if(missionPanel=="missions"){campaignScroll+=d;focusedUi="";}if(missionPanel=="campaign")campaignOverviewScroll+=Math.Sign(d);if(missionPanel=="options")optionScroll+=d;if(missionPanel=="history")historyOffset=Math.Clamp(historyOffset+d,0,Math.Max(0,mission.Messages.Count-1));return true;}
   if(b.ButtonIndex==MouseButton.Left&&!b.Pressed&&(uiPointerOwned||minimapDragging)){if(minimapDragging)MoveMinimap(b.Position);var hit=missionHits.LastOrDefault(h=>h.Rect.HasPoint(b.Position));string action=pressedUi;pressedUi="";uiPointerOwned=false;minimapDragging=false;if(hit is {Enabled:true}&&hit.Id==action){if(rewardsVisible)focusedUi=action;ActivateMissionUi(action);}return true;}
   if(b.Pressed&&b.ButtonIndex==MouseButton.Left){focusedUi="";var hit=missionHits.LastOrDefault(h=>h.Rect.HasPoint(b.Position));if(hit!=null){uiPointerOwned=true;pressedUi=hit.Enabled?hit.Id:"";return true;}if(!MissionModal&&MiniMapRect.HasPoint(b.Position)){minimapDragging=true;MoveMinimap(b.Position);return true;}}
   if(MissionModal)return true;
   if(b.ButtonIndex==MouseButton.Right&&b.Pressed&&(targetMode.Length>0||nativeAttackMove)){targetMode="";nativeAttackMove=false;nativeSelecting=false;return true;}
   if(!NativeArea.HasPoint(b.Position)){if(b.Pressed&&b.ButtonIndex==MouseButton.Left)uiPointerOwned=true;if(!b.Pressed&&b.ButtonIndex==MouseButton.Left)nativeSelecting=false;if(!b.Pressed&&b.ButtonIndex==MouseButton.Middle)dragging=false;return true;}
   if(UnitTargetInput(b))return true;
   if(b.Pressed&&b.ButtonIndex==MouseButton.Right){var cell=NativeCell((b.Position-NativeOffset)/zoom);if(nativeWorld.Navigation.Inside(cell)){var e=UiHover(b.Position);if(e!=null&&!nativeWorld.Relations[LocalTeam,e.Owner])UiOrder(OriginalOrder.Attack,target:e.Slot);else UiOrder(OriginalOrder.MoveOnly,ResourceAt(b.Position)?.Cell??cell);}return true;}
  }
  return MissionModal;
 }
 void DrawMissionPanel(){bool prior=landfallDrawing;landfallDrawing=true;try{DrawLandfallMissionPanel();}finally{landfallDrawing=prior;}}
 void DrawLandfallMissionPanel(){
  if(missionPanel=="options"){DrawLandfallOptions();return;}
  if(IsWar&&missionPanel=="diplomacy"){DrawWarDiplomacy();return;}
  if(mainMenu&&missionPanel is "campaign" or "load" or "options"){DrawSurfaceSubmenu();return;}
  if(DrawSurfaceDialog())return;
  if(missionPanel=="menu"){DrawCompactMissionMenu();return;}
  if(missionPanel=="playtesting"){DrawPlaytestingOptions();return;}
  missionHits.Clear();DrawRect(OverlayRect,new Color(0,0,0,.72f));float panelHeight=missionPanel=="options"?670:620;var r=new Rect2(mainMenu?120:(OverlayWidth-700)/2,Math.Max(12,(UiSize.Y-panelHeight)/2),700,panelHeight);UiFrame(r,true);float x=r.Position.X+30,y=r.Position.Y+43;
  UiLabel(missionPanel.ToUpperInvariant(),x,y,26,uiGreen);UiLabel(mainMenu?OriginalCampaign.Label(SelectedCampaign)+" campaign":missionBriefing?"Mission briefing":mission.AtOutcome?"Mission result":BattleStatus,x,y+32,16,uiDim);y+=82;
  if(missionPanel is "save" or "load" or "overwrite" or "campaign"){DrawPersistencePanel(x,y,r);
  }else if(missionPanel=="quit-confirm"){UiWrap("Quit OpenDC?",x,y,620,24,uiText);UiButton("quit-confirmed","Quit",new(x,y+100,300,48),"Exit the game");UiButton("close","Cancel",new(x+320,y+100,300,48),"Return to menu");}
  else if(missionPanel=="abandon"){
   UiWrap(campaignWritePending?"Campaign progress could not be written. Leaving now loses this result. Cancel and retry from the outcome screen.":"Leave this session? Progress since your last save will be lost. Existing saves and campaign results are kept.",x,y,620,22,uiText,4);
   UiButton("abandon-confirm",abandonAction=="quit"?"Quit without saving":abandonAction=="restart"?"Restart without saving":"Return to main menu",new(x,y+155,310,48),"Discard unsaved play");UiButton("abandon-cancel","Keep playing",new(x+330,y+155,310,48),"Keep this mission");
  }else if(missionPanel=="options"){
   DrawSurfaceOptions(x,y);
  }else if(missionPanel=="objectives"){
   if(IsTraining){DrawTrainingObjectives(x,y);}else if(IsWar){DrawWarObjectives(x,y);}else if(IsCouncil){DrawCouncilObjectives(x,y);}else if(IsHuman){DrawHumanObjectives(x,y);}else if(campaignMission.Index==14){UiWrap("Destroy all three human bases. Protect your hive.",x,y,620,26,uiGreen);UiWrap("The Portalis is being brought to Destiny One. Use it with your forces to defeat the human armies. Develop your hive and secure Petra-7.",x,y+138,620,19,uiText);}else if(campaignMission.Index==13){UiWrap("Find the chamber. Keep your commander alive.",x,y,620,26,uiGreen);UiWrap("Explore the underground passages and reach the chamber with your forces. Human guards and cameras watch the corridors.",x,y+116,620,19,uiText);UiWrap("There is no hive or production here. Use your starting force carefully. Your commander cannot be recovered underground.",x,y+250,620,18,uiDim);}else if(campaignMission.Index is 11 or 12){UiWrap("Destroy both human bases. Protect your hive.",x,y,620,26,uiGreen);UiWrap("The two human factions are allied. Gather Petra-7, develop your hive and eliminate both strongholds. Your commander can be recovered once after falling in battle.",x,y+116,620,19,uiText);UiWrap("A Neural Hive reveals artifact sites within your sight. Brozaars excavate buried artifacts. Tektaaras capture neutral wildlife; Lunateks drive nearby forces berserk. Both are consumed on activation.",x,y+250,620,18,uiDim);}else if(campaignMission.Index==10){UiWrap("Find the Gorrems, steal Petra-7 and destroy the human base.",x,y,620,26,uiGreen);UiWrap("Use your Ortus to spot deployed human miners. Rescue the hidden Gorrems, then deploy them at a safe distance to siphon half the miner’s income. Protect your damaged hive.",x,y+116,620,19,uiText);UiWrap("Select Steal Petra-7 or press D. Your team must currently see another owner’s deployed miner when the Gorrem deploys. Retract before moving. A miner supports only one siphoner.",x,y+250,620,18,uiDim);}else if(campaignMission.Index==9){UiWrap("Establish your hive and destroy the human base.",x,y,620,26,uiGreen);UiWrap("Your former hive is under attack. Build at the new site before the relocation grace period ends. Gather Petra-7 and develop your forces.",x,y+116,620,19,uiText);UiWrap("A Neural Hive reveals artifact sites within your sight. Brozaars excavate them. Activate a Tektaara to take control of neutral wildlife, subject to the army limit; the artifact is consumed.",x,y+250,620,18,uiDim);}else if(campaignMission.Index==8){UiWrap("Find the Chamber of Ancient Knowledge and escape with your commander.",x,y,620,26,uiGreen);UiWrap("Recover the lost teams and explore the underground. Enter the chamber to begin the scan; the mind link will announce when the exit is ready. Keep your commander alive.",x,y+132,620,19,uiText);UiWrap("Gorrem Disease requires full energy and weapon tier II. Select Disease, then choose ground. Its spores damage ground forces of either side and block passage temporarily. No excavation is required for the scan.",x,y+280,620,18,uiDim);}else if(campaignMission.Index==7){UiWrap("Pursue Commander Tick. Keep your commander alive.",x,y,620,26,uiGreen);UiWrap(mission.Blocks[9]!.Remaining==0?"The mind link is returning your commander. Develop your hive and destroy the human base.":"Follow Tick through the valley. Watch for the minefield and await your reinforcements. Tick remains under his own command.",x,y+116,620,19,uiText);UiWrap("Move only avoids stopping for nearby enemies. After your return, gather Petra-7 and develop your forces. Protect your hive.",x,y+250,620,18,uiDim);}else if(campaignMission.Index==6){UiWrap("Destroy the hostile human bases. Protect your Mind Hive.",x,y,620,26,uiGreen);UiWrap(mission.Blocks[12]!.Remaining==0&&mission.Blocks[13]!.Remaining>0?"Stratus has broken the alliance. Destroy its remaining base.":"Fight alongside Stratus against the other human bases. Stratus forces remain under their own command.",x,y+116,620,19,uiText);UiWrap("Zisps automatically heal and repair your nearby units and buildings while idle. Healing uses energy, which recharges over time. Move a Brozaar onto an active Petra-7 vent to harvest; build a Neural Hive to discover artifacts.",x,y+250,620,18,uiDim);}else if(campaignMission.Index==5){UiWrap("Destroy the human Citadel. Protect your hive.",x,y,620,26,uiGreen);UiWrap("Commander Tick provides a diversion. Develop your hive and gather Petra-7 as new vents become available. Build a Neural Hive to detect ancient artifact sites within your sight.",x,y+116,620,19,uiText);UiWrap("Move a Brozaar onto an artifact site to excavate. A Lunatek is consumed on activation, driving nearby units of either side berserk temporarily. Keep friendly forces clear. Use D or Activate.",x,y+250,620,18,uiDim);}else if(campaignMission.Index==4){UiWrap("Find and extract Commander Tick from Area 51.",x,y,620,26,uiGreen);UiWrap("Infiltrate the human installation and reach the commander near the Stratus offices. Move to the rendezvous to call the extraction saucers. Keep your commander alive.",x,y+138,620,19,uiText);UiWrap("Allied forces share current sight after the mind link is established. They remain under their own command. Use Move only (M) to avoid stopping to engage defenders.",x,y+280,620,18,uiDim);}else if(campaignMission.Index==3){UiWrap("Infiltrate the energy depot, then destroy the human base.",x,y,620,26,uiGreen);UiWrap("Keep your commander alive during infiltration. After reaching the energy tanks, the mind link returns him to your hive. Gather Petra-7 and develop forces for the assault. Protect your hive.",x,y+130,620,19,uiText);UiWrap("Ortu scouts fly across terrain. Atril fires long-range area artillery; keep friendly units clear of impacts. Breeder Pod II and Genetics upgrades unlock the expanded roster.",x,y+280,620,18,uiDim);}else if(campaignMission.Index==2){UiWrap("Locate Ancona's secret. Protect the discovery and destroy the human outpost.",x,y,620,26,uiGreen);UiWrap("Develop your hive and gather Petra-7. Losing your hive or the allied cryo-chamber ends the mission. Xenoworts deploy permanently into defensive turrets with D.",x,y+138,620,19,uiText);}else if(economicMission){UiWrap("Destroy all six human COMMUNICATION RELAYS.",x,y,620,26,uiGreen);UiLabel($"{nativeWorld.Deaths[1,86]} of 6 destroyed",x,y+84,21);UiWrap("Develop your hive and gather Petra-7 to produce a force. Protect the hive: losing all five main organs ends the mission. An injured commander can be recovered and returned.",x,y+138,620,19,uiText);UiWrap("Brozaar: right-click a vent to harvest. D retracts a Brozaar or deploys a Slom. Use the original Forces, Hive and Upgrades controls in the sidebar.",x,y+280,620,18,uiDim);}
   else {
   UiWrap("Destroy all ATMOSPHERE CONTAMINATORS.",x,y,620,26,uiGreen);UiLabel($"{nativeWorld.Deaths[1,82]} of 11 destroyed",x,y+84,21);UiWrap("You must put an end to the HUMANS' meddling. Damage to our planet's ecosystem will not be tolerated.",x,y+138,620,19,uiText);
   UiWrap("Human night-time optical sensors are far inferior to ours. Attacks at night are favorable.",x,y+252,620,18,uiDim);
   }
  }else if(missionPanel=="history"){
   var msgs=mission.Messages.Skip(historyOffset).Take(4).ToArray();if(msgs.Length==0)UiLabel("No transmissions yet.",x,y,20,uiDim);
   foreach(var msg in msgs){UiWrap(InstructionText(msg.TextId).Replace('\n',' '),x,y,620,17,uiGreen,3);y+=91;}
   UiButton("history-up","Earlier",new(x,r.End.Y-114,140,36),"Earlier transmissions");UiButton("history-down","Later",new(x+150,r.End.Y-114,140,36),"Later transmissions");
  }else UiWrap("Select: click a friendly unit or drag a box.\nRight-click: move or attack a visible enemy.\nM: Move only · A: Move & attack · S: Stop\nI: Inspire · Tab: select and centre commander\nEsc or right-click: cancel targeting.\nOuter window edges, arrows or middle drag: pan · Wheel / + / −: zoom\nF: fit map · Minimap: click or drag to navigate\n[ / ]: interface size · J: objectives · O: options (game speed, artwork, sound)\nW: waypoints (single-player) · R: restart · Esc: menu (single-player pauses)",x,y,640,18,uiText);
  if(missionPanel!="menu")UiButton("close",mainMenu?"Back   Esc":missionBriefing?"Back to briefing   Esc":mission.AtOutcome?"Back to result   Esc":"Return to battle   Esc",new(x,r.End.Y-68,640,40),"Close this panel.");
 }
 void DrawMissionFrontend(){bool prior=landfallDrawing;landfallDrawing=true;try{DrawLandfallFrontend();}finally{landfallDrawing=prior;}}
 void DrawLandfallFrontend(){
  if(IsWar&&mission.AtOutcome){DrawWarResults();return;}
  if(IsWar&&missionPanel=="diplomacy"){DrawWarDiplomacy();return;}
  if(mainMenu&&missionPanel is "campaign" or "load" or "options"){DrawSurfaceSubmenu();return;}
  if(DrawSurfaceDialog())return;
  if(missionPanel=="menu"){DrawCompactMissionMenu();return;}
  if(missionPanel=="playtesting"){DrawPlaytestingOptions();return;}
  missionHits.Clear();DrawRect(new(Vector2.Zero,GetViewportRect().Size),new Color("050909"));var r=FrontRect;var grid=MissionGrid();UiFrame(r,true);float x=grid.Content.Position.X,y=grid.Content.Position.Y;
  UiLabel(OriginalCampaign.Label(campaignMission.CampaignId).ToUpperInvariant()+$" · {(IsTraining?"LESSON":"MISSION")} {campaignMission.Index+1:00}",x,y+18,14,OdcUiTokens.Signal);UiWrap(campaignMission.Title,x,y+68,grid.Content.Size.X,40,OdcUiTokens.Text,2);UiLabel(campaignMission.Region+"  /  "+(IsWar?"SINGLE-PLAYER WAR":OriginalCampaign.Label(campaignMission.CampaignId).ToUpperInvariant()+(IsTraining?"":" CAMPAIGN")),x,y+(grid.Body.Position.Y-grid.Content.Position.Y)-24,16,uiDim);
  if(missionFault!=null){UiLabel("Mission interrupted",x,y+190,28);UiWrap("An unexpected error stopped this mission. A diagnostic report has been saved. Restart to begin a fresh mission.",x,y+240,r.Size.X-68,22,uiText);}
  else if(missionNext){UiLabel("Mission complete",x,y+190,28,uiGreen);UiWrap(nextMissionNotice,x,y+240,r.Size.X-68,22,uiText);}
  else if(missionBriefing){
   DrawBriefing(grid);
  }else {UiLabel(mission.Outcome?.Success==true?"MISSION SUCCESS":"MISSION FAILURE",x,grid.Body.Position.Y+26,28,uiGreen);DrawRewards(UiSize.X,UiSize.Y);}
  if(missionVideo is not {Visible:true}){
   float footerY=grid.Footer.Position.Y;
   UiButton("main-menu",missionBriefing?"Main menu   Esc":"Exit   Esc",new(x,footerY,grid.Left.Size.X,UiActionHeight),"Return to the main menu.");
   if(missionBriefing){
    UiButton("brief-reveal","Reveal all   Space",new(grid.Right.Position.X,footerY,220,UiActionHeight),"Show the complete transmission without starting the mission.",BriefingRevealing);
    UiButton("begin","To battle   Enter",new(grid.Footer.End.X-290,footerY,290,UiActionHeight),"Begin the mission.");
   }else{
    float replayX=resultContinueAvailable?grid.Footer.End.X-2*250-UiGap:grid.Footer.End.X-250;
    UiButton("restart","Replay",new(replayX,footerY,250,UiActionHeight),"Return to this mission's briefing.");
    if(resultContinueAvailable)UiButton("continue",resultFinal?(IsTraining?"Finish training":"Finish campaign"):resultBatchEnd?"Return to campaign":"Continue",new(grid.Footer.End.X-250,footerY,250,UiActionHeight),resultFinal?(IsTraining?"Finish training and return to lesson selection.":"Complete the campaign and return to the main menu."):resultBatchEnd?"Keep this mission completion and return to campaign selection. Further missions are not yet available.":"Continue to the next mission.",active:true);
   }
  }
  if(persistenceNotice.Length>0)UiTextLine(persistenceNotice,new(x+350,grid.Body.Position.Y,grid.Body.Size.X-350-(campaignWritePending?266:0),36),14,uiGreen);
  if(campaignWritePending)UiButton("retry-progress","Retry saving progress",new(grid.Body.End.X-250,grid.Body.Position.Y,250,36),"Retry campaign storage before leaving this result.");
  if(missionPanel.Length>0)DrawMissionPanel();
 }
}
