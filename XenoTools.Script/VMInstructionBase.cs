﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Syroot.BinaryData.Memory;

using XenoTools.Script.Instructions;
using XenoTools.Script;

namespace XenoTools.Script;

public record VmInstDescriptor(VmInstType InstType, short ValueTypeSize, short StackChange);

public abstract class VMInstructionBase
{
    public abstract VmInstType Type { get; }
    public int Offset { get; set; }

    public abstract void Read(ref SpanReader sr);
    public abstract void Write(ref SpanReader sr);
    
    public int ReadValue(ref SpanReader sr)
    {
        return VMInstructionBase.InstToLayout[(int)Type].ValueTypeSize switch
        {
            4 => sr.ReadInt32(),
            2 => sr.ReadInt16(),
            1 => sr.ReadByte(),
            0 => 0,
            _ => throw new InvalidDataException("Invalid instruction value type?")
        };
    }

    public static VMInstructionBase NewByType(VmInstType type)
    {
        return type switch
        {
            VmInstType.NOP => new VmNop(),
            VmInstType.CONST_0 => new VmConst0(),
            VmInstType.CONST_1 => new VmConst1(),
            VmInstType.CONST_2 => new VmConst2(),
            VmInstType.CONST_3 => new VmConst3(),
            VmInstType.CONST_4 => new VmConst4(),
            VmInstType.CONST_I => new VmConstInteger(),
            VmInstType.CONST_I_W => new VmConstInteger_Word(),
            VmInstType.POOL_INT => new VmPoolInt(),
            VmInstType.POOL_INT_W => new VmPoolInt_Word(),
            VmInstType.POOL_FLOAT => new VmPoolFloat(),
            VmInstType.POOL_FLOAT_W => new VmPoolFloat_Word(),
            VmInstType.POOL_STR => new VmPoolString(),
            VmInstType.POOL_STR_W => new VmPoolString_Word(),
            VmInstType.LD => throw new NotImplementedException(),
            VmInstType.ST => throw new NotImplementedException(),
            VmInstType.LD_ARG => new VmLoadArg(),
            VmInstType.ST_ARG => throw new NotImplementedException(),
            VmInstType.ST_ARG_OMIT => throw new NotImplementedException(),
            VmInstType.LD_0 => new VmLoad0(),
            VmInstType.LD_1 => new VmLoad1(),
            VmInstType.LD_2 => new VmLoad2(),
            VmInstType.LD_3 => new VmLoad3(),
            VmInstType.ST_0 => new VmStore0(),
            VmInstType.ST_1 => new VmStore1(),
            VmInstType.ST_2 => new VmStore2(),
            VmInstType.ST_3 => new VmStore3(),
            VmInstType.LD_ARG_0 => new VmLoadArgument0(),
            VmInstType.LD_ARG_1 => new VmLoadArgument1(),
            VmInstType.LD_ARG_2 => new VmLoadArgument2(),
            VmInstType.LD_ARG_3 => new VmLoadArgument3(),
            VmInstType.ST_ARG_0 => throw new NotImplementedException(),
            VmInstType.ST_ARG_1 => throw new NotImplementedException(),
            VmInstType.ST_ARG_2 => throw new NotImplementedException(),
            VmInstType.ST_ARG_3 => throw new NotImplementedException(),
            VmInstType.LD_STATIC => new VmLoadStatic(),
            VmInstType.LD_STATIC_W => throw new NotImplementedException(),
            VmInstType.ST_STATIC => new VmStoreStatic(),
            VmInstType.ST_STATIC_W => throw new NotImplementedException(),
            VmInstType.LD_AR => throw new NotImplementedException(),
            VmInstType.ST_AR => throw new NotImplementedException(),
            VmInstType.LD_NIL => new VmLoadNil(),
            VmInstType.LD_TRUE => new VmLoadTrue(),
            VmInstType.LD_FALSE => new VmLoadFalse(),
            VmInstType.LD_FUNC => new VmLoadFunction(),
            VmInstType.LD_FUNC_W => throw new NotImplementedException(),
            VmInstType.LD_PLUGIN => throw new NotImplementedException(),
            VmInstType.LD_PLUGIN_W => throw new NotImplementedException(),
            VmInstType.LD_FUNC_FAR => throw new NotImplementedException(),
            VmInstType.LD_FUNC_FAR_W => throw new NotImplementedException(),
            VmInstType.MINUS => throw new NotImplementedException(),
            VmInstType.NOT => throw new NotImplementedException(),
            VmInstType.L_NOT => new VmLogicalNot(),
            VmInstType.ADD => throw new NotImplementedException(),
            VmInstType.SUB => throw new NotImplementedException(),
            VmInstType.MUL => throw new NotImplementedException(),
            VmInstType.DIV => throw new NotImplementedException(),
            VmInstType.MOD => throw new NotImplementedException(),
            VmInstType.OR => throw new NotImplementedException(),
            VmInstType.AND => throw new NotImplementedException(),
            VmInstType.R_SHIFT => throw new NotImplementedException(),
            VmInstType.L_SHIFT => throw new NotImplementedException(),
            VmInstType.EQ => new VmEquals(),
            VmInstType.NE => new VmNotEquals(),
            VmInstType.GT => new VmGreaterThan(),
            VmInstType.LT => new VmLesserThan(),
            VmInstType.GE => new VmGreaterOrEquals(),
            VmInstType.LE => new VmLesserOrEquals(),
            VmInstType.L_OR => new VmLogicalOr(),
            VmInstType.L_AND => new VmLogicalAnd(),
            VmInstType.JMP => new VmJump(),
            VmInstType.JPF => new VmJumpFalse(),
            VmInstType.CALL => new VmCall(),
            VmInstType.CALL_W => throw new NotImplementedException(),
            VmInstType.CALL_IND => throw new NotImplementedException(),
            VmInstType.RET => new VmRet(),
            VmInstType.NEXT => new VmNext(),
            VmInstType.PLUGIN => new VmPlugin(),
            VmInstType.PLUGIN_W => new VmPlugin_Word(),
            VmInstType.CALL_FAR => throw new NotImplementedException(),
            VmInstType.CALL_FAR_W => throw new NotImplementedException(),
            VmInstType.GET_OC => new VmGetOC(),
            VmInstType.GET_OC_W => throw new NotImplementedException(),
            VmInstType.GETTER => new VmGetter(),
            VmInstType.GETTER_W => throw new NotImplementedException(),
            VmInstType.SETTER => new VmSetter(),
            VmInstType.SETTER_W => throw new NotImplementedException(),
            VmInstType.SEND => new VmSend(),
            VmInstType.SEND_W =>new VmSend_Word(),
            VmInstType.TYPEOF => throw new NotImplementedException(),
            VmInstType.SIZEOF => throw new NotImplementedException(),
            VmInstType.SWITCH => new VmSwitch(),
            VmInstType.INC => new VmIncrement(),
            VmInstType.DEC => throw new NotImplementedException(),
            VmInstType.EXIT => new VmExit(),
            VmInstType.BP => throw new NotImplementedException(),
        };
    }

