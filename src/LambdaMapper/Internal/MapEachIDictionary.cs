using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace LambdaMapper.Internal
{
    internal static class MapEachIDictionary
    {
        internal static BlockExpression Execute(
            Type sourceType,
            Type destinationType,
            Expression source,
            UnaryExpression mapper)
        {
            var sourceGenericTypeArguments = sourceType
                .GenericTypeArguments;
            var sourceKvpType = typeof(KeyValuePair<,>)
                .MakeGenericType(sourceGenericTypeArguments);

            var destination = Variable(destinationType, "dest");
            var sourceKvp = Parameter(sourceKvpType, "sourceKvp");
            var getEnumerator = sourceType.GetMethod(nameof(IEnumerable.GetEnumerator));
            var enumeratorType = getEnumerator.ReturnType;
            var enumerator = Variable(enumeratorType, "enumerator");
            var addMethod = destinationType.GetMethod(nameof(IDictionary.Add));

            return Block(
                new[] { enumerator, destination },
                IfThen(
                    NotEqual(source, Constant(null)),
                    Block(
                        Assign(enumerator, Call(source, getEnumerator)),
                        Assign(destination, New(destinationType)),
                        EnumerationHelpers.EnumerationLoop(
                            enumerator,
                            Block(
                                new[] { sourceKvp },
                                Assign(sourceKvp, Property(enumerator, nameof(IEnumerator.Current))),
                                Call(
                                    destination,
                                    addMethod,
                                    Property(
                                        sourceKvp,
                                        sourceKvpType.GetProperty(nameof(KeyValuePair<object, object>.Key))),
                                    Invoke(
                                        mapper,
                                        Property(
                                            sourceKvp,
                                            sourceKvpType.GetProperty(nameof(KeyValuePair<object, object>.Value))))))))),
                destination);
        }
    }
}