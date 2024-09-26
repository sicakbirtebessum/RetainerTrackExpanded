using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Models
{
    public class PlayerWithPaginationDto
    {
        [JsonProperty("C")]
        public int Cursor { get; init; }
        [JsonProperty("N")]
        public int NextCount { get; init; }
        [JsonProperty("D")]
        public List<PlayerDto> Data { get; init; }

        public PlayerWithPaginationDto(int cursor, int nextCount, List<PlayerDto> data)
        {
            Cursor = cursor;
            NextCount = nextCount;
            Data = data;
        }
    }
}
