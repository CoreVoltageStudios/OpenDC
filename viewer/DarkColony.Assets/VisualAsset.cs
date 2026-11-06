namespace DarkColony.Assets;

// Minimal replacement boundary: identity + logical footprint do not come from texture resolution.
// A replacement supplies new pixels while keeping LogicalWidth/Height and Origin unchanged.
public sealed record VisualAsset(string Id, Raster Pixels, int OriginalPixelWidth, int OriginalPixelHeight,
    float LogicalWidth, float LogicalHeight, float OriginX, float OriginY);
public interface IVisualResolver { VisualAsset Resolve(string id); }
