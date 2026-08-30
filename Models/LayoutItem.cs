using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartPageDuplicate.Models
{
    public class LayoutItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("displayId")]
        public int DisplayId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("stopNamesConcatenated")]
        public string? StopNamesConcatenated { get; set; }

        [JsonPropertyName("groupIds")]
        public List<int>? GroupIds { get; set; }
    }
}