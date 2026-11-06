namespace DarkColony.Assets;

public sealed record PixelAnchor(int X,int Y);
public static class OriginalCoordinates {
    // Original units: 256/tile, 8/source pixel; z up. 0x436328 includes the -1.
    public static PixelAnchor Project(int worldX,int worldZ,int height,int mapRows) =>
        new(worldX>>3,((mapRows*256-worldZ-1)>>3)-(height>>3));
}

// Narrow reconstruction of the nonmirrored ground body mask, not entity Y-sorting.
// Logical mask texels are original map pixels, independent of replacement art resolution.
public sealed class TerrainOcclusion {
    readonly MapAsset map;readonly byte[] mask;
    public int Width=>map.Width*32;
    public int Height=>map.Height*32;
    public TerrainOcclusion(MapAsset map,TerrainAsset terrain) {
        this.map=map;mask=new byte[Width*Height];var tiles=terrain.ByEditorId;
        for(int row=0;row<map.Height;row++)for(int col=0;col<map.Width;col++) {
            var cell=map.At(col,map.Height-1-row);if(cell.ForegroundId==0)continue;
            if(!tiles.TryGetValue(cell.ForegroundId,out var tile))throw new InvalidDataException("Unresolved foreground ID "+cell.ForegroundId);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                mask[(row*32+y)*Width+col*32+x]=(byte)(tile.Indices[y*32+((cell.Attributes&0x40)!=0?31-x:x)]!=0?1:0);
        }
    }
    // 0x469500: T=h-level*32+31-(bottom&31), and 0x468454 masks only if T-sy<0.
    // Equivalent absolute cutoff: row*32+31-level*32. Equality stays visible.
    // Bottom includes FIN.Y. No-foreground cells have level cleared by 0x4534a0.
    public int Cutoff(int pixelX,int bottomY) {
        if(pixelX<0 || pixelX>=Width || bottomY<0 || bottomY>=Height)
            throw new ArgumentOutOfRangeException("Cutoff requires an in-map body anchor/column");
        int row=bottomY>>5;var cell=map.At(pixelX>>5,map.Height-1-row);
        int level=cell.ForegroundId==0?0:cell.Attributes&15;
        return row*32+31-level*32;
    }
    public bool Hidden(int pixelX,int pixelY,int bottomY) =>
        HiddenWithCutoffOffset(pixelX,pixelY,bottomY,0);
    // Conservative rejection only. A false result proves the exact per-pixel
    // mask cannot touch this rectangle; a true result still uses the old blitter.
    // Both body and projected-shadow cutoffs depend on the anchor's terrain row.
    public bool MayMask(int left,int top,int width,int height,int bottomY,int offset=0) {
        if(bottomY<0||bottomY>=Height)return false;
        int right=Math.Min(Width-1,left+width-1),bottom=Math.Min(Height-1,top+height-1);
        left=Math.Max(0,left);top=Math.Max(0,top);
        if(left>right||top>bottom)return false;
        for(int col=left>>5;col<=right>>5;col++) {
            int first=Math.Max(top,Cutoff(col*32,bottomY)+offset+1);
            for(int row=first>>5;first<=bottom&&row<=bottom>>5;row++)
                if(map.At(col,map.Height-1-row).ForegroundId!=0)return true;
        }
        return false;
    }
    public bool HiddenWithCutoffOffset(int pixelX,int pixelY,int bottomY,int offset) =>
        pixelX>=0 && pixelX<Width && pixelY>=0 && pixelY<Height &&
        bottomY>=0 && bottomY<Height && pixelY>Cutoff(pixelX,bottomY)+offset && mask[pixelY*Width+pixelX]!=0;
    public Raster ApplyGroundBody(VisualAsset visual,int anchorX,int anchorY,int bottomY) {
        var source=visual.Pixels;var output=new Raster(source.Width,source.Height,(byte[])source.Rgba.Clone());
        for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++) {
            if(output.Rgba[(y*source.Width+x)*4+3]==0)continue;
            int px=(int)Math.Floor(anchorX-visual.OriginX+(x+.5)*visual.LogicalWidth/source.Width);
            int py=(int)Math.Floor(anchorY-visual.OriginY+(y+.5)*visual.LogicalHeight/source.Height);
            if(Hidden(px,py,bottomY))output.Rgba[(y*source.Width+x)*4+3]=0;
        }
        return output;
    }
}
