using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliRedirect
{
    //Support the TTL of a cached item based on ConcurrentLru.
    internal sealed class ConcurrentLruCache<K, V>
    {
        private readonly TimeSpan _lifetime;
        private readonly ConcurrentLru<K, (V value, DateTime timestamp)> _cache;
        private readonly Func<K, ValueTask<V>> _func;

        public ConcurrentLruCache(int capacity, TimeSpan lifetime, Func<K, ValueTask<V>> func)
        {
            _lifetime = lifetime;
            _cache = new(capacity);
            _func = func;
        }

        public async ValueTask<V> GetAsync(K key)
        {
            var now = DateTime.Now;
            var ret = await _cache.GetOrAddAsync(key, GetWithTimestampAsync);
            if (ret.timestamp < now - _lifetime)
            {
                //Multiple threads may trigget this remove, which makes the value to be calculated
                //for multiple times.
                //Protect it with a lock.
                lock (this)
                {
                    if (_cache.TryGet(key, out ret))
                    {
                        if (ret.timestamp < now - _lifetime)
                        {
                            //Here we can ensure we remove the corrent item.
                            _cache.TryRemove(key);
                        }
                    }
                }
                //Now that the old has been removed. We recalculate (if not by other threads) outside
                //the lock.
                ret = await _cache.GetOrAddAsync(key, GetWithTimestampAsync);
            }
            return ret.value;
        }

        private async Task<(V, DateTime)> GetWithTimestampAsync(K key)
        {
            return (await _func(key), DateTime.Now);
        }
    }
}
