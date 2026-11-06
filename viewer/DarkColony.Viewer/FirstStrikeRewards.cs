using Godot;
using DarkColony.Original;
namespace DarkColony.Viewer;

public partial class Viewer {
 OriginalRewards? missionRewards;readonly Dictionary<string,ImageTexture> rewardTextures=new();
 double rewardMilliseconds;bool rewardsVisible;
 OriginalRewardAnimation missionRewardAnimation=new();
 int resultScroll,resultRows,resultLineCount;
 bool resultContinueAvailable,resultFinal,resultBatchEnd;ulong resultAcceptAfter;
 bool ResultInputReady=>Time.GetTicksMsec()>=resultAcceptAfter;
 string ResultDefault=>resultContinueAvailable?"continue":"restart";
 void BeginRewards() {
  if(rewardsVisible)return;rewardsVisible=true;resultScroll=0;rewardMilliseconds=0;missionRewardAnimation=new();
  var catalog=OriginalCampaign.For(nativeData);int next=mission.ResultProfile?.CampaignIndex??-1;
  resultFinal=mission.Outcome?.Success==true&&next==catalog.Length&&mission.CampaignIndex==catalog.Length-1;
  resultBatchEnd=IsCouncil&&mission.Outcome?.Success==true&&next>=0&&next<catalog.Length&&!catalog[next].Implemented;
  resultContinueAvailable=mission.Outcome?.Success==true&&(resultFinal||resultBatchEnd||next>=0&&next<catalog.Length&&catalog[next].Implemented&&File.Exists(Path.Combine(nativeData.DataRoot,catalog[next].Scenario)));
  focusedUi=ResultDefault;pressedUi="";uiPointerOwned=false;resultAcceptAfter=Time.GetTicksMsec()+250;
  ClearMissionAudioBuffer();
  missionRewards??=new(nativeData);
  // Two original frontend activations40425f/404291; six-copy ACTIVE pool permits both.
  PlayMissionSound(186);PlayMissionSound(186);
 }
 ImageTexture RewardTexture(string key,Func<DarkColony.Assets.Raster> pixels) {
  if(!rewardTextures.TryGetValue(key,out var texture))rewardTextures[key]=texture=OriginalTexture(pixels());return texture;
 }
 void DrawRewards(float width,float height) {
  if(!rewardsVisible||missionRewards==null)return;
  var outcome=mission.Outcome!;var grid=MissionGrid();
  var body=new Rect2(grid.Body.Position+new Vector2(0,52),grid.Body.Size-new Vector2(0,52));
  float panel=body.Size.X,textWidth=(panel-UiGap)*.54f;
  UiFrame(new(body.Position,new(textWidth,body.Size.Y)),true);
  var lines=UiLines(outcomeText,textWidth-2*UiInset-16,19);resultLineCount=lines.Count;resultRows=Math.Max(1,(int)((body.Size.Y-90)/28));resultScroll=Math.Clamp(resultScroll,0,Math.Max(0,lines.Count-resultRows));
  for(int i=0;i<resultRows&&i+resultScroll<lines.Count;i++)UiLabel(lines[i+resultScroll],body.Position.X+UiInset,body.Position.Y+UiInset+19+i*28,19,uiText);
  if(lines.Count>resultRows){float sy=body.Position.Y+UiInset,sh=body.Size.Y-86;DrawRect(Px(new(body.Position.X+textWidth-12,sy,3,sh)),OdcUiTokens.Line);DrawRect(Px(new(body.Position.X+textWidth-12,sy+sh*resultScroll/lines.Count,3,sh*resultRows/lines.Count)),OdcUiTokens.Signal);UiButton("result-up","↑",new(body.Position.X+UiInset,body.End.Y-54,46,36),"Scroll debriefing up",resultScroll>0);UiButton("result-down","↓",new(body.Position.X+UiInset+56,body.End.Y-54,46,36),"Scroll debriefing down",resultScroll+resultRows<lines.Count);}
  float x=body.Position.X+textWidth+UiGap,y=body.Position.Y+UiInset;
  if(IsTraining){UiLabel(outcome.Success?"Lesson complete":"Try this lesson again",x,y+10,24,uiGreen);UiWrap("Training progress is separate from campaign progress. No commander ranks or awards are earned here.",x,y+62,body.End.X-x-UiInset,19,uiDim);return;}
  if(!outcome.Success)return;
  int entryRank=campaignEntry?.Rank??0;var rank=RewardTexture("rank"+entryRank,()=>missionRewards.Icon("RANKS",IsHuman?0:1));
  DrawTextureRect(rank,new(new Vector2(x,y-20)*ui,rank.GetSize()*2*ui),false);
  UiLabel((IsHuman?missionRewards.HumanRanks:missionRewards.GrayRanks)[entryRank]+(IsHuman?"  COMMANDER":"  TAAR"),x+72,y+8,23,uiGreen);
  UiTextLine(campaignMission.Title+" commander",new(x+72,y+20,body.End.X-x-72,24),16,uiDim);
  if(outcome.Success&&outcome.Rank>0)UiWrap((finalCampaignMission?"Final rank: ":"Next mission rank: ")+(IsHuman?missionRewards.HumanRanks:missionRewards.GrayRanks)[outcome.Rank],x,y+75,panel-textWidth-50,17,uiText);
  y+=126;
  var medals=outcome.Success?outcome.Medals.Where(id=>id>=(IsHuman?0:6)).ToArray():Array.Empty<int>();

  float scale=Math.Clamp(((body.End.Y-y)/Math.Max(1,medals.Length)-18)/120,.35f,1.1f);
  foreach(int id in medals) {
   var bank=missionRewards.Medal(id);int frame=missionRewardAnimation.Display;if(frame==0)continue;
   var art=RewardTexture($"medal{id}/{frame}",()=>OriginalRewards.Pixels(bank,bank.Cells[frame]));
   DrawTextureRect(art,new(new Vector2(x,y-16)*ui,art.GetSize()*scale*ui),false);
   UiWrap(missionRewards.Descriptions[id],x+80*scale+18,y+12,body.End.X-x-100,16,uiGreen);y+=120*scale+18;
  }
 }
}
