using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenMap.Logic;

public sealed class CloudflareTunnel : IDisposable
{
    private static readonly Regex UrlPattern = new(
        @"https://[a-z0-9\-]+\.trycloudflare\.com",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private Process _process;

    public string Url { get; private set; }
    public bool IsRunning => _process != null && !_process.HasExited;

    public async Task<string> StartAsync(int localPort, TimeSpan timeout, CancellationToken ct = default)
    {
        if (IsRunning) return Url;

        var exe = Path.Combine(AppContext.BaseDirectory, "cloudflared.exe");
        if (!File.Exists(exe))
            throw new FileNotFoundException("cloudflared.exe was not found next to the application.", exe);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"tunnel --url http://localhost:{localPort} --http-host-header localhost:{localPort}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        void OnLine(object _, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            var m = UrlPattern.Match(e.Data);
            if (m.Success) tcs.TrySetResult(m.Value);
        }

        proc.OutputDataReceived += OnLine;
        proc.ErrorDataReceived += OnLine;
        proc.Exited += (_, _) =>
            tcs.TrySetException(new InvalidOperationException(
                $"cloudflared exited (code {proc.ExitCode}) before reporting a tunnel URL."));

        if (!proc.Start())
            throw new InvalidOperationException("Failed to start cloudflared.exe");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        _process = proc;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        using (cts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException("Timed out waiting for the trycloudflare.com URL."))))
        {
            try
            {
                Url = await tcs.Task.ConfigureAwait(false);
                return Url;
            }
            catch
            {
                Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        var proc = _process;
        _process = null;
        if (proc == null) return;
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(2000);
            }
        }
        catch { /* best-effort */ }
        proc.Dispose();
    }
}
