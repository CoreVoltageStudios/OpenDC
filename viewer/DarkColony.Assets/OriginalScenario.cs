using System.Security.Cryptography;
using System.Text;
using System.Globalization;
namespace DarkColony.Assets;

public sealed record ScenarioSource(string Path,string Sha256,byte[] Bytes) {
 public string? OriginalSha256 {get;init;}
 public string? CorrectionId {get;init;}
}
public sealed record ScenarioLine(int Number,string Text);
public sealed record OriginalType(int Id,string Sprite,int[] Fields,int SourceLine) {
 public int Race=>Fields[0]; public int MaximumHealth=>Fields[11];
 public int AlternateType=>Fields[30]; public int Flying=>Fields[12];
 public int OccupancyClass=>Fields[14]; public int InitialAngle=>unchecked((byte)Fields[21]);
}
public sealed record OriginalDependency(int Id,int Cost,int Icon,int Kind,int Subject,int Level,int RaceOrTier,int[] Parents,int[] Raw,int SourceLine);
public sealed record ScenarioTeam(int Id,int HeaderFlag,int Race,int Money,int Ai,int Color,
 int[] Rally,int[] Base,int[] RawDisabledDependencies,bool[] DisabledDependencies,int[] Allies,int[] RawAiSlots,
 int[] RawCity,int[] CityHealth,int[] CityLevels,int[][] RawOverrides,int[] WeaponTiers,int[] ArmorTiers,
 int[] DependencyStatus,int SourceLine) {
 public int CommanderRecoveryDisabled=>0;
 public int AutomaticIncome=>3; // 41c339: paid only while city slot0 health is nonzero.
 public int[] CommanderIds=>new[]{-1,-1,-1,-1};
 public int[] ProductionIds=>Enumerable.Repeat(-1,15).ToArray();
 public bool[] CityInitializationPending=>CityHealth.Select(h=>h!=0).ToArray();
}
public sealed record ScenarioSpawnSource(int Id,int SourceLine,int X,int Z,int Type,int Count,int[] Raw);
public sealed record ScenarioArtifactSite(int Slot,int X,int Z,int[] Contents);
// Validated match setup overrides are applied before faction conversion and placement.
// Original source bytes and their fingerprints remain untouched.
public sealed record ScenarioMatchOverrides(int[] Races,int[] Ai,int[] Colors,bool[] Active,
 int FlowFactor,int QuantityFactor,int Artifacts);
public sealed record ScenarioEntity(string Identity,int NativeId,string Kind,int SourceLine,int SourceOrdinal,
 int SourceType,int Type,int Owner,int X256,int Z256,int Health,int RawHealth,int Flag,int[] Raw,
 int? ResourceFlow,int Height,int Angle,int OccupancyLayer) {
 public int ActiveState=>1;
 public int InitialEnergy=>Kind=="city"?0:64;
}
public sealed record TripMap(int Width,int Height,byte[] Values,byte[] Source) {
 public byte At(int x,int z)=>x>=0&&z>=0&&x<Width&&z<Height?Values[z*Width+x]:throw new ArgumentOutOfRangeException();
 public static TripMap Decode(byte[] b,int w,int h) {
  if(b.Length!=2+w*h||b[0]!=w||b[1]!=h)throw new InvalidDataException("MTG: dimension/length mismatch");
  // 453b0c..453b25 writes row h-fileRow through table+c00, whose row0
  // is at+c04 (453608..45361b): native z = h-1-fileRow. Keep source intact.
  var values=new byte[w*h];
  for(int row=0;row<h;row++)for(int x=0;x<w;x++)values[(h-1-row)*w+x]=(byte)(b[2+row*w+x]&63);
  return new(w,h,values,(byte[])b.Clone());
 }
}
public sealed record OriginalScenario(string Scenario,string Boundary,ScenarioTerrainHeader Header,
 string MapName,string RawHeader3,string RawHeader4,MapAsset Map,TerrainAsset Terrain,PathAsset Path,TripMap Trips,
 OriginalType[] Types,OriginalDependency[] Dependencies,ScenarioTeam[] Teams,ScenarioEntity[] Entities,
 int NextDynamicId,int[] GroundOccupancy,int[] AirOccupancy,int[] OtherOccupancy,
 int[] MissingTerrainIds,string[] Limitations,ScenarioSource[] Sources) {
 public ScenarioSpawnSource[] SpawnSources {get;init;}=Array.Empty<ScenarioSpawnSource>();
 public ScenarioArtifactSite[] ArtifactSites {get;init;}=Array.Empty<ScenarioArtifactSite>();
 public MapAsset SourceMap {get;init;}=Map;
 public bool Paused=>true;
 public int Tick=>0;
 public bool ScriptsExecuted=>false;
 public int[] ResourceOptionFactors=>new[]{256,256};
 public int[] AllianceBits=>Teams.Select(t=>t.Allies.Select((v,i)=>v<<i).Aggregate(0,(a,b)=>a|b)).ToArray();
 public int[] SharedVisionBits=>Enumerable.Range(0,8).Select(i=>1<<i).ToArray();
 // Separate cached matrix: only neutral-wildlife links are set before the first world update.
 public int[][] InitialRelationshipCache=>Enumerable.Range(0,10).Select(a=>Enumerable.Range(0,10).Select(b=>(a==9&&b<8)||(b==9&&a<8)?1:0).ToArray()).ToArray();
 public int[] ResourceCells=>Entities.Where(e=>e.Type==40).Select(e=>(e.Z256>>8)*Map.Width+(e.X256>>8)).ToArray();
}

