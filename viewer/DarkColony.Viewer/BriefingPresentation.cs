using Godot;
using DarkColony.Original;
using System.Text;
namespace DarkColony.Viewer;

public partial class Viewer {
 // DC16's >1ms gate is not evidence of effective original UI cadence. Dennis's
 // playtest rejected500chars/s. Provisional readable33.3chars/s; not native parity.
 // Globe403109/403169 remains50ms. Neither clock depends on gameplay speed.
 const double BriefCharacterSeconds=.03,BriefGlobeSeconds=.05,BriefSoundSeconds=.08;
 double briefingLastSound=-BriefSoundSeconds;int briefingClosingEnd;
 double briefingElapsed;int briefingCharacters;bool briefingFollow=true;
 byte[] briefingColors=Array.Empty<byte>();
 readonly List<(int Start,int Length)> briefingLines=new();float briefingLineWidth=-1;
 OriginalAudioMixer.Voice? briefingTyping;
 bool BriefingRevealing=>briefingCharacters<briefing.Length;
 bool BriefingTransmissionComplete=>briefingCharacters>=briefingClosingEnd;
 string BriefingStatus=>IsHuman?(BriefingTransmissionComplete?"Transmission complete":"Transmission active"):(BriefingTransmissionComplete?"Mind link closed":"Mind link active");
 static int ClosingEnd(string text,bool human){
  string marker=human?(text.Contains("MESSAGE OUT",StringComparison.OrdinalIgnoreCase)?"MESSAGE OUT":"TRANSMISSION OUT"):"MIND LINK BROKEN";
  int at=text.LastIndexOf(marker,StringComparison.OrdinalIgnoreCase);
  if(at<0)return text.Length;
  int end=at+marker.Length;while(end<text.Length&&text[end]=='.')end++;
  return end;
 }
 void PrepareBriefingText(){
  var raw=Encoding.ASCII.GetString(nativeData.Read(campaignMission.TextStem+".TXT")).Replace("\r","");
  if(IsTraining)raw=TrainingInstructions.MapBriefing(raw);
  var text=new StringBuilder();var colors=new List<byte>();byte color=0;
  for(int i=0;i<raw.Length;i++){
   if(raw[i]=='~'&&i+1<raw.Length&&raw[i+1] is >= '0' and <= '7'){color=(byte)(raw[++i]-'0');continue;}
   text.Append(raw[i]);colors.Add(color);
  }
  briefing=text.ToString();briefingColors=colors.ToArray();briefingLines.Clear();briefingLineWidth=-1;
  briefingClosingEnd=ClosingEnd(briefing,IsHuman);
 }
 void ResetBriefingPresentation(){StopBriefingTyping();briefingElapsed=0;briefingLastSound=-BriefSoundSeconds;briefingCharacters=0;briefingFollow=true;briefingScroll=0;}
 void StopBriefingTyping(bool flush=false){missionMixer?.Stop(briefingTyping);briefingTyping=null;if(flush)ClearMissionAudioBuffer();}
 void RevealBriefing(){briefingCharacters=briefing.Length;StopBriefingTyping(true);QueueRedraw();}
 void ScrollBriefing(float lines){briefingFollow=false;briefingScroll=Math.Clamp(briefingScroll+lines,0,Math.Max(0,briefingTotalLines-briefingVisibleLines));}
 void ProcessBriefing(double delta){
  if(!missionBriefing){StopBriefingTyping();return;}
  briefingElapsed+=Math.Max(0,delta);
  int before=briefingCharacters;briefingCharacters=Math.Max(before,Math.Min(briefing.Length,(int)(briefingElapsed/BriefCharacterSeconds)));
  if(BriefingTransmissionComplete){if(briefingTyping!=null)StopBriefingTyping(true);return;}
  // Native427ed2..427ee1 requests132 (BEEP.WAV) for nonblank glyphs; its one-copy
  // pool prevents overlap. Coalesce catch-up requests instead of allocating a sound per glyph.
  if(briefingCharacters>before&&briefingElapsed-briefingLastSound>=BriefSoundSeconds&&briefingTyping is not {Playing:true}&&briefing.AsSpan(before,briefingCharacters-before).IndexOfAnyExcept(' ','\n')>=0){
   briefingTyping=missionMixer.Play(132)??briefingTyping;briefingLastSound=briefingElapsed;
  }
 }
 void LayoutBriefing(float width){
  if(briefingLineWidth==width&&briefingLines.Count>0)return;
  briefingLineWidth=width;briefingLines.Clear();
  int start=0;
  while(start<briefing.Length){
   int end=briefing.IndexOf('\n',start);if(end<0)end=briefing.Length;
   if(end==start){briefingLines.Add((start,0));start++;continue;}
   int cut=end;
   if(UiFont.GetStringSize(briefing[start..end],fontSize:18).X>width){
    int lo=start+1,hi=end;while(lo<hi){int mid=(lo+hi+1)/2;if(UiFont.GetStringSize(briefing[start..mid],fontSize:18).X<=width)lo=mid;else hi=mid-1;}cut=lo;
    int space=briefing.LastIndexOf(' ',cut-1,cut-start);if(space>start)cut=space;
   }
   briefingLines.Add((start,cut-start));start=cut;
   if(start==end){if(start<briefing.Length)start++;}else while(start<end&&briefing[start]==' ')start++;
  }
  if(briefingLines.Count==0)briefingLines.Add((0,0));
 }
 // Original briefing~codes are shared by both factions, not a blanket faction tint.
 // SHUMAN/INTRFACE palette team ramps138..143, mapped by native selector~0..7:
 // red,blue,yellow,purple,green,orange,tan,cyan. Lift dark extrema for modern text.
 static Color BriefColor(byte code)=>code switch{0=>new("ed8078"),1=>new("648eff"),2=>new("e2cd69"),3=>new("d58cf0"),4=>new("80dc75"),5=>new("f0a262"),6=>new("d6b68e"),_=>new("70ded9")};
 void DrawBriefingLine(int index,float x,float y){
  var line=briefingLines[index];int end=Math.Min(line.Start+line.Length,briefingCharacters),at=line.Start;
  while(at<end){byte color=briefingColors[at];int next=at+1;while(next<end&&briefingColors[next]==color)next++;
   string part=briefing[at..next];UiLabel(part,x,y,18,BriefColor(color));x+=UiFont.GetStringSize(part,fontSize:18).X;at=next;
  }
 }
 (string Bank,int Cell) BriefingGlobe(){
  int frame=(int)(briefingElapsed/BriefGlobeSeconds);bool earth=campaignMission.RawFlag!=0;
  // 403578..403615: play the whole G opening, then S up to the authored
  // region frame; hold it while EPIC loops. G/S are not faction variants.
  string opening=earth?"EARTHG":"GLOBEG",rotation=earth?"EARTHS":"GLOBES";
  int count=UiBank(opening).Cells.Length-1;
  if(frame<count)return(opening,frame+1);
  int target=Math.Clamp(campaignMission.RawParameters[0],1,UiBank(rotation).Cells.Length-1);
  return(rotation,Math.Min(target,frame-count+1));
 }
 void DrawBriefingGlobe(Rect2 box){
  var (bank,cell)=BriefingGlobe();var art=UiArt(bank,cell);var asset=UiBank(bank).Cells[cell];
  // Opening cells have varying bounds. Use the original origin on a fixed canvas
  // rather than independently enlarging each tiny opening frame to fill the panel.
  float scale=Math.Min(box.Size.X/244,box.Size.Y/244);var canvas=box.GetCenter()-new Vector2(244,244)*scale/2;
  // All banks share the original origin. The full globe's bounds start at3,3;
  // normalizing each bank separately would shift rotation/marker alignment.
  var p=canvas+new Vector2(asset.OffsetX-3,asset.OffsetY-3)*scale;
  DrawTextureRect(art,Px(new(p,art.GetSize()*scale)),false);
  int opening=UiBank(campaignMission.RawFlag!=0?"EARTHG":"GLOBEG").Cells.Length-1;
  double locatedAt=(opening+Math.Max(0,campaignMission.RawParameters[0]-1))*BriefGlobeSeconds;
  if(briefingElapsed>=locatedAt){
   // GSCENE/HSCENE: frame, absolute native x,y (globe origin34,26).
   var epic=UiBank("EPIC");int marker=1+(int)((briefingElapsed-locatedAt)/.045)%(epic.Cells.Length-1);
   var c=epic.Cells[marker];var texture=UiArt("EPIC",marker);
   var at=canvas+new Vector2(campaignMission.RawParameters[1]-34+c.OffsetX-3,campaignMission.RawParameters[2]-26+c.OffsetY-3)*scale;
   DrawTextureRect(texture,Px(new(at,texture.GetSize()*scale)),false);
  }
 }
}
