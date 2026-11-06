namespace DarkColony.Assets;
public static class ArtworkSampling {
 // Extrude RGB only into fully transparent texels to prevent sampling fringes.
 // A transparent gutter prevents texture-edge clamping from clipping the fade.
 // Scale2 is nearest replication followed by GPU linear filtering: half as wide
 // a footprint as ordinary bilinear, without inventing sprite detail.
 public static Raster Prepare(Raster source,int scale,bool gutter) {
  if(scale is <1 or >2)throw new ArgumentOutOfRangeException(nameof(scale));
  int pad=gutter?1:0,w=source.Width+pad*2,h=source.Height+pad*2;
  var result=new byte[w*h*scale*scale*4];
  for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
   int sx=x-pad,sy=y-pad,p=sy*source.Width+sx;bool inside=sx>=0&&sy>=0&&sx<source.Width&&sy<source.Height;
   int r=0,g=0,b=0,a=inside?source.Rgba[p*4+3]:0;
   if(a!=0){r=source.Rgba[p*4];g=source.Rgba[p*4+1];b=source.Rgba[p*4+2];}
   else {int weight=0;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=sx+dx,ny=sy+dy;if(nx<0||ny<0||nx>=source.Width||ny>=source.Height)continue;int n=(ny*source.Width+nx)*4,alpha=source.Rgba[n+3];weight+=alpha;r+=source.Rgba[n]*alpha;g+=source.Rgba[n+1]*alpha;b+=source.Rgba[n+2]*alpha;}if(weight>0){r/=weight;g/=weight;b/=weight;}}
   for(int yy=0;yy<scale;yy++)for(int xx=0;xx<scale;xx++){int at=((y*scale+yy)*(w*scale)+x*scale+xx)*4;result[at]=(byte)r;result[at+1]=(byte)g;result[at+2]=(byte)b;result[at+3]=(byte)a;}
  }
  return new(w*scale,h*scale,result);
 }
}
