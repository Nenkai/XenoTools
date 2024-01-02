using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.BinaryData.ID
{
    public class BinaryDataID
    {
        public int IDNumber { get; set; }
        public List<BinaryDataValue> Values { get; set; } = new();
    }
}
