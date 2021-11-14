using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;
using System.Runtime.CompilerServices;

namespace LambdaMapper.Internal
{
    internal static class MapperExpressionHelpers
    {
        public static BlockExpression MapEachIEnumerable(
            Type sourceType,
            Type destinationType,
            Expression source,
            UnaryExpression mapper)
        {
            var counter = Variable(typeof(int), "counter");
            var length = Variable(typeof(int), "length");
            var underlyingSourceType = sourceType.GenericTypeArguments.First();
            var underlyingDestinationType = destinationType.GenericTypeArguments.First();
            var array = NewArrayBounds(underlyingDestinationType, length);
            var destination = Variable(array.Type, "dest");
            var destinationAccess = ArrayAccess(destination, counter);
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
            var count = typeof(MapperExpressionHelpers)
                .GetMethod(nameof(MapperExpressionHelpers.Count));

            if (isList)
            {
                var listType = typeof(List<>)
                    .MakeGenericType(destinationType.GenericTypeArguments);
                destination = Variable(
                    listType,
                    "dest");
                var addMethod = listType.GetMethod("Add");

                return Block(
                    new[] { counter, length, enumerator, destination },
                    IfThen(
                        NotEqual(source, Constant(null)),
                        Block(
                            Assign(
                                destination,
                                New(typeof(List<>).MakeGenericType(destinationType.GenericTypeArguments))),
                            Assign(enumerator, Call(source, getEnumerator)),
                            EnumerationLoop(
                                enumerator,
                                Block(
                                    new[] { element },
                                    Assign(element, Property(enumerator, "Current")),
                                    Call(
                                        destination,
                                        addMethod,
                                        new [] { Invoke(
                                            mapper,
                                            element)}))))),
                    destination);
            }

            return Block(
                new[] { counter, length, enumerator, destination },
                IfThen(
                    NotEqual(source, Constant(null)),
                    Block(
                        Assign(counter, Constant(0)),
                        Assign(enumerator, Call(source, getEnumerator)),
                        Assign(length, Call(count, new [] { enumerator })),
                        Call(enumerator, resetMethod),
                        Assign(destination, array),
                        EnumerationLoop(
                            enumerator,
                            Block(
                                new[] { element },
                                Assign(element, Property(enumerator, "Current")),
                                Assign(
                                    destinationAccess,
                                    Invoke(
                                        mapper,
                                        element)),
                                Assign(
                                    counter,
                                    Add(counter, Constant(1))))))),
                destination);
        }

        public static BlockExpression MapEachIDictionary(
            Type sourceType,
            Type destinationType,
            Expression source,
            UnaryExpression mapper)
        {
            var sourceGenericTypeArguments = sourceType
                .GenericTypeArguments;
            var sourceKvpType = typeof(KeyValuePair<,>)
                .MakeGenericType(
                    sourceGenericTypeArguments[0],
                    sourceGenericTypeArguments[1]);

            var destination = Variable(destinationType, "dest");
            var sourceKvp = Parameter(sourceKvpType, "sourceKvp");
            var getEnumerator = sourceType.GetMethod(nameof(IEnumerable.GetEnumerator));
            var enumeratorType = getEnumerator.ReturnType;
            var enumerator = Variable(enumeratorType, "enumerator");
            var addMethod = destinationType.GetMethod(nameof(IDictionary.Add));

            return Block(
                new[] { enumerator, destination },
                Assign(enumerator, Call(source, getEnumerator)),
                Assign(destination, New(destinationType)),
                EnumerationLoop(
                    enumerator,
                    Block(
                        new[] { sourceKvp },
                        Assign(sourceKvp, Property(enumerator, nameof(IEnumerator.Current))),
                        Call(
                            destination,
                            addMethod,
                            Property(
                                sourceKvp,
                                sourceKvpType.GetProperty("Key")),
                            Invoke(
                                mapper,
                                Property(
                                    sourceKvp,
                                    sourceKvpType.GetProperty("Value")))))),
                destination);
        }

