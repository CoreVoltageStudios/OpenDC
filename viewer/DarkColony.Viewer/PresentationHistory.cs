namespace DarkColony.Viewer;
public partial class Viewer {
 // Replay/capture fixtures retain complete evidence. Ordinary play only needs
 // recent diagnostics; audio and script trace consumers run before compaction.
 void TrimPresentationHistory(bool verify=false){
  if(!verify&&(capturing||perfActive))return;
  static int Trim<T>(List<T> list,int consumed){
   if(list.Count<=8192)return 0;int n=Math.Min(list.Count-4096,consumed);
   if(n>0)list.RemoveRange(0,n);return n;
  }
  reinforcementWorldCursor-=Trim(nativeWorld.Events,reinforcementWorldCursor);
  reinforcementScriptCursor-=Trim(mission.Events,reinforcementScriptCursor);
  nativeSoundCursor-=Trim(nativeWorld.AudioCues,nativeSoundCursor);
  Trim(missionAudioAudit,missionAudioAudit.Count);
 }
}
