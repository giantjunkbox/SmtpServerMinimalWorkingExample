using System.Threading;
using SmtpServer;
using SmtpServer.Protocol;

namespace SmtpServerHackJobReceiver
{
    public class MessageStore : SmtpServer.Storage.MessageStore
    {
        private static int _receivedMessages;

        public static int ReceivedMessages
        {
            get => _receivedMessages;
        }

        public override SmtpResponse SaveAsync(ISessionContext context, IMessageTransaction transaction)
        {
            Interlocked.Increment(ref _receivedMessages);

            return SmtpResponse.Ok;
        }
    }
}
