namespace Operator.Domain
{
    internal class JobRuner<TJobParameter>
    {
        public DateTime? LastExecutedAt { get; private set; }
        private ILogger _logger = LoggerFactory.GetLogger<JobRuner<TJobParameter>>();
        private BlockingCollection<TJobParameter> _queue;
        private Task[] _tasks;

        public JobRuner(Job<TJobParameter> job)
        {
            _queue = new BlockingCollection<TJobParameter>(1000);
            _tasks = new Task[Program.AppConfiguration.WorkerCount];

            for (int i = 0; i < _tasks.Length; i++)
            {
                _tasks[i] = Task.Factory.StartNew(async () =>
                {
                    foreach (TJobParameter jobParameter in _queue.GetConsumingEnumerable())
                    {
                        try
                        {
                            await job.RunAsync(jobParameter).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e, "Job is failed {job}", job.GetType().Name);
                        }
                    }
                }, TaskCreationOptions.LongRunning).Unwrap();
            }
        }

        public void Enqueue(TJobParameter jobParameter)
        {
            LastExecutedAt = DateTime.UtcNow;
            _queue.Add(jobParameter);
        }
    }

    internal abstract class Job<TJobParameter>
    {
        public abstract Task RunAsync(TJobParameter jobParameter);
    }
}
