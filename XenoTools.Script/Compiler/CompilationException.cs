using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Compiler;

public class ScriptCompilationException : Exception
{
    public ScriptCompilationException(string message)
        : base(message)
    {

    }
}