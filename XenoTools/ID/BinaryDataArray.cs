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

        public override uint GetSize()
        {
            return (uint)(NumericType switch
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
            } * Values.Count);
        }
        public override string ToString()
        {
            return $"{Name} ({Type}) - {Values}";
        }
    }
}
