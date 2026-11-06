namespace DarkColony.Assets;

public sealed record PixelAnchor(int X,int Y);
public static class OriginalCoordinates {
    // Original units: 256/tile, 8/source pixel; z up. 0x436328 includes the -1.
    public static PixelAnchor Project(int worldX,int worldZ,int height,int mapRows) =>
        new(worldX>>3,((mapRows*256-worldZ-1)>>3)-(height>>3));
}

// Native cutoff plus a presentation-only release after passing tall foreground.
// Logical mask texels are original map pixels, independent of replacement art resolution.
public sealed class TerrainOcclusion {
    readonly MapAsset map;readonly ushort[] foregroundBase;
    public int Width=>map.Width*32;
    public int Height=>map.Height*32;
    public TerrainOcclusion(MapAsset map,TerrainAsset terrain) {
        this.map=map;
        if(Height>=ushort.MaxValue)throw new InvalidDataException("Terrain exceeds foreground depth range");
        var mask=new byte[checked(Width*Height)];var tiles=terrain.ByEditorId;
        for(int row=0;row<map.Height;row++)for(int col=0;col<map.Width;col++) {
            var cell=map.At(col,map.Height-1-row);if(cell.ForegroundId==0)continue;
            if(!tiles.TryGetValue(cell.ForegroundId,out var tile))throw new InvalidDataException("Unresolved foreground ID "+cell.ForegroundId);
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                mask[(row*32+y)*Width+col*32+x]=(byte)(tile.Indices[y*32+((cell.Attributes&0x40)!=0?31-x:x)]!=0?1:0);
        }
        foregroundBase=BuildForegroundBases(mask);
    }
    // Presentation refinement: a tall, connected foreground shape cannot hide a
    // ground body after its anchor has passed the shape's southernmost pixel.
    // Build once per map; no flood-fill, allocations or state changes during draw.
    // Small detached details retain the native mask rather than becoming floating
    // leaves with independent depth. Touching shapes conservatively share a base.
    ushort[] BuildForegroundBases(byte[] mask) {
        var bases=new ushort[mask.Length];var pending=new Stack<int>();
        var spans=new List<(int Start,int Count)>();
        for(int seed=0;seed<mask.Length;seed++) {
            if(mask[seed]!=1)continue;
            pending.Push(seed);spans.Clear();int north=Height,south=0;
            while(pending.Count!=0) {
                int at=pending.Pop();if(mask[at]!=1)continue;
                int row=at/Width,x=at%Width,left=x,right=x;
                while(left>0&&mask[row*Width+left-1]==1)left--;
                while(right+1<Width&&mask[row*Width+right+1]==1)right++;
                int first=row*Width+left,count=right-left+1;
                Array.Fill(mask,(byte)2,first,count);spans.Add((first,count));
                north=Math.Min(north,row);south=Math.Max(south,row);
                for(int delta=-1;delta<=1;delta+=2) {
                    int y=row+delta;if(y<0||y>=Height)continue;
                    int end=Math.Min(Width-1,right+1);
                    for(int col=Math.Max(0,left-1);col<=end;col++)if(mask[y*Width+col]==1) {
                        pending.Push(y*Width+col);
                        while(col<end&&mask[y*Width+col+1]==1)col++;
                    }
                }
            }
            ushort depth=south-north+1>=32?(ushort)(south+1):ushort.MaxValue;
            foreach(var span in spans)Array.Fill(bases,depth,span.Start,span.Count);
        }
        return bases;
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
    // native mask cannot touch this rectangle; a true result uses the full body/shadow mask.
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
        bottomY>=0 && bottomY<Height && pixelY>Cutoff(pixelX,bottomY)+offset && foregroundBase[pixelY*Width+pixelX]!=0;
    // Keep the exact native cutoff API for reference checks. Body/shadow drawing
    // adds only this one-way release, using ground position rather than FIN bob.
    public bool HiddenForGround(int pixelX,int pixelY,int bottomY,int groundY,int offset=0) =>
        HiddenWithCutoffOffset(pixelX,pixelY,bottomY,offset) &&
        groundY<foregroundBase[pixelY*Width+pixelX];
    public Raster ApplyGroundBody(VisualAsset visual,int anchorX,int anchorY,int bottomY) {
        var source=visual.Pixels;var output=new Raster(source.Width,source.Height,(byte[])source.Rgba.Clone());
        for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++) {
            if(output.Rgba[(y*source.Width+x)*4+3]==0)continue;
            int px=(int)Math.Floor(anchorX-visual.OriginX+(x+.5)*visual.LogicalWidth/source.Width);
            int py=(int)Math.Floor(anchorY-visual.OriginY+(y+.5)*visual.LogicalHeight/source.Height);
            if(HiddenForGround(px,py,bottomY,anchorY))output.Rgba[(y*source.Width+x)*4+3]=0;
        }
        return output;
    }
}
