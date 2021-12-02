namespace System.Runtime.CompilerServices
{
    /*
        NullableAttribute cannot be used in code unless a stub implementation
        of it is included.
        Implementation taken from https://github.com/dotnet/roslyn/blob/main/docs/features/nullable-metadata.md
    */

    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Event |
        AttributeTargets.Field |
        AttributeTargets.GenericParameter |
        AttributeTargets.Parameter |
        AttributeTargets.Property |
        AttributeTargets.ReturnValue,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class NullableAttribute : Attribute
    {
        public readonly byte[] NullableFlags;
        public NullableAttribute(byte flag)
        {
            NullableFlags = new byte[] { flag };
        }
        public NullableAttribute(byte[] flags)
        {
            NullableFlags = flags;
        }
    }
}