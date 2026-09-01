using CommunityToolkit.Mvvm.Messaging;
using NUnit.Runner.Messages;
using System.Diagnostics;
using System.Net;

#if NETFX_CORE
using Windows.Networking;
using Windows.Networking.Sockets;
#else
using System.Net.Sockets;
#endif

namespace NUnit.Runner.Services
{
    class TcpTunnelWriter : TextWriter
    {
#if NETFX_CORE
        StreamSocket _socket;
#endif
        StreamWriter _writer;
        readonly TcpWriterInfo _info;
        TcpClient _client;
        TcpListener _server;
        public TcpTunnelWriter(TcpWriterInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            _info = info;
        }

        public async Task Connect()
        {
#if NETFX_CORE
#else
            try
            {
                _server = new TcpListener(IPAddress.Any, _info.Port);
                _server.Server.ReceiveTimeout = 10000;
                _server.Start();

                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (!_server.Pending())
                {
                    Thread.Sleep(300);
                    if (sw.ElapsedMilliseconds > _info.Timeout * 1000)
                        throw new TimeoutException();
                }

                _client = _server.AcceptTcpClient();
                // Block until we have the ping from the client side
                byte[] buffer = new byte[16384];
                var stream = _client.GetStream();
                while ((_ = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    var message = System.Text.Encoding.UTF8.GetString(buffer);
                    if (message.Contains("ping"))
                        break;
                }
                _writer = new StreamWriter(_client.GetStream());
                _writer.Write("pong");
                _writer.Flush();
            }
            catch (TimeoutException)
            {
                WeakReferenceMessenger.Default.Send(new ErrorMessage($"Timeout connecting to {_info} after {_info.Timeout} seconds.\n\nIs your server running?"));
            }
            catch (Exception ex)
            {
                WeakReferenceMessenger.Default.Send(new ErrorMessage($"Cannot connect to {_info}: {ex.Message}"));
            }
#endif
        }

        public override void Write(char value)
        {
            _writer?.Write(value);
        }

        public override void Write(string value)
        {
            _writer?.Write(value);
        }

        public override void WriteLine(string value)
        {
            _writer?.WriteLine(value);
            _writer?.Flush();
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        protected override void Dispose(bool disposing)
        {
#if !NETFX_CORE
            string endTag = new string('\n', 2048);
            _writer?.Write(endTag);
            _writer?.Flush();
            _writer?.Dispose();
#endif
        }
    }
}