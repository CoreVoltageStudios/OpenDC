using Godot;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;
public partial class Viewer {
 bool terrainSelectorLab;int selectorPage;
 readonly List<ImageTexture> selectorImages=new(),selectorTerrain=new();
 readonly string[] selectorNames={"initialized","bypass","controlled"};
 readonly string[] selectorTitles={"STATIC INITIALIZATION / before object updates","EXPLICIT BYPASS / original supported branch","CONTROLLED BOUNDARY / manual formula fixture"};
 readonly string[] selectorNotes={"Every cell word = 0x000003ff. Bit31 clear -> level0. This is not a playable mission snapshot.",
  "Bypass forces level16 at every cell. No spatial transition is supported under this condition.",
  "Manual words 000003ff / 800003ff / c00003ff; coefficient128 -> S3. Not reconstructed mission history."};
 void PrepareTerrainSelectorLab() {
  var f=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"fixtures/terrain-selectors.json"))).RootElement;
  int ox=f.GetProperty("tileX").GetInt32(),oy=f.GetProperty("fileRow").GetInt32();
  uint mask=f.GetProperty("ownerMask").GetUInt32();int w=visuals.Map.Width,h=visuals.Map.Height;
  var records=new List<object>();
  for(int i=0;i<3;i++) {
   var words=Enumerable.Repeat(0x3ffu,w*h).ToArray();
   if(i==2)for(int z=0;z<h;z++)for(int x=0;x<w;x++)words[z*w+x]=(x<36 || h-1-z<23)?0x3ffu:x<40?0x800003ffu:0xc00003ffu;
   int coefficient=i==2?128:visuals.TerrainHeader.InitialCoefficient;
   var surface=TerrainSelectors.Build(visuals.Fade,words,w,h,ox,oy,16,14,mask,i==1,coefficient);
   var terrain=TerrainSelectors.Render(visuals.Map,visuals.Terrain,visuals.Palette,surface,false);
   var foreground=TerrainSelectors.Render(visuals.Map,visuals.Terrain,visuals.Palette,surface,true);
   Presentation.Blit(terrain,foreground,0,0);
   var heat=TerrainSelectors.Visualize(surface);
   selectorImages.Add(FromRaster(heat));selectorTerrain.Add(FromRaster(terrain));
   string name=selectorNames[i];
   File.WriteAllBytes(Path.Combine(output,name+"-selectors.bin"),surface.Values);
   File.WriteAllBytes(Path.Combine(output,name+"-terrain.rgba"),terrain.Rgba);
   File.WriteAllBytes(Path.Combine(output,name+"-foreground.rgba"),foreground.Rgba);
   using(var file=new BinaryWriter(File.Create(Path.Combine(output,name+"-words.bin"))))foreach(uint word in words)file.Write(word);
   using(var png=Image.CreateFromData(512,448,false,Image.Format.Rgba8,terrain.Rgba))png.SavePng(Path.Combine(output,name+"-terrain.png"));
   using(var png=Image.CreateFromData(512,448,false,Image.Format.Rgba8,heat.Rgba))png.SavePng(Path.Combine(output,name+"-selectors.png"));
   records.Add(new{name,coefficient,lowSelector=TerrainSelectors.LowSelector(coefficient),bypass=i==1,ownerMask=mask,
    histogram=surface.Values.GroupBy(v=>(int)v).OrderBy(g=>g.Key).Select(g=>new{selector=g.Key,level=g.Key>>3,pixels=g.Count()}),
    sourceRect=new[]{0,0,384,320},heatRect=new[]{64,200,768,640},terrainRect=new[]{1000,200,768,640}});
  }
  File.WriteAllText(Path.Combine(output,"selector-lab.json"),JsonSerializer.Serialize(new{
   map="SCENARIO/HUMAN/HUMAN04.MAP",mapWidth=w,mapHeight=h,tileX=ox,fileRow=oy,width=512,height=448,
   scenario=new{visuals.TerrainHeader.Phase,visuals.TerrainHeader.Period,visuals.TerrainHeader.Counter,visuals.TerrainHeader.Ramp,visuals.TerrainHeader.InitialCoefficient},
   meaning="Upper5 = integer level; lower3 = coefficient-derived S. No mission history or running original observed.",records
  },new JsonSerializerOptions{WriteIndented=true}));
 }
 void DrawTerrainSelectorLab() {
  DrawSetTransform(Vector2.Zero);DrawRect(GetViewportRect(),new Color("101b1c"));
  float scale=Math.Min(GetViewportRect().Size.X/1920,GetViewportRect().Size.Y/1080);
  DrawSetTransform((GetViewportRect().Size-new Vector2(1920,1080)*scale)/2,0,Vector2.One*scale);
  LabText("OpenDC / TERRAIN SELECTOR TRACE",48,48,28,accent);
  LabText(selectorTitles[selectorPage],48,91,25,ink);
  LabText(selectorNotes[selectorPage],48,128,20,muted);
  LabText("Selector upper bits: level 0..16",64,180,22,accent);LabText("JUNGLE.GIF / original terrain bank / RGB565",1000,180,22,accent);
  var crop=new Rect2(0,0,384,320);
  DrawTextureRectRegion(selectorImages[selectorPage],new(64,200,768,640),crop);
  DrawTextureRectRegion(selectorTerrain[selectorPage],new(1000,200,768,640),crop);
  LabText("LEVEL",64,894,18,muted);
  for(int l=0;l<=16;l++){int x=144+l*40;DrawRect(new(x,864,32,32),Color.Color8((byte)(l*15),(byte)(l*15),(byte)(l*15)));LabText(l.ToString(),x+3,923,17,ink);}
  LabText($"S = {(selectorPage==2?3:visuals.InitialTerrainSelector)}   selector byte = (level << 3) | S",1000,894,22,ink);
  LabText("Native surface 512 x 448; shown crop 384 x 320 at 2x. File-row origin (32,20), source pixels (1024,640).",48,971,21,muted);
  LabText("Static formulas checked independently. Initial grid and bypass are constant; variation above is manual input only.",48,1008,20,muted);
  LabText($"{selectorPage+1}/3   Left / Right: case    F12: capture    Esc: quit",48,1054,20,accent);
  DrawSetTransform(Vector2.Zero);
 }
 void SelectorInput(InputEvent e) {
  if(e is not InputEventKey k || !k.Pressed || k.Echo)return;
  if(k.Keycode==Key.Escape)GetTree().Quit();
  if(k.Keycode is Key.Right or Key.Left){selectorPage=(selectorPage+(k.Keycode==Key.Right?1:2))%3;inputEvents++;QueueRedraw();}
  if(k.Keycode==Key.F12)CaptureManual();
 }
 public async void SelectorSuite() {
  try {
   for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   for(int i=0;i<3;i++){selectorPage=i;await Save("case-"+selectorNames[i]);}
   await Send(Key.Right);Check(selectorPage==0&&inputEvents>0,"selector paging via Godot input");
   await Save("case-initialized-repeat");
   File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));GD.Print("TERRAIN_SELECTOR_SUITE_PASS");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());GetTree().Quit(1);}
 }
}
