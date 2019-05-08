using System.Threading;
using SmtpServer;
using SmtpServer.Protocol;

namespace SmtpServerHackJobReceiver
{
    public class MessageStore : SmtpServer.Storage.MessageStore
    {
        public static int ReceivedMessages;

 
        public override SmtpResponse SaveAsync(ISessionContext context, IMessageTransaction transaction)
        {
            Interlocked.Increment(ref ReceivedMessages);

            return SmtpResponse.Ok;
        }
    }
}
