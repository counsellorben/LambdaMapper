using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;
using System.Reflection;

namespace LambdaMapper.Internal
{
    internal static class ClonerExpressionBuilder
    {
        internal static ConcurrentDictionary<Type, LambdaExpression> _typeClonerExpressions =
            new ConcurrentDictionary<Type, LambdaExpression>();

        public static void CreateCloner<T>()
        {

            if (typeof(T).IsValueType)
            {
                foreach (var genericTypeArgument in typeof(T).GenericTypeArguments)
                {
                    if (!_typeClonerExpressions.ContainsKey(genericTypeArgument))
                    {
                        AddTypeCloner(genericTypeArgument);
                    }
                }
            }

            if (typeof(T).GetInterface(nameof(IDictionary)) != null)
            {
                var itemType = typeof(T).GenericTypeArguments[1];
                if (!_typeClonerExpressions.ContainsKey(itemType))
                {
                    AddTypeCloner(itemType);
                }
                return;
            }

            var sourceProperties = typeof(T).GetProperties().Where(dest => dest.CanWrite);
            var parameterExpression = Parameter(typeof(T), "src");
            var bindings = typeof(T)
                .GetProperties()
                .Where(p => p.CanWrite)
                .Select(p =>
                    MapperExpressionBuilder.BuildBinding(parameterExpression, p, sourceProperties))
                .Where(binding => binding != null);

            Expression<Func<T, T>> lambda;
            if (typeof(T).GetConstructor(Type.EmptyTypes) == null)
            {
                lambda = CreateClonerForObjectWithoutParameterlessConstructor<T>(parameterExpression, bindings);
            }
            else
            {
                var dest = Parameter(typeof(T), "dest");
                lambda = Lambda<Func<T, T>>(
                    Block(
                        new [] { dest },
                        Assign(
                            dest,
                            Condition(
                                NotEqual(
                                    parameterExpression,
                                    Constant(null, typeof(T))),
                                MemberInit(
                                    New(typeof(T)),
                                    bindings),
                                Constant(null, typeof(T)))),
                        dest),
                    parameterExpression);
            }

            _typeClonerExpressions.TryAdd(typeof(T), lambda);
        }

        internal static LambdaExpression GetTypeCloner(Type sourceGenericType, Type propertyType)
        {
            if (!_typeClonerExpressions.ContainsKey(sourceGenericType))
            {
                AddTypeCloner(propertyType);
            }

            return _typeClonerExpressions
                .Single(kvp => sourceGenericType == kvp.Key).Value;
        }

        private static Expression<Func<T, T>> CreateClonerForObjectWithoutParameterlessConstructor<T>(
            ParameterExpression parameterExpression,
            IEnumerable<MemberAssignment> bindings)
        {
            var propertyTypes = typeof(T)
                .GetProperties()
                .Where(p => p.CanWrite)
                .Select(p => p.PropertyType)
                .ToArray();
            var ctor = typeof(T).GetConstructors()
                .SingleOrDefault(c => c.GetParameters().Length == propertyTypes.Length);

            if (ctor == null)
            {
                // number of properties and number of ctor parameters not aligned,
                // get default ctor and give it the expected objects,
                // then MemberInit the instantiated object
                ctor = typeof(T).GetConstructors().First();
                var parameterNames = ctor.GetParameters().Select(p => p.Name);
                var ctorExpressions = bindings
                    .Where(b => parameterNames.Contains(b.Member.Name))
                    .Select(b => b.Expression);
                var dest = Parameter(typeof(T), "dest");
                return Lambda<Func<T, T>>(
                    Block(
                        new [] { dest },
                        Assign(
                            dest,
                            Condition(
                                NotEqual(
                                    parameterExpression,
                                    Constant(null, typeof(T))),
                                MemberInit(
                                    New(ctor, ctorExpressions),
                                    bindings),
                                Constant(null, typeof(T)))),
                        dest),
                    parameterExpression);
            }

            var destination = Parameter(typeof(T), "destination");
            return Lambda<Func<T, T>>(
                Block(
                    new [] { destination },
                    Assign(
                        destination,
                        Condition(
                            NotEqual(
                                parameterExpression,
                                Constant(null, typeof(T))),
                            New(ctor, MapperExpressionBuilder.GetConstructorArguments<T>(bindings)),
                        Constant(null, typeof(T)))),
                    destination),
                parameterExpression);
        }

        private static void AddTypeCloner(Type genericTypeArgument)
        {
            var actionExpression = Lambda<Action>(
                Call(
                    typeof(ClonerExpressionBuilder),
                    nameof(ClonerExpressionBuilder.CreateCloner),
                    new[]
                    {
                        genericTypeArgument,
                    }));
            var action = actionExpression.Compile();
            action.DynamicInvoke();
        }

        internal static MemberAssignment BuildClonerBinding(
            Expression parameterExpression,
            MemberInfo destinationProperty,
            PropertyInfo sourceProperty,
            Type destinationType)
        {
            if ((sourceProperty.PropertyType.BaseType == null || !sourceProperty.PropertyType.BaseType.Equals(typeof(ValueType))) && 
                sourceProperty.PropertyType.IsConstructedGenericType ||
                sourceProperty.PropertyType.IsNested)
            {
                if (!_typeClonerExpressions.ContainsKey(sourceProperty.PropertyType))
                {
                    AddTypeCloner(sourceProperty.PropertyType);
                }

                if (sourceProperty.PropertyType.IsValueType)
                {
                    var genericTypes = sourceProperty.PropertyType.GenericTypeArguments;
                    var valueTypeCloners = new List<(UnaryExpression mappers, int sourceIndex, int destinationIndex)>();
                    var index = 0;
                    foreach (var genericType in genericTypes)
                    {
                        valueTypeCloners.Add(
                            (Quote(
                                _typeClonerExpressions
                                    .Single(kvp => genericTypes.Contains(kvp.Key)).Value),
                            index,
                            index));
                        index++;
                    }

                    return Bind(
                        destinationProperty,
                        MapEachTupleValueType.Execute(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Property(parameterExpression, sourceProperty),
                            valueTypeCloners));
                }

                if (sourceProperty.PropertyType.GetInterface(nameof(IDictionary)) != null)
                {
                    var dictionaryCloner = _typeClonerExpressions[sourceProperty.PropertyType.GenericTypeArguments[1]];
                    return MapperExpressionBuilder.ExpressionBind(
                        Property(parameterExpression, sourceProperty),
                        sourceProperty,
                        destinationProperty,
                        destinationType,
                        MapEachIDictionary.Execute(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Property(parameterExpression, sourceProperty),
                            Quote(dictionaryCloner)));
                }

                var cloner = _typeClonerExpressions[sourceProperty.PropertyType];
                return MapperExpressionBuilder.ExpressionBind(
                    Property(parameterExpression, sourceProperty),
                    sourceProperty,
                    destinationProperty,
                    destinationType,
                    Invoke(
                        Quote(cloner),
                        Property(parameterExpression, sourceProperty)));
            }

            return MapperExpressionBuilder.ExpressionBind(
                Property(parameterExpression, sourceProperty),
                sourceProperty,
                destinationProperty,
                destinationType,
                Property(parameterExpression, sourceProperty));
        }
    }
}
