using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetainerTrackExpanded.API.Query
{
    public class PlayerQueryObject
    {
        public long? LocalContentId { get; set; } = null;
        public string? Name { get; set; } = null;
        public int Cursor { get; set; } = 0;
        public bool IsFetching { get; set; }
    }
}
