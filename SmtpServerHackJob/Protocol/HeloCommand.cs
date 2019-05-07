﻿using System.Threading;
using System.Threading.Tasks;
using SmtpServer.IO;

namespace SmtpServer.Protocol
{
    public sealed class HeloCommand : SmtpCommand
    {
        public const string Command = "HELO";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="options">The server options.</param>
        /// <param name="domainOrAddress">The domain name.</param>
        internal HeloCommand(ISmtpServerOptions options, string domainOrAddress) : base(options)
        {
            DomainOrAddress = domainOrAddress;
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
            var response = new SmtpResponse(SmtpReplyCode.Ok, $"Hello {DomainOrAddress}, haven't we met before?");

            context.NetworkClient.ReplyAsync(response);

            return true;
        }

        /// <summary>
        /// Gets the domain name.
        /// </summary>
        public string DomainOrAddress { get; }
    }
}