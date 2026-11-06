namespace DarkColony.Assets;

public sealed record OriginalInterfaceControl(int Id,string Kind,int Normal,int Pressed,string Description);
public static class OriginalInterface {
 // DEPEND field2 is a MAINE control ID, NOT a SPR index. MAINE declares
 // pictures intrface/mainbut and each control's actual normal/pressed artwork.
 public static Dictionary<int,OriginalInterfaceControl> Controls(byte[] source){
  var text=new Dictionary<int,string>();var result=new Dictionary<int,OriginalInterfaceControl>();
  foreach(var line in OriginalScenarioImport.Lines(source,"MAINE")){
   var p=line.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   if(p[0]=="textmsg"){text[int.Parse(p[1])]=string.Join(" ",p.Skip(2));continue;}
   if(p[0] is not ("count" or "pushb" or "checkb")||p.Length<9)continue;
   int id=int.Parse(p[1]);result[id]=new(id,p[0],int.Parse(p[7]),int.Parse(p[8]),text.GetValueOrDefault(id,""));
  }
  return result;
 }
}
