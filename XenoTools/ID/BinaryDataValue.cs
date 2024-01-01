using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XenoTools.BinaryData;

namespace XenoTools.BinaryData.ID
{
    public abstract class BinaryDataValue
    {
        public string Name { get; set; }
        public BinaryDataMemberType Type { get; set; }

        public abstract uint GetSize();
    }
}
