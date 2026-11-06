using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 const float UiOuter=32,UiGap=16,UiInset=20,UiActionHeight=48;
 readonly record struct FrontGrid(Rect2 Content,Rect2 Body,Rect2 Left,Rect2 Right,Rect2 Footer);
 readonly Dictionary<(string Text,float Width,int Size),string> uiEllipsisCache=new();
 FrontGrid MissionGrid(){
  var c=FrontRect.Grow(-UiOuter);var footer=new Rect2(c.Position.X,c.End.Y-UiActionHeight,c.Size.X,UiActionHeight);
  float header=UiLines(campaignMission.Title,c.Size.X,40).Count>1?172:132;var body=new Rect2(c.Position.X,c.Position.Y+header,c.Size.X,footer.Position.Y-UiGap-c.Position.Y-header);
  float left=Math.Clamp(c.Size.X*.25f,240,300);
  return new(c,body,new(body.Position,new(left,body.Size.Y)),new(body.Position+new Vector2(left+UiGap,0),new(body.Size.X-left-UiGap,body.Size.Y)),footer);
 }
 string UiEllipsis(string text,float width,int size){
  if(UiFont.GetStringSize(text,fontSize:size).X<=width)return text;
  var key=(text,width,size);if(uiEllipsisCache.TryGetValue(key,out var cached))return cached;
  int count=text.Length;while(count>0&&UiFont.GetStringSize(text[..count]+"…",fontSize:size).X>width)count--;
  if(uiEllipsisCache.Count>256)uiEllipsisCache.Clear();return uiEllipsisCache[key]=text[..count]+"…";
 }
 void UiTextLine(string text,Rect2 box,int size=17,Color? color=null,bool right=false){
  text=UiEllipsis(text,box.Size.X,size);float x=right?box.End.X-UiFont.GetStringSize(text,fontSize:size).X:box.Position.X;
  float baseline=box.Position.Y+(box.Size.Y-UiFont.GetHeight(fontSize:size))/2+UiFont.GetAscent(fontSize:size);
  UiLabel(text,x,baseline,size,color);
 }
 int briefingVisibleLines,briefingTotalLines;
 void DrawBriefing(FrontGrid grid){
  var portrait=new Rect2(grid.Left.Position.X,grid.Left.End.Y-144,grid.Left.Size.X,144);
  var globe=new Rect2(grid.Left.Position,new(grid.Left.Size.X,portrait.Position.Y-UiGap-grid.Left.Position.Y));
  UiFrame(globe,true);DrawBriefingGlobe(globe.Grow(-UiInset));
  UiFrame(portrait,true);UiImage(IsHuman?"HCOM":"ACOM",1,new(portrait.Position+new Vector2(UiGap,12),new(80,120)));
  float labelX=portrait.Position.X+112,labelWidth=portrait.End.X-UiGap-labelX;
  UiTextLine(IsTraining?"INSTRUCTOR":IsHuman?"EARTH COMMAND":"XIMAL TAAR",new(labelX,portrait.Position.Y+38,labelWidth,28),18,uiGreen);
  UiTextLine(BriefingStatus,new(labelX,portrait.Position.Y+72,labelWidth,24),14,uiDim);
  UiFrame(grid.Right,true);var text=grid.Right.Grow(-UiInset);
  float lineHeight=26,controlsY=text.End.Y-32,width=text.Size.X-24;
  LayoutBriefing(width);var lines=briefingLines;int visible=Math.Max(1,(int)((controlsY-UiGap-text.Position.Y)/lineHeight));
  briefingVisibleLines=visible;briefingTotalLines=lines.Count;int limit=Math.Max(0,lines.Count-visible);
  if(briefingFollow){int current=lines.FindLastIndex(l=>l.Start<briefingCharacters);briefingScroll=Math.Max(0,current-visible+1);}
  briefingScroll=Math.Clamp(briefingScroll,0,limit);int first=(int)briefingScroll;
  for(int i=first;i<Math.Min(lines.Count,first+visible);i++)DrawBriefingLine(i,text.Position.X,text.Position.Y+18+(i-first)*lineHeight);
  if(limit>0){
   var track=new Rect2(text.End.X-8,text.Position.Y,8,visible*lineHeight);DrawRect(Px(track),new Color("262826"));
   float thumb=Math.Max(24,track.Size.Y*visible/lines.Count);DrawRect(Px(new(track.Position.X,track.Position.Y+(track.Size.Y-thumb)*first/limit,8,thumb)),uiGreen);
   UiButton("brief-up","↑",new(text.Position.X,controlsY,40,32),"Previous briefing lines",first>0);
   UiButton("brief-down","↓",new(text.Position.X+48,controlsY,40,32),"More briefing lines",first<limit);
   UiTextLine($"{first+1}–{Math.Min(lines.Count,first+visible)} / {lines.Count} lines · Wheel / Page Up / Down",new(text.Position.X+104,controlsY,text.Size.X-104,32),14,uiDim);
  }else UiTextLine(BriefingRevealing?"Receiving transmission...":"End of briefing",new(text.Position.X,controlsY,text.Size.X,32),14,uiDim);
 }
}
