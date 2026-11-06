using System.Security.Cryptography;
using System.Text;
namespace DarkColony.ContentImport;
internal static class MusicConverter {
 public static string? Convert(AudioTrack track,string target,CancellationToken cancel){
  if(track.Number is <2 or >5||track.Sectors<75||track.Sectors>75*60*30||track.Stride is not(2352 or 2448))throw new InvalidDataException("Unsupported CDDA track bounds");
  using var input=File.OpenRead(track.File);if(track.Offset<0||track.Offset+track.Sectors*track.Stride>input.Length)throw new InvalidDataException("Truncated CDDA source");input.Position=track.Offset;
  string sourceHash=ContentContract.Hash(track.File);
  string[] nonAudio={"4E6614F1133CAC28C52B28F81748F0FF1D8F43F0D4239D77CAD6512846865920","9EAB81FEB6201189693292B25D17A3954876CD657FDEB44475ECDD8FD30ADEB7","39B78F4B74634A9F58B83196E69B01005E1B90988A4484C1570D41BCFC55E5F5","C6D234F459282C44B7B3277FED12F090CA556364E35E543C6FAF05F3E6751233"};
  if(nonAudio.Contains(sourceHash))return null; // Recorded scrambled-data tracks; never a whitelist of legitimate music.
  Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var output=new FileStream(target,FileMode.CreateNew);using var writer=new BinaryWriter(output,Encoding.ASCII,true);int bytes=checked((int)(track.Sectors*2352));writer.Write("RIFF"u8);writer.Write(bytes+36);writer.Write("WAVEfmt "u8);writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(44100);writer.Write(176400);writer.Write((short)4);writer.Write((short)16);writer.Write("data"u8);writer.Write(bytes);
  byte[] sector=new byte[track.Stride];for(long i=0;i<track.Sectors;i++){cancel.ThrowIfCancellationRequested();input.ReadExactly(sector);output.Write(sector,0,2352);}return sourceHash;
 }
}
