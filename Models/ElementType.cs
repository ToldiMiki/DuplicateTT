using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartpageTimetableDuplicateV1.Models
{
    public class ElementType
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("typeLabel")]
        public string? TypeLabel { get; set; }

        [JsonPropertyName("groupIds")]
        public List<int>? GroupIds { get; set; }
    }
}