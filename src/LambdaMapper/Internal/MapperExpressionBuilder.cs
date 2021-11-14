using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        internal static ConcurrentDictionary<Type, LambdaExpression> _typeClonerExpressions =
            new ConcurrentDictionary<Type, LambdaExpression>();

        public static Func<TSource, TDestination> CreateMapper<TSource, TDestination>()
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
                        IfThen(
                            NotEqual(
                                parameterExpression,
                                Constant(null, typeof(TSource))),
                            Assign(
                                dest,
                                MemberInit(
                                    New(typeof(TDestination)),
                                    bindings))),
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
                        IfThen(
                            NotEqual(
                                parameterExpression,
                                Constant(null, typeof(TSource))),
                            Assign(
                                dest,
                                MemberInit(
                                    New(
                                        ctor,
                                        ctorExpressions),
                                    bindings))),
                        dest),
                    parameterExpression);
            }

            var destination = Parameter(typeof(TDestination), "destination");
            return Lambda<Func<TSource, TDestination>>(
                Block(
                    new [] { destination },
                    IfThen(
                        NotEqual(
                            parameterExpression,
                            Constant(null, typeof(TSource))),
                        Assign(
                            destination,
                            New(
                                ctor,
                                GetConstructorArguments<TDestination>(bindings)))),
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
                    BuildBinding(parameterExpression, p, sourceProperties))
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
                        IfThen(
                            NotEqual(
                                parameterExpression,
                                Constant(null, typeof(T))),
                            Assign(
                                dest,
                                MemberInit(
                                    New(typeof(T)),
                                    bindings))),
                        dest),
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
                var dest = Parameter(typeof(T), "dest");
                return Lambda<Func<T, T>>(
                    Block(
                        new [] { dest },
                        IfThen(
                            NotEqual(
                                parameterExpression,
                                Constant(null, typeof(T))),
                            Assign(
                                dest,
                                MemberInit(
                                    New(ctor, ctorExpressions),
                                    bindings))),
                        dest),
                    parameterExpression);
            }

            var destination = Parameter(typeof(T), "destination");
            return Lambda<Func<T, T>>(
                Block(
                    new [] { destination },
                    IfThen(
                        NotEqual(
                            parameterExpression,
                            Constant(null, typeof(T))),
                        Assign(
                            destination,
                            New(ctor, GetConstructorArguments<T>(bindings)))),
                    destination),
                parameterExpression);
        }

        private static IEnumerable<Expression> GetConstructorArguments<T>(
            IEnumerable<MemberAssignment> bindings) =>
            bindings.Select(b => b.Expression);

        private static void AddTypeCloner(Type genericTypeArgument)
        {
            var actionExpression = Lambda<Action>(
                Call(
                    typeof(MapperExpressionBuilder),
                    nameof(MapperExpressionBuilder.CreateCloner),
                    new[]
                    {
                        genericTypeArgument,
                    }));
            var action = actionExpression.Compile();
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
                    var sourceGenericTypes = sourceProperty.PropertyType.GenericTypeArguments;
                    var destinationGenericTypes = ((PropertyInfo)destinationProperty).PropertyType.GenericTypeArguments;
                    var valueTypeMappers = new List<UnaryExpression>();
                    var counter = 0;
                    foreach (var sourceGenericType in sourceGenericTypes)
                    {
                        if (sourceGenericType != destinationGenericTypes[counter])
                        {
                            valueTypeMappers.Add(
                                Quote(
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
                                Quote(
                                    _typeClonerExpressions
                                        .Single(kvp => sourceGenericTypes.Contains(kvp.Key)).Value));
                        }
                        counter++;
                    }
                    return Bind(
                        destinationProperty,
                        MapperExpressionHelpers.MapEachValueType(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Property(parameterExpression, sourceProperty),
                            valueTypeMappers));
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
                            MapperExpressionHelpers.MapEachIDictionary(
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
                        MapperExpressionHelpers.MapEachIEnumerable(
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

        private static MemberAssignment BuildClonerBinding(
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
                    var valueTypeCloners = new List<UnaryExpression>();
                    foreach (var genericType in genericTypes)
                    {
                        valueTypeCloners.Add(
                            Quote(
                                _typeClonerExpressions
                                    .Single(kvp => genericTypes.Contains(kvp.Key)).Value));
                    }
                    return Bind(
                        destinationProperty,
                        MapperExpressionHelpers.MapEachValueType(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Property(parameterExpression, sourceProperty),
                            valueTypeCloners));
                }

                if (sourceProperty.PropertyType.GetInterface(nameof(IDictionary)) != null)
                {
                    var dictionaryCloner = _typeClonerExpressions[sourceProperty.PropertyType.GenericTypeArguments[1]];
                    return ExpressionBind(
                        Property(parameterExpression, sourceProperty),
                        sourceProperty,
                        destinationProperty,
                        destinationType,
                        MapperExpressionHelpers.MapEachIDictionary(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Property(parameterExpression, sourceProperty),
                            Quote(dictionaryCloner)));
                }

                var cloner = _typeClonerExpressions[sourceProperty.PropertyType];
                return ExpressionBind(
                    Property(parameterExpression, sourceProperty),
                    sourceProperty,
                    destinationProperty,
                    destinationType,
                    Invoke(
                        Quote(cloner),
                        Property(parameterExpression, sourceProperty)));
            }

            return ExpressionBind(
                Property(parameterExpression, sourceProperty),
                sourceProperty,
                destinationProperty,
                destinationType,
                Property(parameterExpression, sourceProperty));
        }

        private static MemberAssignment ExpressionBind(
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
