using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NSENifty50Feeder.Data
{
    class Filemodel
    {
        public class Datum
        {
            public string? name { get; set; }
            public string? type { get; set; }
            public string? folderPath { get; set; }
            public DateTime lastUpdated { get; set; }
            public string? filename { get; set; }
        }
    }
}
