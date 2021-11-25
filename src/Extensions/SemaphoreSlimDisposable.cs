using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Operator.Extensions
{
    internal class SemaphoreSlimDisposable : IDisposable
    {
        private SemaphoreSlim _semaphore;
        public SemaphoreSlimDisposable(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
        }

        public void Dispose()
        {
            _semaphore?.Release();
            _semaphore = null;
            GC.SuppressFinalize(this);
        }
    }
}
