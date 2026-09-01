using CommunityToolkit.Mvvm.Messaging;
using NUnit.Runner.Messages;

#if NETFX_CORE
using Windows.Networking;
using Windows.Networking.Sockets;
#else
using System.Net.Sockets;
#endif

namespace NUnit.Runner.Services {
    class TcpWriter : TextWriter {
#if NETFX_CORE
        StreamSocket _socket;
#endif
        StreamWriter _writer;
        readonly TcpWriterInfo _info;

        public TcpWriter(TcpWriterInfo info) {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            _info = info;
        }

        public async Task Connect() {
#if NETFX_CORE
            try {
                _socket = new StreamSocket();
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(_info.Timeout));

                await _socket.ConnectAsync(new HostName(_info.Hostname), _info.Port.ToString()).AsTask();
                _writer = new StreamWriter(_socket.OutputStream.AsStreamForWrite());
            }
            catch (TaskCanceledException) {
                WeakReferenceMessenger.Default.Send(new ErrorMessage($"Timeout connecting to {_info} after {_info.Timeout} seconds.\n\nIs your server running?"));
            }
            catch (Exception ex) {
                WeakReferenceMessenger.Default.Send(new ErrorMessage(ex.Message));
            }
#else
            try {
                TcpClient client = new TcpClient();
                Task connect = client.ConnectAsync(_info.Hostname, _info.Port);
                Task timeout = Task.Delay(TimeSpan.FromSeconds(_info.Timeout));
                if(await Task.WhenAny(connect, timeout) == timeout) {
                    throw new TimeoutException();
                }
                NetworkStream stream = client.GetStream();
                _writer = new StreamWriter(stream);
            }
            catch (TimeoutException) {
                WeakReferenceMessenger.Default.Send(new ErrorMessage($"Timeout connecting to {_info} after {_info.Timeout} seconds.\n\nIs your server running?"));
            }
            catch (Exception ex) {
                WeakReferenceMessenger.Default.Send(new ErrorMessage($"Cannot connect to {_info}: {ex.Message}"));
            }
#endif
        }

        public override void Write(char value) {
            _writer?.Write(value);
        }

        public override void Write(string value) {
            _writer?.Write(value);
        }

        public override void WriteLine(string value) {
            _writer?.WriteLine(value);
            _writer?.Flush();
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        protected override void Dispose(bool disposing) {
            _writer?.Dispose();
#if NETFX_CORE
            _socket?.Dispose();
#endif
        }
    }
}