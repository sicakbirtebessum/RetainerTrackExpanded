using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Models
{
    public class PlayerDetailed
    {
        [JsonProperty("L")]
        public long LocalContentId { get; set; }
        [JsonProperty("C")]
        public int? AccountId { get; set; }
        [JsonProperty("T")]
        public PlayerTerritoryDto? Territory { get; set; }
        [JsonProperty("N")]
        public List<PlayerNameHistoryDto> PlayerNameHistories { get; set; } = new List<PlayerNameHistoryDto>();
        [JsonProperty("W")]
        public ICollection<PlayerWorldHistoryDto> PlayerWorldHistories { get; set; } = new List<PlayerWorldHistoryDto>();
        [JsonProperty("R")]
        public ICollection<RetainerDto> Retainers { get; set; } = new List<RetainerDto>();
        [JsonProperty("A")]
        public ICollection<PlayerDetailedInfoAltCharDto> PlayerAltCharacters { get; set; } = new List<PlayerDetailedInfoAltCharDto>();

        public class PlayerTerritoryDto
        {
            [JsonProperty("V")]
            public short TerritoryId { get; set; }
            [JsonProperty("A")]
            public int CreatedAt { get; set; }
        }

        public class PlayerDetailedInfoAltCharDto
        {
            [Key, JsonProperty("L")]
            public long LocalContentId { get; set; }
            [JsonProperty("N")]
            public string? Name { get; set; }
            [JsonProperty("W")]
            public short? WorldId { get; set; }
            [JsonProperty("R")]
            public List<RetainerDto> Retainers { get; set; } = new List<RetainerDto>();
        }

        public class PlayerNameHistoryDto
        {
            [JsonProperty("V")]
            public string Name { get; set; } = null!;
            [JsonProperty("A")]
            public int CreatedAt { get; set; }
        }

        public class PlayerWorldHistoryDto
        {
            [JsonProperty("V")]
            public int WorldId { get; set; }
            [JsonProperty("A")]
            public int CreatedAt { get; set; }
        }
        public class RetainerDto
        {
            [JsonProperty("L")]
            public long LocalContentId { get; set; }
            [JsonProperty("O")]
            public long OwnerLocalContentId { get; set; }
            [JsonProperty("S")]
            public int LastSeen { get; set; }
            [JsonProperty("N")]
            public List<RetainerNameHistoryDto> Names { get; set; } = new List<RetainerNameHistoryDto>();
            [JsonProperty("W")]
            public List<RetainerWorldHistoryDto> Worlds { get; set; } = new List<RetainerWorldHistoryDto>();

            public partial class RetainerNameHistoryDto
            {
                [JsonProperty("V")]
                public string Name { get; set; } = null!;
                [JsonProperty("A")]
                public int CreatedAt { get; set; }
            }
            public partial class RetainerWorldHistoryDto
            {
                [JsonProperty("V")]
                public int WorldId { get; set; }
                [JsonProperty("A")]
                public int CreatedAt { get; set; }
            }
        }
    }
}
