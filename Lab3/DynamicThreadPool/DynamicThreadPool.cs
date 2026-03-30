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

        public int ThreadCount => _threads.Count;
        public int ActiveThreadCount => _activeThreadCount;
        public int TaskQueueCount => _taskQueue.Count;

        public DynamicPool(int minThreads = 2, int maxThreads = 8, int idleTimeoutSeconds = 5)
        {
            if (minThreads <= 0) throw new ArgumentException("minThreads must be > 0", nameof(minThreads));
            if (maxThreads < minThreads) throw new ArgumentException("maxThreads must be >= minThreads", nameof(maxThreads));
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeout = TimeSpan.FromSeconds(idleTimeoutSeconds);

            for (int i = 0; i < _minThreads; i++)
                CreateThread();
        }

        public void Enqueue(Action task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (_disposed) throw new ObjectDisposedException(nameof(DynamicPool));

            _taskQueue.Enqueue(task);
            _taskSemaphore.Release();

            lock (_threadListLock)
            {
                if (_threads.Count < _maxThreads && _activeThreadCount >= _threads.Count)
                {
                    CreateThread();
                }
            }
        }

        private void CreateThread()
        {
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"PoolThread-{_threads.Count + 1}"
            };
            _threads.Add(thread);
            thread.Start();
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
                        try
                        {
                            task();
                        }
                        catch (Exception ex)
                        {
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

        private void RemoveCurrentThread()
        {
            var current = Thread.CurrentThread;
            lock (_threadListLock)
            {
                _threads.Remove(current);
            }
            Console.WriteLine($"Поток {current.Name} завершён (простой > {_idleTimeout.TotalSeconds} сек)");
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
            {
                thread.Join(100);
            }
            _taskSemaphore.Dispose();
        }
    }
}