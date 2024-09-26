using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Models
{
    public class PostPlayerRequest
    {
        [JsonPropertyName("L")]
        public ulong LocalContentId { get; set; }
        [JsonPropertyName("N")] //MinLength(4,ErrorMessage = "Name must be at least 4 characters"), MaxLength(32),
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("W")]
        public ushort? WorldId { get; set; }
        [JsonPropertyName("A")]
        public int? AccountId { get; set; }
        [JsonPropertyName("T")]
        public short? TerritoryId { get; set; }
        [JsonPropertyName("C")]
        public int CreatedAt { get; set; }
    }
}
