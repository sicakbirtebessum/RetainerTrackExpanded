using Newtonsoft.Json;

namespace RetainerTrackExpanded.API.Models
{
    public class RetainerDto
    {
        [JsonProperty("L")]
        public long LocalContentId { get; set; }
        [JsonProperty("N")]
        public string? Name { get; set; }
        [JsonProperty("W")]
        public ushort WorldId { get; set; }
        [JsonProperty("O")]
        public long OwnerLocalContentId { get; set; }
        [JsonProperty("C")]
        public int CreatedAt { get; set; }
    }
}
