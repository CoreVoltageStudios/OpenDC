namespace DarkColony.Assets;

// DC16 453d14: cell levels 0/10/16, four-cell corner average, then two-axis
// interpolation. GPU linear interpolation removes FADE's 17-level/565 banding.
// This is terrain shading, never an entity/target visibility query.
public static class ContourFog {
 public static Raster Build(int width,int height,ReadOnlySpan<byte> knowledge) {
  var pixels=new byte[checked((width+1)*(height+1)*4)];
  Write(width,height,knowledge,pixels);return new(width+1,height+1,pixels);
 }
 public static void Write(int width,int height,ReadOnlySpan<byte> knowledge,Span<byte> rgba) {
  if(width<1||height<1||knowledge.Length!=width*height||rgba.Length!=(width+1)*(height+1)*4)throw new ArgumentException("Fog dimensions");
  for(int y=0;y<=height;y++)for(int x=0;x<=width;x++) {
   int sum=0;
   for(int dy=-1;dy<=0;dy++)for(int dx=-1;dx<=0;dx++) {
    int state=knowledge[Math.Clamp(y+dy,0,height-1)*width+Math.Clamp(x+dx,0,width-1)];
    sum+=state switch{0=>0,1=>10,2=>16,_=>throw new ArgumentException("Fog knowledge state")};
   }
   int p=(y*(width+1)+x)*4;rgba[p]=rgba[p+1]=rgba[p+2]=0;rgba[p+3]=(byte)Math.Round((16-(sum>>2))*255d/16);
  }
 }
}
