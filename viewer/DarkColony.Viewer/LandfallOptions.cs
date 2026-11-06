using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 void DrawLandfallOptions(){
  missionHits.Clear();float x,y,width,height;
  if(mainMenu){LandfallHeading("Options","Make yourself at home");x=ReadingLeft;y=UiSize.Y<850?212:278;width=Math.Min(850,UiSize.X-x-64);height=UiSize.Y-y-88;}
  else{DrawRect(OverlayRect,new Color(0,0,0,.72f));width=Math.Min(800,OverlayWidth-96);height=Math.Min(670,UiSize.Y-220);x=(OverlayWidth-width)/2;y=(UiSize.Y-height-150)/2+100;LandfallFrame(new(x-24,y-84,width+48,height+150));UiLabel("OPTIONS · "+BattleStatus.ToUpperInvariant(),x,y-42,28);}
  var rows=new List<(string Label,string[] Ids,string[] Values,string Help)>();
  void Group(string label)=>rows.Add((label,Array.Empty<string>(),Array.Empty<string>(),""));
  void Row(string label,string id,string value,string help)=>rows.Add((label,new[]{id},new[]{value},help));
  Group("DISPLAY");rows.Add(($"Interface size  {ui:0.###}×",new[]{"ui-","ui+"},new[]{"−","+"},"Interface size is independent of world zoom."));
  rows.Add(("World artwork",new[]{"art-classic","art-crisp"},new[]{"Classic","Crisp"},"Original artwork with smooth or unfiltered sampling."));
  rows.Add(("Maximum width",new[]{"aspect:0","aspect:16","aspect:21"},new[]{"Full width","16:9","21:9"},"Center the complete game within this maximum aspect ratio."));
  Row("Reduce motion","reduce-motion",reducedMotion?"On":"Off","Overrides Windows animation preference. Keeps original briefing content.");
  Group("CAMERA");Row("Mouse-edge scrolling","edge-toggle",edgeScrolling?"On":"Off","Scroll at the active game boundary.");
  rows.Add(($"Scroll speed  {edgeSpeed:0.##}×",new[]{"edge-","edge+"},new[]{"−","+"},"Arrow keys remain available. No WASD shortcuts."));
  Group("SOUND");rows.Add(($"Effects and voices  {sfxVolume:P0}",new[]{"sfx-","sfx+"},new[]{"−","+"},"Includes briefing typing, original voices and restoration effects."));rows.Add(($"CD music  {musicVolume:P0}",new[]{"music-","music+"},new[]{"−","+"},"Music requires the original CD audio tracks. Voices and effects are separate."));
  Group("GAME");rows.Add((IsNetworkMatch?"Game speed · set by host":"Game speed",new[]{"speed-classic","speed-fast"},new[]{"Classic 100%","Fast 150%"},"Simulation speed does not change audio pitch or menu timing."));
  Row("Controls","controls","View ›","Current camera, selection and order bindings.");Row("Game content","game-content",mainMenu?"Manage ›":"From main menu","Inspect imported content, add Council Wars or replay restoration.");
  if(!IsNetworkMatch){Group("PLAYTESTING");Row("God mode · session only","god-mode",godMode?"On":"Off","Invulnerability and temporary mission access. No permanent completions while enabled.");}
  int count=Math.Max(1,(int)(height/46));optionScroll=Math.Clamp(optionScroll,0,Math.Max(0,rows.Count-count));
  for(int n=(int)optionScroll;n<Math.Min(rows.Count,(int)optionScroll+count);n++){var row=rows[n];float at=y+(n-optionScroll)*46;if(row.Ids.Length==0){UiLabel(row.Label,x,at+29,14,OdcUiTokens.Signal);continue;}UiTextLine(row.Label,new(x,at,width*.49f,40),21);float cw=(width*.5f-8*(row.Ids.Length-1))/row.Ids.Length;
   for(int i=0;i<row.Ids.Length;i++){string id=row.Ids[i];bool active=id=="art-classic"&&nativeSampling!=0||id=="art-crisp"&&nativeSampling==0||id=="speed-fast"&&fastGame||id=="speed-classic"&&!fastGame||id=="aspect:"+maximumAspect;bool enabled=!(id=="game-content"&&!mainMenu)&&!(IsNetworkMatch&&id.StartsWith("speed-")&&!network!.Host);
    UiButton(id,row.Values[i],new(x+width*.5f+i*(cw+8),at,cw,40),row.Help,enabled,active);
   }
  }
  if(rows.Count>count){UiButton("options-up","↑",new(x+width-100,y+height+8,44,36),"Scroll options upward",optionScroll>0);UiButton("options-down","↓",new(x+width-48,y+height+8,44,36),"Scroll options downward",optionScroll+count<rows.Count);}
  if(!mainMenu)UiButton("close","‹  Return   Esc",new(x,y+height+8,230,38),"Return without resuming until battle is selected");
  if(mainMenu&&UiSize.X-x-width>360){var help=missionHits.FirstOrDefault(h=>h.Id==focusedUi)?.Help??"Use the mouse, Tab or arrow keys. Settings are saved locally; God mode lasts for this session.";UiWrap(help,x+width+56,y+28,Math.Min(380,UiSize.X-x-width-100),22,OdcUiTokens.Secondary);}
 }
}
