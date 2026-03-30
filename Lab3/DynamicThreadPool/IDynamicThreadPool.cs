using System;

namespace DynamicThreadPool
{
    public interface IDynamicThreadPool : IDisposable
    {
        void Enqueue(Action task);
        string GetState();
    }
}