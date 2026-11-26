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
    private readonly ConcurrentDictionary<string, string> referrers = new ConcurrentDictionary<string, string>();
    private readonly ConcurrentDictionary<string, string> targets = new ConcurrentDictionary<string, string>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> gates = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
    private readonly ConcurrentDictionary<TcpClient, Task> clients = new ConcurrentDictionary<TcpClient, Task>();
    private readonly Task accept;

    public string BaseUrl { get; private set; }

    public string PeerUrl { get; set; }

    public string ScriptSource { get; set; }

    public string PolyfillProbeHtml { get; set; }

    public bool Offline { get; set; }

    private static byte[] SwfPayload()
    {
        // Uncompressed SWF 6 with a 20 by 20 pixel stage and one empty frame.
        return new byte[] {
            0x46, 0x57, 0x53, 0x06, 0x16, 0x00, 0x00, 0x00, 0x50, 0x00, 0xc8,
            0x00, 0x0c, 0x80, 0x00, 0x0c, 0x01, 0x00, 0x40, 0x00, 0x00, 0x00
        };
    }

    private static byte[] SwfWithRelativeLoad()
    {
        var emptySwf = SwfPayload();
        var arguments = Encoding.ASCII.GetBytes("relative-probe.swf\0_level1\0");
        using (var output = new MemoryStream())
        using (var writer = new BinaryWriter(output))
        {
            const int headerLength = 18;
            var actionLength = 3 + arguments.Length + 1;
            writer.Write(emptySwf, 0, 4);
            writer.Write(emptySwf.Length + 2 + actionLength);
            writer.Write(emptySwf, 8, headerLength - 8);
            writer.Write((ushort)((12 << 6) | actionLength)); // DoAction
            writer.Write((byte)0x83); // GetURL, to load the relative movie into level 1.
            writer.Write((ushort)arguments.Length);
            writer.Write(arguments);
            writer.Write((byte)0);
            writer.Write(emptySwf, headerLength, emptySwf.Length - headerLength);
            return output.ToArray();
        }
    }

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

    public string RefererFor(string path)
    {
        string value;
        return referrers.TryGetValue(path, out value) ? value : "";
    }

    public string TargetFor(string path)
    {
        string value;
        return targets.TryGetValue(path, out value) ? value : "";
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

                var target = first.Split(' ')[1];
                var path = target.Split('?')[0];
                string cookie = "";
                string referer = "";
                string line;

                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                    if (line.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
                        cookie = line.Substring(7).Trim();
                    else if (line.StartsWith("Referer:", StringComparison.OrdinalIgnoreCase))
                        referer = line.Substring(8).Trim();
                }

                cookies[path] = cookie;
                referrers[path] = referer;
                targets[path] = target;
                requests.AddOrUpdate(path, 1, (key, value) => value + 1);

                var status = "200 OK";
                var type = "text/html; charset=utf-8";
                var extra = "";
                byte[] body;

                if (path == "/manager.user.js" || path == "/slow-install.user.js")
                {
                    type = "application/javascript";
                    body = Encoding.UTF8.GetBytes(ScriptSource ?? string.Empty);
                }
                else if (path == "/download" || path.StartsWith("/slow-", StringComparison.Ordinal))
                {
                    body = Payload;
                    type = "application/octet-stream";
                }
                else if (path == "/swf-page")
                {
                    body = Encoding.UTF8.GetBytes("<title>SWF fixture</title><img src='/embedded.swf'>"
                        + "<iframe src='/swf-frame'></iframe><a href='/unused.swf'>Unused SWF</a>");
                }
                else if (path == "/swf-frame")
                {
                    body = Encoding.UTF8.GetBytes("<script>fetch('/frame.swf');</script>");
                }
                else if (path == "/swf-context-page")
                {
                    extra = "Set-Cookie: swf_context=yes; Path=/; HttpOnly\r\n"
                        + "Set-Cookie: swf_context_strict=yes; Path=/; HttpOnly; SameSite=Strict\r\n";
                    body = Encoding.UTF8.GetBytes("<title>SWF context fixture</title><base href='/swf-assets/'>"
                        + "<script>window.originalPage = true;</script>"
                        + "<object type='application/x-shockwave-flash' data='../movies/context.swf?token=a%2Fb' width='160' height='120'>"
                        + "<param name='FlashVars' value='level=7&amp;value=&lt;/script&gt;&quot;'>"
                        + "<param name='base' value='/swf-assets/'><param name='allowScriptAccess' value='always'></object>");
                }
                else if (path == "/swf-context-outer" || path == "/swf-context-middle")
                {
                    var child = path == "/swf-context-outer" ? PeerUrl + "/swf-context-middle" : "/swf-context-frame";
                    body = Encoding.UTF8.GetBytes("<title>SWF frame container</title><iframe src='" + child + "'></iframe>");
                }
                else if (path == "/swf-context-frame")
                {
                    extra = "Referrer-Policy: no-referrer\r\nSet-Cookie: swf_frame=yes; Path=/; HttpOnly; SameSite=Strict\r\n";
                    body = Encoding.UTF8.GetBytes("<title>SWF context frame</title>"
                        + "<embed type='application/x-shockwave-flash' src='/frame-context.swf' flashvars='frame=child' width='160' height='120'>");
                }
                else if (path == "/movies/context.swf")
                {
                    type = "application/x-shockwave-flash";
                    body = SwfWithRelativeLoad();
                }
                else if (path == "/swf-empty")
                {
                    body = Encoding.UTF8.GetBytes("<title>Empty SWF fixture</title>");
                }
                else if (path == "/swf-redirect")
                {
                    status = "302 Found";
                    extra = "Location: /swf-response\r\n";
                    body = new byte[0];
                }
                else if (path == "/swf-missing.swf")
                {
                    status = "404 Not Found";
                    body = new byte[0];
                }
                else if (path == "/swf-response" || path == "/swf-delayed" || path.EndsWith(".swf", StringComparison.OrdinalIgnoreCase))
                {
                    if (path == "/swf-delayed")
                    {
                        var gate = gates.GetOrAdd(path, key => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                        await gate.Task.ConfigureAwait(false);
                    }

                    type = "application/x-shockwave-flash";
                    body = SwfPayload();
                }
                else if (path == "/failed")
                {
                    status = "500 Internal Server Error";
                    body = Encoding.UTF8.GetBytes("failed");
                }
                else if (path == "/web-install")
                {
                    body = Encoding.UTF8.GetBytes("<title>Web install fixture</title>"
                        + "<a id='install' href='/redirect-install.USER.JS?channel=stable#link'>Install</a>"
                        + "<a id='new-tab-install' target='_blank' href='/redirect-install.USER.JS?channel=stable#new-tab'>Install in new tab</a>");
                }
                else if (path == "/web-install-background")
                {
                    body = Encoding.UTF8.GetBytes("<title>Background script fixture</title>"
                        + "<iframe src='/child-install.user.js'></iframe><script src='/resource-install.user.js'></script>");
                }
                else if (path == "/child-install.user.js" || path == "/invalid-install.user.js")
                {
                    body = Encoding.UTF8.GetBytes("<title>Plain web page</title>");
                }
                else if (path == "/resource-install.user.js" || path == "/normal.js")
                {
                    type = "application/javascript";
                    body = Encoding.UTF8.GetBytes("window.ordinaryResourceRan = true;");
                }
                else if (path == "/redirect-install.USER.JS")
                {
                    status = "302 Found";
                    extra = "Location: /manager.user.js\r\n";
                    body = new byte[0];
                }
                else if (path == "/missing-install.user.js")
                {
                    status = "404 Not Found";
                    body = new byte[0];
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
                else if (path == "/polyfill" || path == "/polyfill-frames")
                {
                    extra = "Content-Security-Policy: script-src 'nonce-cfb-polyfill-probe'; object-src 'none'; base-uri 'none'\r\n";
                    var html = PolyfillProbeHtml;
                    if (path == "/polyfill-frames")
                    {
                        html += "<iframe src='/polyfill'></iframe><iframe src='" + WebUtility.HtmlEncode(PeerUrl) + "/polyfill'></iframe>"
                            + "<iframe srcdoc=\"" + WebUtility.HtmlEncode(PolyfillProbeHtml) + "\"></iframe>";
                    }
                    body = Encoding.UTF8.GetBytes(html);
                }
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
