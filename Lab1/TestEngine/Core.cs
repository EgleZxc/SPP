using System;

namespace TestEngine
{
    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public interface ISharedContext : IDisposable
    {
        void Initialize();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class TestSuiteAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class FactAttribute : Attribute
    {
        public string Description { get; set; }
        public int Priority { get; set; } = 0;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TestDataAttribute : Attribute
    {
        public object[] Parameters { get; }
        public TestDataAttribute(params object[] parameters) => Parameters = parameters;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SkipAttribute : Attribute
    {
        public string Reason { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class InitializeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class CleanupAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class SharedContextTypeAttribute : Attribute
    {
        public Type ContextType { get; }
        public SharedContextTypeAttribute(Type contextType) => ContextType = contextType;
    }
}