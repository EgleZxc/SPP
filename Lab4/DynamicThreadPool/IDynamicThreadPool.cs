using System;

namespace DynamicThreadPool
{
    public interface IDynamicThreadPool : IDisposable
    {
        void Enqueue(Action task);
        string GetState();
        int TaskQueueCount { get; }
        int ActiveThreadCount { get; }
        event EventHandler<ThreadEventArgs> ThreadCreated;
        event EventHandler<ThreadEventArgs> ThreadDestroyed;
        event EventHandler<TaskEventArgs> TaskQueued;
        event EventHandler<TaskEventArgs> TaskStarted;
        event EventHandler<TaskEventArgs> TaskCompleted;
        event EventHandler<TaskEventArgs> TaskFailed;
        event EventHandler PoolDisposed;
    }
}