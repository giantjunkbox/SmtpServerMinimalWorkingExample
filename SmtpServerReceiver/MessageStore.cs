using System.Threading;
using System.Threading.Tasks;
using SmtpServer;
using SmtpServer.Protocol;

namespace SmtpServerReceiver
{
    public class MessageStore : SmtpServer.Storage.MessageStore
    {
        public static int ReceivedMessages;

        public override Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ReceivedMessages);

            return Task.FromResult(SmtpResponse.Ok);
        }
    }
}
