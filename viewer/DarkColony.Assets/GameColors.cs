using System.Text;
namespace DarkColony.Assets;

public sealed record Rgb8(int R,int G,int B) {
    public int[] Channels=>new[]{R,G,B};
    public override string ToString()=>$"{R:D3},{G:D3},{B:D3}";
}
public sealed record GifPalette(byte[] RawRgb8,byte[] ActiveRgb8,byte[] Source);
public enum ColorBank { Terrain, Body }
public enum PixelPacking { Rgb565, Rgb555 }
public sealed record ColorTrace(int Index,int MappedIndex,Rgb8 Source,Rgb8 Mapped,Rgb8 Mixed,Rgb8 Lit,ushort Packed,Rgb8 Display);

// DC16 SHA 3159a24c...; evidence/palette/INDEX.txt. No engine or file I/O.
// Selector parameters are explicit: supporting a lookup is NOT evidence it was selected at runtime.
public static class GameColors {
    static readonly int[] TeamFive={47,61,65,66,67,254}; // VA 0x48c058
    public static GifPalette ReadGifPalette(byte[] bytes) {
        if(bytes.Length<13 || !(Encoding.ASCII.GetString(bytes,0,6) is "GIF87a" or "GIF89a"))
            throw new InvalidDataException("Palette: invalid/truncated GIF header");
        if((bytes[10]&0x80)==0 || (2<<(bytes[10]&7))!=256)
            throw new NotSupportedException("Palette: only a 256-entry GIF global table is supported; no image decoding attempted");
        if(bytes.Length<781)throw new InvalidDataException("Palette: truncated global table");
        var raw=bytes.AsSpan(13,768).ToArray();var active=(byte[])raw.Clone();
        // GIF loader 0x44ed42..0x44ed66 overrides endpoints even for palette-only reads.
        Array.Clear(active,0,3);for(int i=765;i<768;i++)active[i]=255;
        return new(raw,active,(byte[])bytes.Clone());
    }
    public static ColorTrace Trace(GifPalette palette,int index,int selector,int light,ColorBank bank,PixelPacking format=PixelPacking.Rgb565) {
        if(index<0 || index>255 || selector<0 || selector>7 || light<0 || light>31)throw new ArgumentOutOfRangeException("Palette index/selector/light");
        if(!Enum.IsDefined(typeof(ColorBank),bank) || !Enum.IsDefined(typeof(PixelPacking),format))throw new NotSupportedException("Unknown color bank/packing");
        if(palette.ActiveRgb8.Length!=768)throw new ArgumentException("Palette requires 768 RGB8 bytes");
        Rgb8 Read(int id)=>new(palette.ActiveRgb8[id*3],palette.ActiveRgb8[id*3+1],palette.ActiveRgb8[id*3+2]);
        bool team=index>=138 && index<144;
        int mapped=team?(selector==5?TeamFive[index-138]:index-(7-selector)*6):index;
        var source=Read(index);var rgb=Read(mapped);var mixed=rgb;
        if(bank==ColorBank.Terrain && !team) {
            int weighted=3*rgb.R+6*rgb.G+rgb.B;
            int Mix(int c)=>(10*c*(11-selector)+weighted*selector)/110;
            mixed=new(Mix(rgb.R),Mix(rgb.G),Mix(rgb.B));
        }
        int Light(int c)=>Math.Min(255,c*light/16);
        var lit=new Rgb8(Light(mixed.R),Light(mixed.G),Light(mixed.B));
        ushort packed=Pack(lit,format);
        return new(index,mapped,source,rgb,mixed,lit,packed,Expand(packed,format));
    }
    public static ushort Pack(Rgb8 rgb,PixelPacking format) {
        if(rgb.Channels.Any(c=>c<0 || c>255))throw new ArgumentOutOfRangeException(nameof(rgb));
        return format switch {
            PixelPacking.Rgb565=>(ushort)((rgb.R>>3)<<11 | (rgb.G>>2)<<5 | rgb.B>>3),
            PixelPacking.Rgb555=>(ushort)((rgb.R>>3)<<10 | (rgb.G>>3)<<5 | rgb.B>>3),
            _=>throw new NotSupportedException("Unknown packing")};
    }
    // Modern RGBA display convention: bit replication. Exact original result is Packed.
    // This expansion does not establish the original monitor/driver's analog output.
    public static Rgb8 Expand(ushort packed,PixelPacking format) {
        int E5(int v)=>(v<<3)|(v>>2);int E6(int v)=>(v<<2)|(v>>4);
        return format switch {
            PixelPacking.Rgb565=>new(E5(packed>>11),E6((packed>>5)&63),E5(packed&31)),
            PixelPacking.Rgb555=>new(E5((packed>>10)&31),E5((packed>>5)&31),E5(packed&31)),
            _=>throw new NotSupportedException("Unknown packing")};
    }
    public static Rgb8[] Table(GifPalette palette,int selector,int light,ColorBank bank,PixelPacking format=PixelPacking.Rgb565)=>
        Enumerable.Range(0,256).Select(i=>Trace(palette,i,selector,light,bank,format).Display).ToArray();
    public static Raster Indexed(byte[] indices,int width,int height,Rgb8[] colors,byte[]? coverage=null,bool zeroTransparent=false) {
        if(indices.Length!=checked(width*height) || colors.Length!=256 || (coverage!=null && coverage.Length!=indices.Length))throw new ArgumentException("Image dimensions/table/coverage");
        var rgba=new byte[indices.Length*4];
        for(int i=0;i<indices.Length;i++) {
            var c=colors[indices[i]];rgba[i*4]=(byte)c.R;rgba[i*4+1]=(byte)c.G;rgba[i*4+2]=(byte)c.B;
            rgba[i*4+3]=coverage!=null?coverage[i]:(byte)(zeroTransparent && indices[i]==0?0:255);
        }
        return new(width,height,rgba);
    }
    public static Raster Body(SpriteCell sprite,Rgb8[] colors)=>Indexed(sprite.Indices,sprite.Width,sprite.Height,colors,
        sprite.Coverage??throw new NotSupportedException("Body color path requires retained RLE coverage"));
}
