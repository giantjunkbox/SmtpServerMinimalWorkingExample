using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Authentication;
using SmtpServer.IO;
using SmtpServer.Storage;

namespace SmtpServer.Protocol
{
    public class AuthCommand : SmtpCommand
    {
        public const string Command = "AUTH";

        string _user;
        string _password;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="options">The server options.</param>
        /// <param name="method">The authentication method.</param>
        /// <param name="parameter">The authentication parameter.</param>
        internal AuthCommand(ISmtpServerOptions options, AuthenticationMethod method, string parameter) : base(options)
        {
            Method = method;
            Parameter = parameter;
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
            context.IsAuthenticated = false;

            switch (Method)
            {
                case AuthenticationMethod.Plain:
                    if (TryPlainAsync(context) == false)
                    {
                        context.NetworkClient.ReplyAsync(SmtpResponse.AuthenticationFailed);
                        return false;
                    }
                    break;

                case AuthenticationMethod.Login:
                    if (TryLoginAsync(context) == false)
                    {
                        context.NetworkClient.ReplyAsync(SmtpResponse.AuthenticationFailed);
                        return false;
                    }
                    break;
            }

            using (var container = new DisposableContainer<IUserAuthenticator>(Options.UserAuthenticatorFactory.CreateInstance(context)))
            {
                if (container.Instance.AuthenticateAsync(context, _user, _password) == false)
                {
                    context.NetworkClient.ReplyAsync(SmtpResponse.AuthenticationFailed);
                    return false;
                }
            }

            context.NetworkClient.ReplyAsync(SmtpResponse.AuthenticationSuccessful);

            context.IsAuthenticated = true;
            context.RaiseSessionAuthenticated();

            return true;
        }

        /// <summary>
        /// Attempt a PLAIN login sequence.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>true if the PLAIN login sequence worked, false if not.</returns>
        bool TryPlainAsync(SmtpSessionContext context)
        {
            var authentication = Parameter;

            if (String.IsNullOrWhiteSpace(authentication))
            { 
                context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.ContinueWithAuth, " "));

                authentication = context.NetworkClient.ReadLineAsync(Encoding.ASCII);
            }

            if (TryExtractFromBase64(authentication) == false)
            {
                context.NetworkClient.ReplyAsync(SmtpResponse.AuthenticationFailed);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempt to extract the user name and password combination from a single line base64 encoded string.
        /// </summary>
        /// <param name="base64">The base64 encoded string to extract the user name and password from.</param>
        /// <returns>true if the user name and password were extracted from the base64 encoded string, false if not.</returns>
        bool TryExtractFromBase64(string base64)
        {
            var match = Regex.Match(Encoding.UTF8.GetString(Convert.FromBase64String(base64)), "\x0000(?<user>.*)\x0000(?<password>.*)");

            if (match.Success == false)
            {
                return false;
            }

            _user = match.Groups["user"].Value;
            _password = match.Groups["password"].Value;

            return true;
        }

        /// <summary>
        /// Attempt a LOGIN login sequence.
        /// </summary>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>true if the LOGIN login sequence worked, false if not.</returns>
        bool TryLoginAsync(SmtpSessionContext context)
        {
            if (String.IsNullOrWhiteSpace(Parameter) == false)
            {
                _user = Encoding.UTF8.GetString(Convert.FromBase64String(Parameter));
            }
            else
            {
                context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.ContinueWithAuth, "VXNlcm5hbWU6"));

                _user = ReadBase64EncodedLineAsync(context.NetworkClient);
            }

            context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.ContinueWithAuth, "UGFzc3dvcmQ6"));

            _password = ReadBase64EncodedLineAsync(context.NetworkClient);

            return true;
        }

        /// <summary>
        /// Read a Base64 encoded line.
        /// </summary>
        /// <param name="client">The client to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The decoded Base64 string.</returns>
        string ReadBase64EncodedLineAsync(INetworkClient client)
        {
            var text = client.ReadLineAsync(Encoding.ASCII);

            return text == null 
                ? String.Empty 
                : Encoding.UTF8.GetString(Convert.FromBase64String(text));
        }

        /// <summary>
        /// The authentication method.
        /// </summary>
        public AuthenticationMethod Method { get; }

        /// <summary>
        /// The athentication parameter.
        /// </summary>
        public string Parameter { get; }
    }
}