// Static adapter, not a mission simulator. Contracts/addresses: FIRST-STRIKE-SPEC.txt.
public static class OriginalScenarioImport {
 static readonly int[] CityTypes={16,17,18,20,22,81,25,25,25,25,25,25,25,0,0,
  16,17,19,21,22,81,25,25,25,25,25,25,25,0,0,
  28,29,30,32,34,81,25,25,25,25,25,25,25,0,0,
  28,29,31,33,34,81,25,25,25,25,25,25,25,0,0}; // DC16 48aff0
 static readonly int[] CityOffsets={-64,15,0,0,32,64,64,10,-32,65,0,32,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}; // 48abb8, signed dwords
 static readonly int[] UpgradeTypes={0,2,3,6,43,5,1,4,8,10,11,14,44,13,9,12}; // 41adc0
 static readonly int[][] CityFootprints={new[]{-3,0,-2,0,-3,1,-2,1},new[]{-1,-1,0,-1,-1,-2,0,-2},new[]{0,2,1,2,0,3,1,3},new[]{1,0,2,0,1,1,2,1},new[]{-1,2,-1,3,-2,2,-2,3}}; // 48ac30, repeated-pair terminator; slot5 has no footprint.
 static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
 public static int CityType(int race,int level,int slot)=>CityTypes[race*30+level*15+slot];
 public static (int X,int Z) CityAnchor(int[] basis,int slot)=>(basis[0]*256+CityOffsets[slot*2]*8,basis[1]*256+CityOffsets[slot*2+1]*8);
 public static IEnumerable<(int X,int Z)> CityFootprint(int[] basis,int slot) {
  if(slot>=5)yield break;var cells=CityFootprints[slot];
  for(int i=0;i<cells.Length;i+=2)yield return(basis[0]+cells[i],basis[1]+cells[i+1]);
 }
 public static OriginalScenario Load(string root,string scenario="SCENARIO/ALIEN/ALIEN01.SCN",ScenarioMatchOverrides? match=null) =>
  Read(scenario,p=>File.ReadAllBytes(Resolve(root,p)),match);
 static string Resolve(string root,string path) {
  string current=Path.GetFullPath(root);
  foreach(string part in path.Replace('\\','/').Split('/')) {
   if(part is "" or "." or ".."||part.Contains(':'))throw new InvalidDataException("Non-relative original source path");
   var matches=Directory.EnumerateFileSystemEntries(current).Where(p=>Path.GetFileName(p).Equals(part,StringComparison.OrdinalIgnoreCase)).ToArray();
   if(matches.Length!=1)throw new FileNotFoundException("Missing/ambiguous original source: "+path);
   current=matches[0];
  }
  return current;
 }
 public static ScenarioLine[] Lines(byte[] bytes,string label) {
  if(bytes.Any(b=>b==0||b>127))throw new InvalidDataException(label+": unsupported non-ASCII/NUL text");
  return Encoding.ASCII.GetString(bytes).Split('\n').Select((s,i)=>new ScenarioLine(i+1,s.Trim())).Where(l=>l.Text.Length>0&&!l.Text.StartsWith("%")).ToArray();
 }
 static int[] Numbers(ScenarioLine line,string label,int min,int max) {
  var words=line.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
  if(words.Length<min||words.Length>max)throw new InvalidDataException($"{label}:{line.Number}: expected {min}..{max} fields");
  var result=new int[words.Length];
  for(int i=0;i<words.Length;i++)if(!int.TryParse(words[i],NumberStyles.Integer,Inv,out result[i]))throw new InvalidDataException($"{label}:{line.Number}: invalid integer");
  return result;
 }
 static int[] Terminated(ScenarioLine line,string label,int? count=null) {
  var v=Numbers(line,label,1,112);
  if(v[v.Length-1]!=-1||v.Take(v.Length-1).Any(x=>x<0)||(count.HasValue&&v.Length!=count+1))throw new InvalidDataException($"{label}:{line.Number}: invalid terminated list");
  return v.Take(v.Length-1).ToArray();
 }
 public static OriginalType[] ReadTypes(byte[] bytes) {
  var lines=Lines(bytes,"GAMESTAT");if(lines.Length==0)throw new InvalidDataException("Empty GAMESTAT");
  int count=Numbers(lines[0],"GAMESTAT",1,1)[0];
  if(count<1||count>118||lines.Length!=count+1)throw new InvalidDataException("GAMESTAT count mismatch");
  return lines.Skip(1).Select((l,id)=>{
   var words=l.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   if(words.Length!=33||words[0].Any(c=>!char.IsLetterOrDigit(c)&&c!='_'))throw new InvalidDataException($"GAMESTAT:{l.Number}: unsupported record");
   var fields=Numbers(new(l.Number,string.Join(" ",words.Skip(1))),"GAMESTAT",32,32);
   if(fields[11]<=0||fields[30]<-1||fields[30]>=count)throw new InvalidDataException("GAMESTAT: invalid HP/alternate type");
   return new OriginalType(id,words[0],fields,l.Number);
  }).ToArray();
 }
 public static OriginalDependency[] ReadDependencies(byte[] bytes) {
  var lines=Lines(bytes,"DEPEND");if(lines.Length==0)throw new InvalidDataException("Empty DEPEND");
  int count=Numbers(lines[0],"DEPEND",1,1)[0];
  if(count<1||count>110||lines.Length!=count+1)throw new InvalidDataException("DEPEND count mismatch");
  var result=new List<OriginalDependency>();
  foreach(var line in lines.Skip(1)) {
   var v=Numbers(line,"DEPEND",6,13);int kind=v[3],prefix=kind==1?5:7;
   if(kind<0||kind>2||v.Length<=prefix||v[v.Length-1]!=-1||v.Skip(prefix).Take(v.Length-prefix-1).Any(x=>x<0||x>=110)||v[0]<0||v[0]>=110||result.Any(d=>d.Id==v[0]))throw new InvalidDataException("DEPEND: invalid/unsupported record");
   if(v[4]<0||(kind==0&&(v[4]>=15||v[5]<0||v[5]>1||v[6]<0||v[6]>1))||(kind==2&&(v[5]<0||v[5]>1||v[6]<0)))throw new InvalidDataException("DEPEND: invalid subject/level/race");
   result.Add(new(v[0],v[1],v[2],kind,v[4],kind==1?0:v[5],kind==1?0:v[6],v.Skip(prefix).Take(v.Length-prefix-1).ToArray(),v,line.Number));
  }
  return result.ToArray();
 }
 public static OriginalScenario Read(string scenario,Func<string,byte[]> read,ScenarioMatchOverrides? match=null) {
  scenario=scenario.Replace('\\','/').ToUpperInvariant();
  if(scenario.Split('/').Any(p=>p is "" or "." or ".."||p.Contains(':'))||!scenario.EndsWith(".SCN",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Expected relative SCN path");
  var sources=new List<ScenarioSource>();
  byte[] Source(string p) {
   var source=OriginalContentCorrections.Read(p,read(p));sources.Add(source);return source.Bytes;
  }
  var scn=Source(scenario);var lines=Lines(scn,"SCN");
  if(lines.Length<8)throw new InvalidDataException("Truncated SCN header");
  var header=ScenarioTerrainHeader.Read(scn);
  string directory=scenario.Substring(0,scenario.LastIndexOf('/')+1);
  string stem=lines[1].Text;
  if(stem.Length==0||stem.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='~'&&c!='-'))throw new InvalidDataException("SCN map basename");
  if(header.TerrainName.Any(c=>!char.IsLetterOrDigit(c)&&c!='.'&&c!='_'&&c!='-'))throw new InvalidDataException("SCN terrain basename");
  string companion=directory+stem.ToUpperInvariant();
  var map=Import.Map(Source(companion+".MAP"));var terrain=Import.Terrain(Source("SCENARIO/"+header.TerrainName.ToUpperInvariant()));
  var path=PathAsset.Decode(Source(companion+".PTH"),map.Width,map.Height);
  var trips=TripMap.Decode(Source(companion+".MTG"),map.Width,map.Height);
  var types=ReadTypes(Source("GAMESTAT/GAMESTAT.TXT"));var dependencies=ReadDependencies(Source("GAMESTAT/DEPEND.TXT"));
  // Preserve associated combat definitions as opaque evidence; this importer executes no weapons.
  Source("GAMESTAT/WEAPSTAT.TXT");Source("GAMESTAT/MBULLET.TXT");Source("GAMESTAT/BOOMSTAT.TXT");
  // Associated script/message bytes are retained but never executed or inserted into initial entities.
  Source(companion+".TRO");if(!scenario.StartsWith("SCENARIO/MPLAYER/"))Source(companion+".MSG");
  if(match!=null&&(!scenario.StartsWith("SCENARIO/MPLAYER/")||match.Races.Length!=8||match.Ai.Length!=8||match.Colors.Length!=8||match.Active.Length!=8))throw new InvalidDataException("Invalid War initialization overrides");
  var teams=new List<ScenarioTeam>();var entities=new List<ScenarioEntity>();var limits=new List<string>();int pos=8;
  ScenarioLine Next(){if(pos>=lines.Length)throw new InvalidDataException("SCN truncated inside team records");return lines[pos++];}
  int Scalar()=>Numbers(Next(),"SCN",1,1)[0];
  int[] Pair()=>Numbers(Next(),"SCN",2,2);
  for(int id=0;id<8;id++) {
   var line=Next();var tokens=line.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   if(tokens.Length!=3||tokens[0]!="TEAM"||tokens[1]!=id.ToString(Inv)||!int.TryParse(tokens[2],out int flag))throw new InvalidDataException($"SCN:{line.Number}: expected ordered TEAM {id}");
   int race=Scalar(),money=Scalar(),ai=Scalar(),color=Scalar();
   if(match!=null){race=match.Races[id];ai=match.Ai[id];color=match.Colors[id];}
   if(race is <0 or >1||ai is <0 or >4)throw new NotSupportedException("SCN: race/AI outside established campaign contract");
   if(color<0||color>7)color=id;
   var rawDisabled=Terminated(Next(),"SCN");var disabled=new bool[110];
   foreach(int d in rawDisabled){if(d>=110)throw new InvalidDataException("SCN dependency out of bounds");disabled[d]=true;}
   var allies=Terminated(Next(),"SCN allies",8);if(allies.Any(x=>x>1))throw new InvalidDataException("SCN alliance is not boolean");allies[id]=1;
   var aiSlots=Terminated(Next(),"SCN AI slots",15);var rally=Pair();var cityBase=Pair();var cityLine=Next();
   var rawCity=Numbers(cityLine,"SCN city",10,64);
   if(cityBase.Any(v=>v<0)||cityBase[0]>=map.Width||cityBase[1]>=map.Height)throw new InvalidDataException("SCN base out of map");
   if(rally[0]==0&&rally[1]==0)rally=(int[])cityBase.Clone();
   if(cityBase[0]==0||cityBase[1]==0){disabled[0]=true;disabled[14]=true;}
   var health=new int[15];var levels=new int[15];
   for(int slot=0;slot<5;slot++) {
    int stage=rawCity[slot*2],hp=rawCity[slot*2+1];
    if(cityBase[0]!=0&&stage>0){if(stage>2)throw new NotSupportedException("SCN city stage outside native table");levels[slot]=stage-1;health[slot]=hp==-1?types[CityTypes[slot+(stage-1)*15]].MaximumHealth:hp;}
   }
   health[5]=cityBase[0]!=0&&cityBase[1]!=0?1:0;
   if(match!=null&&!match.Active[id]){Array.Clear(health,0,health.Length);Array.Clear(levels,0,levels.Length);}
   var overrides=new int[8][];var weapons=new int[types.Length];var armor=new int[types.Length];
   for(int row=0;row<8;row++) {
    overrides[row]=Numbers(Next(),"SCN type override",5,5);int type=UpgradeTypes[race*8+row];
    if(type>=types.Length)throw new InvalidDataException("GAMESTAT missing override type");
    weapons[type]=unchecked((byte)overrides[row][2]);armor[type]=unchecked((byte)overrides[row][3]);
   }
   var team=new ScenarioTeam(id,flag,race,money,ai,color,rally,cityBase,rawDisabled,disabled,allies,aiSlots,rawCity,health,levels,overrides,weapons,armor,Enumerable.Repeat(-1,110).ToArray(),line.Number);
   teams.Add(team);
   for(int slot=0;slot<15;slot++)if(health[slot]!=0) {
    int type=CityTypes[slot+levels[slot]*15+race*30];
    if(type>=types.Length)throw new InvalidDataException("GAMESTAT missing city type");
    entities.Add(new($"{scenario}:city:{id}:{slot}",id*15+slot,"city",cityLine.Number,slot,type,type,id,
     cityBase[0]*256+CityOffsets[slot*2]*8,cityBase[1]*256+CityOffsets[slot*2+1]*8,health[slot],slot<5?rawCity[slot*2+1]:1,0,(int[])rawCity.Clone(),null,0,0,0));
   }
  }
  int ordinal=0,dynamicId=152;var spawnSources=new List<ScenarioSpawnSource>();
  var artifacts=new List<(int Slot,int X,int Z,List<int> Contents)>();
  while(pos<lines.Length) {
   var line=Next();var v=Numbers(line,"SCN placed",5,6);int x=v[0],z=v[1],type=v[2],owner=v[3],hp=v[4],flag=v.Length==6?unchecked((byte)v[5]):0,sourceType=type;
   if(type<0||type>=types.Length||x<0||z<0||x>=map.Width||z>=map.Height)throw new InvalidDataException($"SCN:{line.Number}: type/coordinate out of bounds");
   if(owner>=0&&owner<8&&types[type].Race!=teams[owner].Race&&types[type].AlternateType!=-1)type=types[type].AlternateType;
   if(owner==-1){if(hp>=10||spawnSources.Count>=25)throw new InvalidDataException("RNAT registry exceeds native source/count bounds");spawnSources.Add(new(spawnSources.Count,line.Number,x,z,type,hp,(int[])v.Clone()));ordinal++;continue;}
   // 41b950/440760/440810: records at a registered site enter its FIFO;
   // they consume neither a dynamic identity nor map occupancy.
   var site=artifacts.FindIndex(s=>s.X==x&&s.Z==z);
   if(site>=0){artifacts[site].Contents.Add(type);ordinal++;continue;}
   if(type==37){if(match?.Artifacts==0){ordinal++;continue;}artifacts.Add((dynamicId,x,z,new()));owner=8;flag=0;}
   int? flow=null;
   if(type==40){flow=unchecked((short)(owner*(match?.FlowFactor??256)/256));hp=hp*(match?.QuantityFactor??256)/256;owner=8;flag=0;} // Native resource columns are flow/amount; its optional flag is ignored.
   else if(owner<0||owner>9)throw new InvalidDataException($"SCN:{line.Number}: owner out of bounds");
   // Retain the authored commander type. The run's explicit profile binds the
   // local rank in NewEntity; importing immutable scenario data must not invent it.
   if(dynamicId>=800)throw new InvalidDataException("SCN exceeds native entity capacity");
   if(match!=null&&owner<8&&!match.Active[owner]){dynamicId++;ordinal++;continue;} // Native leaves this identity unused.
   var definition=types[type];int effectiveHp=hp<0?definition.MaximumHealth:hp;
   entities.Add(new($"{scenario}:placed:{ordinal}",dynamicId++,"placed",line.Number,ordinal,sourceType,type,owner,x*256+128,z*256+128,effectiveHp,hp,flag,v,flow,definition.Flying!=0?600:0,definition.InitialAngle,definition.OccupancyClass!=0?2:definition.Flying!=0?1:0));
   ordinal++;
  }
  // Native dependency cache evaluates in numeric ID order. It is an availability state,
  // not a promise that construction/production is implemented. 0 satisfied,1 available,2 blocked.
  foreach(var t in teams) {
   foreach(var d in dependencies)t.DependencyStatus[d.Id]=0;
   foreach(var d in dependencies.OrderBy(d=>d.Id)) {
    bool satisfied=d.Kind==0&&d.Subject>=0&&d.Subject<15&&t.CityHealth[d.Subject]!=0&&t.CityLevels[d.Subject]>=d.Level&&t.Race==d.RaceOrTier;
    if(d.Kind==2) {
     if(d.Subject<0||d.Subject>=types.Length||d.Level is <0 or >1)throw new InvalidDataException("DEPEND upgrade out of bounds");
     satisfied=(d.Level==0?t.WeaponTiers[d.Subject]:t.ArmorTiers[d.Subject])>=d.RaceOrTier;
    }
    if(satisfied){t.DependencyStatus[d.Id]=0;continue;}
    bool blocked=t.DisabledDependencies[d.Id]||(d.Kind==0&&d.Subject>=0&&d.Subject<15&&t.CityHealth[d.Subject]!=0);
    foreach(int parent in d.Parents) {
     var p=dependencies.SingleOrDefault(q=>q.Id==parent);
     if(p==null)throw new InvalidDataException("DEPEND missing parent");
     blocked|=parent==d.Id||t.DependencyStatus[parent]!=0||(p.Kind==0&&t.CityHealth[p.Subject]!=0);
    }
    t.DependencyStatus[d.Id]=blocked?2:1;
   }
  }
  var ground=Enumerable.Repeat(-1,map.Width*map.Height).ToArray();var air=(int[])ground.Clone();var other=(int[])ground.Clone();
  foreach(var e in entities.Where(e=>e.Kind=="city"&&e.SourceOrdinal<5)) {
   var footprint=CityFootprints[e.SourceOrdinal];var b=teams[e.Owner].Base;
   for(int i=0;i<footprint.Length;i+=2){int x=b[0]+footprint[i],z=b[1]+footprint[i+1];
    if(x<0||z<0||x>=map.Width||z>=map.Height)throw new InvalidDataException("SCN city footprint out of map");
    if(ground[z*map.Width+x]!=-1)throw new InvalidDataException("SCN city footprints overlap");
    ground[z*map.Width+x]=e.NativeId;
   }
  }
  foreach(var e in entities.Where(e=>e.Kind=="placed"&&e.Owner!=8)) {
   var layer=e.OccupancyLayer==0?ground:e.OccupancyLayer==1?air:other;int at=(e.Z256>>8)*map.Width+(e.X256>>8);
   if(layer[at]!=-1)limits.Add($"Native placement overwrites occupancy {layer[at]} with {e.NativeId}; both entities retained");
   layer[at]=e.NativeId;
  }
  var ids=new HashSet<uint>(terrain.Tiles.Select(t=>t.EditorId));
  var missing=map.Cells.SelectMany(c=>new[]{c.BackgroundId,c.ForegroundId}).Where(id=>id!=0&&!ids.Contains(id)).Select(id=>(int)id).Distinct().OrderBy(id=>id).ToArray();
  if(missing.Length>0)limits.Add("Native zero-LUT terrain aliases to slot0: "+string.Join(",",missing));
  limits.Add("Paused post-SCN snapshot; no opening scripts, AI, objectives, production, sound or gameplay visibility have run");
  return new(scenario,"after SCN initialization, before first native update/TRO",header,lines[1].Text,lines[2].Text,lines[3].Text,Import.ResolveTerrain(map,terrain),terrain,path,trips,types,dependencies,teams.ToArray(),entities.ToArray(),dynamicId,ground,air,other,missing,limits.ToArray(),sources.ToArray()){SpawnSources=spawnSources.ToArray(),ArtifactSites=artifacts.Select(a=>new ScenarioArtifactSite(a.Slot,a.X,a.Z,a.Contents.ToArray())).ToArray(),SourceMap=map};
 }
}
