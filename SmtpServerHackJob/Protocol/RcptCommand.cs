using System;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.IO;
using SmtpServer.Mail;
using SmtpServer.Storage;

namespace SmtpServer.Protocol
{
    public sealed class RcptCommand : SmtpCommand
    {
        public const string Command = "RCPT";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="options">The server options.</param>
        /// <param name="address">The address.</param>
        internal RcptCommand(ISmtpServerOptions options, IMailbox address) : base(options)
        {
            Address = address;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns true if the command executed successfully such that the transition to the next state should occurr, false 
        /// if the current state is to be maintained.</returns>
        internal override bool ExecuteAsync(SmtpSessionContext context)
        {
            using (var container = new DisposableContainer<IMailboxFilter>(Options.MailboxFilterFactory.CreateInstance(context)))
            {
                switch (container.Instance.CanDeliverToAsync(context, Address, context.Transaction.From))
                {
                    case MailboxFilterResult.Yes:
                        context.Transaction.To.Add(Address);
                        context.NetworkClient.ReplyAsync(SmtpResponse.Ok);
                        return true;

                    case MailboxFilterResult.NoTemporarily:
                        context.NetworkClient.ReplyAsync(SmtpResponse.MailboxUnavailable);
                        return false;

                    case MailboxFilterResult.NoPermanently:
                        context.NetworkClient.ReplyAsync(SmtpResponse.MailboxNameNotAllowed);
                        return false;
                }
            }

            throw new NotSupportedException("The Acceptance state is not supported.");
        }

        /// <summary>
        /// Gets the address that the mail is to.
        /// </summary>
        public IMailbox Address { get; }
    }
}