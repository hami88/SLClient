using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SLClient;

/// <summary>
/// A simple Telnet client for connecting to and communicating with Telnet servers.
/// </summary>
public class TelnetClient
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    /// <summary>
    /// Gets whether the client is currently connected to the server.
    /// </summary>
    public bool IsConnected => _tcpClient?.Connected ?? false;

    /// <summary>
    /// Asynchronously connects to the specified Telnet host and port.
    /// </summary>
    /// <param name="host">The hostname or IP address of the server.</param>
    /// <param name="port">The port number to connect to.</param>
    /// <returns>True if the connection was successful; otherwise, false.</returns>
    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            _stream = _tcpClient.GetStream();

            _reader = new StreamReader(_stream, Encoding.ASCII);
            _writer = new StreamWriter(_stream, Encoding.ASCII)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            return true;
        }
        catch
        {
            Disconnect(); // Clean up in case of partial connection
            return false;
        }
    }

    /// <summary>
    /// Asynchronously receives a single line of text from the Telnet server.
    /// </summary>
    /// <returns>The received line, or null if an error occurred or the connection is closed.</returns>
    public async Task<string?> ReceiveAsync()
    {
        if (_reader == null)
            return null;

        try
        {
            return await _reader.ReadLineAsync();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Asynchronously sends a command line to the Telnet server.
    /// </summary>
    /// <param name="command">The command to send.</param>
    public async Task SendAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || _writer == null)
            return;

        try
        {
            await _writer.WriteLineAsync(command);
        }
        catch
        {
            // Ignore send errors
        }
    }

    /// <summary>
    /// Disconnects from the Telnet server and releases all resources.
    /// </summary>
    public void Disconnect()
    {
        try
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _stream?.Dispose();
            _tcpClient?.Close();
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            _reader = null;
            _writer = null;
            _stream = null;
            _tcpClient = null;
        }
    }
}
