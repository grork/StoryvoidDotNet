using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("DatabaseTests")]
[assembly: InternalsVisibleToAttribute("TestUtilitiesTests")]

// Support `init` keyword properties on non-.net 5.0 targets. Compiler looks for
// the type in the assembly, so this fakes it out. Magic.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;
    /// <summary>
    /// Reserved to be used by the compiler for tracking metadata.
    /// This class should not be used by developers in source code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}