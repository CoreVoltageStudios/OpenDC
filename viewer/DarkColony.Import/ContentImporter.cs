using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DarkColony.Media;
using DarkColony.Assets;
namespace DarkColony.ContentImport;

public sealed class ImportPlan:IDisposable {
 public PresentationSnapshot? VerifiedPresentation {get;internal set;}
 internal string Workspace="";internal FileStream? Lock;internal ContentStore Store=null!;internal List<ImportSource> Sources=new();internal Dictionary<ImportSource,string> Identities=new();
 public SourceFinding[] Findings {get;internal set;}=Array.Empty<SourceFinding>();
 public bool HasBase=>Identities.Values.Contains("base");public bool HasCouncil=>Identities.Values.Contains("council");
 public void Dispose(){try{if(Workspace.Length>0&&Directory.Exists(Workspace)&&File.Exists(Path.Combine(Workspace,".odc-import-owned")))SafePath.DeleteOwnedStage(Workspace);}finally{Lock?.Dispose();Lock=null;}}
}
public static class ContentImporter {
 public static string ConversionRuntime=>System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription+"; System.IO.Compression "+typeof(System.IO.Compression.GZipStream).Assembly.GetName().Version;
 const long Reserve=7L*1024*1024*1024;
 public static void CheckFreeSpace(long available){if(available<Reserve)throw new IOException("Import requires at least 7 GiB free for staging and conversion; existing content is retained.");}
 static readonly ContentContract C=ContentContract.Current;
 public static Task<ImportPlan> InspectAsync(ContentStore store,IReadOnlyList<string> sources,CancellationToken cancel,Action<ImportProgress>? progress=null)=>Task.Run(()=>{
  var plan=new ImportPlan{Store=store};try{
   if(sources.Count is <1 or >3)throw new InvalidDataException("Choose one base source and optionally one Council Wars source");
   foreach(string path in sources){string p=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,d=store.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(d.StartsWith(p,StringComparison.OrdinalIgnoreCase)||p.StartsWith(d,StringComparison.OrdinalIgnoreCase))throw new IOException("Import destination must be separate from the original source");}
   plan.Lock=store.Lock();CheckFreeSpace(new DriveInfo(Path.GetPathRoot(store.DirectoryPath)!).AvailableFreeSpace);SafePath.NoLinks(Path.Combine(store.DirectoryPath,"staging"));SafePath.NoLinks(Path.Combine(store.DirectoryPath,"generations"));
   plan.Workspace=Path.Combine(store.DirectoryPath,"staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(plan.Workspace);File.WriteAllText(Path.Combine(plan.Workspace,".odc-import-owned"),"OpenDC importer contract1");
   var findings=new List<SourceFinding>();foreach(string path in sources){cancel.ThrowIfCancellationRequested();progress?.Invoke(new(ImportStage.Inspecting,"Inspecting original files",Detail:Path.GetFileName(path)));var s=ImportSource.Open(path,Path.Combine(plan.Workspace,"source-"+plan.Sources.Count),cancel,progress);plan.Sources.Add(s);string? ex=s.Find("EXPENG/ENGEXP16.EXE")??s.Find("ENGEXP16.EXE"),kind="council";if(ex==null){ex=s.Find("DC16.EXE");kind="base";}if(ex==null)throw new InvalidDataException("No supported game executable data found. An installed folder may be incomplete; select the original disc image or extracted disc.");string hash=Convert.ToHexString(SHA256.HashData(s.Read(ex)));if(hash!=(kind=="base"?C.BaseNative:C.CouncilNative))throw new InvalidDataException("Unsupported "+kind+" executable data SHA-256 "+hash+". This build supports verified English EU base / English Council Wars mappings only.");plan.Identities.Add(s,kind);
    var rules=kind=="base"?C.BaseFiles:C.CouncilFiles.Where(f=>f.Origin!="base").ToArray();var missing=rules.Where(f=>Resolve(s,f,kind)==null).Select(f=>f.Path).ToArray();var warnings=s.Warnings.ToList();if(s.Audio.Count==0)warnings.Add("CD music is unavailable from this source. Briefing voices and sound effects remain available.");findings.Add(new(path,s.Kind,kind=="base"?C.BaseEdition:C.CouncilEdition,missing,warnings.ToArray(),missing.Length==0));}
   if(plan.Identities.Values.GroupBy(k=>k).Any(g=>g.Count()>1))throw new InvalidDataException("Multiple sources for the same content identity. Select one complete disc source at a time.");
   if(!plan.HasBase&&store.ActiveRoot==null)throw new InvalidDataException("Council Wars requires an imported base game. Select both sources, or import the base game first.");
   plan.Findings=findings.ToArray();progress?.Invoke(new(ImportStage.AwaitingConfirmation,"Review the detected edition and content before importing"));return plan;
  }catch{plan.Dispose();throw;}
 },cancel);
 static string? Resolve(ImportSource source,RequiredFile file,string kind){
  if(kind=="base")return source.Find("DC/"+file.Path)??source.Find(file.Path);
  if(file.Origin=="exp")return source.Find("EXPENG/EXP/"+file.Path)??source.Find("EXP/"+file.Path);
  return source.Find("DC/"+file.Path);
 }
 public static Task<ImportReceipt> ImportAsync(ImportPlan plan,bool music,CancellationToken cancel,Action<ImportProgress>? progress=null)=>Task.Run(()=>{
  string stage=Path.Combine(plan.Workspace,"prepared");Directory.CreateDirectory(stage);var receipts=new List<AssetReceipt>();var warnings=plan.Findings.SelectMany(f=>f.Warnings).ToList();var watch=Stopwatch.StartNew();
  void Record(string path,string source,string input,string operation,string version,string parameters=""){string relative=Path.GetRelativePath(stage,path).Replace('\\','/');receipts.RemoveAll(r=>r.Path==relative);receipts.Add(new(relative,new FileInfo(path).Length,ContentContract.Hash(path),source,input,operation,version,parameters,File.GetLastWriteTimeUtc(path).Ticks));}
  void Inherit(){
   string? active;ImportReceipt old;
   try{active=plan.Store.ActiveRoot;if(active==null)return;old=ContentStore.ReadReceipt(active);}
   catch(Exception e)when(plan.HasBase&&(e is IOException or InvalidDataException or JsonException)){warnings.Add("The previous installation record could not be read. Content is being rebuilt from the selected source; previous generations are retained. Optional Council Wars must be imported again if not selected. "+e.Message);return;}
   bool CouncilPath(string p)=>p.StartsWith("private/CouncilWars/")||p.StartsWith("generated/council-wars-first/");
   bool MusicPath(string p)=>p.StartsWith("generated/original-music/")||p.StartsWith("generated/council-wars-first/music/");
   bool Valid(AssetReceipt r){cancel.ThrowIfCancellationRequested();string p=SafePath.Within(active,r.Path);return File.Exists(p)&&new FileInfo(p).Length==r.Bytes&&ContentContract.Hash(p)==r.Sha256;}
   bool omitCouncil=plan.HasBase&&!plan.HasCouncil&&old.CouncilEdition!=null&&ContentStore.ValidateRoot(active,true,cancel,null).Any(p=>CouncilPath(p)&&!MusicPath(p));
   if(omitCouncil)warnings.Add("Previously imported Council Wars needs repair and was left in the retained generation. Base-game content is ready; reimport Council Wars to enable it again.");
   foreach(var r in old.Files){cancel.ThrowIfCancellationRequested();if(omitCouncil&&CouncilPath(r.Path))continue;
    bool replaced=plan.HasBase&&r.Path.StartsWith("private/DC/")||plan.HasBase&&(r.Path.StartsWith("generated/original-")||r.Path.StartsWith("generated/first-strike-ui/"))||plan.HasCouncil&&CouncilPath(r.Path);
    string identity=CouncilPath(r.Path)?"council":"base";var source=plan.Identities.FirstOrDefault(p=>p.Value==identity).Key;
    bool retainMusic=music&&MusicPath(r.Path)&&source!=null&&source.Audio.Count==0;
    if(replaced&&!retainMusic)continue;
    if(MusicPath(r.Path)&&!Valid(r)){warnings.Add("An invalid optional CD music file was omitted: "+r.Path);continue;}
    string src=SafePath.Within(active,r.Path),dest=SafePath.Within(stage,r.Path);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(src,dest);if(ContentContract.Hash(dest)!=r.Sha256)throw new InvalidDataException("Existing content needs repair: "+r.Path);receipts.Add(r with{Stamp=File.GetLastWriteTimeUtc(dest).Ticks});
   }
  }

  try{
   if(plan.Findings.Any(f=>!f.Compatible))throw new InvalidDataException("Required disc files are missing:\n"+string.Join("\n",plan.Findings.SelectMany(f=>f.Missing).Take(25))+"\nSelect a complete original disc/image; installed folders often omit movies and voices.");
   Inherit();foreach(string identity in new[]{"base","council"}){
    var source=plan.Identities.FirstOrDefault(k=>k.Value==identity).Key;if(source==null)continue;bool cw=identity=="council";string data=Path.Combine(stage,"private",cw?"CouncilWars":"DC");var rules=cw?C.CouncilFiles:C.BaseFiles;int done=0;
    foreach(var f in rules){cancel.ThrowIfCancellationRequested();string target=SafePath.Within(data,f.Path);if(cw&&f.Origin=="base"){string src=SafePath.Within(Path.Combine(stage,"private/DC"),f.Path);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(src,target);}else source.Copy(Resolve(source,f,identity)??throw new FileNotFoundException(f.Path),target,cancel);string hash=ContentContract.Hash(target);if(!f.Hashes.Contains(hash))throw new InvalidDataException("Unsupported, mixed or damaged file: "+f.Path+" ("+hash+")");Record(target,cw&&f.Origin=="base"?receipts.Single(r=>r.Path=="private/DC/"+f.Path).Source:source.Selected+" :: "+Resolve(source,f,identity),hash,"byte-exact copy","1");if(++done%16==0)progress?.Invoke(new(ImportStage.Copying,cw?"Preparing Council Wars files":"Preparing base-game files",done,rules.Length,f.Path));}
    if(!cw){try{plan.VerifiedPresentation=PresentationSnapshot.ReadVerified(data);progress?.Invoke(new(ImportStage.Copying,"Base artwork verified",rules.Length,rules.Length){Presentation=plan.VerifiedPresentation});}catch(Exception e)when(e is IOException or InvalidDataException){progress?.Invoke(new(ImportStage.Copying,"Preparing content",Detail:"Restoration artwork unavailable: "+e.Message));}}
    string exe=source.Find(cw?"EXPENG/ENGEXP16.EXE":"DC16.EXE")??source.Find("ENGEXP16.EXE")!;var tables=cw?NativeTables.Council(source.Read(exe),File.ReadAllBytes(Path.Combine(stage,"private/DC/native-tables.json"))):NativeTables.Base(source.Read(exe));string native=Path.Combine(data,"native-tables.json");File.WriteAllBytes(native,tables);Record(native,source.Selected+" :: "+exe,cw?C.CouncilNative:C.BaseNative,"extract canonical data spans",C.Converters["native"]);
    if(cw){string marker=Path.Combine(data,"odc-content.json");File.WriteAllText(marker,JsonSerializer.Serialize(new{pack="council-wars",revision=1,native=C.CouncilNative}));Record(marker,"content-contract",C.CouncilNative,"content identity","1");}
    string movies=Path.Combine(stage,cw?"generated/council-wars-first/video":"generated/original-video/v1");var movieRules=rules.Where(r=>r.Role=="movie").ToArray();done=0;
    foreach(var f in movieRules){cancel.ThrowIfCancellationRequested();string input=Path.Combine(data,f.Path),target=Path.Combine(movies,Path.GetFileNameWithoutExtension(f.Path).ToLowerInvariant()+".odcv");Directory.CreateDirectory(movies);string hash=ContentContract.Hash(input),baseMovie=Path.Combine(stage,"generated/original-video/v1",Path.GetFileName(target));var reuse=cw?receipts.FirstOrDefault(r=>r.Path==Path.GetRelativePath(stage,baseMovie).Replace('\\','/')&&r.SourceHash==hash):null;
     string detail;if(reuse!=null){File.Copy(baseMovie,target);detail="Reused identical source conversion; "+reuse.Parameters;}else{var result=AviConverter.Convert(input,target,cancel,(n,total)=>progress?.Invoke(new(ImportStage.Converting,"Preparing original movies: "+Path.GetFileName(input),n,total,$"Movie {done+1}/{movieRules.Length}")));detail=JsonSerializer.Serialize(result);}
     Record(target,(cw?"private/CouncilWars/":"private/DC/")+f.Path,hash,"Cinepak RGB + PCM, preserved AVI frame timeline",C.Converters["cinepak"]+"/"+C.Converters["movie"],detail);done++;
    }
    if(!cw)foreach(var skin in new[]{("mission-frame","INTRFACE",0,0,518,460),("console-frame","SHUMAN",0,0,304,305),("circuit","WINGAME",113,0,252,160)}){string input=Path.Combine(data,"INTRFACE",skin.Item2+".GIF"),target=Path.Combine(stage,"generated/first-strike-ui/skin",skin.Item1+".png");GifSkin.Crop(File.ReadAllBytes(input),target,skin.Item3,skin.Item4,skin.Item5,skin.Item6);Record(target,"INTRFACE/"+skin.Item2+".GIF",ContentContract.Hash(input),"RGBA crop",C.Converters["skin"],$"{skin.Item3},{skin.Item4},{skin.Item5},{skin.Item6}");}
    if(music)foreach(var track in source.Audio){cancel.ThrowIfCancellationRequested();progress?.Invoke(new(ImportStage.Converting,"Preparing CD music",track.Number-1,4));string target=Path.Combine(stage,cw?"generated/council-wars-first/music":"generated/original-music",$"track{track.Number:00}.wav");var result=MusicConverter.Convert(track,target,cancel);if(result==null){warnings.Add("Track "+track.Number+": researched scrambled non-audio sectors; CD music omitted.");continue;}Record(target,source.Selected+" :: "+Path.GetFileName(track.File),result,"CDDA signed PCM16 stereo 44100 Hz",C.Converters["music"],track.Boundary+"; offset="+track.Offset+"; sectors="+track.Sectors+"; stride="+track.Stride);}
    else warnings.Add((cw?"Council Wars":"Base game")+": CD music not selected; voices and effects are available.");
   }
   foreach(var pack in new[]{("Base game","generated/original-music"),("Council Wars","generated/council-wars-first/music")})if(pack.Item1=="Base game"||Directory.Exists(Path.Combine(stage,"private/CouncilWars"))){int tracks=Enumerable.Range(2,4).Count(n=>File.Exists(Path.Combine(stage,pack.Item2,$"track{n:00}.wav")));if(tracks<4)warnings.Add($"{pack.Item1}: {tracks}/4 CD music tracks installed; briefing voices and sound effects remain available.");}
   warnings.AddRange(C.Fallbacks);
   string correction=Path.Combine(stage,"private/DC/odc-corrections.json");File.WriteAllText(correction,JsonSerializer.Serialize(new {id=OriginalContentCorrections.Human09Id,source=OriginalContentCorrections.Human09Path,sourceSha256=ContentContract.Hash(Path.Combine(stage,"private/DC",OriginalContentCorrections.Human09Path)),effectiveSha256=OriginalContentCorrections.Human09Corrected,operation="Hash-checked in-memory removal of bytes 149/150 only for the known unpatched input; known corrected input unchanged. Original copied file is preserved.",patchArchiveSha256=OriginalContentCorrections.Human09Archive,publisherProvenance="Unverified; investigated downloaded patch, not authenticated publisher attribution."}));Record(correction,"content correction research",OriginalContentCorrections.Human09Archive,"record existing runtime correction",OriginalContentCorrections.Human09Id);
   var receipt=new ImportReceipt(C.Revision,C.BaseEdition,Directory.Exists(Path.Combine(stage,"private/CouncilWars"))?C.CouncilEdition:null,DateTime.UtcNow,warnings.Distinct().ToArray(),receipts.OrderBy(r=>r.Path,StringComparer.Ordinal).ToArray(),ConversionRuntime);File.WriteAllText(Path.Combine(stage,"content-manifest.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));
   var errors=ContentStore.ValidateRoot(stage,true,cancel,progress);if(errors.Length>0)throw new InvalidDataException("Prepared content validation failed: "+string.Join(", ",errors.Take(5)));cancel.ThrowIfCancellationRequested();plan.Store.Activate(stage);progress?.Invoke(new(ImportStage.Complete,"Content is ready",Detail:$"{watch.Elapsed.TotalSeconds:0.0} seconds; {receipts.Sum(r=>r.Bytes)} bytes"));return receipt;
  }catch(OperationCanceledException){progress?.Invoke(new(ImportStage.Cancelled,"Import cancelled; previously installed content is unchanged"));throw;}catch(Exception e){progress?.Invoke(new(ImportStage.Failed,"Import failed; previously installed content is unchanged",Detail:e.Message));throw;}finally{plan.Dispose();}
 },CancellationToken.None);
}
