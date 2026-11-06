// Cinepak RGB decoder, adapted to bounded managed buffers from FFmpeg n7.1
// libavcodec/cinepak.c. Copyright (C) 2003 The FFmpeg project.
// Original author Ewald Snel; colorspace support (C) 2013 Rl, Aetey Global Technologies AB.
// Managed adaptation (C) 2026 Dennis Nilsson.
// SPDX-License-Identifier: LGPL-2.1-or-later
// See COPYING.LGPLv2.1 and NOTICE.txt in this directory. No FFmpeg executable is used.
using System.Buffers.Binary;
namespace DarkColony.Media;

public sealed class Cinepak {
 sealed class Strip { public readonly byte[] V1=new byte[256*12],V4=new byte[256*12]; }
 readonly Strip[] strips=Enumerable.Range(0,32).Select(_=>new Strip()).ToArray();
 readonly int width,height;
 public byte[] Rgb {get;}
 public Cinepak(int w,int h){if(w<4||h<4||w>2048||h>2048||w%4!=0)throw new InvalidDataException("Unsupported Cinepak dimensions");width=w;height=h;Rgb=new byte[checked(w*h*3)];}
 static int U16(ReadOnlySpan<byte> b,int p)=>BinaryPrimitives.ReadUInt16BigEndian(b.Slice(p,2));
 static int U24(ReadOnlySpan<byte> b,int p)=>(b[p]<<16)|(b[p+1]<<8)|b[p+2];
 ref struct Bits {
  readonly ReadOnlySpan<byte> bytes;int at;uint flags,mask;
  public Bits(ReadOnlySpan<byte> b){bytes=b;at=0;flags=mask=0;}
  public int Left=>bytes.Length-at;
  public byte Byte(){if(at>=bytes.Length)throw new InvalidDataException("Truncated Cinepak vector");return bytes[at++];}
  public bool Bit(){if((mask>>=1)==0){if(Left<4)throw new InvalidDataException("Truncated Cinepak flags");flags=BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);at+=4;mask=0x80000000;}return (flags&mask)!=0;}
 }
 static void Book(byte[] book,int id,ReadOnlySpan<byte> data){
  var r=new Bits(data);bool partial=(id&1)!=0,gray=(id&4)!=0;
  for(int i=0;i<256&&r.Left>0;i++){
   if(partial&&!r.Bit())continue;
   if(r.Left<(gray?4:6))throw new InvalidDataException("Truncated Cinepak codebook");
   int a=r.Byte(),b=r.Byte(),c=r.Byte(),d=r.Byte();int u=0,v=0;if(!gray){u=(sbyte)r.Byte();v=(sbyte)r.Byte();}
   for(int j=0;j<4;j++){int y=j switch{0=>a,1=>b,2=>c,_=>d},p=i*12+j*3;book[p]=(byte)Math.Clamp(y+v*2,0,255);book[p+1]=(byte)Math.Clamp(y-u/2-v,0,255);book[p+2]=(byte)Math.Clamp(y+u*2,0,255);}
  }
 }
 void Vectors(Strip strip,int id,ReadOnlySpan<byte> data,int x1,int y1,int x2,int y2){
  var r=new Bits(data);
  for(int y=y1;y<y2;y+=4)for(int x=x1;x<x2;x+=4){
   if((id&1)!=0&&!r.Bit())continue;
   bool v1=(id&2)!=0||!r.Bit();
   if(v1){int index=r.Byte()*12;for(int yy=0;yy<4;yy++)for(int xx=0;xx<4;xx++)Pixel(strip.V1,index+((yy/2)*2+xx/2)*3,x+xx,y+yy);}
   else {for(int q=0;q<4;q++){int index=r.Byte()*12;for(int yy=0;yy<2;yy++)for(int xx=0;xx<2;xx++)Pixel(strip.V4,index+(yy*2+xx)*3,x+(q%2)*2+xx,y+(q/2)*2+yy);}}
  }
 }
 void Pixel(byte[] book,int at,int x,int y){if(x>=width||y>=height)return;int p=(y*width+x)*3;Rgb[p]=book[at];Rgb[p+1]=book[at+1];Rgb[p+2]=book[at+2];}
 public void Decode(ReadOnlySpan<byte> packet){
  if(packet.Length<10||U24(packet,1)>packet.Length||U16(packet,4)!=width||U16(packet,6)!=height)throw new InvalidDataException("Invalid Cinepak frame");
  int count=U16(packet,8);if(count>32)throw new InvalidDataException("Too many Cinepak strips");int p=10,y0=0;
  for(int i=0;i<count;i++){
   if(p+12>packet.Length)throw new InvalidDataException("Truncated Cinepak strip");
   int length=U24(packet,p+1),y1=U16(packet,p+4),x1=U16(packet,p+6),y2=U16(packet,p+8),x2=U16(packet,p+10);
   if(y1==0){y1=y0;y2+=y0;}if(length<12||p+length>packet.Length||x1>=x2||y1>=y2||x2>width||y2>height||x1%4!=0)throw new InvalidDataException("Invalid Cinepak strip bounds");
   var strip=strips[i];if(i>0&&(packet[0]&1)==0){strips[i-1].V1.CopyTo(strip.V1,0);strips[i-1].V4.CopyTo(strip.V4,0);}
   int end=p+length,q=p+12;bool decoded=false;
   while(q+4<=end){int id=packet[q],size=U24(packet,q+1);if(size<4||q+size>end)throw new InvalidDataException("Invalid Cinepak chunk");var data=packet.Slice(q+4,size-4);
    switch(id){case 0x20:case 0x21:case 0x24:case 0x25:Book(strip.V4,id,data);break;case 0x22:case 0x23:case 0x26:case 0x27:Book(strip.V1,id,data);break;case 0x30:case 0x31:case 0x32:Vectors(strip,id,data,x1,y1,x2,y2);decoded=true;break;default:throw new InvalidDataException($"Unsupported Cinepak chunk {id:x}");}
    q+=size;if(decoded)break;
   }
   if(!decoded)throw new InvalidDataException("Cinepak strip has no vectors");p=end;y0=y2;
  }
 }
}
