using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BiliRedirect
{
    internal sealed class Server
    {
        private readonly object[] _redirectPageTemplate;
        private byte[] _aboutUrlData;
        private LinkCache _cache;

        //We are probably using VPS with 1 CPU core.
        public int Threads { get; set; } = 1;
        public string Prefix { get; set; } = "http://127.0.0.1:13637/";
        public int CacheCapacity { get; set; } = 10000;
        public int CacheLifetime { get; set; } = 3600;
        public string AboutUrl { get; set; } = null;
        private HttpListener _listener;

        public Server()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BiliRedirect.Template.html");
            using var reader = new StreamReader(stream);
            var str = reader.ReadToEnd();
            var fields = new Regex(@"\{\{([a-zA-Z0-9_]+)\}\}");
            var matches = fields.Matches(str).OrderBy(m => m.Index);

            List<object> items = new();
            var lastEnd = 0;
            foreach (var match in matches)
            {
                items.Add(Encoding.UTF8.GetBytes(str[lastEnd..match.Index]));
                items.Add(match.Groups[1].Value);
                lastEnd = match.Index + match.Length;
            }
            items.Add(Encoding.UTF8.GetBytes(str[lastEnd..]));
            _redirectPageTemplate = items.ToArray();
        }

        //Blocking.
        public void Start()
        {
            Console.WriteLine("Starting server with parameters:");
            Console.WriteLine($"  Listen on {Prefix}.");
            Console.WriteLine($"  {Threads} worker threads.");
            Console.WriteLine($"  LRU cache capacity {CacheCapacity}.");
            Console.WriteLine($"  LRU cache lifetime {CacheLifetime} s.");

            _cache = new(CacheCapacity, CacheLifetime);
            _aboutUrlData = AboutUrl is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(AboutUrl);

            Console.WriteLine($"Listener Starting.");
            _listener = new HttpListener();

            _listener.Prefixes.Add(Prefix);
            try
            {
                _listener.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot listen on {Prefix}.\n{e}");
                return;
            }

            Task[] workers = new Task[Threads];
            for (int i = 0; i < Threads; ++i)
            {
                workers[i] = WorkerThread(i);
            }
            Task.WaitAll(workers);
            Console.WriteLine("All worker threads exited.");

            _listener.Stop();
        }

        private async Task WrappedWorkerThread(int threadID)
        {
            while (true)
            {
                try
                {
                    await WorkerThread(threadID);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[{threadID:00}] unhandled exception.\n{e}");
                    await Task.Delay(5000);
                }
            }
        }

        private async Task WorkerThread(int threadID)
        {
            var templateParameters = new Dictionary<string, ReadOnlyMemory<byte>>();
            var requestRegex = new Regex(@"^/\?bvid=(BV[a-zA-Z0-9]+)&cid=(\d+)$");

            Console.WriteLine($"[{threadID:00}] Started.");
            while (_listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                try
                {
                    var url = context.Request.RawUrl;
                    Console.WriteLine($"[{threadID:00}] {url}");

                    if (url.StartsWith("/?bvid="))
                    {
                        var match = requestRegex.Match(url);
                        if (match.Success)
                        {
                            await HandleBvCid(context.Response, match, templateParameters);
                            context.Response.Close();
                            continue;
                        }
                    }

                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
        }

        private async ValueTask HandleBvCid(HttpListenerResponse response,
            Match requestUrl, Dictionary<string, ReadOnlyMemory<byte>> dict)
        {
            if (!int.TryParse(requestUrl.Groups[2].Value, out var cidValue))
            {
                response.StatusCode = 404;
                return;
            }
            var videoInfo = await _cache.Get(requestUrl.Groups[1].Value, cidValue);
            if (!videoInfo.HasValue)
            {
                response.StatusCode = 404;
                return;
            }

            dict.Clear();
            dict.Add("Title", videoInfo.Value.Title);
            dict.Add("TargetUrl", videoInfo.Value.Url);
            dict.Add("AboutUrl", _aboutUrlData);

            response.StatusCode = 200;
            response.Headers.Add(HttpResponseHeader.CacheControl, "public, max-age=" + CacheLifetime);

            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;

            await WriteTemplate(response.OutputStream, dict);
        }

        private async ValueTask WriteTemplate(Stream output, Dictionary<string, ReadOnlyMemory<byte>> parameters)
        {
            foreach (var item in _redirectPageTemplate)
            {
                if (item is byte[] data)
                {
                    await output.WriteAsync(data);
                }
                else if (item is string key)
                {
                    await output.WriteAsync(parameters[key]);
                }
            }
        }
    }
}
