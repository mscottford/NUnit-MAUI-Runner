using CommunityToolkit.Mvvm.Messaging;
using NUnit.Runner.Messages;
using NUnit.Runner.Helpers;

namespace NUnit.Runner.Services {
    class TcpWriterProcessor : TestResultProcessor {
        public TcpWriterProcessor(TestOptions options)
            : base(options) { }

        public override async Task Process(ResultSummary result) {
            if (Options.TcpWriterParameters != null) {
                try {
                    if (Options.TcpWriterParameters.UseTunnelToDevice)
                        await WriteResultToTunel(result);
                    else
                        await WriteResult(result);
                }
                catch (Exception exception) {
                    string message = $"Fatal error while trying to send xml result by TCP to {Options.TcpWriterParameters}\n\n{exception.Message}\n\nIs your server running?";
                    WeakReferenceMessenger.Default.Send(new ErrorMessage(message));
                }
            }

            if (Successor != null) {
                await Successor.Process(result);
            }
        }

        private async Task WriteResult(ResultSummary testResult) {
            using (var tcpWriter = new TcpWriter(Options.TcpWriterParameters)) {
                await tcpWriter.Connect().ConfigureAwait(false);
                tcpWriter.Write(ResultSummary.ToXml(testResult));
            }
        }

        private async Task WriteResultToTunel(ResultSummary testResult)
        {
            using (var tcpWriter = new TcpTunnelWriter(Options.TcpWriterParameters))
            {
                await tcpWriter.Connect().ConfigureAwait(false);
                tcpWriter.Write(ResultSummary.ToXml(testResult));
            }
        }
    }
}