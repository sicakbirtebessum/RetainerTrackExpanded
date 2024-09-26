using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Common.Math;
using RetainerTrackExpanded.Handlers;
using FFXIVClientStructs.FFXIV.Common.Lua;

namespace RetainerTrackExpanded
{
    public static class Tools
    {
        public static int UnixTime
        {
            get
            {
                return (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            }
        }

        public static string ToTimeSinceString(int unixTime)
        {
            var value = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
            TimeSpan ts = DateTime.Now.Subtract(value);

            if (ts.Days > 0)
                return string.Format("{0} {1} ago", ts.Days, (ts.Days > 1) ? "days" : "day");
            //return string.Format("{0} {1} {2} hours ago", ts.Days, (ts.Days > 1) ? "days" : "day", ts.Hours);
            else if (ts.Hours > 0)
                return string.Format("{0} {1} {2} minutes ago", ts.Hours, (ts.Hours > 1) ? "hours" : "hour", ts.Minutes);
            else if (ts.Minutes > 0)
                return string.Format("{0} {1} {2} seconds ago", ts.Minutes, (ts.Minutes > 1) ? "minutes" : "minute", ts.Seconds);
            else if (ts.Seconds > 0)
                return string.Format("{0} {1} ago", ts.Seconds, (ts.Seconds > 1) ? "seconds" : "second");
            return ts.ToString();
        }

        public static string TimeFromNow(int unixTime)
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
            TimeSpan span = dt - DateTime.Now;

            if (span.Days > 365)
            {
                int years = (span.Days / 365);
                return String.Format("about {0} {1} from now", years, years == 1 ? "year" : "years");
            }
            if (span.Days > 30)
            {
                int months = (span.Days / 30);
                return String.Format("about {0} {1} from now", months, months == 1 ? "month" : "months");
            }
            if (span.Days > 0)
                return String.Format("about {0} {1} from now", span.Days, span.Days == 1 ? "day" : "days");
            if (span.Hours > 0)
                return String.Format("about {0} {1} from now", span.Hours, span.Hours == 1 ? "hour" : "hours");
            if (span.Minutes > 0)
                return String.Format("about {0} {1} from now", span.Minutes, span.Minutes == 1 ? "minute" : "minutes");
            if (span.Seconds > 0)
                return String.Format("about {0} seconds from now", span.Seconds);
            if (span.Seconds == 0)
                return "just now";
            return string.Empty;
        }

        public static string UnixTimeConverter(int unixTime)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime.ToString();
        }

        public static string GetTerritoryName(ushort territoryId)
        {
            var territory = PersistenceContext.Instance._territories.First(row => row.RowId == territoryId);
            var territoryName = territory.PlaceName.Value?.Name;
            var territoryRegion = territory.PlaceNameRegion.Value?.Name;
            return $"{territoryName}, {territoryRegion}";
        }

        public static string HashPassword(string rawPassword)
        {
            // Crypt using the Blowfish crypt ("BCrypt") algorithm.
            return BCrypt.Net.BCrypt.HashPassword(rawPassword);
        }

    }
}
