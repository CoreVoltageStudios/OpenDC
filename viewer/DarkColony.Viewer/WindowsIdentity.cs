using Godot;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace DarkColony.Viewer;

// Window grouping belongs to ODC, not to the shared Godot editor/host executable.
public partial class Viewer {
 static string installProfile="";
 static string InstalledProfileRoot=>Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),"OpenDarkColony","Comparisons",installProfile);
 System.Threading.Mutex? installMutex;
 void ConfigureWindowsIdentity(){
  if(!OperatingSystem.IsWindows())return;
  bool source=OS.HasFeature("editor");string install=Path.GetDirectoryName(OS.GetExecutablePath())!;
  string config=Path.Combine(install,"install-profile.json");
  if(!source&&File.Exists(config)){
   using var doc=JsonDocument.Parse(File.ReadAllText(config));installProfile=doc.RootElement.GetProperty("comparison").GetString()??"";
   if(installProfile.Length>0&&!Regex.IsMatch(installProfile,"^[a-z0-9-]{1,32}$"))throw new InvalidDataException("Invalid installation profile.");
  }
  string id=source?"OpenDarkColony.Source":"OpenDarkColony.Private."+(installProfile.Length==0?"Normal":installProfile);
  if(!source)installMutex=new(false,"Local\\"+id);
  string icon=source?ProjectSettings.GlobalizePath("res://Branding/Icon/odc-icon.ico"):Path.Combine(install,"odc-icon.ico");
  string command='"'+OS.GetExecutablePath()+'"'+(source?" --path \""+ProjectSettings.GlobalizePath("res://")+"\" -- --main-menu":"");
  try{
   Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(id));
   var iid=typeof(IWindowProperties).GUID;Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow((IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle),ref iid,out var store));
   try{
    void Set(uint key,string value){var k=new PropertyKey{Format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),Id=key};var v=new PropertyValue{Type=31,Pointer=Marshal.StringToCoTaskMemUni(value)};try{Marshal.ThrowExceptionForHR(store.SetValue(ref k,ref v));}finally{Marshal.FreeCoTaskMem(v.Pointer);}}
    Set(2,command);Set(3,icon+",0");Set(4,"OpenDC"+(source?" (Source)":installProfile.Length>0?" ("+installProfile+")":""));Set(5,id);Marshal.ThrowExceptionForHR(store.Commit());
   }finally{Marshal.ReleaseComObject(store);}
   GD.Print("WINDOWS_IDENTITY "+id+" icon="+icon);
  }catch(Exception e){GD.PrintErr("WINDOWS_IDENTITY_ERROR "+e.Message);}
 }
 void GuardPlayerBuild(string identity){
  using var doc=JsonDocument.Parse(identity);var build=Version.Parse(doc.RootElement.GetProperty("windowsVersion").GetString()!);
  string path=Path.Combine(PlayerDataRoot,"highest-release-version.json");
  if(File.Exists(path)){
   using var previous=JsonDocument.Parse(File.ReadAllText(path));var highest=Version.Parse(previous.RootElement.GetProperty("windowsVersion").GetString()!);
   if(highest>build)throw new InvalidDataException("This profile was opened by a newer test build. Install that build or use a separate comparison installation. Player data has not been changed.");
   if(highest==build)return;
  }
  Directory.CreateDirectory(PlayerDataRoot);string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new{windowsVersion=build.ToString()}));File.Move(temp,path,true);
 }
 [StructLayout(LayoutKind.Sequential)] struct PropertyKey{public Guid Format;public uint Id;}
 [StructLayout(LayoutKind.Explicit,Size=24)] struct PropertyValue{[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public IntPtr Pointer;}
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IWindowProperties{
  [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out PropertyKey key);[PreserveSig]int GetValue(ref PropertyKey key,out PropertyValue value);[PreserveSig]int SetValue(ref PropertyKey key,ref PropertyValue value);[PreserveSig]int Commit();
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string id);
 [DllImport("shell32.dll")]static extern int SHGetPropertyStoreForWindow(IntPtr hwnd,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IWindowProperties store);
}
