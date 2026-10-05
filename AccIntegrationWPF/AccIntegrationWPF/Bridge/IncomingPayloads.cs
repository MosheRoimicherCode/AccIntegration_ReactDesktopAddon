using System.Text.Json;
using AccIntegration.Core.Models;

namespace AccIntegrationWPF.Bridge;

/// <summary>
/// Reads the payloads React sends (shapes in AccIntegrationReact/src/bridge/types.ts).
/// </summary>
public static class IncomingPayloads
{
    /// <summary>
    /// chooseCoordinateSystem: { wkt: string | null } — the currently selected coordinate system.
    /// </summary>
    public static string? ReadCurrentWkt(JsonElement payload)
    {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty("wkt", out JsonElement wkt) &&
               wkt.ValueKind == JsonValueKind.String
            ? wkt.GetString()
            : null;
    }

    /// <summary>
    /// convert: { coordinates: { x, y, z }, coordinateSystem: { name, wkt } }.
    /// </summary>
    public static IfcPlacement ReadConvert(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("coordinates", out JsonElement coordinates) ||
            !payload.TryGetProperty("coordinateSystem", out JsonElement coordinateSystem) ||
            coordinateSystem.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Convert request is missing coordinates or coordinate system.");
        }

        string wkt = coordinateSystem.TryGetProperty("wkt", out JsonElement wktElement) &&
                     wktElement.ValueKind == JsonValueKind.String
            ? wktElement.GetString()!
            : string.Empty;

        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("Choose a coordinate system before converting.");

        return new IfcPlacement
        {
            X = ReadNumber(coordinates, "x"),
            Y = ReadNumber(coordinates, "y"),
            Z = ReadNumber(coordinates, "z"),
            HasPivot = true,
            CoordinateSystem = new CoordinateSystemInfo
            {
                Wkt = wkt,
                Name = coordinateSystem.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString()!
                    : string.Empty
            }
        };
    }

    /// <summary>
    /// switchToNext: coordinates and an optional coordinate system, preserving edits while a model is deferred.
    /// </summary>
    public static IfcPlacement ReadPlacementDraft(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("coordinates", out JsonElement coordinates))
            throw new ArgumentException("The model switch is missing coordinates.");

        CoordinateSystemInfo? coordinateSystemInfo = null;
        if (payload.TryGetProperty("coordinateSystem", out JsonElement coordinateSystem) &&
            coordinateSystem.ValueKind == JsonValueKind.Object &&
            coordinateSystem.TryGetProperty("wkt", out JsonElement wkt) &&
            wkt.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(wkt.GetString()))
        {
            coordinateSystemInfo = new CoordinateSystemInfo
            {
                Wkt = wkt.GetString()!,
                Name = coordinateSystem.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString()!
                    : string.Empty
            };
        }

        return new IfcPlacement
        {
            X = ReadNumber(coordinates, "x"),
            Y = ReadNumber(coordinates, "y"),
            Z = ReadNumber(coordinates, "z"),
            HasPivot = true,
            CoordinateSystem = coordinateSystemInfo
        };
    }

    private static double ReadNumber(JsonElement coordinates, string name)
    {
        if (coordinates.ValueKind == JsonValueKind.Object &&
            coordinates.TryGetProperty(name, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out double number) &&
            double.IsFinite(number))
        {
            return number;
        }

        throw new ArgumentException($"Coordinate '{name.ToUpperInvariant()}' must be a number.");
    }
}
