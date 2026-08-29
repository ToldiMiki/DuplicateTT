using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartpageTimetableDuplicateV1.Models
{
    public class LayoutItems
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("prioritySn")]
        public int PrioritySn { get; set; }

        [JsonPropertyName("elementTypeId")]
        public int ElementTypeId { get; set; }

        [JsonPropertyName("elementTypeLabel")]
        public string? ElementTypeLabel { get; set; }

        [JsonPropertyName("textValue")]
        public string? TextValue { get; set; }

        [JsonPropertyName("imageId")]
        public int? ImageId { get; set; }

        [JsonPropertyName("imageResizeType")]
        public string? ImageResizeType { get; set; }

        [JsonPropertyName("gridId")]
        public int? GridId { get; set; }

        [JsonPropertyName("announcement")]
        public Announcement? Announcement { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("anchorX")]
        public int? AnchorX { get; set; }

        [JsonPropertyName("anchorXLabel")]
        public string? AnchorXLabel { get; set; }

        [JsonPropertyName("anchorY")]
        public int? AnchorY { get; set; }

        [JsonPropertyName("anchorYLabel")]
        public string? AnchorYLabel { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("rasterFontId")]
        public int? RasterFontId { get; set; }

        [JsonPropertyName("fontColor")]
        public int? FontColor { get; set; }

        [JsonPropertyName("backgroundColor")]
        public int? BackgroundColor { get; set; }

        [JsonPropertyName("ttFontName")]
        public string? TtFontName { get; set; }

        [JsonPropertyName("dynamicTimetableId")]
        public int? DynamicTimetableId { get; set; }

        [JsonPropertyName("rasterContent")]
        public string? RasterContent { get; set; }

        [JsonPropertyName("layoutId")]
        public int LayoutId { get; set; }

        [JsonPropertyName("groupIds")]
        public List<int>? GroupIds { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("contentWidth")]
        public int ContentWidth { get; set; }

        [JsonPropertyName("contentHeight")]
        public int ContentHeight { get; set; }
    }

    public class Announcement
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("defaultText")]
        public string? DefaultText { get; set; }

        // A backend a közlemény elemeit "items" néven küldi. Korábban itt "textAnnouncements" és
        // "imageAnnouncements" szerepelt - ilyen mezőt a szerver nem ad, így az items tartalma
        // beolvasáskor eldobódott, mentéskor pedig a két nem létező mező null-ként kimaradt.
        // A tömb elemeinek szerkezete nincs dokumentálva, ezért nyers JSON-ként megy át.
        [JsonPropertyName("items")]
        public List<JsonElement>? Items { get; set; }

        [JsonPropertyName("groupIds")]
        public object? GroupIds { get; set; }
    }
}