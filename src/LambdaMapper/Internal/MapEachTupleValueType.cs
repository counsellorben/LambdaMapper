using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace LambdaMapper.Internal
{
    internal static class MapEachTupleValueType
    {
        internal static BlockExpression Execute(
            Type sourceType,
            Type destinationType,
            Expression source,
            IEnumerable<(UnaryExpression mapper, int sourceIndex, int destinationIndex)> maps)
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
                var map = maps.SingleOrDefault(m => m.sourceIndex == i);

                if (map.mapper == null)
                    continue;

                var mapper = map.mapper;
                var sourceItemParameter = Parameter(sourceTypeArguments[i], $"sourceItem{i}");
                sourceItemParameters.Add(sourceItemParameter);
                var destinationItemParameter = Parameter(destinationTypeArguments[map.destinationIndex], $"destinationItem{i}");
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
                    Assign(
                        destinationItemParameter,
                        Condition(
                            NotEqual(sourceItemParameter, Constant(null)),
                            Invoke(
                                mapper,
                                sourceItemParameter),
                            Constant(null, destinationTypeArguments[i]))),
                    AddAssign(counter, Constant(1))
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
    }
}