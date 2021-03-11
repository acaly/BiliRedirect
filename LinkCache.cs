using BitFaster.Caching.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BiliRedirect
{
    internal sealed class LinkCache
    {
        public struct Item
        {
            public ReadOnlyMemory<byte> Title;
            public ReadOnlyMemory<byte> Url;
        }

        private struct InternalCacheItem
        {
            public byte[] Title;
            public byte[] Url;
        }

        private class BilibiliVideoPageInfo
        {
            public int Page { get; init; }
            public int Cid { get; init; }
            public string Part { get; init; }
        }

        private class BilibiliVideoDataInfo
        {
            public List<BilibiliVideoPageInfo> Pages { get; init; } = new();
        }

        private class BilibiliVideoInfo
        {
            public BilibiliVideoDataInfo Data { get; init; } = new();
        }

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string _bvInfoApi = "https://api.bilibili.com/x/web-interface/view?bvid=";
        private const string _outputUrl = "https://www.bilibili.com/video/";

        private readonly ConcurrentLruCache<(string bvid, int cid), InternalCacheItem?> _pageCache;
        private readonly ConcurrentLruCache<string, BilibiliVideoInfo> _videoCache;
        private readonly HttpClient _apiClient = new();

        public LinkCache(int capacity, int lifetimeSeconds)
        {
            _pageCache = new(capacity, TimeSpan.FromSeconds(lifetimeSeconds), CacheItemFactory);
            _videoCache = new(capacity / 5 + 1, TimeSpan.FromSeconds(lifetimeSeconds), VideoInfoFactory);
        }

        public async ValueTask<Item?> Get(string bvid, int cid)
        {
            var internalItem = await _pageCache.GetAsync((bvid, cid));
            if (!internalItem.HasValue)
            {
                return null;
            }
            return new Item
            {
                Title = internalItem.Value.Title,
                Url = internalItem.Value.Url,
            };
        }

        private async ValueTask<InternalCacheItem?> CacheItemFactory((string bvid, int cid) key)
        {
            var videoInfo = await _videoCache.GetAsync(key.bvid);
            foreach (var page in videoInfo.Data.Pages)
            {
                if (page.Cid == key.cid)
                {
                    return new InternalCacheItem
                    {
                        Title = Encoding.UTF8.GetBytes(page.Part),
                        Url = Encoding.UTF8.GetBytes($"{_outputUrl}{key.bvid}?p={page.Page}"),
                    };
                }
            }
            return null;
        }

        private async ValueTask<BilibiliVideoInfo> VideoInfoFactory(string bvid)
        {
            try
            {
                return await _apiClient.GetFromJsonAsync<BilibiliVideoInfo>(_bvInfoApi + bvid, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}
