using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RetainerTrackExpanded.API.Models.User;

namespace RetainerTrackExpanded.API.Models
{
    public class UserUpdateDto
    {
        public List<UserCharacterDto?> Characters { get; set; }
    }
}
