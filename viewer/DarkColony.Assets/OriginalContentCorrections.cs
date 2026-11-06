using System.Security.Cryptography;
namespace DarkColony.Assets;

// Versioned import overlay, never a write to the player's original files. This
// exact correction comes from the investigated community-hosted English patch;
// the archive's publisher provenance has not been authenticated.
public static class OriginalContentCorrections {
 public const string Human09Path="SCENARIO/HUMAN/HUMAN09.TRO";
 public const string Human09Id="human09-victory-and-1";
 public const string Human09Original="7349E54940D75F4DDA60B09533B0D2CA13A7DACDBAE8F9414D47AA032FD6F8A9";
 public const string Human09Corrected="79D3CFE17F61F2F4B7CFE3F73188B8D19B7E743033D061EFCC83FC21A444C2F9";
 public const string Human09Archive="371351027123B4DC03B1643664CAD2775CC27636C6CC60B54C5B2010364B1ACD";
 static string Hash(byte[] bytes){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");}
 public static ScenarioSource Read(string path,byte[] input) {
  string original=Hash(input);byte[] bytes=(byte[])input.Clone();string? correction=null;
  if(path.Replace('\\','/').Equals(Human09Path,StringComparison.OrdinalIgnoreCase)){
   correction=Human09Id;
   if(original==Human09Original){
    // Known 1113-byte input: remove only offsets149 and150. No text replacement,
    // parser exception or relaxed matching for unknown editions/modified files.
    bytes=new byte[input.Length-2];input.AsSpan(0,149).CopyTo(bytes);input.AsSpan(151).CopyTo(bytes.AsSpan(149));
   }else if(original!=Human09Corrected)throw new InvalidDataException("Unsupported HUMAN09.TRO variant (SHA-256 "+original+"). Extermination requires the investigated original or corrected English script; the source was not modified.");
   if(Hash(bytes)!=Human09Corrected)throw new InvalidDataException("Human09 content correction failed its effective hash check");
  }
  return new(path,Hash(bytes),bytes){OriginalSha256=original,CorrectionId=correction};
 }
}
