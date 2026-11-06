using System.Security.Cryptography;
namespace DarkColony.Viewer;
public partial class Viewer {
 // Metadata/eligibility only: never retain a mutable restored mission. Loading a
 // selected slot still performs the complete content and snapshot validation.
 sealed record CatalogEntry(string Digest,bool CheckedForContinue,bool Invalid,string Label,string Title,string Campaign,string Date,string Details,CatalogInput[] Inputs);
 sealed record CatalogInput(string Path,long Length,DateTime Modified);
 readonly Dictionary<string,CatalogInput[]> validatedCatalogInputs=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,CatalogEntry> saveCatalogCache=new(StringComparer.OrdinalIgnoreCase);
 bool cacheSaveCatalog=true; // Diagnostic A/B switch, not a player preference.
 string? CatalogDigest(int slot){
  if(!saveExists[slot])return null;
  try{using var stream=File.OpenRead(SavePath(slot));return Convert.ToHexString(SHA256.HashData(stream));}catch(Exception e) when(e is IOException or UnauthorizedAccessException){return null;}
 }
 bool ReuseCatalog(int slot,string? digest){
  if(!cacheSaveCatalog||digest==null||!saveCatalogCache.TryGetValue(SavePath(slot),out var entry)||entry.Digest!=digest||entry.Invalid||!CatalogInputsUnchanged(entry.Inputs)||(mainMenu&&!entry.CheckedForContinue))return false;
  saveInvalid[slot]=entry.Invalid;saveLabels[slot]=entry.Label;saveTitles[slot]=entry.Title;saveCampaigns[slot]=entry.Campaign;saveDates[slot]=entry.Date;saveDetails[slot]=entry.Details;return true;
 }
 bool CatalogInputsUnchanged(CatalogInput[] inputs){
  try{foreach(var input in inputs){var file=new FileInfo(input.Path);if(!file.Exists||file.Length!=input.Length||file.LastWriteTimeUtc!=input.Modified)return false;}return true;}
  catch(Exception e) when(e is IOException or UnauthorizedAccessException){return false;}
 }
 void CaptureCatalogInputs(int slot,System.Text.Json.Nodes.JsonNode inputs,string contentRoot){
  validatedCatalogInputs[SavePath(slot)]=inputs.AsObject().Select(pair=>{var file=new FileInfo(Path.Combine(contentRoot,pair.Key));return new CatalogInput(file.FullName,file.Length,file.LastWriteTimeUtc);}).Concat(File.Exists(Path.Combine(contentRoot,"odc-content.json"))?new[]{new FileInfo(Path.Combine(contentRoot,"odc-content.json"))}.Select(f=>new CatalogInput(f.FullName,f.Length,f.LastWriteTimeUtc)):Array.Empty<CatalogInput>()).ToArray();
 }
 void RememberCatalog(int slot,string? digest){
  if(digest!=null)saveCatalogCache[SavePath(slot)]=new(digest,mainMenu,saveInvalid[slot],saveLabels[slot],saveTitles[slot],saveCampaigns[slot],saveDates[slot],saveDetails[slot],validatedCatalogInputs.GetValueOrDefault(SavePath(slot))??Array.Empty<CatalogInput>());
  else saveCatalogCache.Remove(SavePath(slot));
 }
}
