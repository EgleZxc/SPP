using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace DynamicThreadPool
{
    public class DynamicPool : IDynamicThreadPool
    {
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly TimeSpan _idleTimeout;
        private readonly ConcurrentQueue<Action> _taskQueue = new();
        private readonly List<Thread> _threads = new();
        private readonly object _threadListLock = new();
        private readonly SemaphoreSlim _taskSemaphore = new(0);
        private bool _disposed;
        private int _activeThreadCount;

        public event EventHandler<ThreadEventArgs>? ThreadCreated;
        public event EventHandler<ThreadEventArgs>? ThreadDestroyed;
        public event EventHandler<TaskEventArgs>? TaskQueued;
        public event EventHandler<TaskEventArgs>? TaskStarted;
        public event EventHandler<TaskEventArgs>? TaskCompleted;
        public event EventHandler<TaskEventArgs>? TaskFailed;
        public event EventHandler? PoolDisposed;

        public int ThreadCount => _threads.Count;
        public int ActiveThreadCount => _activeThreadCount;
        public int TaskQueueCount => _taskQueue.Count;

        public DynamicPool(int minThreads = 2, int maxThreads = 8, int idleTimeoutSeconds = 5)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeout = TimeSpan.FromSeconds(idleTimeoutSeconds);
            for (int i = 0; i < _minThreads; i++)
                CreateThread();
        }

        private void CreateThread()
        {
            var thread = new Thread(WorkerLoop) { IsBackground = true, Name = $"PoolThread-{_threads.Count + 1}" };
            _threads.Add(thread);
            thread.Start();
            ThreadCreated?.Invoke(this, new ThreadEventArgs(thread.Name));
        }

        private void RemoveCurrentThread()
        {
            var current = Thread.CurrentThread;
            lock (_threadListLock)
            {
                _threads.Remove(current);
            }
            ThreadDestroyed?.Invoke(this, new ThreadEventArgs(current.Name));
        }

        public void Enqueue(Action task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (_disposed) throw new ObjectDisposedException(nameof(DynamicPool));

            _taskQueue.Enqueue(task);
            _taskSemaphore.Release();
            TaskQueued?.Invoke(this, new TaskEventArgs(task.Method.Name));

            lock (_threadListLock)
            {
                if (_threads.Count < _maxThreads && _activeThreadCount >= _threads.Count)
                {
                    CreateThread();
                }
            }
        }

        private void WorkerLoop()
        {
            while (!_disposed)
            {
                try
                {
                    if (!_taskSemaphore.Wait(_idleTimeout))
                    {
                        lock (_threadListLock)
                        {
                            if (_threads.Count > _minThreads && _activeThreadCount == 0)
                            {
                                RemoveCurrentThread();
                                return;
                            }
                        }
                        continue;
                    }

                    if (_disposed) break;

                    if (_taskQueue.TryDequeue(out var task))
                    {
                        Interlocked.Increment(ref _activeThreadCount);
                        TaskStarted?.Invoke(this, new TaskEventArgs(task.Method.Name));
                        try
                        {
                            task();
                            TaskCompleted?.Invoke(this, new TaskEventArgs(task.Method.Name));
                        }
                        catch (Exception ex)
                        {
                            TaskFailed?.Invoke(this, new TaskEventArgs(task.Method.Name, ex));
                            Console.WriteLine($"[Ошибка в потоке {Thread.CurrentThread.Name}]: {ex.Message}");
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _activeThreadCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Фатальная ошибка в потоке {Thread.CurrentThread.Name}]: {ex.Message}");
                }
            }
        }

        public string GetState()
        {
            lock (_threadListLock)
            {
                return $"Потоков: {_threads.Count} (активных: {_activeThreadCount}), задач в очереди: {_taskQueue.Count}";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _taskSemaphore.Release(_threads.Count);
            foreach (var thread in _threads)
                thread.Join(100);
            _taskSemaphore.Dispose();
            PoolDisposed?.Invoke(this, EventArgs.Empty);
        }
    }
}