        public static BlockExpression MapEachValueType(
            Type sourceType,
            Type destinationType,
            Expression source,
            IEnumerable<UnaryExpression> mappers)
        {
            var sourceTypeArguments = sourceType.GenericTypeArguments;
            var destinationTypeArguments = destinationType.GenericTypeArguments;
            var itemCount = Constant(sourceTypeArguments.Length);
            var tuple = sourceType.GetInterface(nameof(ITuple));
            var counter = Variable(typeof(int), "counter");
            var array = NewArrayBounds(typeof(ParameterExpression), itemCount);
            var ctorInfo = destinationType.GetConstructors().First();
            var getItemMethod = tuple.GetMethod("get_Item");
            var breakLabel = Label();

            var expressions = new List<Expression>();
            var sourceItemParameters = new List<ParameterExpression>();
            var destinationItemParameters = new List<ParameterExpression>();
            for (var i = 0; i < sourceTypeArguments.Length; i++)
            {
                var mapper = mappers.ElementAt(i);
                var sourceItemParameter = Parameter(sourceTypeArguments[i], $"sourceItem{i}");
                sourceItemParameters.Add(sourceItemParameter);
                var destinationItemParameter = Parameter(destinationTypeArguments[i], $"destinationItem{i}");
                destinationItemParameters.Add(destinationItemParameter);
                expressions.AddRange( new Expression []
                {
                    Assign(
                        sourceItemParameter,
                        Convert(
                            Call(
                                source,
                                getItemMethod,
                                new [] { counter }),
                            sourceTypeArguments[i])),
                    IfThen(
                        NotEqual(sourceItemParameter, Constant(null)),
                        Assign(
                            destinationItemParameter,
                            Invoke(
                                mapper,
                                sourceItemParameter))),
                    Assign(counter, Add(counter, Constant(1)))
                });
            }

            var expressionParameters = new List<ParameterExpression>(destinationItemParameters);
            expressionParameters.AddRange(sourceItemParameters);
            expressionParameters.Add(counter);
            var blockExpressions = new List<Expression>();
            blockExpressions.Add(Assign(counter, Constant(0)));
            blockExpressions.AddRange(expressions);
            blockExpressions.Add(New(ctorInfo, destinationItemParameters));
            return Block(
                expressionParameters,
                blockExpressions);
        }

        public static int Count(IEnumerator iter) {
            var count = 0;
            using (iter as IDisposable)
            {
                while (iter.MoveNext()) count++;
            }
            return count;
        }

        /*
        *  The following code to iterate through an enumeration and to dispose
        *  of it properly is taken from the following StackOverflow answer:
        *  https://stackoverflow.com/a/54727768
        */

        public static Expression EnumerationLoop(ParameterExpression enumerator, Expression loopContent)
        {
            var loop = While(
                Call(
                    enumerator,
                    typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))),
                loopContent);

            var enumeratorType = enumerator.Type;
            if (typeof(IDisposable).IsAssignableFrom(enumeratorType))
                return Using(enumerator, loop);

            if (!enumeratorType.IsValueType)
            {
                var disposable = Variable(typeof(IDisposable), "disposable");
                return TryFinally(
                    loop,
                    Block(new[] { disposable },
                        Assign(disposable, TypeAs(enumerator, typeof(IDisposable))),
                        IfThen(
                            NotEqual(disposable, Constant(null)),
                            Call(
                                disposable,
                                typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))))));
            }

            return loop;
        }

        public static TryExpression Using(ParameterExpression variable, Expression content)
        {
            var variableType = variable.Type;

            if (!typeof(IDisposable).IsAssignableFrom(variableType))
                throw new Exception($"'{variableType.FullName}': type used in a using statement must be implicitly convertible to 'System.IDisposable'");

            var getMethod = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose));

            if (variableType.IsValueType)
            {
                return TryFinally(
                    content,
                    Call(Convert(variable, typeof(IDisposable)), getMethod));
            }

            if (variableType.IsInterface)
            {
                return TryFinally(
                    content,
                    IfThen(
                        NotEqual(variable, Constant(null)),
                        Call(variable, getMethod)));
            }

            return TryFinally(
                content,
                IfThen(
                    NotEqual(variable, Constant(null)),
                    Call(Convert(variable, typeof(IDisposable)), getMethod)));
        }

        public static LoopExpression While(Expression loopCondition, Expression loopContent)
        {
            var breakLabel = Label();
            return Loop(
                IfThenElse(
                    loopCondition,
                    loopContent,
                    Break(breakLabel)),
                breakLabel);
        }
    }
}