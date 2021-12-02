using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace LambdaMapper.Internal
{
    internal static class MapEachIEnumerable
    {
        internal static BlockExpression Execute(
            Type sourceType,
            Type destinationType,
            Expression source,
            UnaryExpression mapper)
        {
            var counter = Variable(typeof(int), "counter");
            var length = Variable(typeof(int), "length");
            var underlyingSourceType = sourceType.GenericTypeArguments.First();
            var underlyingDestinationType = destinationType.GenericTypeArguments.First();
            var listType = typeof(List<>)
                .MakeGenericType(destinationType.GenericTypeArguments);
            var destination = Variable(
                listType,
                "dest");
            var element = Parameter(underlyingSourceType, "element");
            var getEnumerator = sourceType.GetMethod(nameof(IEnumerable.GetEnumerator));
            if (getEnumerator is null)
                getEnumerator = typeof(IEnumerable<>)
                    .MakeGenericType(sourceType)
                    .GetMethod(nameof(IEnumerable.GetEnumerator));
            var enumeratorType = getEnumerator.ReturnType;
            var enumerator = Variable(enumeratorType, "enumerator");
            var resetMethod = typeof(IEnumerator).GetMethod(nameof(IEnumerator.Reset));
            var isList = !(enumeratorType.DeclaringType is null);
            var count = typeof(EnumerationHelpers)
                .GetMethod(nameof(EnumerationHelpers.Count));
            var asEnumerable = typeof(Enumerable)
                .GetMethod(nameof(Enumerable.AsEnumerable))
                .MakeGenericMethod(destinationType.GenericTypeArguments[0]);

            var addMethod = listType.GetMethod(nameof(List<object>.Add));
            Expression returnExpression = Call(asEnumerable, destination);

            if (isList)
            {
                count = typeof(EnumerationHelpers)
                    .GetMethod(nameof(EnumerationHelpers.ListCount))
                    .MakeGenericMethod(sourceType.GenericTypeArguments);
                returnExpression = destination;
            }

            return Block(
                new[] { enumerator, destination },
                IfThen(
                    NotEqual(source, Constant(null)),
                    Block(
                        Assign(enumerator, Call(source, getEnumerator)),
                        Assign(
                            destination,
                            New(typeof(List<>)
                                .MakeGenericType(destinationType.GenericTypeArguments))),
                        EnumerationHelpers.EnumerationLoop(
                            enumerator,
                            Block(
                                new[] { element },
                                Assign(element, Property(
                                    enumerator,
                                    nameof(IEnumerator.Current))),
                                Call(
                                    destination,
                                    addMethod,
                                    new [] { Invoke(
                                        mapper,
                                        element)}))))),
                returnExpression);
        }
    }
}