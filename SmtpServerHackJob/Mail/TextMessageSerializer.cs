using System.Threading;
using System.Threading.Tasks;
using SmtpServer.IO;

namespace SmtpServer.Mail
{
    internal sealed class TextMessageSerializer : IMessageSerializer
    {
        /// <summary>
        /// Deserialize a message from the stream.
        /// </summary>
        /// <param name="networkClient">The network client to deserialize the message from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The message that was deserialized.</returns>
        public IMessage DeserializeAsync(INetworkClient networkClient)
        {
            var stream = new ByteArrayStream(networkClient.ReadDotBlockAsync());

            return new TextMessage(stream);
        }
    }
}