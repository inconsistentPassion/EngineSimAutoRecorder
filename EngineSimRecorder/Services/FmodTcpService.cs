using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EngineSimRecorder.Services;

/// <summary>
/// Connects to FMOD Studio via TCP (127.0.0.1:3663) and executes ES3 JavaScript.
/// </summary>
public sealed class FmodTcpService : IDisposable
{
    private const string Host = "127.0.0.1";
    private const int Port = 3663;
    private const int ReadTimeoutMs = 5000;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _disposed;

    public bool IsConnected => _client?.Connected == true && !_disposed;

    /// <summary>True if the most recent <see cref="Execute"/> response contained an error keyword.</summary>
    public bool LastResponseHadError { get; private set; }

    /// <summary>
    /// Opens a TCP connection to FMOD Studio on 127.0.0.1:3663.
    /// Throws <see cref="InvalidOperationException"/> if FMOD is not running.
    /// </summary>
    public void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _client?.Dispose();
        _client = new TcpClient();

        try
        {
            _client.Connect(Host, Port);
        }
        catch (SocketException ex)
        {
            _client.Dispose();
            _client = null;
            throw new InvalidOperationException(
                $"Cannot connect to FMOD Studio on {Host}:{Port}. " +
                "Make sure FMOD Studio 1.08 is running.", ex);
        }

        _stream = _client.GetStream();
        _stream.ReadTimeout = ReadTimeoutMs;

        // Drain the early greeting/banner from FMOD: "log(): Connected to FMOD Studio on 127.0.0.1:3663."
        // We do this by checking if data is available immediately.
        try
        {
            if (_stream.DataAvailable || WaitWithTimeout(() => _stream.DataAvailable, 500))
            {
                var buffer = new byte[1024];
                _stream.Read(buffer, 0, buffer.Length);
            }
        }
        catch { /* Ignore drain errors */ }
    }

    private static bool WaitWithTimeout(Func<bool> condition, int ms)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }
        return false;
    }

    /// <summary>
    /// Sends a JS command to FMOD and returns the raw response string.
    /// Throws <see cref="FmodResponseException"/> when FMOD reports an error.
    /// </summary>
    public string Execute(string jsCode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsConnected)
            throw new InvalidOperationException("Not connected. Call Connect() first.");

        // Clear any leftover data in stream
        while (_stream!.DataAvailable) _stream.ReadByte();

        // Send command terminated by newline
        byte[] payload = Encoding.UTF8.GetBytes(jsCode + "\n");
        _stream!.Write(payload, 0, payload.Length);

        // Read response until newline or timeout
        // FMOD often sends multiple log(): lines before an out(): line.
        var sb = new StringBuilder();
        var buffer = new byte[4096];
        string result = string.Empty;

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < ReadTimeoutMs)
        {
            if (_stream.DataAvailable)
            {
                int n = _stream.Read(buffer, 0, buffer.Length);
                if (n > 0)
                {
                    string chunk = Encoding.UTF8.GetString(buffer, 0, n);
                    sb.Append(chunk);

                    // FMOD typically terminates the final "out():" response with a newline.
                    // We look for any line starting with out():
                    string current = sb.ToString();
                    if (current.Contains("\nout():") || current.StartsWith("out():"))
                    {
                        // We think we have the final answer. Wait a tiny bit more for the rest of the message.
                        Thread.Sleep(50);
                        if (_stream.DataAvailable) continue; // Keep reading if more is coming

                        result = current.Trim();
                        break;
                    }
                }
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        if (string.IsNullOrEmpty(result))
            result = sb.ToString().Trim();

        if (string.IsNullOrEmpty(result))
            throw new TimeoutException($"FMOD did not respond within {ReadTimeoutMs / 1000} seconds.");

        LastResponseHadError = result.Contains("Error:", StringComparison.OrdinalIgnoreCase) ||
                               result.Contains("err():", StringComparison.OrdinalIgnoreCase) ||
                               result.Contains("TypeError:", StringComparison.OrdinalIgnoreCase) ||
                               result.Contains("ReferenceError:", StringComparison.OrdinalIgnoreCase) ||
                               result.Contains("Exception:", StringComparison.OrdinalIgnoreCase);

        return result;
    }

    /// <summary>Async wrapper for <see cref="Execute"/> — runs on a thread-pool thread.</summary>
    public Task<string> ExecuteAsync(string jsCode, CancellationToken ct = default)
        => Task.Run(() => Execute(jsCode), ct);

    public void Disconnect()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}


