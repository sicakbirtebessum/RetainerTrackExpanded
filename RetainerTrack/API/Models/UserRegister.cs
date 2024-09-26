using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Models
{
    public class UserRegister
    {
        public int GameAccountId { get; set; }
        public long UserLocalContentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
