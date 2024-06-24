using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueryCSV
{
    public class WorkerArgs
    {
        public string FilePath { get; set; }
        public char Delimiter { get; set; }
        public bool HasHeaders { get; set; }
        public int RowsToLoad { get; set; }
    }
}
