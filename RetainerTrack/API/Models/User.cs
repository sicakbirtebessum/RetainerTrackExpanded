using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Models
{
    public class User
    {
        public int GameAccountId { get; set; }
        public long LocalContentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public int AppRoleId { get; set; }
        public long? DiscordId { get; set; }
        public int? UploadedPlayersCount { get; set; }
        public int? UploadedPlayerInfoCount { get; set; }
        public int? UploadedRetainersCount { get; set; }
        public int? UploadedRetainerInfoCount { get; set; }
        public int? LastSyncedTime { get; set; }
        public bool IsProfilePrivate { get; set; }

        public enum Roles
        {
            Guest = 0,
            Member = 1,
            Admin = 9
        }

    }
}
