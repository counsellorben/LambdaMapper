using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace LambdaMapper.Internal
{
    internal static class MapperExpressionBuilder
    {
        internal static ConcurrentDictionary<Type, LambdaExpression> _typeMapperExpressions =
            new ConcurrentDictionary<Type, LambdaExpression>();

        internal static Func<TSource, TDestination> CreateMapper<TSource, TDestination>()
            where TSource : class
            where TDestination : class
        {
            var sourceProperties = typeof(TSource).GetProperties();
            var destinationProperties = typeof(TDestination).GetProperties().Where(dest => dest.CanWrite);
            var parameterExpression = Parameter(typeof(TSource), "src");

            var bindings = destinationProperties
                .Select(destinationProperty =>
                    BuildBinding(parameterExpression, destinationProperty, sourceProperties))
                .Where(binding => binding != null);

            Expression<Func<TSource, TDestination>> lambda;
            if (typeof(TDestination).GetConstructor(Type.EmptyTypes) == null)
            {
                lambda = CreateMapperForObjectWithoutParameterlessConstructor<TSource, TDestination>(
                    parameterExpression,
                    bindings);
            }
            else
            {
                var dest = Parameter(typeof(TDestination), "dest");
                lambda = Lambda<Func<TSource, TDestination>>(
                    Block(
                        new [] { dest },
                        Assign(
                            dest,
                            Condition(
                                NotEqual(
                                    parameterExpression,
                                    Constant(null, typeof(TSource))),
                                MemberInit(
                                    New(typeof(TDestination)),
                                    bindings),
                                Constant(null, typeof(TDestination)))),
                        dest),
                    parameterExpression);
            }

            _typeMapperExpressions.TryAdd(typeof(TSource), lambda);
            return lambda.Compile();
        }

        private static Expression<Func<TSource, TDestination>> CreateMapperForObjectWithoutParameterlessConstructor<TSource, TDestination>(
            ParameterExpression parameterExpression,
            IEnumerable<MemberAssignment> bindings)
            where TSource : class
            where TDestination : class
        {
            var propertyTypes = typeof(TDestination)
                .GetProperties()
                .Where(p => p.CanWrite)
                .Select(p => p.PropertyType)
                .ToArray();
            var ctor = typeof(TDestination).GetConstructors()
                .SingleOrDefault(c => c.GetParameters().Length == propertyTypes.Length);

            if (ctor == null)
            {
                // number of properties and number of ctor parameters not aligned,
                // get default ctor and give it the expected objects,
                // then MemberInit the instantiated object
                ctor = typeof(TDestination).GetConstructors().First();
                var parameterNames = ctor.GetParameters().Select(p => p.Name);
                var ctorExpressions = bindings
                    .Where(b => parameterNames.Contains(b.Member.Name))
                    .Select(b => b.Expression);
                var dest = Parameter(typeof(TDestination), "dest");
                return Lambda<Func<TSource, TDestination>>(
                    Block(
                        new [] { dest },
                        Assign(
                            dest,
                            Condition(
                                NotEqual(
                                    parameterExpression,
                                    Constant(null, typeof(TSource))),
                                MemberInit(
                                    New(
                                        ctor,
                                        ctorExpressions),
                                    bindings),
                                Constant(null, typeof(TDestination)))),
                        dest),
                    parameterExpression);
            }

            var destination = Parameter(typeof(TDestination), "destination");
            return Lambda<Func<TSource, TDestination>>(
                Block(
                    new [] { destination },
                    Assign(
                        destination,
                        Condition(
                            NotEqual(
                                parameterExpression,
                                Constant(null, typeof(TSource))),
                            New(
                                ctor,
                                GetConstructorArguments<TDestination>(bindings)),
                            Constant(null, typeof(TDestination)))),
                    destination),
                parameterExpression);
        }

        private static Expression<Func<TSource, TDestination>> GetMapperExpression<TSource, TDestination>(
            TSource source,
            TDestination destination)
            where TSource : class
            where TDestination : class
        {
            var mapperExpressionExists = _typeMapperExpressions.TryGetValue(
                typeof(TSource),
                out var mapperExpression);

            if (mapperExpressionExists)
            {
                return (Expression<Func<TSource, TDestination>>)mapperExpression;
            }

            return (Expression<Func<TSource, TDestination>>)GetMapperExpression(source, destination);
        }

        internal static IEnumerable<Expression> GetConstructorArguments<T>(
            IEnumerable<MemberAssignment> bindings) =>
            bindings.Select(b => b.Expression);

        internal static MemberAssignment BuildBinding(
            Expression parameterExpression,
            MemberInfo destinationProperty,
            IEnumerable<PropertyInfo> sourceProperties)
        {
            var sourceProperty = sourceProperties.FirstOrDefault(src => 
                src.Name == destinationProperty.Name &&
                src.PropertyType == ((PropertyInfo)destinationProperty).PropertyType);

            var destinationType = ((PropertyInfo)destinationProperty)
                .PropertyType;

            if (sourceProperty != null)
            {
                return ClonerExpressionBuilder.BuildClonerBinding(
                    parameterExpression,
                    destinationProperty,
                    sourceProperty,
                    destinationType);
            }

            if (sourceProperty == null && sourceProperties
                .Select(src => src.Name)
                .Contains(destinationProperty.Name))
            {
                return BuildMapperBinding(
                    parameterExpression,
                    destinationProperty,
                    sourceProperties,
                    destinationType);
            }

            var propertyNames = SplitCamelCase(destinationProperty.Name);

            if (propertyNames.Length == 2)
            {
                sourceProperty = sourceProperties.FirstOrDefault(src => src.Name == propertyNames[0]);

                if (sourceProperty != null)
                {
                    var sourceChildProperty = sourceProperty
                        .PropertyType
                        .GetProperties()
                        .FirstOrDefault(src => src.Name == propertyNames[1]);

                    if (sourceChildProperty != null)
                    {
                        return ExpressionBind(
                            Property(parameterExpression, sourceProperty),
                            sourceChildProperty,
                            destinationProperty,
                            destinationType,
                            Property(
                                Property(
                                    parameterExpression,
                                    sourceProperty),
                                sourceChildProperty));
                    }
                }
            }

            return null;
        }

        private static MemberAssignment BuildMapperBinding(
            Expression parameterExpression,
            MemberInfo destinationProperty,
            IEnumerable<PropertyInfo> sourceProperties,
            Type destinationType)
        {
            var sourceProperty = sourceProperties.FirstOrDefault(src =>
                src.Name == destinationProperty.Name);
            var sourceType = sourceProperty.PropertyType;
            var source = Property(parameterExpression, sourceProperty);

            if (!_typeMapperExpressions.ContainsKey(sourceType))
            {
                if (sourceProperty.PropertyType.IsValueType)
                {
                    var sourceNamesAttribute = sourceProperty.CustomAttributes
                        .SingleOrDefault(a => a.AttributeType.Equals(typeof(TupleElementNamesAttribute)));
                    var destinationNamesAttribute = destinationProperty.CustomAttributes
                        .SingleOrDefault(a => a.AttributeType.Equals(typeof(TupleElementNamesAttribute)));

                    var sourceGenericTypes = sourceProperty.PropertyType.GenericTypeArguments;
                    var destinationGenericTypes = ((PropertyInfo)destinationProperty).PropertyType.GenericTypeArguments;
                    var destinationGenericTypeCount = destinationGenericTypes.Length;
                    var sourceCounter = 0;
                    var valueTypeMaps = new List<(UnaryExpression mapper, int sourceIndex, int destinationIndex)>();

                    if (sourceNamesAttribute == null || destinationNamesAttribute == null)
                    {
                        foreach (var sourceGenericType in sourceGenericTypes)
                        {
                            var destinationCounter = 0;
                            var noMatch = true;
                            while (noMatch && destinationCounter < destinationGenericTypeCount)
                            {
                                if (valueTypeMaps.Any(m => m.destinationIndex == destinationCounter))
                                    continue;

                                if (sourceGenericType == destinationGenericTypes[destinationCounter])
                                {
                                    valueTypeMaps.Add(
                                        (Quote(
                                            ClonerExpressionBuilder.GetTypeCloner(
                                                sourceGenericType,
                                                sourceProperty.PropertyType)),
                                        sourceCounter,
                                        destinationCounter));
                                    noMatch = false;
                                }
                                else if (_typeMapperExpressions.Any(kvp => kvp.Key == sourceGenericType))
                                {
                                    valueTypeMaps.Add(
                                        (Quote(
                                            _typeMapperExpressions
                                                .Single(kvp => sourceGenericTypes.Contains(kvp.Key)).Value),
                                        sourceCounter,
                                        destinationCounter));
                                    noMatch = false;
                                }

                                destinationCounter++;
                            }

                            sourceCounter++;
                        }

                        return Bind(
                            destinationProperty,
                            MapEachTupleValueType.Execute(
                                sourceProperty.PropertyType,
                                ((PropertyInfo)destinationProperty).PropertyType,
                                Property(parameterExpression, sourceProperty),
                                valueTypeMaps));
                    }
                    else
                    {
                        var sourceNames = ((ReadOnlyCollection<CustomAttributeTypedArgument>)sourceNamesAttribute
                            .ConstructorArguments
                            .First()
                            .Value)
                                .Select(a => a.Value.ToString())
                                .ToList();
                        var destinationNames = ((ReadOnlyCollection<CustomAttributeTypedArgument>)destinationNamesAttribute
                            .ConstructorArguments
                            .First()
                            .Value)
                                .Select(a => a.Value.ToString())
                                .ToList();
                        foreach (var sourceName in sourceNames)
                        {
                            if (!destinationNames.Contains(sourceName))
                                continue;

                            var destinationCounter = destinationNames.IndexOf(sourceName);
                            var sourceGenericType = sourceGenericTypes[sourceCounter];

                            if (sourceGenericType == destinationGenericTypes[destinationCounter])
                            {
                                valueTypeMaps.Add(
                                    (Quote(
                                        ClonerExpressionBuilder.GetTypeCloner(
                                            sourceGenericType,
                                            sourceProperty.PropertyType)),
                                    sourceCounter,
                                    destinationCounter));
                            }
                            else
                            {
                                valueTypeMaps.Add(
                                    (Quote(
                                        _typeMapperExpressions
                                            .Single(kvp => sourceGenericTypes.Contains(kvp.Key)).Value),
                                    sourceCounter,
                                    destinationCounter));
                            }

                            sourceCounter++;
                        }

                        return Bind(
                            destinationProperty,
                            MapEachTupleValueType.Execute(
                                sourceProperty.PropertyType,
                                ((PropertyInfo)destinationProperty).PropertyType,
                                Property(parameterExpression, sourceProperty),
                                valueTypeMaps));
                    }
                }

                if (sourceProperty.PropertyType.IsConstructedGenericType)
                {
                    var sourceGenericTypeArguments = sourceType
                        .GenericTypeArguments;
                    var destinationGenericTypeArguments = destinationType
                        .GenericTypeArguments;

                    if (sourceType.GetInterface(nameof(IDictionary)) != null)
                    {
                        var sourceValueTypeArgument = sourceGenericTypeArguments[1];
                        if (!_typeMapperExpressions.ContainsKey(sourceValueTypeArgument))
                            throw new Exception("mapper missing");

                        var dictionaryMapper = _typeMapperExpressions[sourceGenericTypeArguments[1]];
                        return ExpressionBind(
                            Property(parameterExpression, sourceProperty),
                            sourceProperty,
                            destinationProperty,
                            destinationType,
                            MapEachIDictionary.Execute(
                                sourceType,
                                destinationType,
                                source,
                                Quote(dictionaryMapper)));
                    }

                    var sourceGenericTypeArgument = sourceGenericTypeArguments[0];
                    if (!_typeMapperExpressions.ContainsKey(sourceGenericTypeArgument))
                        throw new Exception("mapper missing");

                    var collectionMapper = _typeMapperExpressions[sourceGenericTypeArgument];
                    return ExpressionBind(
                        Property(parameterExpression, sourceProperty),
                        sourceProperty,
                        destinationProperty,
                        destinationType,
                        MapEachIEnumerable.Execute(
                            sourceType,
                            destinationType,
                            source,
                            Quote(collectionMapper)));
                }

                throw new Exception("mapper missing");
            }

            var mapper = _typeMapperExpressions[sourceType];
            return ExpressionBind(
                Property(parameterExpression, sourceProperty),
                sourceProperty,
                destinationProperty,
                destinationType,
                Invoke(
                    Quote(mapper),
                    Property(parameterExpression, sourceProperty)));
        }

        internal static MemberAssignment ExpressionBind(
            MemberExpression memberExpression,
            PropertyInfo sourceProperty,
            MemberInfo destinationProperty,
            Type destinationType,
            Expression bindingExpression)
        {
            if (sourceProperty.GetCustomAttribute<NullableAttribute>() != null)
            {
                return Bind(
                    destinationProperty,
                    Condition(
                        NotEqual(
                            memberExpression,
                            Constant(null, sourceProperty.PropertyType)),
                        bindingExpression,
                        Constant(null, bindingExpression.Type)));
            }

            return Bind(
                destinationProperty,
                bindingExpression);
        }

        private static string[] SplitCamelCase(string input)
        {
            return Regex.Replace(input, "([A-Z])", " $1", RegexOptions.Compiled).Trim().Split(' ');
        }
    }
}