    public static VmInstDescriptor[] InstToLayout =
    {
        // | Instruction type        | Size of value   | Stack change
        new (VmInstType.NOP          , 0,               0),
        new (VmInstType.CONST_0      , 0,               1),
        new (VmInstType.CONST_1      , 0,               1),
        new (VmInstType.CONST_2      , 0,               1),
        new (VmInstType.CONST_3      , 0,               1),
        new (VmInstType.CONST_4      , 0,               1),
        new (VmInstType.CONST_I      , 1,               1),
        new (VmInstType.CONST_I_W    , 2,               1),
        new (VmInstType.POOL_INT     , 1,               1),
        new (VmInstType.POOL_INT_W   , 2,               1),
        new (VmInstType.POOL_FLOAT   , 1,               1),
        new (VmInstType.POOL_FLOAT_W , 2,               1),
        new (VmInstType.POOL_STR     , 1,               1),
        new (VmInstType.POOL_STR_W   , 2,               1),
        new (VmInstType.LD           , 1,               1),
        new (VmInstType.ST           , 1,               -1),
        new (VmInstType.LD_ARG       , 1,               1),
        new (VmInstType.ST_ARG       , 1,               -1),
        new (VmInstType.ST_ARG_OMIT  , 1,               -1),
        new (VmInstType.LD_0         , 0,               1),
        new (VmInstType.LD_1         , 0,               1),
        new (VmInstType.LD_2         , 0,               1),
        new (VmInstType.LD_3         , 0,               1),
        new (VmInstType.ST_0         , 0,               -1),
        new (VmInstType.ST_1         , 0,               -1),
        new (VmInstType.ST_2         , 0,               -1),
        new (VmInstType.ST_3         , 0,               -1),
        new (VmInstType.LD_ARG_0     , 0,               1),
        new (VmInstType.LD_ARG_1     , 0,               1),
        new (VmInstType.LD_ARG_2     , 0,               1),
        new (VmInstType.LD_ARG_3     , 0,               1),
        new (VmInstType.ST_ARG_0     , 0,               -1),
        new (VmInstType.ST_ARG_1     , 0,               -1),
        new (VmInstType.ST_ARG_2     , 0,               -1),
        new (VmInstType.ST_ARG_3     , 0,               -1),
        new (VmInstType.LD_STATIC    , 1,               1),
        new (VmInstType.LD_STATIC_W  , 2,               1),
        new (VmInstType.ST_STATIC    , 1,               -1),
        new (VmInstType.ST_STATIC_W  , 2,               -1),
        new (VmInstType.LD_AR        , 0,               -1),
        new (VmInstType.ST_AR        , 0,               -3),
        new (VmInstType.LD_NIL       , 0,               1),
        new (VmInstType.LD_TRUE      , 0,               1),
        new (VmInstType.LD_FALSE     , 0,               1),
        new (VmInstType.LD_FUNC      , 1,               1),
        new (VmInstType.LD_FUNC_W    , 2,               1),
        new (VmInstType.LD_PLUGIN    , 1,               1),
        new (VmInstType.LD_PLUGIN_W  , 2,               1),
        new (VmInstType.LD_FUNC_FAR  , 1,               1),
        new (VmInstType.LD_FUNC_FAR_W, 2,               1),
        new (VmInstType.MINUS        , 0,               0),
        new (VmInstType.NOT          , 0,               -1),
        new (VmInstType.L_NOT        , 0,               -1),
        new (VmInstType.ADD          , 0,               -1),
        new (VmInstType.SUB          , 0,               -1),
        new (VmInstType.MUL          , 0,               -1),
        new (VmInstType.DIV          , 0,               -1),
        new (VmInstType.MOD          , 0,               -1),
        new (VmInstType.OR           , 0,               -1),
        new (VmInstType.AND          , 0,               -1),
        new (VmInstType.R_SHIFT      , 0,               -1),
        new (VmInstType.L_SHIFT      , 0,               -1),
        new (VmInstType.EQ           , 0,               -1),
        new (VmInstType.NE           , 0,               -1),
        new (VmInstType.GT           , 0,               -1),
        new (VmInstType.LT           , 0,               -1),
        new (VmInstType.GE           , 0,               -1),
        new (VmInstType.LE           , 0,               -1),
        new (VmInstType.L_OR         , 0,               -1),
        new (VmInstType.L_AND        , 0,               -1),
        new (VmInstType.JMP          , 2,               0),
        new (VmInstType.JPF          , 2,               -1),
        new (VmInstType.CALL         , 1,               0),
        new (VmInstType.CALL_W       , 2,               0),
        new (VmInstType.CALL_IND     , 0,               0),
        new (VmInstType.RET          , 0,               0),
        new (VmInstType.NEXT         , 0,               0),
        new (VmInstType.PLUGIN       , 1,               0),
        new (VmInstType.PLUGIN_W     , 2,               0),
        new (VmInstType.CALL_FAR     , 1,               0),
        new (VmInstType.CALL_FAR_W   , 2,               0),
        new (VmInstType.GET_OC       , 1,               0),
        new (VmInstType.GET_OC_W     , 2,               0),
        new (VmInstType.GETTER       , 1,               0),
        new (VmInstType.GETTER_W     , 2,               0),
        new (VmInstType.SETTER       , 1,               -1),
        new (VmInstType.SETTER_W     , 2,               -1),
        new (VmInstType.SEND         , 1,               0),
        new (VmInstType.SEND_W       , 2,               0),
        new (VmInstType.TYPEOF       , 0,               0),
        new (VmInstType.SIZEOF       , 0,               0),
        new (VmInstType.SWITCH       , 1,               0),
        new (VmInstType.INC          , 0,               0),
        new (VmInstType.DEC          , 0,               0),
        new (VmInstType.EXIT         , 0,               0),
        new (VmInstType.BP           , 0,               0),
    };
}

