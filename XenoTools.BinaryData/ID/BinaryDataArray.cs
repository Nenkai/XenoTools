using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XenoTools.BinaryData;

namespace XenoTools.BinaryData.ID
{
    public class BinaryDataArray : BinaryDataValue
    {
        public BinaryDataVariableType NumericType { get; set; }
        public List<object> Values { get; set; }

        public BinaryDataArray(string name, BinaryDataMemberType type, BinaryDataVariableType numericType, List<object> values)
        {
            Name = name;
            Type = type;
            NumericType = numericType;
            Values = values;
        }

        public override string ToString()
        {
            return $"{Name} ({Type}) - {Values}";
        }
    }
}
