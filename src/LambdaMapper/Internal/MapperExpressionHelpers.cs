using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
// using System.Linq.Expressions;
using FastExpressionCompiler.LightExpression;
using System.Runtime.CompilerServices;

namespace LambdaMapper.Internal
{
    internal static class MapperExpressionHelpers
    {
        public static BlockExpression MapEachIEnumerable(
            Type sourceType,
            Type destinationType,
            ParameterExpression source,
            UnaryExpression mapper)
        {
            var counter = Expression.Variable(typeof(int), "counter");
            var length = Expression.Variable(typeof(int), "length");
            var underlyingSourceType = sourceType.GenericTypeArguments.First();
            var underlyingDestinationType = destinationType.GenericTypeArguments.First();
            var array = Expression.NewArrayBounds(underlyingDestinationType, length);
            var destination = Expression.Variable(array.Type, "dest");
            var destinationAccess = Expression.ArrayAccess(destination, counter);
            var element = Expression.Parameter(underlyingSourceType, "element");
            var getEnumerator = sourceType.GetMethod(nameof(IEnumerable.GetEnumerator));
            if (getEnumerator is null)
                getEnumerator = typeof(IEnumerable<>)
                    .MakeGenericType(sourceType)
                    .GetMethod(nameof(IEnumerable.GetEnumerator));
            var enumeratorType = getEnumerator.ReturnType;
            var enumerator = Expression.Variable(enumeratorType, "enumerator");
            var resetMethod = typeof(IEnumerator).GetMethod(nameof(IEnumerator.Reset));
            var isList = !(enumeratorType.DeclaringType is null);
            var count = isList
                ? typeof(MapperExpressionHelpers)
                    .GetMethod(nameof(MapperExpressionHelpers.ListCount))
                    .MakeGenericMethod(sourceType.GenericTypeArguments)
                : typeof(MapperExpressionHelpers)
                    .GetMethod(nameof(MapperExpressionHelpers.Count));

            if (isList)
            {
                var listType = typeof(List<>)
                    .MakeGenericType(destinationType.GenericTypeArguments);
                destination = Expression.Variable(array.Type, "dest");
                destination = Expression.Variable(
                    listType,
                    "dest");
                var addMethod = listType.GetMethod("Add");

                return Expression.Block(
                    new[] { enumerator, destination },
                    Expression.IfThen(
                        Expression.NotEqual(source, Expression.Constant(null)),
                        Expression.Block(
                            Expression.Assign(
                                destination,
                                Expression.New(
                                    typeof(List<>)
                                    .MakeGenericType(destinationType.GenericTypeArguments))),
                            Expression.Assign(enumerator, Expression.Call(source, getEnumerator)),
                            Expression.Call(enumerator, resetMethod),
                            EnumerationLoop(
                                enumerator,
                                Expression.Block(
                                    new[] { element },
                                    Expression.Assign(element, Expression.Property(enumerator, "Current")),
                                    Expression.Call(
                                        destination,
                                        addMethod,
                                        new [] { Expression.Invoke(
                                            mapper,
                                            element)}))))),
                    destination);
            }

            return Expression.Block(
                new[] { counter, length, enumerator, destination },
                Expression.IfThen(
                    Expression.NotEqual(source, Expression.Constant(null)),
                    Expression.Block(
                        Expression.Assign(counter, Expression.Constant(0)),
                        Expression.Assign(enumerator, Expression.Call(source, getEnumerator)),
                        Expression.Assign(length, Expression.Call(count, new [] { enumerator })),
                        Expression.Call(enumerator, resetMethod),
                        Expression.Assign(destination, array),
                        EnumerationLoop(
                            enumerator,
                            Expression.Block(
                                new[] { element },
                                Expression.Assign(element, Expression.Property(enumerator, "Current")),
                                Expression.Assign(
                                    destinationAccess,
                                    Expression.Invoke(
                                        mapper,
                                        element)),
                                Expression.Assign(
                                    counter,
                                    Expression.Add(counter, Expression.Constant(1))))))),
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

            var destination = Expression.Variable(destinationType, "dest");
            var sourceKvp = Expression.Parameter(sourceKvpType, "sourceKvp");
            var getEnumerator = sourceType.GetMethod(nameof(IEnumerable.GetEnumerator));
            var enumeratorType = getEnumerator.ReturnType;
            var enumerator = Expression.Variable(enumeratorType, "enumerator");
            var addMethod = destinationType.GetMethod(nameof(IDictionary.Add));

            return Expression.Block(
                new[] { enumerator, destination },
                Expression.Assign(enumerator, Expression.Call(source, getEnumerator)),
                Expression.Assign(destination, Expression.New(destinationType)),
                EnumerationLoop(
                    enumerator,
                    Expression.Block(
                        new[] { sourceKvp },
                        Expression.Assign(sourceKvp, Expression.Property(enumerator, nameof(IEnumerator.Current))),
                        Expression.Call(
                            destination,
                            addMethod,
                            Expression.Property(
                                sourceKvp,
                                sourceKvpType.GetProperty("Key")),
                            Expression.Invoke(
                                mapper,
                                Expression.Property(
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
            var itemCount = Expression.Constant(sourceTypeArguments.Length);
            var tuple = sourceType.GetInterface(nameof(ITuple));
            var counter = Expression.Variable(typeof(int), "counter");
            var array = Expression.NewArrayBounds(typeof(ParameterExpression), itemCount);
            var ctorInfo = destinationType.GetConstructors().First();
            var getItemMethod = tuple.GetMethod("get_Item");
            var breakLabel = Expression.Label();

            var expressions = new List<Expression>();
            var sourceItemParameters = new List<ParameterExpression>();
            var destinationItemParameters = new List<ParameterExpression>();
            for (var i = 0; i < sourceTypeArguments.Length; i++)
            {
                var mapper = mappers.ElementAt(i);
                var sourceItemParameter = Expression.Parameter(sourceTypeArguments[i], $"sourceItem{i}");
                sourceItemParameters.Add(sourceItemParameter);
                var destinationItemParameter = Expression.Parameter(destinationTypeArguments[i], $"destinationItem{i}");
                destinationItemParameters.Add(destinationItemParameter);
                expressions.AddRange( new Expression []
                {
                    Expression.Assign(
                        sourceItemParameter,
                        Expression.Convert(
                            Expression.Call(
                                source,
                                getItemMethod,
                                new [] { counter }),
                            sourceTypeArguments[i])),
                    Expression.IfThen(
                        Expression.NotEqual(sourceItemParameter, Expression.Constant(null)),
                        Expression.Assign(
                            destinationItemParameter,
                            Expression.Invoke(
                                mapper,
                                sourceItemParameter))),
                    Expression.Assign(counter, Expression.Add(counter, Expression.Constant(1)))
                });
            }

            var expressionParameters = new List<ParameterExpression>(destinationItemParameters);
            expressionParameters.AddRange(sourceItemParameters);
            expressionParameters.Add(counter);
            var blockExpressions = new List<Expression>();
            blockExpressions.Add(Expression.Assign(counter, Expression.Constant(0)));
            blockExpressions.AddRange(expressions);
            blockExpressions.Add(Expression.New(ctorInfo, destinationItemParameters.ToArray()));
            return Expression.Block(
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

        public static int ListCount<T>(List<T>.Enumerator iter) where T : class {
            var count = 0;
            using (iter as IDisposable)
            {
                while (iter.MoveNext()) count++;
            }
            return count;
        }

        public static Expression EnumerationLoop(ParameterExpression enumerator, Expression loopContent)
        {
            var loop = While(
                Expression.Call(
                    enumerator,
                    typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))),
                loopContent);

            var enumeratorType = enumerator.Type;
            if (typeof(IDisposable).IsAssignableFrom(enumeratorType))
                return Using(enumerator, loop);

            if (!enumeratorType.IsValueType)
            {
                var disposable = Expression.Variable(typeof(IDisposable), "disposable");
                return Expression.TryCatchFinally(
                    loop,
                    Expression.Block(new[] { disposable },
                        Expression.Assign(disposable, Expression.TypeAs(enumerator, typeof(IDisposable))),
                        Expression.IfThen(
                            Expression.NotEqual(disposable, Expression.Constant(null)),
                            Expression.Call(
                                disposable,
                                typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))))),
                    new CatchBlock[0]);
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
                return Expression.TryCatchFinally(
                    content,
                    Expression.Call(Expression.Convert(variable, typeof(IDisposable)), getMethod),
                    new CatchBlock[0]);
            }

            if (variableType.IsInterface)
            {
                return Expression.TryCatchFinally(
                    content,
                    Expression.IfThen(
                        Expression.NotEqual(variable, Expression.Constant(null)),
                        Expression.Call(variable, getMethod)),
                    new CatchBlock[0]);
            }

            return Expression.TryCatchFinally(
                content,
                Expression.IfThen(
                    Expression.NotEqual(variable, Expression.Constant(null)),
                    Expression.Call(Expression.Convert(variable, typeof(IDisposable)), getMethod)),
                new CatchBlock[0]);
        }

        public static LoopExpression While(Expression loopCondition, Expression loopContent)
        {
            var breakLabel = Expression.Label();
            return Expression.Loop(
                Expression.IfThenElse(
                    loopCondition,
                    loopContent,
                    Expression.Break(breakLabel)),
                breakLabel);
        }
    }
}