using System;

namespace LambdaMapper.Internal
{
    internal static class MapEnums
    {
        public static T MapEnum<T>(string sourceEnumValue) where T : struct, IConvertible
        {
            var parsed = Enum.TryParse<T>(sourceEnumValue, true, out var result);
            if (!parsed)
                throw new Exception($"No match for value '{sourceEnumValue}' in enum '{typeof(T).Name}'");
            return result;
        }
    }
}