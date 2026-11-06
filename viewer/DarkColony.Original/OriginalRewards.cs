using DarkColony.Assets;
namespace DarkColony.Original;

// 403a48 frontend, WINGAMEE labels, KNOBE.FIN and MDL[a-l].SPR. No reward predicates here.
public sealed class OriginalRewards {
 readonly OriginalDefinitions data;readonly AnimationAsset fin;readonly SpriteAsset icons;
 readonly Dictionary<int,SpriteAsset> medals=new();
 public string[] GrayRanks {get;}
 public string[] HumanRanks {get;}
 public Dictionary<int,string> Descriptions {get;}=new();
 public OriginalRewards(OriginalDefinitions data) {
  this.data=data;fin=data.Animation("KNOBE");icons=data.Sprite("KNOBE");
  HumanRanks=data.Reference.Ints(0x488cec,4).Select(p=>data.Reference.Text((uint)p).Trim()).ToArray();
  GrayRanks=data.Reference.Ints(0x488cfc,4).Select(p=>data.Reference.Text((uint)p).Trim()).ToArray();
  foreach(var line in OriginalScenarioImport.Lines(data.Read("INTRFACE/WINGAMEE"),"WINGAMEE")) {
   var p=line.Text.Split((char[]?)null,3,StringSplitOptions.RemoveEmptyEntries);
   if(p.Length==3&&p[0]=="textmsg"&&int.TryParse(p[1],out int n)&&n>=3&&n<=14)Descriptions[n-3]=p[2];
  }
 }
 public SpriteAsset Medal(int id) {
  if(id is <0 or >11)throw new ArgumentOutOfRangeException(nameof(id));
  if(!medals.TryGetValue(id,out var s))medals[id]=s=Import.Sprite(data.Read($"INTRFACE/MDL{(char)('A'+id)}.SPR"));return s;
 }
 public Raster Icon(string name,int frame) {
  var a=fin.Animations.Single(a=>a.Name==name);var f=fin.Frames[a.First+frame];
  if(a.First+frame>a.Last||f.Cells.Length!=1)throw new InvalidDataException("Reward icon frame");
  return Pixels(icons,icons.Cells[f.Cells[0].CellId]);
 }
 public static Raster Pixels(SpriteAsset bank,SpriteCell cell) {
  var raster=Presentation.Indexed(cell.Indices,cell.Width,cell.Height,bank.Palette6,false);
  if(cell.Coverage!=null)for(int i=0;i<cell.Indices.Length;i++)raster.Rgba[i*4+3]=cell.Coverage[i];return raster;
 }
}
// 428aa8 starts at1; mode4 -> state5 at428c6c, strict elapsed>66ms, one frame then hold last.
public sealed class OriginalRewardAnimation {
 public int Display {get;private set;}public int Next {get;private set;}=1;public double Last {get;private set;}
 public void Step(double milliseconds,int cells) {
  if(Next>=cells||milliseconds-Last<=66)return;
  Display=Next++;Last=milliseconds;
 }
}
