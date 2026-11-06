namespace DarkColony.Assets;

public sealed record Raster(int Width,int Height,byte[] Rgba);
public sealed record Placement(int X,int Y,int Width,int Height);
public static class Presentation {
    // Canonical importer stores z-up coordinates. PNG screen y is downward: file row order.
    public static int ScreenRow(MapAsset map,int z)=>map.Height-1-z;
    public static Raster Indexed(byte[] indices,int width,int height,byte[] palette6,bool zeroTransparent) {
        if(indices.Length!=checked(width*height) || palette6.Length!=768)throw new ArgumentException("Invalid image dimensions/palette");
        var rgba=new byte[indices.Length*4];
        for(int i=0;i<indices.Length;i++) {
            int id=indices[i];for(int j=0;j<3;j++){int v=palette6[id*3+j];rgba[i*4+j]=(byte)((v<<2)|(v>>4));}
            rgba[i*4+3]=(byte)(zeroTransparent && id==0?0:255);
        }
        return new(width,height,rgba);
    }
    public static Raster Terrain(MapAsset map,TerrainAsset terrain,bool foreground,Rgb8[]? colors=null) {
        var bank=terrain.ByEditorId;var raster=new Raster(map.Width*32,map.Height*32,new byte[map.Width*32*map.Height*32*4]);
        for(int z=0;z<map.Height;z++)for(int x=0;x<map.Width;x++) {
            var c=map.At(x,z);ushort id=foreground?c.ForegroundId:c.BackgroundId;
            if(foreground && id==0)continue;
            if(!bank.TryGetValue(id,out var tile))throw new InvalidDataException($"Unresolved BTS editor ID {id} at ({x},{z}), foreground={foreground}");
            // 0x45050c: both layers occupy the same tile. Attributes 0x20/0x40 flip X.
            var indices=tile.Indices;
            if((c.Attributes & (foreground?0x40:0x20))!=0) {
                indices=new byte[1024];
                for(int ty=0;ty<32;ty++)for(int tx=0;tx<32;tx++)indices[ty*32+tx]=tile.Indices[ty*32+31-tx];
            }
            Blit(raster,colors==null?Indexed(indices,32,32,terrain.Palette6,foreground):GameColors.Indexed(indices,32,32,colors,zeroTransparent:foreground),x*32,ScreenRow(map,z)*32);
        }
        return raster;
    }
    public static Placement AnimationPlacement(FrameCell cell,SpriteCell sprite) =>
        // World body path 0x439934 -> 0x436328 -> 0x4695e0. OffsetY is NOT used.
        // FIN x*8 / -y*8 is applied before fixed-world projection; no pose recentering.
        new(cell.X+sprite.OffsetX,cell.Y-sprite.Height,sprite.Width,sprite.Height);
    public static void Blit(Raster destination,Raster source,int x,int y) {
        for(int sy=0;sy<source.Height;sy++)for(int sx=0;sx<source.Width;sx++) {
            int dx=x+sx,dy=y+sy;if(dx<0 || dy<0 || dx>=destination.Width || dy>=destination.Height)continue;
            int si=(sy*source.Width+sx)*4,di=(dy*destination.Width+dx)*4;
            if(source.Rgba[si+3]!=0)Array.Copy(source.Rgba,si,destination.Rgba,di,4);
        }
    }
    public static void RequireSelectedFrame(AnimationFrame frame) {
        if(frame.Parts.Any(p=>p.Name!="NONAME" && p.Name!=""))throw new NotSupportedException("Nested FIN animation rendering not implemented; records preserved");
        if(frame.Cells.Any(c=>!c.UnknownWords.SequenceEqual(new ushort[]{0,16,1,0})))
            throw new NotSupportedException("FIN rendering supports only observed TRSCMOVE0 extra words [0,16,1,0]; records preserved");
    }
}
