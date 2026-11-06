namespace DarkColony.Viewer;
public partial class Viewer {
 static string? IsolatedPlayerData=>System.Environment.GetEnvironmentVariable("ODC_USER_DATA") is {Length:>0} p?Path.GetFullPath(p):null;
 static string LocalData=>System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
 static string NormalPlayerRoot=>Path.Combine(LocalData,"OpenDarkColony");
 // Source/editor launches never default to an installed profile: a newer source build can
 // write saves/campaign keys an installed build rejects, and a direct mission win becomes
 // that profile's first (canonical) completion. Main-menu and direct/lab launches are also
 // kept apart, so direct entries never become canonical source-campaign progress.
 // An explicit ODC_USER_DATA always wins.
 static string developmentProfile="Direct";
 static bool SourceLaunch=>Godot.OS.HasFeature("editor");
 static string DevelopmentRoot=>Path.Combine(LocalData,"OpenDarkColony-Development",developmentProfile);
 static string PlayerDataRoot=>IsolatedPlayerData??(SourceLaunch?DevelopmentRoot:installProfile.Length>0?InstalledProfileRoot:NormalPlayerRoot);
 static string PlayerDataKind=>IsolatedPlayerData!=null?"explicit ODC_USER_DATA":SourceLaunch?"source development "+developmentProfile:installProfile.Length>0?"installed comparison "+installProfile:"installed normal";
 void ReportPlayerData(){
  Godot.GD.Print("PLAYER_DATA "+PlayerDataKind+" "+PlayerDataRoot);
  // Honour explicit overrides, but make shared installed data visible in the session log.
  string full=Path.GetFullPath(PlayerDataRoot).TrimEnd(Path.DirectorySeparatorChar),normal=Path.GetFullPath(NormalPlayerRoot);
  if(SourceLaunch&&(full.Equals(normal,StringComparison.OrdinalIgnoreCase)||full.StartsWith(normal+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))
   Godot.GD.Print("PLAYER_DATA_WARNING source build is using installed player data; newer saves or campaign keys may be rejected by installed builds");
 }
}
