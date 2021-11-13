using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
// using System.Linq.Expressions;
using FastExpressionCompiler.LightExpression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
// using FastExpressionCompiler;

namespace LambdaMapper.Internal
{
    internal static class MapperExpressionBuilder
    {
        internal static ConcurrentDictionary<Type, LambdaExpression> _typeMapperExpressions =
            new ConcurrentDictionary<Type, LambdaExpression>();

        internal static ConcurrentDictionary<Type, LambdaExpression> _typeClonerExpressions =
            new ConcurrentDictionary<Type, LambdaExpression>();

        public static Func<TSource, TDestination> CreateMapper<TSource, TDestination>()
            where TSource : class
            where TDestination : class
        {
            var sourceProperties = typeof(TSource).GetProperties();
            var destinationProperties = typeof(TDestination).GetProperties().Where(dest => dest.CanWrite);
            var parameterExpression = Expression.Parameter(typeof(TSource), "src");

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
                lambda = Expression.Lambda<Func<TSource, TDestination>>(
                    Expression.MemberInit(
                        Expression.New(typeof(TDestination)),
                        bindings.ToArray()),
                    parameterExpression);
            }

            _typeMapperExpressions.TryAdd(typeof(TSource), lambda);
            var code = lambda.ToCSharpString();
            return lambda.CompileFast();
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
                return Expression.Lambda<Func<TSource, TDestination>>(
                    Expression.MemberInit(
                        Expression.New(
                            ctor,
                            ctorExpressions.ToArray()),
                        bindings.ToArray()),
                    parameterExpression);
            }

            return Expression.Lambda<Func<TSource, TDestination>>(
                Expression.New(
                    ctor,
                    GetConstructorArguments<TDestination>(bindings).ToArray()),
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
            var parameterExpression = Expression.Parameter(typeof(T), "src");
            var bindings = typeof(T)
                .GetProperties()
                .Where(p => p.CanWrite)
                .Select(p =>
                    BuildBinding(parameterExpression, p, sourceProperties))
                .Where(binding => binding != null);

            Expression<Func<T, T>> lambda;
            if (typeof(T).GetConstructor(Type.EmptyTypes) == null)
            {
                lambda = CreateClonerForObjectWithoutParameterlessConstructor<T>(parameterExpression, bindings);
            }
            else
            {
                lambda = Expression.Lambda<Func<T, T>>(
                    Expression.MemberInit(
                        Expression.New(typeof(T)),
                        bindings.ToArray()),
                    parameterExpression);
            }

            _typeClonerExpressions.TryAdd(typeof(T), lambda);
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
                return Expression.Lambda<Func<T, T>>(
                    Expression.MemberInit(
                        Expression.New(
                            ctor,
                            ctorExpressions.ToArray()),
                        bindings.ToArray()),
                    parameterExpression);
            }

