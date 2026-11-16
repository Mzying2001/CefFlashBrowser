using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class UserScriptHttpFixture : IDisposable
{
    private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly ConcurrentDictionary<string, int> requests = new ConcurrentDictionary<string, int>();
    private readonly ConcurrentDictionary<string, string> cookies = new ConcurrentDictionary<string, string>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> gates = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
    private readonly ConcurrentDictionary<TcpClient, Task> clients = new ConcurrentDictionary<TcpClient, Task>();
    private readonly Task accept;

    public string BaseUrl { get; private set; }

    public string PeerUrl { get; set; }

    public bool Offline { get; set; }

    public byte[] Payload
    {
        get
        {
            return new byte[] { 0, 1, 2, 3, 127, 128, 254, 255, 42, 17, 9, 0 };
        }
    }

    public UserScriptHttpFixture()
    {
        listener.Start();
        BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        accept = AcceptAsync();
    }

    public int RequestsFor(string path)
    {
        int count;
        return requests.TryGetValue(path, out count) ? count : 0;
    }

    public string CookieFor(string path)
    {
        string value;
        return cookies.TryGetValue(path, out value) ? value : "";
    }

    public void Release(string path)
    {
        gates.GetOrAdd(path, key => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(true);
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

                Task ignored = work.ContinueWith(completed =>
                {
                    Task removed;

                    clients.TryRemove(client, out removed);
                }, TaskScheduler.Default);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (!cancellation.IsCancellationRequested)
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
                var first = await reader.ReadLineAsync().ConfigureAwait(false);

                if (first == null)
                    return;

                var path = first.Split(' ')[1].Split('?')[0];
                string cookie = "";
                string line;

                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                    if (line.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                        cookie = line.Substring(7).Trim();

                requests.AddOrUpdate(path, 1, (key, value) => value + 1);
                cookies[path] = cookie;

                var status = "200 OK";
                var type = "text/html; charset=utf-8";
                var extra = "";
                byte[] body;

                if (path == "/download" || path.StartsWith("/slow-", StringComparison.Ordinal))
                {
                    body = Payload;
                    type = "application/octet-stream";
                }
                else if (path == "/failed")
                {
                    status = "500 Internal Server Error";
                    body = Encoding.UTF8.GetBytes("failed");
                }
                else if (path == "/login")
                {
                    extra = "Set-Cookie: session=fixture; Path=/\r\n";
                    body = Encoding.UTF8.GetBytes("<title>Login fixture</title>");
                }
                else if (path == "/echo" || path == "/anonymous")
                {
                    type = "text/plain";
                    body = Encoding.UTF8.GetBytes(cookie);
                }
                else if (path == "/blocked.js")
                {
                    type = "application/javascript";
                    body = Encoding.UTF8.GetBytes("window.blockedResourceRan = true;");
                }
                else if (path == "/redirect")
                {
                    status = "302 Found";
                    extra = "Location: " + (PeerUrl ?? BaseUrl) + "/echo\r\n";
                    body = new byte[0];
                }
                else if (path == "/dependency.js")
                {
                    type = "application/javascript";

                    if (Offline)
                        status = "503 Service Unavailable";

                    body = Encoding.UTF8.GetBytes("var preparedDependency = 'dependency-ready';");
                }
                else if (path == "/resource.txt")
                {
                    type = "text/plain; charset=utf-8";

                    if (Offline)
                        status = "503 Service Unavailable";

                    body = Encoding.UTF8.GetBytes("resource-ready");
                }
                else if (path == "/blocked.png")
                {
                    type = "image/png";
                    body = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/pkAAAAASUVORK5CYII=");
                }
                else if (path == "/frames")
                    body = Encoding.UTF8.GetBytes("<title>Frame fixture</title><iframe src='/frame'></iframe><iframe src='" + (PeerUrl ?? BaseUrl) + "/frame'></iframe>");
                else if (path == "/frame")
                    body = Encoding.UTF8.GetBytes("<title>Child fixture</title><p>child</p>");
                else
                    body = Encoding.UTF8.GetBytes("<title>Userscript fixture</title><script>window.inlineRan=true;</script><script src='/blocked.js'></script><p>fixture</p>");

                var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + type
                    + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\n" + extra + "\r\n");

                await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);

                if (path.StartsWith("/slow-", StringComparison.Ordinal))
                {
                    await stream.WriteAsync(body, 0, 3).ConfigureAwait(false);

                    var gate = gates.GetOrAdd(path, key => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

                    await gate.Task.ConfigureAwait(false);
                    await stream.WriteAsync(body, 3, body.Length - 3).ConfigureAwait(false);
                }
                else
                    await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        listener.Stop();

        foreach (var gate in gates.Values)
            gate.TrySetCanceled();

        foreach (var client in clients.Keys)
            client.Close();

        accept.GetAwaiter().GetResult();
        cancellation.Dispose();
    }
}
