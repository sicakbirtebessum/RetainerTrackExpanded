using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Models
{
    public class ServerStatsDto
    {
        [JsonProperty("P")]
        public int TotalPlayerCount { get; set; }
        [JsonProperty("A")]
        public int TotalPrivatePlayerCount { get; set; }
        [JsonProperty("R")]
        public int TotalRetainerCount { get; set; }
        [JsonProperty("B")]
        public int TotalPrivateRetainerCount { get; set; }
        [JsonProperty("U")]
        public int TotalUserCount { get; set; }
        [JsonProperty("L")]
        public long LastUpdate { get; set; }
    }
}
