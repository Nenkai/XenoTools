using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.BinaryData.ID
{
    public class BinaryDataID
    {
        public List<BinaryDataValue> Values { get; set; } = new();

        public uint GetSize()
        {
            uint size = 0;
            for (int i = 0; i < Values.Count; i++)
                size += Values[i].GetSize();

            return size;
        }
    }
}
