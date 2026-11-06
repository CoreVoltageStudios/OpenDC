namespace DarkColony.ContentImport;
public static class SourceDiscovery {
 // Deliberately bounded: data-disc roots only, never an entire user drive scan.
 // CDDA requires a verified image adapter; finding a data volume does not extract it.
 public static Task<string[]> MountedAsync(CancellationToken cancel,Action<ImportProgress>? progress=null)=>Task.Run(()=>{
  progress?.Invoke(new(ImportStage.LookingForSource,"Looking for mounted game data discs"));var paths=new List<string>();
  foreach(var d in DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.CDRom)){cancel.ThrowIfCancellationRequested();try{if(d.IsReady&&(Directory.Exists(Path.Combine(d.RootDirectory.FullName,"DC"))||Directory.Exists(Path.Combine(d.RootDirectory.FullName,"EXPENG"))))paths.Add(d.RootDirectory.FullName);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}}
  progress?.Invoke(new(ImportStage.AwaitingConfirmation,paths.Count>0?"Review the detected source before importing":"Choose an original disc image or folder"));return paths.ToArray();
 },cancel);
}
