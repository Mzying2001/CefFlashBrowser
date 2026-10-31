using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class FaviconHttpFixture : IDisposable
{
    private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    private readonly TaskCompletionSource<bool> slowResponse = new TaskCompletionSource<bool>();
    private readonly Task acceptTask;
    private int slowRequests;
    public string BaseUrl { get; private set; }
    public int SlowRequests { get { return Volatile.Read(ref slowRequests); } }

    public FaviconHttpFixture()
    {
        listener.Start();
        BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        acceptTask = AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                Task ignored = Task.Run(() => RespondAsync(client));
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { if (!cancellation.IsCancellationRequested) throw; }
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
                string request = await reader.ReadLineAsync().ConfigureAwait(false);
                if (request == null)
                    return;
                string path = request.Split(' ')[1].Split('?')[0];
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync().ConfigureAwait(false))) { }
                string status = "200 OK";
                string type = "text/html";
                string extra = "";
                byte[] body;
                if (path == "/redirect")
                {
                    status = "302 Found";
                    extra = "Location: /b\r\n";
                    body = new byte[0];
                }
                else if (path == "/a" || path == "/b")
                {
                    body = Encoding.UTF8.GetBytes("<title>Favicon " + path + "</title><link rel='icon' href='" + path + "-icon.png'><p>fixture</p>");
                }
                else if (path == "/a-icon.png" || path == "/b-icon.png")
                {
                    type = "image/png";
                    body = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/pkAAAAASUVORK5CYII=");
                }
                else
                {
                    if (path == "/slow-bad.png")
                    {
                        Interlocked.Increment(ref slowRequests);
                        await slowResponse.Task.ConfigureAwait(false);
                    }
                    status = path == "/missing.png" ? "404 Not Found" : "200 OK";
                    type = "image/png";
                    body = Encoding.UTF8.GetBytes("invalid image bytes");
                }
                var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + type
                    + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\n" + extra + "\r\n");
                await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
                await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }
    }

    public void ReleaseSlowResponse()
    {
        slowResponse.TrySetResult(true);
    }

    public void Dispose()
    {
        cancellation.Cancel();
        ReleaseSlowResponse();
        listener.Stop();
        acceptTask.GetAwaiter().GetResult();
        cancellation.Dispose();
    }
}
