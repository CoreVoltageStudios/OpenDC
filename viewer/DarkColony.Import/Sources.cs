using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
namespace DarkColony.ContentImport;

// All paths are private read-only adapters. Sources are never patched or executed.
public sealed record AudioTrack(int Number,string File,long Offset,long Sectors,int Stride,string Boundary);
public sealed class ImportSource {
 internal sealed record Entry(long Bytes,Func<Stream> Open);
 internal Dictionary<string,Entry> Files {get;}=new(StringComparer.OrdinalIgnoreCase);
 public List<AudioTrack> Audio {get;}=new();
 public List<string> Warnings {get;}=new();
 public string Selected {get;private set;}="";
 public string Kind {get;private set;}="";
 internal string? Find(string suffix)=>Files.Keys.FirstOrDefault(k=>k.Equals(suffix,StringComparison.OrdinalIgnoreCase)||k.EndsWith('/'+suffix,StringComparison.OrdinalIgnoreCase));
 internal byte[] Read(string key){var e=Files[key];if(e.Bytes>16*1024*1024)throw new InvalidDataException("Oversized metadata: "+key);using var s=e.Open();using var m=new MemoryStream();s.CopyTo(m);return m.ToArray();}
 internal void Copy(string key,string target,CancellationToken cancel){Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var input=Files[key].Open();using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);byte[] b=new byte[262144];long copied=0;int n;while((n=input.Read(b))>0){cancel.ThrowIfCancellationRequested();output.Write(b,0,n);copied+=n;}if(copied!=Files[key].Bytes)throw new IOException("Source changed while reading: "+key);}
 public static ImportSource Open(string path,string scratch,CancellationToken cancel,Action<ImportProgress>? progress=null){
  path=Path.GetFullPath(path);var source=new ImportSource{Selected=path};SafePath.NoLinks(path);
  if(!Directory.Exists(path)&&Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase)){
   Directory.CreateDirectory(scratch);using var z=ZipFile.OpenRead(path);if(z.Entries.Count>30000||z.Entries.Sum(e=>e.Length)>8L*1024*1024*1024)throw new InvalidDataException("Archive exceeds supported source bounds");var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long done=0,total=z.Entries.Sum(e=>e.Length);
   foreach(var e in z.Entries){cancel.ThrowIfCancellationRequested();string target=SafePath.Within(scratch,e.FullName.TrimEnd('/'));if(!seen.Add(target)||(e.ExternalAttributes>>16&0xf000)==0xa000)throw new InvalidDataException("Archive has duplicate paths or links");if(e.FullName.EndsWith('/')){Directory.CreateDirectory(target);continue;}Directory.CreateDirectory(Path.GetDirectoryName(target)!);using(var a=e.Open())using(var b=new FileStream(target,FileMode.CreateNew)){byte[] buffer=new byte[262144];int n;long bytes=0;while((n=a.Read(buffer))>0){cancel.ThrowIfCancellationRequested();b.Write(buffer,0,n);bytes+=n;if(bytes>e.Length)throw new InvalidDataException("Archive length mismatch");}if(bytes!=e.Length)throw new InvalidDataException("Truncated archive");}done+=e.Length;progress?.Invoke(new(ImportStage.Extracting,"Reading source archive",done,total,e.FullName));}
   path=scratch;
  }
  if(Directory.Exists(path)){
   var files=Walk(path).ToArray();var cues=files.Where(f=>Path.GetExtension(f).Equals(".cue",StringComparison.OrdinalIgnoreCase)).ToArray();var mds=files.Where(f=>Path.GetExtension(f).Equals(".mds",StringComparison.OrdinalIgnoreCase)).ToArray();var iso=files.Where(f=>Path.GetExtension(f).Equals(".iso",StringComparison.OrdinalIgnoreCase)).ToArray();
   if(cues.Length==1)source.LoadCue(cues[0]);else if(mds.Length==1)source.LoadMds(mds[0]);else if(iso.Length==1)source.LoadIso(iso[0],0,2048,0);else if(cues.Length+mds.Length+iso.Length>1)throw new InvalidDataException("Several disc images found. Select the desired CUE, MDS or ISO file.");else if(files.Count(f=>Path.GetExtension(f).Equals(".zip",StringComparison.OrdinalIgnoreCase))==1&&!files.Any(f=>Path.GetFileName(f).Equals("DC16.EXE",StringComparison.OrdinalIgnoreCase))){return Open(files.Single(f=>Path.GetExtension(f).Equals(".zip",StringComparison.OrdinalIgnoreCase)),scratch,cancel,progress);}
   else{source.Kind="Extracted or installed folder";foreach(string f in files){var local=f;source.Files.Add(Path.GetRelativePath(path,f).Replace('\\','/'),new(new FileInfo(f).Length,()=>File.OpenRead(local)));}}
  }else switch(Path.GetExtension(path).ToLowerInvariant()){
   case ".cue":source.LoadCue(path);break;case ".mds":source.LoadMds(path);break;case ".iso":source.LoadIso(path,0,2048,0);break;
   case ".bin":case ".mdf":throw new InvalidDataException("Select the accompanying CUE or MDS descriptor so track boundaries are preserved.");
   default:throw new InvalidDataException("Choose a game folder, ISO, CUE, MDS or ZIP. Other archives must be extracted first.");
  }
  return source;
 }
 static IEnumerable<string> Walk(string root){var queue=new Queue<(string,int)>();queue.Enqueue((root,0));int count=0;while(queue.TryDequeue(out var d)){if(d.Item2>10)throw new InvalidDataException("Source folder nesting is too deep");foreach(string f in Directory.EnumerateFiles(d.Item1)){if(++count>30000)throw new InvalidDataException("Source folder is too broad; select the game folder");if((File.GetAttributes(f)&FileAttributes.ReparsePoint)!=0)throw new IOException("Source links are not followed");yield return f;}foreach(string child in Directory.EnumerateDirectories(d.Item1)){if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)queue.Enqueue((child,d.Item2+1));}}}
 sealed record CueTrack(int Number,string Mode,string File){public long Index=0,Gap=-1;public bool HasIndex;}
 void LoadCue(string cue){
  Kind="CUE/BIN";var tracks=new List<CueTrack>();string? file=null;foreach(string line in File.ReadLines(cue)){
   var f=Regex.Match(line,"^\\s*FILE\\s+\"([^\"]+)\"\\s+BINARY\\s*$",RegexOptions.IgnoreCase);if(f.Success){file=SafePath.Within(Path.GetDirectoryName(cue)!,f.Groups[1].Value);continue;}
   var t=Regex.Match(line,@"^\s*TRACK\s+(\d+)\s+(\S+)\s*$",RegexOptions.IgnoreCase);if(t.Success){if(file==null)throw new InvalidDataException("CUE track before FILE");tracks.Add(new(int.Parse(t.Groups[1].Value),t.Groups[2].Value.ToUpperInvariant(),file));continue;}
   var i=Regex.Match(line,@"^\s*INDEX\s+(00|01)\s+(\d+):(\d+):(\d+)\s*$",RegexOptions.IgnoreCase);if(i.Success){if(tracks.Count==0)throw new InvalidDataException("CUE index before track");int sec=int.Parse(i.Groups[3].Value),frames=int.Parse(i.Groups[4].Value);if(sec>=60||frames>=75)throw new InvalidDataException("Invalid CUE time");long at=(int.Parse(i.Groups[2].Value)*60L+sec)*75+frames;if(i.Groups[1].Value=="01"){tracks[^1].Index=at;tracks[^1].HasIndex=true;}else tracks[^1].Gap=at;continue;}
   if(Regex.IsMatch(line,@"^\s*(FILE|TRACK|INDEX)\b",RegexOptions.IgnoreCase))throw new InvalidDataException("Unsupported CUE directive: "+line.Trim());
  }
  if(tracks.Count is <1 or >5||tracks[0].Number!=1||tracks.Any(t=>!t.HasIndex)||tracks.Skip(1).Any(t=>t.Mode!="AUDIO")||!tracks.Select(t=>t.Number).SequenceEqual(Enumerable.Range(1,tracks.Count)))throw new InvalidDataException("Unsupported disc track layout");
  int stride=tracks[0].Mode switch{"MODE1/2048"=>2048,"MODE1/2352" or "MODE2/2352"=>2352,_=>throw new InvalidDataException("Unsupported CUE data mode")};
  if(stride!=2352&&tracks.Skip(1).Any(t=>t.File==tracks[0].File))throw new InvalidDataException("Mixed-stride single BIN is not supported");
  LoadIso(tracks[0].File,tracks[0].Index*stride,stride,tracks[0].Mode=="MODE2/2352"?24:stride==2048?0:16);Kind="CUE/BIN";
  for(int k=1;k<tracks.Count;k++){var t=tracks[k];SafePath.NoLinks(t.File);long length=new FileInfo(t.File).Length;if(length%2352!=0)throw new InvalidDataException("Audio BIN sector alignment");long end=k+1<tracks.Count&&tracks[k+1].File==t.File?(tracks[k+1].Gap>=0?tracks[k+1].Gap:tracks[k+1].Index):length/2352;if(t.Index<0||end<=t.Index||end>length/2352)throw new InvalidDataException("Audio track bounds");Audio.Add(new(t.Number,t.File,t.Index*2352,end-t.Index,2352,"CUE INDEX01 to next INDEX00/01 or file end"));}
 }
 void LoadMds(string mds){var d=File.ReadAllBytes(mds);int U16(int p)=>BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(p));int I32(int p)=>BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p));long I64(int p)=>BinaryPrimitives.ReadInt64LittleEndian(d.AsSpan(p));
  if(d.Length<88||Encoding.ASCII.GetString(d,0,16)!="MEDIA DESCRIPTOR"||d[16]!=1||d[17]!=3||U16(18)!=0||U16(20)!=1)throw new InvalidDataException("Only the researched single-session MDS 1.3 CD layout is supported");
  string mdf=Path.ChangeExtension(mds,".mdf");if(!File.Exists(mdf))mdf=Directory.EnumerateFiles(Path.GetDirectoryName(mds)!).SingleOrDefault(f=>Path.GetFileName(f).Equals(Path.GetFileName(mdf),StringComparison.OrdinalIgnoreCase))??throw new FileNotFoundException("MDS backing MDF is missing");
  SafePath.NoLinks(mdf);int session=I32(80);if(U16(session+12)!=1||U16(session+14)!=5)throw new InvalidDataException("Unsupported MDS track count");int table=I32(session+20);var found=new List<int>();for(int j=0;j<d[session+10];j++){int p=table+j*80,number=d[p+4];if(number is <1 or >5)continue;int extra=I32(p+12),stride=U16(p+16),sectors=I32(extra+4);long offset=I64(p+40);if(stride!=2448||d[p+1]!=8||I32(p+48)!=1||d[p]!=(number==1?0xaa:0xa9)||offset<0||sectors<=0||offset+sectors*(long)stride>new FileInfo(mdf).Length)throw new InvalidDataException("Unsupported or truncated MDS/MDF layout");found.Add(number);if(number==1)LoadIso(mdf,offset,stride,16);else Audio.Add(new(number,mdf,offset,sectors,stride,"MDS descriptor INDEX01 offsets; strips 96 subchannel bytes"));}
  if(!found.SequenceEqual(new[]{1,2,3,4,5}))throw new InvalidDataException("Incomplete MDS track table");Kind="MDS/MDF 1.3";Warnings.Add("MDF music follows descriptor boundaries. This researched image differs from the CUE reference; reference matching does not establish audible quality or authenticity.");
 }
 void LoadIso(string file,long offset,int stride,int skip){
  SafePath.NoLinks(file);Kind="ISO9660";byte[] Read(long logical,int count){using var s=new SectorStream(file,offset,stride,skip,logical,count);var b=new byte[count];s.ReadExactly(b);return b;}
  var pvd=Read(16*2048,2048);if(pvd[0]!=1||Encoding.ASCII.GetString(pvd,1,5)!="CD001"||BinaryPrimitives.ReadUInt16LittleEndian(pvd.AsSpan(128))!=2048)throw new InvalidDataException("No supported ISO9660 data volume");long volume=BinaryPrimitives.ReadUInt32LittleEndian(pvd.AsSpan(80))*2048L;if(volume<2048||offset+((volume-1)/2048)*stride+skip+2048>new FileInfo(file).Length)throw new InvalidDataException("Truncated data image");
  var visited=new HashSet<uint>();int files=0;void Walk(byte[] record,string prefix,int depth){uint sector=BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(2)),length=BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(10));if(depth>10||length>16*1024*1024||sector*2048L+length>volume||!visited.Add(sector))throw new InvalidDataException("Invalid ISO directory graph");var bytes=Read(sector*2048L,(int)length);for(int p=0;p<bytes.Length;){int n=bytes[p];if(n==0){p=(p/2048+1)*2048;continue;}if(n<34||p+n>bytes.Length)throw new InvalidDataException("Invalid ISO record");var r=bytes.AsSpan(p,n).ToArray();p+=n;int names=r[32];if(33+names>r.Length)throw new InvalidDataException("Invalid ISO name");if(names==1&&r[33]<=1)continue;string name=Encoding.ASCII.GetString(r,33,names).Split(';')[0].TrimEnd('.');if(name.IndexOfAny(new[]{'/','\\',':'})>=0||name is "" or "." or "..")throw new InvalidDataException("Unsafe ISO name");string key=prefix+name;if((r[25]&2)!=0)Walk(r,key+'/',depth+1);else{if(++files>30000||(r[25]&128)!=0)throw new InvalidDataException("Unsupported ISO multi-extent or file count");long start=BinaryPrimitives.ReadUInt32LittleEndian(r.AsSpan(2))*2048L,size=BinaryPrimitives.ReadUInt32LittleEndian(r.AsSpan(10));if(start+size>volume)throw new InvalidDataException("ISO file outside volume");Files.Add(key,new(size,()=>new SectorStream(file,offset,stride,skip,start,size)));}}}
  Walk(pvd.AsSpan(156,pvd[156]).ToArray(),"",0);
 }
 sealed class SectorStream:Stream {
  readonly FileStream file;readonly long offset,start,length;readonly int stride,skip;long position;
  public SectorStream(string path,long offset,int stride,int skip,long start,long length){file=File.OpenRead(path);this.offset=offset;this.stride=stride;this.skip=skip;this.start=start;this.length=length;}
  public override int Read(byte[] buffer,int at,int count)=>Read(buffer.AsSpan(at,count));
  public override int Read(Span<byte> buffer){int total=(int)Math.Min(buffer.Length,length-position),done=0;while(done<total){long logical=start+position;int part=(int)Math.Min(total-done,2048-logical%2048);file.Position=offset+logical/2048*stride+skip+logical%2048;file.ReadExactly(buffer.Slice(done,part));done+=part;position+=part;}return done;}
  protected override void Dispose(bool disposing){if(disposing)file.Dispose();base.Dispose(disposing);}public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>length;public override long Position{get=>position;set=>throw new NotSupportedException();}public override void Flush(){}public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
 }
}
