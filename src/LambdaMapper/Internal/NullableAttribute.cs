namespace System.Runtime.CompilerServices
{
    /*
        NullableAttribute cannot be used in code unless a stub implementation
        of it is included.
        Implementation adapted from http://code.fitness/post/2019/02/nullableattribute.html
    */

    [AttributeUsage (AttributeTargets.Class | AttributeTargets.Event | AttributeTargets.Field |
                     AttributeTargets.GenericParameter | AttributeTargets.Module | AttributeTargets.Parameter |
                     AttributeTargets.Property | AttributeTargets.ReturnValue,
                     AllowMultiple = false)]
    public class NullableAttribute : Attribute
    {
        public byte Mode { get; }

        public NullableAttribute(byte mode)
        {
            Mode = mode;
        }

        public NullableAttribute(byte[] _) => throw new NotImplementedException();
    }
}
