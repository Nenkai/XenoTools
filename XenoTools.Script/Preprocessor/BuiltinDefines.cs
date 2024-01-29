using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Preprocessor;

public class BuiltinDefines
{
    public static Dictionary<string, string> CompilerProvidedConstants = new()
    {
        // Numeric min/max
        { "INT_MIN", $"{int.MinValue}" },
        { "INT_MAX", $"{int.MaxValue}" },

        // XC1 - game::getFlagTypeName
        { "F1_COLONY6", $"0x2578001E" },
        { "F1_QUESTEVE", $"0x259601F4" },

        { "F1_MAPEFFECT", $"0x278A012C" },
        { "F1_COLLECT_REG", $"0x28B60200" },
        { "F1_COLLECT_JCMP", $"0x2AB600C8" },

        { "F1_SCQUEST", $"0x31F40064" },
        { "F1_PLAYAWARD", $"0x325800C8" },
        { "F1_MAPJUMP", $"0x3320001E" },

        { "F1_COLLECT_PCMP", $"0x2B7E001E" },
        { "F1_VALUABLE", $"0x2B9C012C" },
        { "F1_GIMMICK", $"0x2CC8052C" },

        { "F1_AUTOFADEIN", $"0x37140001" },
        { "F1_CLEAR_SAVE", $"0x37150001" },
        { "F1_IS_MERIA", $"0x37160001" },

        { "F1_CLEAR", "0x36FC0001" },
        { "F1_PASSIVELINE", "0x36FD000F" },
        { "F1_VOICECNT", "0x370C0008" },

        { "F1_HELP", "0x346C00E4" },
        { "F1_HELPVIEW", "0x355000E4" },
        { "F1_PTNVIEW", "0x363400C8" },

        { "F1_LANDJUMP", "0x333E0001" },
        { "F1_SYSTEMSAVE", "0x333F0001" },
        { "F1_BUFF_UI", "0x3340012C" },

        { "F32_PCJOIN", "1" },
        { "F16_SCENARIO", "0x200001" },

        { "F16_POPULAR", "0x210007" },
        { "F16_FRIENDLY", "0x280016" },
        { "F16_COLONY6", "0x3E0001" },

        { "F16_PC08WPNID", "0x1090001" },
        { "F16_NAMED", "0x10A0001" },
        { "F8_QUEST", "0x2200514" },

        { "F16_LANDMARK", "0x3F0001" },
        { "F16_PLAYAWARD", "0x4000C8" },
        { "F16_PCJOIN", "0x1080001" },

        { "F1_LOCEVENT", "0x20640064" },
        { "F1_LANDMARK", "0x20C803E8" },
        { "F1_PTNEVENT", "0x24B000C8" },

        { "F1_NPCMEET", "0xA20012C" },
        { "F1_NAMED", "0x1D44012C" },
        { "F1_SCNEVENT", "0x1E7001F4" },

        { "F8_COLONY6_BAT", "0x9300008" },
        { "F8_ITEMBOX_LV", "0x9380001" },
        { "F8_PC08WPNSLOT", "0x9390001" },

        { "F8_RELATE", "0x7340190" },
        { "F8_SELECT", "0x8C40064" },
        { "F8_COLONY6", "0x9280008" },
    };
}