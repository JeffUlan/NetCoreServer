﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using NetCoreServer;

namespace HttpsServer
{
    class CommonCache
    {
        public static CommonCache GetInstance()
        {
            if (_instance == null)
                _instance = new CommonCache();
            return _instance;
        }

        public bool GetCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                    return true;
                else
                    return false;
            }
        }

        public void SetCache(string key, string value)
        {
            lock (_cacheLock)
                _cache[key] = value;
        }

        public bool DeleteCache(string key, out string value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out value))
                {
                    _cache.Remove(key);
                    return true;
                }
                else
                    return false;
            }
        }

        private readonly object _cacheLock = new object();
        private SortedDictionary<string, string> _cache = new SortedDictionary<string, string>();
        private static CommonCache _instance;
    }

    class HttpsCacheSession : HttpsSession
    {
        public HttpsCacheSession(NetCoreServer.HttpsServer server) : base(server) { }

        protected override void OnReceivedRequest(HttpRequest request)
        {
            // Show HTTP request content
            Console.WriteLine(request.GetString());

            // Process HTTP request methods
            if (request.Method == "HEAD")
                SendResponseAsync(Response.MakeHeadResponse());
            else if (request.Method == "GET")
            {
                // Get the cache value
                string cache;
                if (CommonCache.GetInstance().GetCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Required cache value was not found for the key: " + request.Url));
            }
            else if ((request.Method == "POST") || (request.Method == "PUT"))
            {
                // Set the cache value
                CommonCache.GetInstance().SetCache(request.Url, request.Body);
                // Response with the cache value
                SendResponseAsync(Response.MakeOKResponse());
            }
            else if (request.Method == "DELETE")
            {
                // Delete the cache value
                string cache;
                if (CommonCache.GetInstance().DeleteCache(request.Url, out cache))
                {
                    // Response with the cache value
                    SendResponseAsync(Response.MakeGetResponse(cache));
                }
                else
                    SendResponseAsync(Response.MakeErrorResponse("Deleted cache value was not found for the key: " + request.Url));
            }
            else if (request.Method == "OPTIONS")
                SendResponseAsync(Response.MakeOptionsResponse());
            else if (request.Method == "TRACE")
                SendResponseAsync(Response.MakeTraceResponse(request.Cache));
            else
                SendResponseAsync(Response.MakeErrorResponse("Unsupported HTTP method: " + request.Method));
        }

        protected override void OnReceivedRequestError(HttpRequest request, string error)
        {
            Console.WriteLine($"Request error: {error}");
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS session caught an error: {error.ToString()}");
        }
    }

    class HttpsCacheServer : NetCoreServer.HttpsServer
    {
        public HttpsCacheServer(SslContext context, IPAddress address, int port) : base(context, address, port) {}

        protected override SslSession CreateSession() { return new HttpsCacheSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"HTTPS server caught an error: {error.ToString()}");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // HTTPS server port
            int port = 8443;
            if (args.Length > 0)
                port = int.Parse(args[0]);
            // HTTPS server content path
            string www = "../../../../../www/api";
            if (args.Length > 1)
                www = args[1];

            Console.WriteLine($"HTTPS server port: {port}");
            Console.WriteLine($"HTTPS server static content path: {www}");
            Console.WriteLine($"HTTPS server website: https://localhost:{port}/api/index.html");

            Console.WriteLine();

            // Create and prepare a new SSL server context
            var context = new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", "qwerty"));

            // Create a new HTTP server
            var server = new HttpsCacheServer(context, IPAddress.Any, port);
            server.AddStaticContent(www, "/api");

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (; ; )
            {
                string line = Console.ReadLine();
                if (line == string.Empty)
                    break;

                // Restart the server
                if (line == "!")
                {
                    Console.Write("Server restarting...");
                    server.Restart();
                    Console.WriteLine("Done!");
                }
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
