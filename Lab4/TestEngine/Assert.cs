using System;
using System.Collections;
using System.Linq.Expressions;

namespace TestEngine
{
    public static class Assert
    {
        public static void True(bool condition, string? message = null)
        {
            if (!condition)
                throw new AssertionException(message ?? "Ожидалось истина, но получено ложь.");
        }

        public static void False(bool condition, string? message = null)
        {
            if (condition)
                throw new AssertionException(message ?? "Ожидалось ложь, но получено истина.");
        }

        public static void Equal<T>(T expected, T actual, string? message = null)
        {
            if (!Equals(expected, actual))
                throw new AssertionException(message ?? $"Ожидалось: {expected}, Фактически: {actual}");
        }

        public static void NotEqual<T>(T expected, T actual, string? message = null)
        {
            if (Equals(expected, actual))
                throw new AssertionException(message ?? $"Ожидалось не равно, но оба значения {expected}");
        }

        public static void Null(object? obj, string? message = null)
        {
            if (obj != null)
                throw new AssertionException(message ?? "Ожидалось null, но получено не null.");
        }

        public static void NotNull(object? obj, string? message = null)
        {
            if (obj == null)
                throw new AssertionException(message ?? "Ожидалось не null, но получено null.");
        }

        public static void Throws<TException>(Action action, string? message = null) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new AssertionException(message ?? $"Ожидалось исключение {typeof(TException).Name}, но получено {ex.GetType().Name}.");
            }
            throw new AssertionException(message ?? $"Ожидалось исключение {typeof(TException).Name}, но исключение не было выброшено.");
        }

        public static void Contains(string substring, string fullString, string? message = null)
        {
            if (!fullString.Contains(substring))
                throw new AssertionException(message ?? $"Строка '{fullString}' не содержит '{substring}'.");
        }

        public static void Empty(IEnumerable collection, string? message = null)
        {
            if (collection.GetEnumerator().MoveNext())
                throw new AssertionException(message ?? "Ожидалась пустая коллекция, но она содержит элементы.");
        }

        public static void NotEmpty(IEnumerable collection, string? message = null)
        {
            if (!collection.GetEnumerator().MoveNext())
                throw new AssertionException(message ?? "Ожидалась непустая коллекция, но она пуста.");
        }

        public static void GreaterThan(int value, int threshold, string? message = null)
        {
            if (value <= threshold)
                throw new AssertionException(message ?? $"{value} не больше {threshold}.");
        }

        public static void LessThan(int value, int threshold, string? message = null)
        {
            if (value >= threshold)
                throw new AssertionException(message ?? $"{value} не меньше {threshold}.");
        }

        public static void WithInfo(Expression<Func<bool>> condition, string? message = null)
        {
            var compiled = condition.Compile();
            bool result = compiled();
            if (!result)
            {
                string expressionText = condition.ToString();
                string details = ExpressionDetail(condition.Body);
                string fullMessage = string.IsNullOrEmpty(message)
                    ? $"Условие провалено: {expressionText}\nДетали: {details}"
                    : $"{message}\nВыражение: {expressionText}\nДетали: {details}";
                    throw new AssertionException(fullMessage);
            }
        }

        private static string ExpressionDetail(Expression expr)
        {
            if (expr is BinaryExpression binary)
            {
                var leftVal = Expression.Lambda(binary.Left).Compile().DynamicInvoke();
                var rightVal = Expression.Lambda(binary.Right).Compile().DynamicInvoke();
                return $"{binary.NodeType} (левый: {leftVal}, правый: {rightVal})";
            }
            if (expr is MethodCallExpression call)
            {
                return $"Вызов метода {call.Method.Name}";
            }
            if (expr is MemberExpression member)
            {
                var val = Expression.Lambda(member).Compile().DynamicInvoke();
                return $"Поле/свойство {member.Member.Name} = {val}";
            }
            return expr.ToString();
        }
    }
}