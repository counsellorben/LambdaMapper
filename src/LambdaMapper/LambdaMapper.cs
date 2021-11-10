using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using FastExpressionCompiler;
using LambdaMapper.Internal;

[assembly: AllowPartiallyTrustedCallers]
[assembly: SecurityTransparent]
[assembly: SecurityRules(SecurityRuleSet.Level2,SkipVerificationInFullTrust=true)]
namespace LambdaMapper
{
    public static class LambdaMapper
    {
        private static readonly ConcurrentDictionary<Type, object> _typeMappers =
            new ConcurrentDictionary<Type, object>();

        private static readonly List<(Type type, LambdaExpression expression)> _typeMapperExpressions =
            new List<(Type type, LambdaExpression expression)>();

        /// <summary>
        /// Creates a map between the source type and the destination type
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TDestination"></typeparam>
        /// <returns></returns>
        public static void CreateMap<TSource, TDestination>()
            where TSource : class
            where TDestination : class
        {
            _typeMapperExpressions.Add(
                (typeof(TSource),
                Expression.Lambda<Action>(
                    Expression.Call(
                        typeof(LambdaMapper),
                        "CreateMapper",
                        new [] 
                        {
                            typeof(TSource),
                            typeof(TDestination)
                        }))));
        }

        public static void CreateMapper<TSource, TDestination>()
            where TSource : class
            where TDestination : class
        {
            _typeMappers.TryAdd(
                typeof(TSource),
                MapperExpressionBuilder.CreateMapper<TSource, TDestination>());
        }

        public static void InstantiateMapper()
        {
            _typeMappers.Clear();

            var consecutiveErrors = 0;
            while (_typeMapperExpressions.Any() && consecutiveErrors < 16)
            {
                var typeFuncExpr = _typeMapperExpressions.First();
                try
                {
                    var func = typeFuncExpr.expression.CompileFast();
                    var result = func.DynamicInvoke();
                    _typeMappers.TryAdd(typeFuncExpr.type, result);
                    consecutiveErrors = 0;
                    _typeMapperExpressions.Remove(typeFuncExpr);
                }
                catch
                {
                    _typeMapperExpressions.Remove(typeFuncExpr);
                    _typeMapperExpressions.Add(typeFuncExpr);
                    consecutiveErrors++;
                }
            }

            if (consecutiveErrors > 0)
            {
                throw new Exception("Missing mappers");
            }
        }

        /// <summary>
        /// Returns an object mapped from the source object
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TDestination"></typeparam>
        /// <param name="source"></param>
        /// <returns>object of type TDestination mapped from object of type TSource</returns>
        public static TDestination MapObject<TSource, TDestination>(TSource source)
            where TSource : class
            where TDestination : class =>
            !_typeMappers.TryGetValue(typeof(TSource), out var mapper)
                ? throw new Exception($"No mapper between '{typeof(TSource).Name}' and '{typeof(TDestination).Name}'")
                : (TDestination)((Func<TSource, TDestination>)mapper)(source);
    }
}