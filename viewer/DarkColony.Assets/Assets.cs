using System.Buffers.Binary;
using System.Text;
namespace DarkColony.Assets;

// Immutable positional records; raw source and unrecognised fields are retained.
public sealed record MapCell(ushort BackgroundId, ushort ForegroundId, ushort Attributes);
public sealed record MapAsset(int Width, int Height, MapCell[] Cells, byte[] Source) {
    // Logical z=0 corresponds to the LAST stored file row. Presentation chooses its own screen transform.
    public MapCell At(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height
        ? Cells[z * Width + x] : throw new ArgumentOutOfRangeException();
}
public sealed record TerrainTile(uint EditorId, byte[] Indices);
public sealed record TerrainAsset(uint RemapCount, byte[] Palette6, TerrainTile[] Tiles, byte[] Source) {
    public Dictionary<uint,TerrainTile> ByEditorId => Tiles.ToDictionary(t => t.EditorId);
}
public sealed record SpriteCell(ushort Width, ushort Height, ushort OffsetX, ushort OffsetY, byte[] Indices, byte[] Encoded) {
    // RLE skip coverage is separate from literal index 0. Null for uncompressed variants.
    public byte[]? Coverage { get; init; }
}
public sealed record SpriteAsset(ushort Flags, uint SizeHint, byte[] Palette6, SpriteCell[] Cells, byte[] Source);
public sealed record AnimationDef(string Name, ushort First, ushort Last);
public sealed record FramePart(string Name, short X, short Y);
public sealed record FrameCell(string Bank, ushort CellId, short X, short Y, ushort[] UnknownWords);
public sealed record AnimationFrame(ushort Duration, FramePart[] Parts, FrameCell[] Cells);
public sealed record AnimationAsset(ushort Flags, string[] Banks, AnimationDef[] Animations,
    AnimationFrame[] Frames, byte[] Trailing, byte[] Source, string[] Warnings);

internal sealed class Cursor {
    readonly byte[] bytes; readonly string label;
    public int Position { get; private set; }
    public int Remaining => bytes.Length - Position;
    public Cursor(byte[] bytes, string label) { this.bytes=bytes; this.label=label; }
    public byte[] Take(int n) {
        if(n<0 || n>Remaining) throw new InvalidDataException($"{label}: truncated at {Position}, need {n}, have {Remaining}");
        var b=bytes.AsSpan(Position,n).ToArray(); Position+=n; return b;
    }
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public short I16() => unchecked((short)U16());
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public string Name(int n) {
        var b=Take(n); int end=Array.IndexOf(b,(byte)0); if(end<0)end=n;
        if(b.Take(end).Any(v=>v<32 || v>126))throw new InvalidDataException($"{label}: unsupported non-ASCII name at {Position-n}");
        return Encoding.ASCII.GetString(b,0,end);
    }
    public void End() { if(Remaining!=0)throw new InvalidDataException($"{label}: unsupported trailing bytes: {Remaining}"); }
}

public static class Import {
    public static MapAsset Map(byte[] bytes) {
        var c=new Cursor(bytes,"MAP"); uint w=c.U32(),h=c.U32();
        if(w<1 || h<1 || w>256 || h>256)throw new InvalidDataException("MAP: dimensions outside observed 1..256");
        int n=checked((int)(w*h)); if(c.Remaining!=n*6)throw new InvalidDataException("MAP: unsupported size/layout");
        var bg=new ushort[n];var fg=new ushort[n];var cells=new MapCell[n];
        for(int i=0;i<n;i++){bg[i]=c.U16();fg[i]=c.U16();}
        for(int i=0;i<n;i++)cells[((int)h-1-i/(int)w)*(int)w+i%(int)w]=new(bg[i],fg[i],c.U16());
        return new((int)w,(int)h,cells,(byte[])bytes.Clone());
    }
    // DC16 45340d..453419 zeroes remapCount*2 bytes; 4534a0 uses that LUT.
    // Unlisted in-range editor IDs therefore resolve to tile index0. This is not guessed art.
    public static MapAsset ResolveTerrain(MapAsset source,TerrainAsset terrain) {
        var lookup=terrain.Tiles.Select((t,i)=>(t.EditorId,i)).ToDictionary(t=>t.EditorId,t=>t.i);
        int Index(ushort id)=>id<terrain.RemapCount?lookup.GetValueOrDefault(id):throw new InvalidDataException("MAP editor ID outside BTS remap table: "+id);
        var cells=source.Cells.Select(c=> {
            int bg=Index(c.BackgroundId),fg=Index(c.ForegroundId);
            return new MapCell(checked((ushort)terrain.Tiles[bg].EditorId),fg==0?(ushort)0:checked((ushort)terrain.Tiles[fg].EditorId),fg==0?(ushort)(c.Attributes&~15):c.Attributes);
        }).ToArray();
        return new(source.Width,source.Height,cells,source.Source);
    }
    public static TerrainAsset Terrain(byte[] bytes) {
        var c=new Cursor(bytes,"BTS");uint remap=c.U32(),n=c.U32();
        if(remap<1 || remap>65536 || n<1 || n>1400 || c.Remaining!=768+(long)n*1028)
            throw new InvalidDataException("BTS: unsupported counts/size");
        var pal=Palette(c);var tiles=new TerrainTile[n];var seen=new HashSet<uint>();
        for(int i=0;i<n;i++){uint id=c.U32();if(id>=remap || !seen.Add(id))throw new InvalidDataException("BTS: invalid/duplicate editor ID");tiles[i]=new(id,c.Take(1024));}
        c.End();return new(remap,pal,tiles,(byte[])bytes.Clone());
    }
    static byte[] Palette(Cursor c) {var p=c.Take(768);if(p.Any(v=>v>63))throw new InvalidDataException("Unsupported palette: components exceed six bits");return p;}
    public static SpriteAsset Sprite(byte[] bytes) {
        var c=new Cursor(bytes,"SPR");ushort flags=c.U16(),n=c.U16();uint hint=c.U32();
        if(flags!=1 && flags!=0x81)throw new InvalidDataException($"SPR: unsupported flags 0x{flags:x4}");
        var pal=Palette(c);if(n>4096 || c.Remaining<n*8)throw new InvalidDataException("SPR: unsupported cell count/directory");
        var dirs=new ushort[n][];for(int i=0;i<n;i++)dirs[i]=new[]{c.U16(),c.U16(),c.U16(),c.U16()};
        var cells=new SpriteCell[n];long total=0;
        for(int i=0;i<n;i++) {
            var d=dirs[i];int size=checked(d[0]*d[1]); total+=size;
            if(size>16777216 || total>67108864)throw new InvalidDataException("SPR: decoded pixel budget exceeded");
            uint length=flags==1?(uint)size:c.U32();if(length>c.Remaining)throw new InvalidDataException("SPR: truncated cell body");
            var encoded=c.Take((int)length);byte[]? coverage=null;
            var pixels=flags==1?(byte[])encoded.Clone():DecodeRle(encoded,size,out coverage);
            cells[i]=new(d[0],d[1],d[2],d[3],pixels,encoded){Coverage=coverage};
        }
        c.End();return new(flags,hint,pal,cells,(byte[])bytes.Clone());
    }
    public static byte[] DecodeRle(byte[] encoded,int pixelCount)=>DecodeRle(encoded,pixelCount,out _);
    public static byte[] DecodeRle(byte[] encoded,int pixelCount,out byte[] coverage) {
        if(pixelCount<0 || pixelCount>16777216)throw new InvalidDataException("RLE: pixel budget exceeded");
        var output=new byte[pixelCount];coverage=new byte[pixelCount];int p=0,q=0;
        while(p<encoded.Length && q<pixelCount) {
            int control=unchecked((sbyte)encoded[p++]);int count=control<0?-control:control+1;
            if(count>pixelCount-q)throw new InvalidDataException("RLE: run exceeds cell pixel count");
            if(control>=0){if(count>encoded.Length-p)throw new InvalidDataException("RLE: truncated literal");Array.Copy(encoded,p,output,q,count);for(int i=0;i<count;i++)coverage[q+i]=255;p+=count;}
            q+=count;
        }
        if(q!=pixelCount || p!=encoded.Length)throw new InvalidDataException("RLE: stream/pixel count mismatch");
        return output;
    }
    public static AnimationAsset Animation(byte[] bytes) {
        var c=new Cursor(bytes,"FIN");ushort flags=c.U16(),nf=c.U16(),na=c.U16(),nb=c.U16();
        if(flags!=0x1d)throw new InvalidDataException($"FIN: unsupported flags 0x{flags:x4}");
        if(nb>256 || nf>8192 || na>8192 || c.Remaining<(long)nb*8+na*20+nf*164)throw new InvalidDataException("FIN: unsupported/truncated layout");
        var banks=new string[nb];for(int i=0;i<nb;i++)banks[i]=c.Name(8);
        var anim=new AnimationDef[na];var names=new HashSet<string>();
        for(int i=0;i<na;i++){string name=c.Name(16);ushort a=c.U16(),b=c.U16();if(a>b || b>=nf || !names.Add(name))throw new InvalidDataException("FIN: invalid animation range/duplicate name");anim[i]=new(name,a,b);}
        var counts=new ushort[nf];var frames=new AnimationFrame[nf];
        for(int i=0;i<nf;i++) {
            counts[i]=c.U16();ushort duration=c.U16();if(counts[i]>100)throw new InvalidDataException("FIN: unsupported cell count");
            var parts=new FramePart[8];for(int j=0;j<8;j++)parts[j]=new(c.Name(16),c.I16(),c.I16());
            frames[i]=new(duration,parts,new FrameCell[counts[i]]);
        }
        for(int i=0;i<nf;i++)for(int j=0;j<counts[i];j++)frames[i].Cells[j]=new(c.Name(8),c.U16(),c.I16(),c.I16(),new[]{c.U16(),c.U16(),c.U16(),c.U16()});
        var trailing=c.Take(c.Remaining);
        return new(flags,banks,anim,frames,trailing,(byte[])bytes.Clone(),trailing.Length==0?Array.Empty<string>():new[]{ $"FIN: preserved {trailing.Length} uninterpreted trailing bytes" });
    }
}
