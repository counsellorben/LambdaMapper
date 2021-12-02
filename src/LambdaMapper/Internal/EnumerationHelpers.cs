using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace LambdaMapper.Internal
{
    internal static class EnumerationHelpers
    {
        public static int Count(IEnumerator iter)
        {
            var count = 0;
            using (iter as IDisposable)
            {
                while (iter.MoveNext()) count++;
            }
            return count;
        }

        public static int ListCount<T>(List<T>.Enumerator iter) where T : class
        {
            var count = 0;
            using (iter as IDisposable)
            {
                while (iter.MoveNext()) count++;
            }
            return count;
        }

        public static int DictionaryCount<TKey, TValue>(Dictionary<TKey, TValue>.Enumerator iter)
        {
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