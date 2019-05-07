using System.Threading;
using System.Threading.Tasks;
using SmtpServer.IO;

namespace SmtpServer.Protocol
{
    public sealed class StartTlsCommand : SmtpCommand
    {
        public const string Command = "STARTTLS";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="options">The server options.</param>
        internal StartTlsCommand(ISmtpServerOptions options) : base(options) { }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns true if the command executed successfully such that the transition to the next state should occurr, false 
        /// if the current state is to be maintained.</returns>
        internal override bool ExecuteAsync(SmtpSessionContext context)
        {
            context.NetworkClient.ReplyAsync(SmtpResponse.ServiceReady);
            context.NetworkClient.UpgradeAsync(Options.ServerCertificate, Options.SupportedSslProtocols);

            return true;
        }
    }
}