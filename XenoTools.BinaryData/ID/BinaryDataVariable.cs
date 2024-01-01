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

        public override uint GetSize()
        {
            return NumericType switch
            {
                BinaryDataVariableType.SByte => 1,
                BinaryDataVariableType.UByte => 1,
                BinaryDataVariableType.Short => 2,
                BinaryDataVariableType.UShort => 2,
                BinaryDataVariableType.Int => 4,
                BinaryDataVariableType.UInt => 4,
                BinaryDataVariableType.Float => 4,
                BinaryDataVariableType.String => 4, // Str offset size
                _ => throw new NotImplementedException(),
            };
        }
    }
}
