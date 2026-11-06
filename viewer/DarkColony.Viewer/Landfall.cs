using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;

public static class OdcUiTokens {
 public static readonly Color Ground=new("090a08"),Surface=new("141612"),Pane=new("0f110d"),Line=new("30352a"),Text=new("ece8dd"),Secondary=new("b4ae9f"),Muted=new("8c877b"),Signal=new("c9d86b");
}
public partial class Viewer {
 readonly Stack<(string Panel,string Focus)> landfallNavigation=new();
 bool landfallDrawing;
 Font? landfallFont;Texture2D? wordmark;
 Font LandfallFont=>landfallFont??=ResourceLoader.Load<FontFile>("res://Fonts/BarlowSemiCondensed-Medium.ttf");
 float ReadingLeft=>Math.Max(0,(UiSize.X-2560)/2)+(UiSize.Y<850?72:120);
 float OverlayWidth=>mainMenu||missionBriefing||rewardsVisible?UiSize.X:MissionBattlefield.Size.X/ui;
 Rect2 OverlayRect=>new(0,0,OverlayWidth*ui,mainMenu?GetViewportRect().Size.Y:MissionBattlefield.Size.Y);
 float optionScroll;int campaignOverviewScroll;
 void LandfallFrame(Rect2 r){DrawRect(Px(r),OdcUiTokens.Pane);DrawRect(Px(r),OdcUiTokens.Line,false,ui);}
 void LandfallButton(UiHit hit,string label,Rect2 r,bool active,bool enabled,bool compact){
  bool focus=focusedUi==hit.Id||hit.Rect.HasPoint(missionPointer),press=pressedUi==hit.Id;
  if(mainMenu&&missionPanel.Length==0){
   if(focus||press)DrawRect(hit.Rect,new Color(.79f,.85f,.42f,press?.18f:.08f));
   if(focus){DrawLine(hit.Rect.Position,hit.Rect.Position+new Vector2(0,hit.Rect.Size.Y),OdcUiTokens.Signal,2*ui);}
   int size=hit.Id is "resume-save"?46:hit.Id=="campaign"&&continueSlot<0?46:hit.Id is "load" or "options" or "quit"?22:32;
   UiTextLine(label,new(r.Position+new Vector2(16,0),r.Size-new Vector2(32,0)),size,enabled?OdcUiTokens.Text:OdcUiTokens.Muted);return;
  }
  DrawRect(hit.Rect,press?new Color("343b22"):focus?new Color("252a1a"):active?new Color("20251a"):new Color("12150f"));
  DrawRect(hit.Rect,focus?OdcUiTokens.Signal:active?new Color("5b6340"):OdcUiTokens.Line,false,ui);
  if(focus)DrawRect(new(hit.Rect.Position,new Vector2(3*ui,hit.Rect.Size.Y)),OdcUiTokens.Signal);
  int split=label.IndexOf("   ",StringComparison.Ordinal);string key=split>=0?label[(split+3)..]:"";if(split>=0)label=label[..split];
  float inset=r.Size.X<64?4:16;
  var box=new Rect2(r.Position+new Vector2(inset,0),r.Size-new Vector2(inset*2+key.Length*8,0));UiTextLine(label,box,compact?16:22,enabled?OdcUiTokens.Text:OdcUiTokens.Muted);
  if(key.Length>0)UiTextLine(key,new(r.End.X-96,r.Position.Y,80,r.Size.Y),14,OdcUiTokens.Muted,true);
 }
 void LandfallHeading(string title,string eyebrow,string sub=""){
  float x=ReadingLeft,y=UiSize.Y<850?76:132;
  UiButton("close",missionPanel=="missions"?"‹  Campaign   Esc":"‹  Main menu   Esc",new(x-16,y-62,260,40),"Return one level");
  if(eyebrow.Length>0)UiLabel(eyebrow.ToUpperInvariant(),x,y,14,OdcUiTokens.Signal);
  UiLabel(title,x,y+58,48);if(sub.Length>0)UiWrap(sub,x,y+90,Math.Min(1300,UiSize.X-x*2),18,OdcUiTokens.Secondary,2);
 }
 void DrawLandfallCampaign(){
  missionHits.Clear();LandfallHeading("Campaign","Choose your journey","Original campaigns and training. Progress is kept separately for each faction and edition.");
  float x=ReadingLeft,y=UiSize.Y<850?208:282,w=Math.Min(1680,UiSize.X-x-60),h=Math.Min(210,(UiSize.Y-y-80)/3);
  // At enlarged scale the edition rows deliberately scroll; focus follows the row.
  h=Math.Max(160,h);int visible=Math.Max(1,(int)((UiSize.Y-y-76)/(h+16)));campaignOverviewScroll=Math.Clamp(campaignOverviewScroll,0,3-visible);
  var editions=new[]{("training","Training","7 lessons per faction","Learn the controls and original systems."),("base","Dark Colony","15 missions per faction","The original struggle for Mars."),("council","Council Wars","8 missions per faction","The conflict continues.")};
  foreach(var e in editions.Skip(campaignOverviewScroll).Take(visible)){
   LandfallFrame(new(x,y,w,h));UiLabel(e.Item2,x+24,y+36,32);UiLabel(e.Item3,x+24,y+64,16,OdcUiTokens.Muted);UiWrap(e.Item4,x+24,y+96,w*.28f,18,OdcUiTokens.Secondary,2);
   if(e.Item1=="council"&&!councilAvailable){UiWrap("Expansion content not installed · optional",x+w*.36f,y+55,w*.55f,22,OdcUiTokens.Secondary);UiButton("game-content","Add expansion content…",new(x+w*.36f,y+85,w*.55f,54),"Import Council Wars without changing base-game progress");}
   else {float card=(w*.63f-24)/2;foreach(var f in new[]{("human","Human","HCAR"),("taar","Taar","ACAR")}){int i=f.Item1=="human"?0:1;float cx=x+w*.35f+i*(card+12);UiButton("journey:"+e.Item1+":"+f.Item1,"",new(cx,y+20,card,h-40),"Choose "+e.Item2+" · "+f.Item2);UiImage(f.Item3,1,new(cx+12,y+30,64,h-64));UiLabel(f.Item2,cx+88,y+65,32);UiTextLine("Select missions  ›",new(cx+88,y+82,card-100,35),18,OdcUiTokens.Secondary);}}
   if(e.Item1!="training"&&File.Exists(IntroductionPath(e.Item1=="council")))UiButton("intro:"+e.Item1,"Watch introduction",new(x+18,y+h-49,w*.29f,33),"Original edition introduction; returns here");
   y+=h+16;
  }
  if(visible<3){UiButton("overview-up","↑",new(x+w-100,UiSize.Y-66,44,36),"Previous edition",campaignOverviewScroll>0);UiButton("overview-down","↓",new(x+w-48,UiSize.Y-66,44,36),"Next edition",campaignOverviewScroll+visible<3);}
 }
 void DrawLandfallMissions(){
  missionHits.Clear();LandfallHeading(IsTraining?"Training":"Mission selection",OriginalCampaign.Label(SelectedCampaign),"Choose an available "+(IsTraining?"lesson":"mission")+". Completed progress and saved games are preserved.");
  var old=missionHits[0];missionHits[0]=old with{Help="Return to Campaign"};
  float x=ReadingLeft,y=UiSize.Y<850?218:282,w=Math.Min(800,UiSize.X-x-80),row=48;
  var list=OriginalCampaign.Catalog(SelectedContentRoot,SelectedCampaign).Where(m=>m.Implemented).ToArray();campaignRows=Math.Max(1,(int)((UiSize.Y-y-96)/row));campaignScroll=Math.Clamp(campaignScroll,0,Math.Max(0,list.Length-campaignRows));
  foreach(var m in list.Skip(campaignScroll).Take(campaignRows)){bool available=godMode||m.Index==0||campaignEntries[m.Index]!=null;
   UiButton("campaign-start:"+m.Index,"",new(x,y,w-20,row-4),available?"Begin "+m.Title:"Complete the preceding mission to unlock",available&&!campaignReadFailed);
   UiTextLine($"{m.Index+1:00}",new(x+14,y,38,row-4),16,OdcUiTokens.Muted);UiTextLine(m.Title,new(x+58,y,w-206,row-4),22,available?OdcUiTokens.Text:OdcUiTokens.Muted);
   UiTextLine(!available?"Locked":m.Index+1<campaignEntries.Length&&campaignEntries[m.Index+1]!=null?"Complete":"Available",new(x+w-144,y,104,row-4),14,available?OdcUiTokens.Signal:OdcUiTokens.Muted,true);y+=row;
  }
  if(list.Length>campaignRows){float top=UiSize.Y<850?218:282,total=campaignRows*row;DrawRect(Px(new(x+w-8,top,4,total)),OdcUiTokens.Line);DrawRect(Px(new(x+w-8,top+total*campaignScroll/list.Length,4,total*campaignRows/list.Length)),OdcUiTokens.Signal);UiButton("campaign-up","↑",new(x,UiSize.Y-76,48,38),"Scroll up · Wheel / Page Up",campaignScroll>0);UiButton("campaign-down","↓",new(x+60,UiSize.Y-76,48,38),"Scroll down · Wheel / Page Down",campaignScroll+campaignRows<list.Length);}
  float detail=x+w+56;if(UiSize.X-detail>340){UiImage(IsHuman?"HCAR":"ACAR",1,new(detail,UiSize.Y<850?230:300,220,270));UiWrap(IsHuman?"Earth command awaits your report.":"The mind link is open.",detail,UiSize.Y<850?548:618,Math.Min(500,UiSize.X-detail-80),24,OdcUiTokens.Secondary);}
  if(campaignReadFailed)UiWrap("Campaign record could not be read. Existing progress is preserved.",x,UiSize.Y-100,w,18,new Color("d9a441"));
 }
 bool LandfallAction(string id){
  if(id=="result-up"||id=="result-down"){resultScroll+=id=="result-up"?-3:3;return true;}
  if(id.StartsWith("journey:")){var parts=id.Split(':');SelectedCampaign=(parts[1]=="training"?"training-":parts[1]=="council"?"cw-":"")+parts[2];RefreshPersistenceCatalog();campaignScroll=0;OpenMissionPanel("missions");int next=Array.FindLastIndex(campaignEntries,p=>p!=null);next=Math.Max(0,next);campaignScroll=Math.Max(0,next-3);focusedUi="campaign-start:"+next;return true;}
  if(id.StartsWith("intro:")){BeginEditionIntroduction(id.EndsWith("council"));return true;}
  if(id=="overview-up"||id=="overview-down"){campaignOverviewScroll+=id=="overview-up"?-1:1;return true;}
  if(id=="options-up"||id=="options-down"){optionScroll+=id=="options-up"?-3:3;return true;}
  if(id=="reduce-motion"){reducedMotion=!reducedMotion;SaveSettings(p=>ArtworkPreference.SaveReducedMotion(p,reducedMotion),"Motion preference could not be saved.");return true;}
  if(id=="restoration-replay"){ReplayRestoration();return true;}
  return false;
 }
}
