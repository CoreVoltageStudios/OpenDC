using Godot;
using DarkColony.Assets;
using System.Text.Json;
namespace DarkColony.Viewer;

public partial class Viewer {
 bool paletteLab;int palettePage;
 sealed record PaletteCase(int Team,int Light,PixelPacking Format);
 readonly PaletteCase[] paletteCases={new(0,16,PixelPacking.Rgb565),new(5,16,PixelPacking.Rgb565),new(7,16,PixelPacking.Rgb565),
  new(0,8,PixelPacking.Rgb565),new(5,8,PixelPacking.Rgb565),new(7,8,PixelPacking.Rgb565),
  new(0,0,PixelPacking.Rgb565),new(0,31,PixelPacking.Rgb565),new(5,16,PixelPacking.Rgb555)};
 readonly List<ImageTexture> paletteScenes=new(),paletteBodies=new();
 ColorTrace[] usedTraces=Array.Empty<ColorTrace>();
 ImageTexture FromRaster(Raster r){using var image=Image.CreateFromData(r.Width,r.Height,false,Image.Format.Rgba8,r.Rgba);return ImageTexture.CreateFromImage(image);}
 void PreparePaletteLab() {
  var c=visuals.Animation.Frames[16].Cells.Single();var s=visuals.Sprites.Cells[c.CellId];
  var v=visuals.Resolve("trsc/move0/0/scene");
  var used=s.Indices.Where((_,i)=>s.Coverage![i]!=0).Select(i=>(int)i).Distinct().Order().ToArray();
  usedTraces=used.Select(i=>GameColors.Trace(visuals.Palette,i,5,8,ColorBank.Body)).ToArray();
  var records=new List<object>();
  for(int i=0;i<paletteCases.Length;i++) {
   var state=paletteCases[i];var table=GameColors.Table(visuals.Palette,state.Team,state.Light,ColorBank.Body,state.Format);
   var body=GameColors.Body(s,table);paletteBodies.Add(FromRaster(body));
   var masked=visuals.Occlusion.ApplyGroundBody(v with{Pixels=body},1184,816,816+c.Y);
   var scene=new Raster(128,144,new byte[128*144*4]);
   Presentation.Blit(scene,visuals.Resolve("map/background").Pixels,-1120,-744);
   Presentation.Blit(scene,visuals.Resolve("map/foreground").Pixels,-1120,-744);
   Presentation.Blit(scene,masked,1184-(int)v.OriginX-1120,816-(int)v.OriginY-744);
   paletteScenes.Add(FromRaster(scene));
   int slot=i%6,x=24+slot%3*632,y=128+slot/3*430;
   records.Add(new{id=i,state.Team,state.Light,format=state.Format.ToString(),capture=i<6?"palette-teams":"palette-boundaries",
    sceneRect=new[]{x+16,y+74,256,288},bodyRect=new[]{x+362,y+80,s.Width*6,s.Height*6},
    traces=used.Select(index=>GameColors.Trace(visuals.Palette,index,state.Team,state.Light,ColorBank.Body,state.Format))});
  }
  File.WriteAllText(Path.Combine(output,"palette-lab.json"),JsonSerializer.Serialize(new{
   scope="Fixed FIN16 / SPR16, anchor1184,816 on HUMAN04; all team/light/packing choices are manual diagnostic states",
   terrain="JUNGLE.GIF first bank S0 L16 RGB565, held fixed across comparisons",
   conversion="Exact packed16 formula; modern RGBA8 expansion uses bit replication",
   sprite=new{s.Width,s.Height,s.OffsetX,s.OffsetY,c.X,c.Y,origin=new[]{v.OriginX,v.OriginY}},
   usedIndices=used,records,tableState=new{team=5,light=8,format="Rgb565"},table=usedTraces
  },new JsonSerializerOptions{WriteIndented=true}));
 }
 void LabText(string s,float x,float y,int size=20,Color? color=null)=>DrawString(font,new(x,y),s,HorizontalAlignment.Left,-1,size,color??ink);
 static Color Swatch(Rgb8 c)=>Color.Color8((byte)c.R,(byte)c.G,(byte)c.B);
 void DrawPaletteLab() {
  DrawSetTransform(Vector2.Zero);DrawRect(GetViewportRect(),new Color("101b1c"));
  // Fixed 1920x1080 diagnostic canvas, fitted proportionally if launched at another size.
  float scale=Math.Min(GetViewportRect().Size.X/1920,GetViewportRect().Size.Y/1080);
  DrawSetTransform((GetViewportRect().Size-new Vector2(1920,1080)*scale)/2,0,Vector2.One*scale);
  LabText("DARK COLONY  /  TRSC COLOUR TRACE",24,38,28,accent);
  LabText("Fixed FIN 16 / SPR 16  |  JUNGLE.GIF  |  manual team, light and pixel-format states",24,74);
  LabText("Original lookup arithmetic verified statically; running-original appearance has not been observed.",24,105,19,muted);
  if(palettePage<2) {
   int first=palettePage*6,last=Math.Min(first+6,paletteCases.Length);
   for(int i=first;i<last;i++) {
    int slot=i-first,x=24+slot%3*632,y=128+slot/3*430;var state=paletteCases[i];
    DrawRect(new(x,y,608,414),panel);
    LabText($"T{state.Team}   L{state.Light}/16   {state.Format}",x+16,y+32,24,accent);
    LabText("Fixed terrain + masked body",x+16,y+60,17,muted);LabText("Unoccluded pose",x+334,y+60,17,muted);
    DrawTextureRect(paletteScenes[i],new(x+16,y+74,256,288),false);
    for(int sy=0;sy<272;sy+=16)for(int sx=0;sx<176;sx+=16)
     DrawRect(new(x+350+sx,y+74+sy,16,16),new Color((sx/16+sy/16)%2==0?"334349":"25343a"));
    DrawTextureRect(paletteBodies[i],new(x+362,y+80,paletteBodies[i].GetWidth()*6,paletteBodies[i].GetHeight()*6),false);
    LabText("Anchor (1184,816) / 67 pixels masked",x+16,y+390,18,ink);
   }
  } else {
   int first=(palettePage-2)*15;
   LabText($"ALL USED INDICES  /  T5 L8 RGB565  /  rows {first+1}-{Math.Min(first+15,usedTraces.Length)} of {usedTraces.Length}",24,152,23,accent);
   string[] labels={"INDEX","SOURCE RGB8","MAP -> RGB8","MIXED RGB8","LIT RGB8","WORD","DISPLAY RGB8"};
   int[] columns={30,140,450,810,1120,1420,1580};
   for(int j=0;j<labels.Length;j++)LabText(labels[j],columns[j],196,20,muted);
   for(int j=0;j<15 && first+j<usedTraces.Length;j++) {
    var t=usedTraces[first+j];int y=218+j*50;
    DrawRect(new(24,y,1872,46),new Color(j%2==0?"1a2b2c":"142324"));
    string[] cells={t.Index.ToString(),t.Source.ToString(),$"{t.MappedIndex:D3} -> {t.Mapped}",t.Mixed.ToString(),t.Lit.ToString(),$"{t.Packed:X4}",t.Display.ToString()};
    for(int k=0;k<cells.Length;k++)LabText(cells[k],columns[k],y+31,22,t.Index is >=138 and <=143?accent:ink);
    DrawRect(new(1810,y+7,68,32),Swatch(t.Display));
   }
  }
  LabText("Terrain held at S0 L16 RGB565. Body light L16 is supported by normal enqueue; other L values probe the table.",24,1012,19,muted);
  LabText("RGB8 display uses bit replication of packed16. RLE skips preserve holes; opaque black stays opaque.",24,1040,19,muted);
  LabText($"Page {palettePage+1}/5    Left / Right: page    F12: capture    Esc: quit",24,1069,18,accent);
  DrawSetTransform(Vector2.Zero);
 }
 void PaletteInput(InputEvent e) {
  if(e is not InputEventKey k || !k.Pressed || k.Echo)return;
  if(k.Keycode==Key.Escape)GetTree().Quit();
  if(k.Keycode is Key.Right or Key.Left){palettePage=(palettePage+(k.Keycode==Key.Right?1:4))%5;inputEvents++;QueueRedraw();}
  if(k.Keycode==Key.F12)CaptureManual();
 }
 public async void PaletteSuite() {
  try {
   for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   palettePage=0;await Save("palette-teams");palettePage=1;await Save("palette-boundaries");
   for(int i=0;i<3;i++){palettePage=i+2;await Save($"indices-{i+1}");}
   await Send(Key.Right);Check(palettePage==0 && inputEvents>0,"palette page change via Godot input");
   await Save("palette-teams-repeat");
   File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks));
   GD.Print("PALETTE_SUITE_PASS");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());GetTree().Quit(1);}
 }
}
