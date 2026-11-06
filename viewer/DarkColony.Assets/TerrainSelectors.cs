using System.Text;
namespace DarkColony.Assets;

// Exact DC16 adapter. Neutral field names deliberately avoid asserting day/night or fog semantics.
public sealed record ScenarioTerrainHeader(string TerrainName,int Phase,int Period,int Counter,int Ramp,byte[] Source) {
    // SCN terrain basename selects the world GIF: DC16 41e982..41ea43.
    public string PaletteName=>Path.GetFileNameWithoutExtension(TerrainName).ToUpperInvariant()+".GIF";
    public int InitialCoefficient=>Phase*256; // 41bd00..41bd12, before tick updates
    public static ScenarioTerrainHeader Read(byte[] source) {
        var lines=Encoding.ASCII.GetString(source).Split('\n').Select(s=>s.Trim()).Where(s=>s.Length>0 && !s.StartsWith("%")).Take(8).ToArray();
        if(lines.Length!=8 || !int.TryParse(lines[4],out int phase) || !int.TryParse(lines[5],out int period) ||
           !int.TryParse(lines[6],out int counter) || !int.TryParse(lines[7],out int ramp))throw new InvalidDataException("Unsupported SCN terrain header");
        if(phase is <0 or >1 || period<0 || counter<0 || ramp<=0)throw new NotSupportedException("Unsupported SCN selector state");
        return new(lines[0],phase,period,counter,ramp,(byte[])source.Clone());
    }
}
public sealed class FadeTable {
    public byte[] Source {get;}
    public FadeTable(byte[] source) {
        if(source.Length!=17*17*32)throw new InvalidDataException("FADE requires 9248 bytes");
        // Retain source; reject unverified custom tables instead of silently replacing their values.
        for(int a=0;a<=16;a++)for(int b=0;b<=16;b++)for(int x=0;x<32;x++)
            if(source[a*544+b*32+x]!=((a*(31-x)+b*x)/31<<3))throw new NotSupportedException("FADE variant differs from DC16 generator");
        Source=(byte[])source.Clone();
    }
    public byte Lookup(int a,int b,int x,int lowSelector) => (byte)((Source[a*544+b*32+x]&248)|lowSelector);
}
public sealed record TerrainSelectorSurface(int PixelX,int PixelY,int Width,int Height,byte[] Values);
public static class TerrainSelectors {
    public static int LowSelector(int coefficient) {
        if(coefficient<0 || coefficient>256)throw new NotSupportedException("Only supported coefficient range 0..256");
        return coefficient*7/256; // 40ac9f..40acc3 signed IDIV-equivalent, nonnegative supported domain
    }
    public static int CellLevel(uint word,uint ownerMask,bool bypass) => bypass?16:(word&0x80000000)==0?0:(word&ownerMask)==0?10:16;
    // words indexed logical z-up, unlike the source-pixel region's downward Y. One-cell halo clamped at map edges.
    // Limited to the traced normal 512x448 context, or a tile-aligned subset. Other zoom modes are not implied.
    public static TerrainSelectorSurface Build(FadeTable fade,uint[] words,int mapWidth,int mapHeight,
        int tileX,int fileRow,int columns,int rows,uint ownerMask,bool bypass,int coefficient) {
        if(mapWidth<=0 || mapHeight<=0 || words.Length!=checked(mapWidth*mapHeight))throw new ArgumentException("Cell-state dimensions");
        if(columns<1 || columns>16 || rows<1 || rows>14)throw new NotSupportedException("Only normal-view 16x14 tiles or subsets");
        if(tileX<0 || fileRow<0 || tileX+columns>mapWidth || fileRow+rows>mapHeight)throw new ArgumentOutOfRangeException("Region");
        int selector=LowSelector(coefficient),width=columns*32,height=rows*32;
        int Q(int x,int y) {
            x=Math.Max(0,Math.Min(mapWidth-1,tileX+x));y=Math.Max(0,Math.Min(mapHeight-1,fileRow+y));
            return CellLevel(words[(mapHeight-1-y)*mapWidth+x],ownerMask,bypass);
        }
        var corners=new int[(columns+1)*(rows+1)];
        for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            corners[y*(columns+1)+x]=(Q(x-1,y-1)+Q(x,y-1)+Q(x-1,y)+Q(x,y))/4;
        var bytes=new byte[width*height];
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++) {
            int p=y*(columns+1)+x;
            for(int py=0;py<32;py++) {
                int left=fade.Lookup(corners[p],corners[p+columns+1],py,selector)>>3;
                int right=fade.Lookup(corners[p+1],corners[p+columns+2],py,selector)>>3;
                for(int px=0;px<32;px++)bytes[(y*32+py)*width+x*32+px]=fade.Lookup(left,right,px,selector);
            }
        }
        return new(tileX*32,fileRow*32,width,height,bytes);
    }
    // Original indexed rendering ends at Raster. VisualAsset can still supply ordinary enhanced RGBA art.
    public static Raster Render(MapAsset map,TerrainAsset terrain,GifPalette palette,TerrainSelectorSurface surface,bool foreground) {
        if(surface.Values.Length!=checked(surface.Width*surface.Height) || surface.PixelX<0 || surface.PixelY<0 ||
           surface.PixelX+surface.Width>map.Width*32 || surface.PixelY+surface.Height>map.Height*32)throw new ArgumentException("Selector surface dimensions");
        var colors=surface.Values.Distinct().ToDictionary(s=>s,s=>GameColors.Table(palette,s&7,s>>3,ColorBank.Terrain));
        var rgba=new byte[surface.Values.Length*4];var bank=terrain.ByEditorId;
        for(int y=0;y<surface.Height;y++)for(int x=0;x<surface.Width;x++) {
            int wx=x+surface.PixelX,wy=y+surface.PixelY,p=y*surface.Width+x;
            var cell=map.At(wx/32,map.Height-1-wy/32);ushort id=foreground?cell.ForegroundId:cell.BackgroundId;
            if(foreground && id==0)continue;
            if(!bank.TryGetValue(id,out var tile))throw new InvalidDataException($"Unresolved BTS editor ID {id}");
            int tx=wx%32;if((cell.Attributes&(foreground?64:32))!=0)tx=31-tx;
            byte index=tile.Indices[(wy%32)*32+tx];var c=colors[surface.Values[p]][index];
            rgba[p*4]=(byte)c.R;rgba[p*4+1]=(byte)c.G;rgba[p*4+2]=(byte)c.B;
            rgba[p*4+3]=(byte)(foreground && index==0?0:255);
        }
        return new(surface.Width,surface.Height,rgba);
    }
    public static Raster Visualize(TerrainSelectorSurface surface) {
        var rgba=new byte[surface.Values.Length*4];
        for(int p=0;p<surface.Values.Length;p++){byte c=(byte)((surface.Values[p]>>3)*15);rgba[p*4]=c;rgba[p*4+1]=c;rgba[p*4+2]=c;rgba[p*4+3]=255;}
        return new(surface.Width,surface.Height,rgba);
    }
}
