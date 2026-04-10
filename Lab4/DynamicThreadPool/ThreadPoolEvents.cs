using System;

namespace DynamicThreadPool
{
    public class ThreadEventArgs : EventArgs
    {
        public string ThreadName { get; }
        public ThreadEventArgs(string threadName) => ThreadName = threadName;
    }

    public class TaskEventArgs : EventArgs
    {
        public string TaskDescription { get; }
        public Exception? Exception { get; }
        public TaskEventArgs(string description, Exception? exception = null)
        {
            TaskDescription = description;
            Exception = exception;
        }
    }
}