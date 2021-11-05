using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
            var parameterExpression = Expression.Parameter(typeof(TSource), "src");

            var bindings = destinationProperties
                .Select(destinationProperty =>
                    BuildBinding(parameterExpression, destinationProperty, sourceProperties))
                .Where(binding => binding != null);

            Expression<Func<TSource, TDestination>> lambda;
            if (typeof(TDestination).GetConstructor(Type.EmptyTypes) == null)
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
                    lambda = Expression.Lambda<Func<TSource, TDestination>>(
                        Expression.MemberInit(
                            Expression.New(
                                ctor,
                                ctorExpressions),
                            bindings),
                        parameterExpression);
                }
                else
                {
                    lambda = Expression.Lambda<Func<TSource, TDestination>>(
                        Expression.New(
                            ctor,
                            GetConstructorArguments<TDestination>(bindings)),
                        parameterExpression);
                }
            }
            else
            {
                lambda = Expression.Lambda<Func<TSource, TDestination>>(
                    Expression.MemberInit(
                        Expression.New(typeof(TDestination)),
                        bindings),
                    parameterExpression);
            }

            _typeMapperExpressions.TryAdd(typeof(TSource), lambda);
            return lambda.Compile();
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
                    lambda = Expression.Lambda<Func<T, T>>(
                        Expression.MemberInit(
                            Expression.New(
                                ctor,
                                ctorExpressions),
                            bindings),
                        parameterExpression);
                }
                else
                {
                    lambda = Expression.Lambda<Func<T, T>>(
                        Expression.New(
                            ctor,
                            GetConstructorArguments<T>(bindings)),
                        parameterExpression);
                }
            }
            else
            {
                lambda = Expression.Lambda<Func<T, T>>(
                    Expression.MemberInit(
                        Expression.New(typeof(T)),
                        bindings),
                    parameterExpression);
            }

            _typeClonerExpressions.TryAdd(typeof(T), lambda);
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
                        MapEachValueType(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Expression.Property(parameterExpression, sourceProperty),
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
                        return ExpressionNullableBind(
                            Expression.Property(parameterExpression, sourceProperty),
                            sourceProperty,
                            destinationProperty,
                            destinationType,
                            MapEachIDictionary(
                                sourceType,
                                destinationType,
                                source,
                                Expression.Quote(dictionaryMapper)));
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
                        MapEachIEnumerable(
                            sourceType,
                            destinationType,
                            source,
                            Expression.Quote(collectionMapper)));
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
                        MapEachValueType(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Expression.Property(parameterExpression, sourceProperty),
                            valueTypeCloners));
                }

                if (sourceProperty.PropertyType.GetInterface(nameof(IDictionary)) != null)
                {
                    var dictionaryCloner = _typeClonerExpressions[sourceProperty.PropertyType.GenericTypeArguments[1]];
                    return ExpressionNullableBind(
                        Expression.Property(parameterExpression, sourceProperty),
                        sourceProperty,
                        destinationProperty,
                        destinationType,
                        MapEachIDictionary(
                            sourceProperty.PropertyType,
                            ((PropertyInfo)destinationProperty).PropertyType,
                            Expression.Property(parameterExpression, sourceProperty),
                            Expression.Quote(dictionaryCloner)));
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

        private static object RetrieveMapperFunction(Type sourceType, Type destinationType)
        {
            var dictionaryType = typeof(ConcurrentDictionary<Type, object>);
            var dictionary = Expression.Parameter(dictionaryType, "dictionary");
            var key = Expression.Parameter(typeof(Type), "key");
            var result = Expression.Parameter(typeof(object), "result");
            var blockExpression = Expression.Block(
                new[] { result },
                Expression.Assign(
                    result,
                    Expression.Property(dictionary, "Item", key)),
                result);
 
            return null;
        }

        private static BlockExpression MapEachIEnumerable(
            Type sourceType,
            Type destinationType,
            Expression source,
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
            var isList = enumeratorType.DeclaringType is not null;
            var count = isList
                ? typeof(MapperExpressionBuilder)
                    .GetMethod(nameof(MapperExpressionBuilder.ListCount))
                    .MakeGenericMethod(sourceType.GenericTypeArguments)
                : typeof(MapperExpressionBuilder)
                    .GetMethod(nameof(MapperExpressionBuilder.Count));

            if (isList)
            {
                var listType = typeof(List<>)
                    .MakeGenericType(destinationType.GenericTypeArguments);
                destination = Expression.Variable(
                    listType,
                    "dest");
                var addMethod = listType.GetMethod("Add");

                return Expression.Block(
                        new[] { counter, length, enumerator, destination },
                        Expression.IfThen(
                            Expression.NotEqual(source, Expression.Constant(null)),
                            Expression.Block(
                                Expression.Assign(
                                    destination,
                                    Expression.New(typeof(List<>).MakeGenericType(destinationType.GenericTypeArguments))),
                                Expression.Assign(counter, Expression.Constant(0)),
                                Expression.Assign(enumerator, Expression.Call(source, getEnumerator)),
                                Expression.Assign(length, Expression.Call(count, new [] { enumerator })),
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
                                                element)}),
                                        Expression.Assign(counter, Expression.Add(counter, Expression.Constant(1))))))),
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
                                    Expression.Assign(counter, Expression.Add(counter, Expression.Constant(1))))))),
                    destination);
        }

        private static BlockExpression MapEachIDictionary(
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

        private static BlockExpression MapEachValueType(
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
            blockExpressions.Add(Expression.New(ctorInfo, destinationItemParameters));
            return Expression.Block(
                expressionParameters,
                blockExpressions);

            // return Expression.Block(
            //     expressionParameters,
            //     Expression.Assign(counter, Expression.Constant(0)),
            //     Expression.Block(expressions),
            //     Expression.New(ctorInfo, destinationItemParameters));
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

        private static Expression EnumerationLoop(ParameterExpression enumerator, Expression loopContent)
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
                return Expression.TryFinally(
                    loop,
                    Expression.Block(new[] { disposable },
                        Expression.Assign(disposable, Expression.TypeAs(enumerator, typeof(IDisposable))),
                        Expression.IfThen(
                            Expression.NotEqual(disposable, Expression.Constant(null)),
                            Expression.Call(
                                disposable,
                                typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))))));
            }

            return loop;
        }

        private static TryExpression Using(ParameterExpression variable, Expression content)
        {
            var variableType = variable.Type;

            if (!typeof(IDisposable).IsAssignableFrom(variableType))
                throw new Exception($"'{variableType.FullName}': type used in a using statement must be implicitly convertible to 'System.IDisposable'");

            var getMethod = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose));

            if (variableType.IsValueType)
            {
                return Expression.TryFinally(
                    content,
                    Expression.Call(Expression.Convert(variable, typeof(IDisposable)), getMethod));
            }

            if (variableType.IsInterface)
            {
                return Expression.TryFinally(
                    content,
                    Expression.IfThen(
                        Expression.NotEqual(variable, Expression.Constant(null)),
                        Expression.Call(variable, getMethod)));
            }

            return Expression.TryFinally(
                content,
                Expression.IfThen(
                    Expression.NotEqual(variable, Expression.Constant(null)),
                    Expression.Call(Expression.Convert(variable, typeof(IDisposable)), getMethod)));
        }

        private static LoopExpression While(Expression loopCondition, Expression loopContent)
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
