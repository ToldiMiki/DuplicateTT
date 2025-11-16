using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartpageTimetableDuplicateV1.Models
{
    public class TimetableItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("x")]
        public int? X { get; set; }

        [JsonPropertyName("y")]
        public int? Y { get; set; }

        [JsonPropertyName("scrollbarWidth")]
        public int ScrollbarWidth { get; set; }

        [JsonPropertyName("imageId")]
        public int? ImageId { get; set; }

        [JsonPropertyName("imageContent")]
        public string? ImageContent { get; set; }

        [JsonPropertyName("imageWidth")]
        public int ImageWidth { get; set; }

        [JsonPropertyName("imageHeight")]
        public int ImageHeight { get; set; }

        [JsonPropertyName("dynamicRows")]
        public List<DynamicRow>? DynamicRows { get; set; }

        [JsonPropertyName("groupIds")]
        public List<int>? GroupIds { get; set; }
    }

    public class DynamicRow
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("dynamicTimeTableId")]
        public int DynamicTimeTableId { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("rowOrder")]
        public int RowOrder { get; set; }

        [JsonPropertyName("dynamicCells")]
        public List<DynamicCell>? DynamicCells { get; set; }
    }

    public class DynamicCell
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("dynamicTimeRowId")]
        public int DynamicTimeRowId { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("anchorX")]
        public string? AnchorX { get; set; }

        [JsonPropertyName("anchorY")]
        public string? AnchorY { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("textValue")]
        public string? TextValue { get; set; }

        [JsonPropertyName("cellOrder")]
        public int CellOrder { get; set; }

        [JsonPropertyName("backgroundColor")]
        public string? BackgroundColor { get; set; }

        [JsonPropertyName("fontColor")]
        public string? FontColor { get; set; }

        [JsonPropertyName("rasterFontId")]
        public int RasterFontId { get; set; }

        [JsonPropertyName("ttFontName")]
        public string? TtFontName { get; set; }

        [JsonPropertyName("countdownLimitNear")]
        public int CountdownLimitNear { get; set; }

        [JsonPropertyName("exactTimeperiod")]
        public int ExactTimeperiod { get; set; }
    }
}
