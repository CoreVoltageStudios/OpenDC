using DarkColony.Assets;
namespace DarkColony.Viewer;

public sealed class OriginalVisuals : IVisualResolver {
 readonly Dictionary<string,VisualAsset> assets=new();
 public MapAsset Map {get;}
 public AnimationAsset Animation {get;}
 public TerrainOcclusion Occlusion {get;}
 public GifPalette Palette {get;}
 public SpriteAsset Sprites {get;}
 public TerrainAsset Terrain {get;}
 public FadeTable Fade {get;}
 public ScenarioTerrainHeader TerrainHeader {get;}
 public int InitialTerrainSelector=>TerrainSelectors.LowSelector(TerrainHeader.InitialCoefficient);
 public string MapId {get;}
 public int BottomOffset(int frame)=>Animation.Frames[16+frame].Cells[0].Y;
 public readonly List<object> Sources=new();
 public OriginalVisuals(string root,string mapPath="SCENARIO/MPLAYER/J2PLAY09.MAP",string terrainPath="SCENARIO/JUNGLE.BTS") {
  byte[] Read(string p){var b=File.ReadAllBytes(Path.Combine(root,p));Sources.Add(new{path=p,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b))});return b;}
  MapId=Path.GetFileNameWithoutExtension(mapPath);
  Map=Import.Map(Read(mapPath));
  var terrain=Import.Terrain(Read(terrainPath));
  Terrain=terrain;
  Fade=new(Read("FADE.DAT"));
  Occlusion=new(Map,terrain);
  Animation=Import.Animation(Read("ANIMATE/TRSC.FIN"));
  var bank=Import.Sprite(Read("SPRITES/TRSC.SPR"));
  Sprites=bank;
  // SCN first line -> basename -> interface palette override: 0x41bb3f / 0x41e99e.
  TerrainHeader=ScenarioTerrainHeader.Read(Read(Path.ChangeExtension(mapPath,"SCN")));
  var scn=TerrainHeader.TerrainName;
  if(!scn.Equals(Path.GetFileName(terrainPath),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SCN/terrain palette selection mismatch");
  Palette=GameColors.ReadGifPalette(Read(TerrainHeader.PaletteName));
  // Static SCN initialization supplies S; explicitly chosen bypass supplies level16.
  // This is presentation state, not a reconstruction of a running mission's cell history.
  byte terrainSelector=Fade.Lookup(16,16,0,InitialTerrainSelector);
  var terrainColors=GameColors.Table(Palette,terrainSelector&7,terrainSelector>>3,ColorBank.Terrain);
  var bodyColors=GameColors.Table(Palette,0,16,ColorBank.Body);
  var sequence=Animation.Animations.Single(a=>a.Name.Equals("TRSCMOVE0",StringComparison.OrdinalIgnoreCase));
  if(sequence.First!=16 || sequence.Last!=23)
   throw new InvalidDataException("Unsupported selected map/animation variant");
  void Add(string id,Raster raster,float w,float h,float ox=0,float oy=0)=>assets.Add(id,new(id,raster,raster.Width,raster.Height,w,h,ox,oy));
  Add("map/background",Presentation.Terrain(Map,terrain,false,terrainColors),Map.Width*32,Map.Height*32);
  Add("map/foreground",Presentation.Terrain(Map,terrain,true,terrainColors),Map.Width*32,Map.Height*32);
  for(int i=0;i<8;i++) {
   var f=Animation.Frames[16+i];Presentation.RequireSelectedFrame(f);
   if(f.Cells.Length!=1 || f.Cells[0].Bank!="trsc")throw new InvalidDataException("Unsupported selected composition");
   var c=f.Cells[0];var s=bank.Cells[c.CellId];var p=Presentation.AnimationPlacement(c,s);
   Add($"trsc/move0/{i}/scene",GameColors.Body(s,bodyColors),s.Width,s.Height,-p.X,-p.Y);
   Add($"trsc/move0/{i}/terrain",Presentation.Indexed(s.Indices,s.Width,s.Height,terrain.Palette6,true),s.Width,s.Height,-p.X,-p.Y);
   Add($"trsc/move0/{i}/embedded",Presentation.Indexed(s.Indices,s.Width,s.Height,bank.Palette6,true),s.Width,s.Height,-p.X,-p.Y);
  }
 }
 public VisualAsset Resolve(string id)=>assets.TryGetValue(id,out var v)?v:throw new KeyNotFoundException("Unknown visual ID: "+id);
}
