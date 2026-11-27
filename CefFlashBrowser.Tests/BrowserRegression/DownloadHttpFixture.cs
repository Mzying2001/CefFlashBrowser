using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;

public sealed class DownloadHttpFixture : IDisposable
{
    private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly ConcurrentDictionary<TcpClient, Task> clients = new ConcurrentDictionary<TcpClient, Task>();
    private readonly ConcurrentDictionary<string, string> ranges = new ConcurrentDictionary<string, string>();
    private readonly ConcurrentDictionary<string, string> cookies = new ConcurrentDictionary<string, string>();
    private readonly Task accept;
    private int postCount;
    public string BaseUrl { get; private set; }
    public byte[] Payload { get; private set; }
    public int PostCount { get { return Volatile.Read(ref postCount); } }
    public bool ChangedVersion { get; set; }

    public DownloadHttpFixture()
    {
        Payload = new byte[2 * 1024 * 1024];
        for (int i = 0; i < Payload.Length; i++) Payload[i] = (byte)(i * 31 + 7);
        listener.Start();
        BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        accept = AcceptAsync();
    }

    public string RangeFor(string path)
    {
        string value;
        return ranges.TryGetValue(path, out value) ? value : "";
    }

    public string CookieFor(string path)
    {
        string value;
        return cookies.TryGetValue(path, out value) ? value : "";
    }

    public static void RegisterUnrelatedResource(IWebBrowser browser, string url)
    {
        browser.RegisterResourceHandler(url, new MemoryStream(Encoding.UTF8.GetBytes("registered")), "text/plain");
    }

    private async Task AcceptAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var work = RespondAsync(client);
                clients[client] = work;
                Task cleanup = work.ContinueWith(completed =>
                {
                    Task ignored;
                    clients.TryRemove(client, out ignored);
                }, TaskScheduler.Default);
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException)
            {
                if (cancellation.IsCancellationRequested)
                    break;

                throw;
            }
        }
    }

    private async Task RespondAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                    return;

                var parts = line.Split(' ');
                var path = parts[1];
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                        headers[line.Substring(0, colon)] = line.Substring(colon + 1).Trim();
                }

                string contentLength;
                int bodyLength;
                if (headers.TryGetValue("Content-Length", out contentLength) && int.TryParse(contentLength, out bodyLength))
                {
                    var buffer = new char[bodyLength];
                    int offset = 0;
                    while (offset < bodyLength)
                    {
                        int count = await reader.ReadAsync(buffer, offset, bodyLength - offset).ConfigureAwait(false);
                        if (count == 0)
                            break;
                        offset += count;
                    }
                }

                if (parts[0] == "POST")
                    Interlocked.Increment(ref postCount);

                if (path == "/page")
                {
                    var html = Encoding.UTF8.GetBytes("<title>Download fixture</title><a href='/slow' download>Download</a><form id='post' method='post' action='/slow-post'><input name='data' value='test'></form>");
                    var head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: " + html.Length + "\r\nSet-Cookie: download-session=present; Max-Age=3600; Path=/\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(head, 0, head.Length).ConfigureAwait(false);
                    await stream.WriteAsync(html, 0, html.Length).ConfigureAwait(false);
                    return;
                }

                string range;
                headers.TryGetValue("Range", out range);
                ranges[path] = range ?? "";
                string cookie;
                headers.TryGetValue("Cookie", out cookie);
                cookies[path] = cookie ?? "";
                int start = 0;
                if (path != "/ignore-range" && range != null && range.StartsWith("bytes="))
                    int.TryParse(range.Substring(6).TrimEnd('-'), out start);
                if (start >= Payload.Length)
                    start = 0;

                var status = start == 0 ? "200 OK" : "206 Partial Content";
                var response = "HTTP/1.1 " + status + "\r\nContent-Type: application/octet-stream\r\n";
                // The manual download already chooses a destination. An attachment header lets
                // external download managers intercept the fixture response before CEF reads it.
                if (path != "/file")
                    response += "Content-Disposition: attachment; filename=\"" + path.TrimStart('/') + ".bin\"\r\n";
                response += "Content-Length: " + (Payload.Length - start) + "\r\nAccept-Ranges: bytes\r\nETag: \"" + (ChangedVersion ? "v2" : "v1") + "\"\r\nConnection: close\r\n";
                if (start != 0)
                    response += "Content-Range: bytes " + start + "-" + (Payload.Length - 1) + "/" + Payload.Length + "\r\n";

                var bytes = Encoding.ASCII.GetBytes(response + "\r\n");
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                for (int offset = start; offset < Payload.Length; offset += 8192)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    int count = Math.Min(8192, Payload.Length - offset);
                    await stream.WriteAsync(Payload, offset, count).ConfigureAwait(false);
                    if (path.StartsWith("/slow"))
                        await Task.Delay(15, cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        listener.Stop();
        foreach (var client in clients.Keys)
            client.Close();

        try
        {
            accept.GetAwaiter().GetResult();
            Task.WaitAll(new List<Task>(clients.Values).ToArray(), 2000);
        }
        catch (AggregateException) { }

        cancellation.Dispose();
    }
}
