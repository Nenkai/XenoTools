using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XenoTools.BinaryData;

namespace XenoTools.BinaryData.ID
{
    public class BinaryDataVariable : BinaryDataValue
    {
        public BinaryDataVariableType NumericType { get; set; }
        public object Value { get; set; }

        public BinaryDataVariable(string name, BinaryDataMemberType type, BinaryDataVariableType numericType, object value)
        {
            Name = name;
            Type = type;
            NumericType = numericType;
            Value = value;
        }


        public override string ToString()
        {
            return $"{Name} ({Type}) - {Value}";
        }
    }
}