public enum VmInstType
{
    NOP = 0,
    CONST_0 = 1,
    CONST_1 = 2,
    CONST_2 = 3,
    CONST_3 = 4,
    CONST_4 = 5,
    CONST_I = 6,
    CONST_I_W = 7,
    POOL_INT = 8,
    POOL_INT_W = 9,
    POOL_FLOAT = 10,
    POOL_FLOAT_W = 11,
    POOL_STR = 12,
    POOL_STR_W = 13,
    LD = 14,
    ST = 15,
    LD_ARG = 16,
    ST_ARG = 17,
    ST_ARG_OMIT = 18,
    LD_0 = 19,
    LD_1 = 20,
    LD_2 = 21,
    LD_3 = 22,
    ST_0 = 23,
    ST_1 = 24,
    ST_2 = 25,
    ST_3 = 26,
    LD_ARG_0 = 27,
    LD_ARG_1 = 28,
    LD_ARG_2 = 29,
    LD_ARG_3 = 30,
    ST_ARG_0 = 31,
    ST_ARG_1 = 32,
    ST_ARG_2 = 33,
    ST_ARG_3 = 34,
    LD_STATIC = 35,
    LD_STATIC_W = 36,
    ST_STATIC = 37,
    ST_STATIC_W = 38,
    LD_AR = 39,
    ST_AR = 40,
    LD_NIL = 41,
    LD_TRUE = 42,
    LD_FALSE = 43,
    LD_FUNC = 44,
    LD_FUNC_W = 45,
    LD_PLUGIN = 46,
    LD_PLUGIN_W = 47,
    LD_FUNC_FAR = 48,
    LD_FUNC_FAR_W = 49,
    MINUS = 50,
    NOT = 51,
    L_NOT = 52,
    ADD = 53,
    SUB = 54,
    MUL = 55,
    DIV = 56,
    MOD = 57,
    OR = 58,
    AND = 59,
    R_SHIFT = 60,
    L_SHIFT = 61,
    EQ = 62,
    NE = 63,
    GT = 64,
    LT = 65,
    GE = 66,
    LE = 67,
    L_OR = 68,
    L_AND = 69,
    JMP = 70,
    JPF = 71,
    CALL = 72,
    CALL_W = 73,
    CALL_IND = 74,
    RET = 75,
    NEXT = 76,

    /// <summary>
    /// Calls a plugin. Pops one value (argument count) and arguments each.
    /// </summary>
    PLUGIN = 77,
    PLUGIN_W = 78,
    CALL_FAR = 79,
    CALL_FAR_W = 80,
    GET_OC = 81,
    GET_OC_W = 82,
    GETTER = 83,
    GETTER_W = 84,
    SETTER = 85,
    SETTER_W = 86,
    SEND = 87,
    SEND_W = 88,
    TYPEOF = 89,
    SIZEOF = 90,
    SWITCH = 91,
    INC = 92,
    DEC = 93,
    EXIT = 94,
    BP = 95,
}
