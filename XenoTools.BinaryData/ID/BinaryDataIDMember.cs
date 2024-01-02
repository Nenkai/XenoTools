using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.BinaryData.ID
{
    public class BinaryDataIDMember
    {
        public string Name { get; set; }
        public BinaryDataMemberType Type { get; set; }
        public BinaryDataVariableType NumericType { get; set; }
        public int ArrayLength { get; set; }

        public int GetSize()
        {
            if (Type == BinaryDataMemberType.Variable || Type == BinaryDataMemberType.Array)
            {
                int varLen = NumericType switch
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

                if (Type == BinaryDataMemberType.Variable)
                    return varLen;
                else
                    return varLen * ArrayLength;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
