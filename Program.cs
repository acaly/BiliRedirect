using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliRedirect
{
    class Program
    {
        private static readonly Dictionary<string, Action<string>> _argHandlers = new()
        {
            { "threads", val => _server.Threads = int.Parse(val) },
            { "prefix", val => _server.Prefix = val },
            { "about_url", val => _server.AboutUrl = val },
            { "cache_capacity", val => _server.CacheCapacity = int.Parse(val) },
            { "cache_lifetime", val => _server.CacheLifetime = int.Parse(val) },
        };

        private static void HandleArgs(string[] args)
        {
            foreach (var arg in args.Select(aa => aa.Split('=')).GroupBy(aa => aa[0]))
            {
                if (!_argHandlers.TryGetValue(arg.Key, out var handler))
                {
                    throw new Exception($"Unknown argument {arg.Key}");
                }
                if (arg.Count() != 1)
                {
                    throw new Exception($"Duplicate argument {arg.Key}");
                }
                var val = arg.First();
                handler(val.Length switch
                {
                    1 => null,
                    2 => val[1],
                    _ => throw new Exception($"Invalid argument {arg.Key}"),
                });
            }
        }

        private static readonly Server _server = new();

        static void Main(string[] args)
        {
            Console.WriteLine("BiliRedirect");
            HandleArgs(args);
            _server.Start();
        }
    }
}
