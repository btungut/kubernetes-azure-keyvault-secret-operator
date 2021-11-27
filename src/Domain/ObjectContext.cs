namespace Operator.Domain
{
    internal class ObjectContext<TKey, TValue>
    {
        private Dictionary<TKey, TValue> _bag = new Dictionary<TKey, TValue>();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<TKey, SemaphoreSlim> _namedSemaphores = new ConcurrentDictionary<TKey, SemaphoreSlim>();

        public async Task ExclusiveAsync(TKey key, Func<TValue, Task> onAcquired)
        {
            if (_bag.TryGetValue(key, out var obj))
            {
                var semaphore = _namedSemaphores.GetOrAdd(key, (_) => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync();
                using (new SemaphoreSlimDisposable(semaphore))
                {
                    await onAcquired(obj);
                }
            }
        }

        public bool IsExist(TKey key) => _bag.ContainsKey(key);

        public async Task SetAsync(TKey key, TValue value)
        {
            await _semaphore.WaitAsync();
            using (new SemaphoreSlimDisposable(_semaphore))
            {
                _bag[key] = value;
            }
        }

        public async Task RemoveAsync(TKey key)
        {
            await _semaphore.WaitAsync();
            using (new SemaphoreSlimDisposable(_semaphore))
            {
                _bag.Remove(key);
                if(_namedSemaphores.TryRemove(key, out var semaphore))
                {
                    semaphore.Dispose();
                }
            }
        }

        public async Task<KeyValuePair<TKey, TValue>[]> GetSnapshotAsync()
        {
            await _semaphore.WaitAsync();
            using (new SemaphoreSlimDisposable(_semaphore))
            {
                return _bag.ToArray();
            }
        }

    }
}
