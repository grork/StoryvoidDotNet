// These are lifted from various places to provide type information so that when
// we compile FormReader (itself lifted), we don't have to edit the code. These
// are supported on .net standard 2.1+ -- yay. Except we're targeting .net
// standard 2.0. Since they're not available in an external library, we've just
// pulled them in to get that code to compile.
//
// This can be removed when we remove FormReader
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) { }
        public bool ReturnValue { get { throw null!; } }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullWhenAttribute : Attribute
    {
        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Members = new[] { member };
        }

        public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
        {
            ReturnValue = returnValue;
            Members = members;
        }

        public bool ReturnValue { get; }
        public string[] Members { get; }
    }
}