            return Expression.Lambda<Func<T, T>>(
                Expression.New(
                    ctor,
                    GetConstructorArguments<T>(bindings).ToArray()),
                parameterExpression);
        }

        private static IEnumerable<Expression> GetConstructorArguments<T>(
            IEnumerable<MemberAssignment> bindings) =>
            bindings.Select(b => b.Expression);

        private static void AddTypeCloner(Type genericTypeArgument)
        {
            var actionExpression = Expression.Lambda<Action>(
                Expression.Call(
                    typeof(MapperExpressionBuilder),
                    nameof(MapperExpressionBuilder.CreateCloner),
                    new[]
                    {
                        genericTypeArgument,
                    }));
            var action = actionExpression.CompileFast();
            action.DynamicInvoke();
        }

        private static MemberAssignment BuildBinding(
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
                return BuildClonerBinding(
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
                        return ExpressionNullableBind(
                            Expression.Property(parameterExpression, sourceProperty),
                            sourceChildProperty,
                            destinationProperty,
                            destinationType,
                            Expression.Property(
                                Expression.Property(
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
            var source = Expression.Property(parameterExpression, sourceProperty);
            var sourceParameterExpression = Expression.Parameter(sourceType, "srcItems");

            if (!_typeMapperExpressions.ContainsKey(sourceType))
            {
                if (sourceProperty.PropertyType.IsValueType)
                {
                    var sourceGenericTypes = sourceProperty.PropertyType.GenericTypeArguments;
                    var destinationGenericTypes = ((PropertyInfo)destinationProperty).PropertyType.GenericTypeArguments;
                    var valueTypeMappers = new List<UnaryExpression>();
                    var counter = 0;
                    foreach (var sourceGenericType in sourceGenericTypes)
                    {
                        if (sourceGenericType != destinationGenericTypes[counter])
                        {
                            valueTypeMappers.Add(
                                Expression.Quote(
                                    _typeMapperExpressions
                                        .Single(kvp => sourceGenericTypes.Contains(kvp.Key)).Value));
                        }
                        else
                        {
                            if (!_typeClonerExpressions.ContainsKey(sourceGenericType))
                            {
                                AddTypeCloner(sourceProperty.PropertyType);
                            }
                            valueTypeMappers.Add(
                                Expression.Quote(
                                    _typeClonerExpressions
                                        .Single(kvp => sourceGenericTypes.Contains(kvp.Key)).Value));
                        }
                        counter++;
                    }
                    return Expression.Bind(
                        destinationProperty,
                        Expression.Invoke(
                            Expression.Lambda(
                                MapperExpressionHelpers.MapEachValueType(
                                    sourceProperty.PropertyType,
                                    ((PropertyInfo)destinationProperty).PropertyType,
                                    Expression.Property(parameterExpression, sourceProperty),
                                    valueTypeMappers),
                                sourceParameterExpression),
                            Expression.Property(parameterExpression, sourceProperty)));
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
                        return ExpressionNullableBind(
                            Expression.Property(parameterExpression, sourceProperty),
                            sourceProperty,
                            destinationProperty,
                            destinationType,
                            Expression.Invoke(
                                Expression.Lambda(
                                    MapperExpressionHelpers.MapEachIDictionary(
                                        sourceType,
                                        destinationType,
                                        source,
                                        Expression.Quote(dictionaryMapper)),
                                    sourceParameterExpression),
                                Expression.Property(parameterExpression, sourceProperty)));
                    }

                    var sourceGenericTypeArgument = sourceGenericTypeArguments[0];
                    if (!_typeMapperExpressions.ContainsKey(sourceGenericTypeArgument))
                        throw new Exception("mapper missing");

                    var collectionMapper = _typeMapperExpressions[sourceGenericTypeArgument];
                    return ExpressionNullableBind(
                        Expression.Property(parameterExpression, sourceProperty),
                        sourceProperty,
                        destinationProperty,
                        destinationType,
                        Expression.Invoke(
                            Expression.Lambda(
                                MapperExpressionHelpers.MapEachIEnumerable(
                                    sourceType,
                                    destinationType,
                                    sourceParameterExpression,
                                    Expression.Quote(collectionMapper)),
                                sourceParameterExpression),
                            Expression.Property(parameterExpression, sourceProperty)));
                }

                throw new Exception("mapper missing");
            }

            var mapper = _typeMapperExpressions[sourceType];
            return ExpressionNullableBind(
                Expression.Property(parameterExpression, sourceProperty),
                sourceProperty,
                destinationProperty,
                destinationType,
                Expression.Invoke(
                    Expression.Quote(mapper),
                    Expression.Property(parameterExpression, sourceProperty)));
        }

        private static MemberAssignment BuildClonerBinding(
            Expression parameterExpression,
            MemberInfo destinationProperty,
            PropertyInfo sourceProperty,
            Type destinationType)
        {
            if (sourceProperty.PropertyType.IsConstructedGenericType || sourceProperty.PropertyType.IsNested)
            {
                if (!_typeClonerExpressions.ContainsKey(sourceProperty.PropertyType))
                {
                    AddTypeCloner(sourceProperty.PropertyType);
                }

                var sourceParameterExpression = Expression.Parameter(sourceProperty.PropertyType, "srcItems");
                if (sourceProperty.PropertyType.IsValueType)
                {
                    var genericTypes = sourceProperty.PropertyType.GenericTypeArguments;
                    var valueTypeCloners = new List<UnaryExpression>();
                    foreach (var genericType in genericTypes)
                    {
                        valueTypeCloners.Add(
                            Expression.Quote(
                                _typeClonerExpressions
                                    .Single(kvp => genericTypes.Contains(kvp.Key)).Value));
                    }
                    return Expression.Bind(
                        destinationProperty,
                        Expression.Invoke(
                            Expression.Lambda(
                                MapperExpressionHelpers.MapEachValueType(
                                    sourceProperty.PropertyType,
                                    ((PropertyInfo)destinationProperty).PropertyType,
                                    Expression.Property(parameterExpression, sourceProperty),
                                    valueTypeCloners),
                                sourceParameterExpression),
                            Expression.Property(parameterExpression, sourceProperty)));
                }

                if (sourceProperty.PropertyType.GetInterface(nameof(IDictionary)) != null)
                {
                    var dictionaryCloner = _typeClonerExpressions[sourceProperty.PropertyType.GenericTypeArguments[1]];
                    return ExpressionNullableBind(
                        Expression.Property(parameterExpression, sourceProperty),
                        sourceProperty,
                        destinationProperty,
                        destinationType,
                        Expression.Invoke(
                            Expression.Lambda(
                                MapperExpressionHelpers.MapEachIDictionary(
                                    sourceProperty.PropertyType,
                                    ((PropertyInfo)destinationProperty).PropertyType,
                                    Expression.Property(parameterExpression, sourceProperty),
                                    Expression.Quote(dictionaryCloner)),
                                sourceParameterExpression),
                            Expression.Property(parameterExpression, sourceProperty)));
                }

                var cloner = _typeClonerExpressions[sourceProperty.PropertyType];
                return ExpressionNullableBind(
                    Expression.Property(parameterExpression, sourceProperty),
                    sourceProperty,
                    destinationProperty,
                    destinationType,
                    Expression.Invoke(
                        Expression.Quote(cloner),
                        Expression.Property(parameterExpression, sourceProperty)));
            }

            return ExpressionNullableBind(
                Expression.Property(parameterExpression, sourceProperty),
                sourceProperty,
                destinationProperty,
                destinationType,
                Expression.Property(parameterExpression, sourceProperty));
        }

        private static MemberAssignment ExpressionNullableBind(
            MemberExpression memberExpression,
            PropertyInfo sourceProperty,
            MemberInfo destinationProperty,
            Type destinationType,
            Expression bindingExpression)
        {
            if (sourceProperty.GetCustomAttribute<NullableAttribute>() != null)
            {
                return Expression.Bind(
                    destinationProperty,
                    Expression.Condition(
                        Expression.NotEqual(memberExpression, Expression.Constant(null)),
                        bindingExpression,
                        Expression.Constant(null, destinationType)));
            }
            return Expression.Bind(
                destinationProperty,
                bindingExpression);
        }

        private static string[] SplitCamelCase(string input)
        {
            return Regex.Replace(input, "([A-Z])", " $1", RegexOptions.Compiled).Trim().Split(' ');
        }
    }
}
