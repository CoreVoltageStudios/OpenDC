using Godot;
namespace DarkColony.Viewer;
public partial class Viewer {
 void DrawSurfaceOptions(float x,float y){
   UiButton("game-content","Game content",new(x+690,y+240,300,48),mainMenu?"Inspect, import or repair original content. Player saves are separate.":"Return to the main menu to manage game content.",mainMenu);
   foreach(var opt in new[]{("sfx",$"Effects & voices   {sfxVolume:P0}"),("music",$"CD music   {musicVolume:P0}"),("ui",$"Interface scale   {ui:0.##}×")}){UiLabel(opt.Item2,x,y+30,21);UiButton(opt.Item1+"-","−",new(x+320,y,143,40),"Decrease",opt.Item1=="sfx"?sfxVolume>0:opt.Item1=="music"?musicVolume>0:ui>.75f);UiButton(opt.Item1+"+","+",new(x+479,y,143,40),"Increase",opt.Item1=="sfx"?sfxVolume<1:opt.Item1=="music"?musicVolume<1:ui<1.5f);y+=44;}
   UiLabel("World artwork",x,y+30,21);UiButton("art-classic","Classic",new(x+320,y,143,40),"Softer, smoothed original artwork.",active:nativeSampling!=0);UiButton("art-crisp","Crisp",new(x+479,y,143,40),"Sharp, unfiltered source pixels.",active:nativeSampling==0);y+=44;
   UiLabel("Mouse-edge scrolling",x,y+30,21);UiButton("edge-toggle",edgeScrolling?"On":"Off",new(x+320,y,302,40),"Scroll at the edges of the active game area. Saved across launches.",active:edgeScrolling);y+=44;
   UiLabel($"Scroll speed   {edgeSpeed:0.##}x",x,y+30,21);UiButton("edge-","-",new(x+320,y,143,40),"Slower edge scrolling",edgeSpeed>.25f);UiButton("edge+","+",new(x+479,y,143,40),"Faster edge scrolling",edgeSpeed<2);y+=44;
   UiLabel("Game speed",x,y+30,21);UiButton("speed-classic","Classic 100%",new(x+320,y,143,40),IsNetworkMatch?"Host controls this session's speed.":"Original fixed simulation rate.",enabled:!IsNetworkMatch||network!.Host,active:!fastGame);UiButton("speed-fast","Fast 150%",new(x+479,y,143,40),IsNetworkMatch?"1.5x simulation. Host controls this session's speed.":"1.5x simulation. Audio plays normally. Saved.",enabled:!IsNetworkMatch||network!.Host,active:fastGame);y+=44;
   UiLabel("Maximum aspect ratio",x,y+30,21);
   foreach(var choice in new[]{(0,"Full width"),(16,"16:9"),(21,"21:9")}){int i=choice.Item1==0?0:choice.Item1==16?1:2;UiButton("aspect:"+choice.Item1,choice.Item2,new(x+320+(i==0?0:i==1?150:230),y,i==0?142:72,40),"Center the entire game with side bars on wider screens. Independent of interface scale and world zoom.",active:maximumAspect==choice.Item1);}y+=60;
   UiButton("playtesting","Playtesting",new(x+320,y-12,302,40),"Session-only playtesting options");y+=38;
   UiWrap(artworkPreferenceDiagnostic??"Display, artwork, speed, camera and audio preferences are saved.",x,y,620,14,uiDim,2);

 }
}
