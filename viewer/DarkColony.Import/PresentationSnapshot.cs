namespace DarkColony.ContentImport;

// A small, detached byte snapshot, not a lease on an importer staging directory.
// Published only after the base contract files have passed their per-file hashes.
// Presentation can retain it across cancellation/activation without open file handles.
public sealed class PresentationSnapshot {
 public static readonly string[] Paths={"DESERT.GIF","COLOUR.SET","SCENARIO/DESERT.BTS","SCENARIO/HUMAN/HUMAN01.MAP","ANIMATE/HUBU.FIN","ANIMATE/TOWR.FIN","ANIMATE/BEAC.FIN","ANIMATE/DISH.FIN","ANIMATE/TRSC.FIN","SPRITES/BEAC.SPR","SPRITES/DISH.SPR","SPRITES/GLAT.SPR","SPRITES/GLIT.SPR","SPRITES/HUBU.SPR","SPRITES/TOWR.SPR","SPRITES/TRSC.SPR"};
 readonly Dictionary<string,byte[]> files;
 PresentationSnapshot(Dictionary<string,byte[]> files)=>this.files=files;
 public byte[] Read(string path)=>(byte[])files[path].Clone();
 public static PresentationSnapshot ReadVerified(string dataRoot){
  var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
  foreach(string path in Paths){var rule=ContentContract.Current.BaseFiles.Single(f=>f.Path.Equals(path,StringComparison.OrdinalIgnoreCase));var bytes=File.ReadAllBytes(Path.Combine(dataRoot,path));var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));if(!rule.Hashes.Contains(hash))throw new InvalidDataException("Restoration source verification failed: "+path);files.Add(path,bytes);}
  return new(files);
 }
}
