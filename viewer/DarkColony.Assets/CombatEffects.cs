namespace DarkColony.Assets;

public sealed record EffectColor(int R,int G,int B,int DestinationWeight);
// Original mode5: C + ((D-C)*A >>6), in5/6/5-bit components. Godot RGBA
// approximates this using alpha1-A/64; destination quantization differs explicitly.
public sealed class EffectPalette {
 public EffectColor[] Entries {get;}
 public EffectPalette(string text) {
  Entries=text.Split('\n').Select(s=>s.Trim()).Where(s=>s.Length>0&&char.IsDigit(s[0])).Select(s=>{
   var n=s.Split(',').Select(int.Parse).ToArray();if(n.Length!=4||n.Take(3).Any(x=>x<0||x>31)||n[3]<0||n[3]>63)throw new InvalidDataException("COLOUR.SET entry");return new EffectColor(n[0],n[1]*2,n[2],n[3]);}).ToArray();
  if(Entries.Length!=176)throw new NotSupportedException("Observed COLOUR.SET has176 contiguous entries");
 }
 public ushort Blend565(int index,ushort destination) {
  var c=Entries[index];int Mix(int d,int s)=>s+((d-s)*c.DestinationWeight>>6);
  return (ushort)(Mix(destination>>11,c.R)<<11|Mix((destination>>5)&63,c.G)<<5|Mix(destination&31,c.B));
 }
 public Raster Decode(SpriteCell cell) {
  if(cell.Coverage==null)throw new NotSupportedException("Effect requires retained RLE coverage");var rgba=new byte[cell.Indices.Length*4];
  for(int i=0;i<cell.Indices.Length;i++){if(cell.Coverage[i]==0)continue;int index=cell.Indices[i];if(index>=Entries.Length)throw new NotSupportedException("Effect index outside observed COLOUR.SET");var c=Entries[index];var rgb=GameColors.Expand((ushort)(c.R<<11|c.G<<5|c.B),PixelPacking.Rgb565);rgba[i*4]=(byte)rgb.R;rgba[i*4+1]=(byte)rgb.G;rgba[i*4+2]=(byte)rgb.B;rgba[i*4+3]=(byte)Math.Round((64-c.DestinationWeight)*255d/64);}
  return new(cell.Width,cell.Height,rgba);
 }
}
public sealed record CombatEffectPose(VisualAsset Visual,int BottomOffset,string Bank,int CellId,int FinFrame);
public sealed class CombatEffects {
 readonly Dictionary<int,CombatEffectPose[]> frames=new();
 public CombatEffectPose[] Projectile {get;}
 public CombatEffects(AnimationAsset fin,IReadOnlyDictionary<string,SpriteAsset> banks,EffectPalette palette,string family) {
  CombatEffectPose[] Decode(int index) {
   var f=fin.Frames[index];var result=new List<CombatEffectPose>();int ordinal=0;
   foreach(var c in f.Cells){int mode=c.UnknownWords[2];if(mode is 1 or 3)continue;
    if(mode!=5||c.UnknownWords[0]!=0||c.UnknownWords[1]!=16||c.UnknownWords[3]>1)throw new NotSupportedException($"Unsupported effect composition {family}:{index}:{c.Bank}:{string.Join(",",c.UnknownWords)}");
    // Dispatcher mode5 never reads mirror byte; 0/1 are retained but not applied.
    var cell=banks[c.Bank].Cells[c.CellId];var placement=Presentation.AnimationPlacement(c,cell);var pixels=palette.Decode(cell);
    result.Add(new(new($"{family}/effect/{index}/{ordinal++}",pixels,cell.Width,cell.Height,cell.Width,cell.Height,-placement.X,-placement.Y),c.Y,c.Bank,c.CellId,index));
   }return result.ToArray();
  }
  foreach(var sequence in fin.Animations.Where(a=>a.Name.StartsWith(family+"FIREA")))for(int i=sequence.First;i<=sequence.Last;i++)frames.Add(i,Decode(i));
  var bullet=fin.Animations.SingleOrDefault(a=>a.Name==family+"BULLET0");Projectile=bullet==null?Array.Empty<CombatEffectPose>():Decode(bullet.First);
 }
 public CombatEffectPose[] Firing(int finFrame)=>frames.TryGetValue(finFrame,out var poses)?poses:Array.Empty<CombatEffectPose>();
}
