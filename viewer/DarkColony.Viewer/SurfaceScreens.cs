using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;
public partial class Viewer {
 readonly string[] saveDates=new string[4];
 readonly bool[] saveInvalid=new bool[4];
 int surfaceSave;
 int campaignScroll,campaignRows=1;
 string surfaceErrorReturn="load";
 string productVersion="Source";
 void DrawSurfaceLogo(float x,float y,float width,bool subtitle){
  wordmark??=ResourceLoader.Load<Texture2D>("res://Branding/Landfall/opendc-wordmark.svg");
  DrawTextureRect(wordmark,Px(new(x,y,width,width*80/404)),false);

 }
 void DrawSurfaceFooter(){
  UiLabel("↑ ↓  Navigate    Enter  Select    Esc  Back",120,UiSize.Y-24,14,uiDim);
  string info=$"{productVersion}     CHRYSE BASIN · MARS     UI {ui:0.##}×";
  UiTextLine(info,new(620,UiSize.Y-44,UiSize.X-676,32),13,uiDim,true);
 }
 void DrawSavePreview(int slot,Rect2 r){
  UiFrame(r);for(float offset=12;offset<r.Size.X;offset+=16){float len=Math.Min(r.Size.Y,r.Size.X-offset);DrawLine((r.Position+new Vector2(offset,0))*ui,(r.Position+new Vector2(offset+len,len))*ui,new Color("1a1c1a"),ui);}
  string title=saveInvalid[slot]?(r.Size.X<100?"!":"! UNREADABLE"):saveExists[slot]?"NO PREVIEW":"EMPTY";
  int size=r.Size.X<100?12:14;float width=UiFont.GetStringSize(title,fontSize:size).X;
  UiLabel(title,r.GetCenter().X-width/2,r.GetCenter().Y+5,size,saveInvalid[slot]?new Color("f2b33d"):uiDim);
 }
 void DrawSurfaceSubmenu(){
  if(missionPanel=="campaign"){DrawLandfallCampaign();return;}if(missionPanel=="missions"){DrawLandfallMissions();return;}if(missionPanel=="options"){DrawLandfallOptions();return;}
  missionHits.Clear();LandfallHeading("Load game","Saved transmissions");float x=ReadingLeft,y=UiSize.Y<850?218:282,w=Math.Min(800,UiSize.X-x-80);
  for(int i=0;i<4;i++){var rect=new Rect2(x,y+i*92,w,80);bool hover=focusedUi=="load-slot:"+i||Px(rect).HasPoint(missionPointer);if(hover)surfaceSave=i;
   UiButton("load-slot:"+i,"",rect,saveExists[i]?"Resume slot "+(i+1):"No saved game",saveExists[i]);
   UiTextLine($"SLOT {i+1:00}  ·  "+saveTitles[i],new(x+20,rect.Position.Y+8,w-40,32),24,saveInvalid[i]?new Color("d9a441"):OdcUiTokens.Text);
   UiTextLine(saveExists[i]?saveCampaigns[i]+" · "+saveDates[i]:"Empty slot",new(x+20,rect.Position.Y+43,w-40,24),15,OdcUiTokens.Muted);
  }
  if(UiSize.X-x-w>400){float dx=x+w+56;UiLabel(saveTitles[surfaceSave],dx,y+32,28);UiWrap(saveDetails[surfaceSave],dx,y+82,Math.Min(500,UiSize.X-dx-60),20,OdcUiTokens.Secondary);}
  DrawSurfaceFooter();
 }

 bool DrawSurfaceDialog(){
  if(missionPanel is not ("quit-confirm" or "overwrite" or "abandon" or "save-error"))return false;
  missionHits.Clear();DrawRect(OverlayRect,new Color(.015f,.02f,.015f,.78f));
  string body=missionPanel=="quit-confirm"?"Quit OpenDC?":missionPanel=="overwrite"?$"Replace slot {overwriteSlot+1}? The previous save is retained if writing fails.":missionPanel=="save-error"?persistenceNotice:campaignWritePending?"Campaign progress could not be written. Leaving loses this result. Keep playing and retry from the result screen.":"Progress since your last save will be lost. Existing saves and campaign results are kept.";
  bool surrender=missionPanel=="abandon"&&abandonAction=="war-surrender";
  if(IsNetworkMatch&&missionPanel=="abandon")body=network!.Host?"Leave this match? As host, you will end the session for every player.":"Leave this match? AI will take control of your remaining forces. Network matches cannot be rejoined.";
  if(surrender&&IsNetworkMatch)body="Surrender control of your forces? Your result is a forfeit. AI will continue with your remaining units; you may watch with your existing vision or leave.";
  else if(surrender)body="Surrender this match? The result will be a defeat. Existing saves and campaign progress are kept.";
  float height=Math.Clamp(160+UiLines(body,496,19).Count*28,300,UiSize.Y-64);
  var r=new Rect2((OverlayWidth-540)/2,(UiSize.Y-height)/2,540,height);UiFrame(r,true);
  string title=missionPanel=="quit-confirm"?"QUIT":missionPanel=="overwrite"?"REPLACE SAVE":missionPanel=="save-error"?"! SAVE UNAVAILABLE":"LEAVE SESSION";
  if(surrender)title="SURRENDER MATCH";else if(missionPanel=="abandon"&&abandonAction=="restart")title=IsWar?"RESTART MATCH":"RESTART MISSION";
  UiLabel(title,r.Position.X+18,r.Position.Y+27,20,OdcUiTokens.Signal);float x=r.Position.X+22,y=r.Position.Y+83;
  UiWrap(body,x,y,496,19,uiText,Math.Max(1,(int)((height-150)/28)));
  if(missionPanel=="save-error")UiButton("error-close","OK   Esc",new(x,r.End.Y-70,496,48),"Return to save slots. No files were changed.");
  else {
   string yes=missionPanel=="quit-confirm"?"quit-confirmed":missionPanel=="overwrite"?"overwrite-yes":"abandon-confirm";
   string no=missionPanel=="quit-confirm"?"close":missionPanel=="overwrite"?"save":"abandon-cancel";
   UiButton(yes,surrender?"Surrender":missionPanel=="quit-confirm"?"Quit":missionPanel=="overwrite"?"Replace save":abandonAction=="restart"?(IsWar?"Restart match":"Restart mission"):"Leave without saving",new(x,r.End.Y-70,242,48),"Confirm this action");
   UiButton(no,missionPanel=="abandon"?"Keep playing   Esc":"Cancel   Esc",new(x+254,r.End.Y-70,242,48),"Keep the current session");
  }
  return true;
 }
}
