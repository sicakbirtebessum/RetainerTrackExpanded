using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Models
{
    public class UserUpdateDto
    {
        public bool? IsProfilePrivate { get; set; }
        public string? DiscordId { get; set; }
    }
}
