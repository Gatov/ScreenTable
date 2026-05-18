using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenMap.Logic;

public class ScreenMapWebServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<Size, Bitmap> _renderSnapshot;
    private CancellationTokenSource _cts;
    private Task _listenerTask;

    private static readonly Size SnapshotSize = new Size(1280, 720);

    private const string HtmlPage = """
        <!DOCTYPE html>
        <html>
        <head>
          <title>ScreenMap – Players View</title>
          <style>
            * { margin: 0; padding: 0; box-sizing: border-box }
            body { background: #000; display: flex; justify-content: center;
                   align-items: center; min-height: 100vh }
            img { max-width: 100vw; max-height: 100vh; object-fit: contain }
          </style>
        </head>
        <body>
          <img id="map" src="/ScreenMapView/image.png" />
          <script>
            const img = document.getElementById('map');
            setInterval(() => { img.src = '/ScreenMapView/image.png?t=' + Date.now(); }, 1000);
          </script>
        </body>
        </html>
        """;

    public ScreenMapWebServer(Func<Size, Bitmap> renderSnapshot)
    {
        _renderSnapshot = renderSnapshot;
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost/ScreenMapView/");
    }

    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* listener closed or transient error */ }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "";
        if (path.EndsWith("/image.png", StringComparison.OrdinalIgnoreCase))
            ServeImage(context);
        else
            ServeHtml(context);
    }

    private void ServeImage(HttpListenerContext context)
    {
        try
        {
            using var bitmap = _renderSnapshot(SnapshotSize);
            if (bitmap == null)
            {
                context.Response.StatusCode = 503;
                context.Response.Close();
                return;
            }
            context.Response.ContentType = "image/png";
            context.Response.Headers["Cache-Control"] = "no-cache, no-store";
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            context.Response.ContentLength64 = ms.Length;
            ms.Position = 0;
            ms.CopyTo(context.Response.OutputStream);
        }
        catch { context.Response.StatusCode = 500; }
        finally { context.Response.Close(); }
    }

    private void ServeHtml(HttpListenerContext context)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(HtmlPage);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
        }
        finally { context.Response.Close(); }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listener.Close(); } catch { }
    }
}
