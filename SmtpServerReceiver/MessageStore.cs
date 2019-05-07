using System.Threading;
using System.Threading.Tasks;
using SmtpServer;
using SmtpServer.Protocol;

namespace SmtpServerReceiver
{
    public class MessageStore : SmtpServer.Storage.MessageStore
    {
        private static int _receivedMessages;

        public static int ReceivedMessages
        {
            get => _receivedMessages;
        }

        public override Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _receivedMessages);

            return Task.FromResult(SmtpResponse.Ok);
        }
    }
}
