
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace Codestellation.SolarWind.Threading
{
    public sealed unsafe class IOTaskScheduler : TaskScheduler
    {
        private static IOTaskScheduler _instance;

        public static IOTaskScheduler Instance => _instance ??= new IOTaskScheduler();

        private readonly ObjectPool<WorkItem> _workItemsPool;

        private IOTaskScheduler()
            => _workItemsPool = new DefaultObjectPool<WorkItem>(new WorkItemPolicy(this));

        protected override void QueueTask(Task task)
        {
            var wi = _workItemsPool.Get();
            wi.Task = task;
            ThreadPool.UnsafeQueueNativeOverlapped(wi.PNOlap);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            => TryExecuteTask(task);

        protected override IEnumerable<Task> GetScheduledTasks()
            => Enumerable.Empty<Task>();

        private class WorkItem
        {
            internal NativeOverlapped* PNOlap;
            internal IOTaskScheduler Scheduler;
            internal Task Task;

            internal void Callback(uint errorCode, uint numBytes, NativeOverlapped* pNOlap)
            {
                Scheduler.TryExecuteTask(Task);

                ObjectPool<WorkItem> pool = Scheduler._workItemsPool;
                if (pool != null)
                {
                    pool.Return(this);
                }
                else
                {
                    Overlapped.Free(pNOlap);
                }
            }
        }

        private class WorkItemPolicy : IPooledObjectPolicy<WorkItem>
        {
            private readonly IOTaskScheduler _scheduler;

            public WorkItemPolicy(IOTaskScheduler scheduler)
            {
                _scheduler = scheduler;
            }
            public WorkItem Create()
            {
                var workItem = new WorkItem {Scheduler = _scheduler};
                workItem.PNOlap = new Overlapped().UnsafePack(workItem.Callback, null);
                return workItem;
            }

            public bool Return(WorkItem obj) => true;
        }
    }
}