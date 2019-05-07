using System;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.IO;
using SmtpServer.Mail;
using SmtpServer.Storage;

namespace SmtpServer.Protocol
{
    public sealed class DataCommand : SmtpCommand
    {
        public const string Command = "DATA";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="options">The server options.</param>
        internal DataCommand(ISmtpServerOptions options) : base(options) { }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Returns true if the command executed successfully such that the transition to the next state should occurr, false 
        /// if the current state is to be maintained.</returns>
        internal override bool ExecuteAsync(SmtpSessionContext context)
        {
            if (context.Transaction.To.Count == 0)
            {
                context.NetworkClient.ReplyAsync(SmtpResponse.NoValidRecipientsGiven);
                return false;
            }

            context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.StartMailInput, "end with <CRLF>.<CRLF>"));

            context.Transaction.Message = ReadMessageAsync(context);

            try
            {
                using (var container = new DisposableContainer<IMessageStore>(Options.MessageStoreFactory.CreateInstance(context)))
                {
                    var response = container.Instance.SaveAsync(context, context.Transaction);

                    context.NetworkClient.ReplyAsync(response);
                }
            }
            catch (Exception)
            {
                context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.TransactionFailed));
            }

            return true;
        }

        /// <summary>
        /// Receive the message content.
        /// </summary>
        /// <param name="context">The SMTP session context to receive the message within.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the operation.</returns>
        IMessage ReadMessageAsync(SmtpSessionContext context)
        {
            var serializer = new MessageSerializerFactory().CreateInstance();

            return serializer.DeserializeAsync(context.NetworkClient);
        }
    }
}