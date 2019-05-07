using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Protocol;
using System.Reflection;
using SmtpServer.IO;
using SmtpServer.Text;

namespace SmtpServer
{
    internal sealed class SmtpSession
    {
        readonly SmtpStateMachine _stateMachine;
        readonly SmtpSessionContext _context;
        TaskCompletionSource<bool> _taskCompletionSource;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">The session context.</param>
        internal SmtpSession(SmtpSessionContext context)
        {
            _context = context;
            _stateMachine = new SmtpStateMachine(_context);
        }

        /// <summary>
        /// Executes the session.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void Run()
        {
            RunAsync();
        }

        /// <summary>
        /// Handles the SMTP session.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which performs the operation.</returns>
        void RunAsync()
        {
            OutputGreetingAsync();

            ExecuteAsync(_context);
        }

        /// <summary>
        /// Execute the command handler against the specified session context.
        /// </summary>
        /// <param name="context">The session context to execute the command handler against.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the execution.</returns>
        void ExecuteAsync(SmtpSessionContext context)
        {
            var retries = _context.ServerOptions.MaxRetryCount;

            while (retries-- > 0 && context.IsQuitRequested == false)
            {
                var text = ReadCommandInputAsync(context);

                if (text == null)
                {
                    return;
                }

                if (TryMake(context, text, out var command, out var response))
                {
                    try
                    {
                        if (ExecuteAsync(command, context))
                        {
                            _stateMachine.Transition(context);
                        }

                        retries = _context.ServerOptions.MaxRetryCount;

                        continue;
                    }
                    catch (SmtpResponseException responseException)
                    {
                        context.IsQuitRequested = responseException.IsQuitRequested;

                        response = responseException.Response;
                    }
                    catch (OperationCanceledException)
                    {
                        context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.ServiceClosingTransmissionChannel, "The session has be cancelled."));
                        return;
                    }
                }

                context.NetworkClient.ReplyAsync(CreateErrorResponse(response, retries));
            }
        }

        /// <summary>
        /// Read the command input.
        /// </summary>
        /// <param name="context">The session context to execute the command handler against.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The input that was received from the client.</returns>
        IReadOnlyList<ArraySegment<byte>> ReadCommandInputAsync(SmtpSessionContext context)
        {
            var timeout = new CancellationTokenSource(_context.ServerOptions.CommandWaitTimeout);

            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
           
            try
            {
                return context.NetworkClient.ReadLineAsync();
            }
            catch (OperationCanceledException)
            {
                if (timeout.IsCancellationRequested)
                {
                    context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.ServiceClosingTransmissionChannel, "Timeout whilst waiting for input."));
                    return null;
                }

                context.NetworkClient.ReplyAsync(new SmtpResponse(SmtpReplyCode.ServiceClosingTransmissionChannel, "The session has be cancelled."));
                return null;
            }
            finally
            {
                timeout.Dispose();
                cancellationTokenSource.Dispose();
            }
        }

        /// <summary>
        /// Create an error response.
        /// </summary>
        /// <param name="response">The original response to wrap with the error message information.</param>
        /// <param name="retries">The number of retries remaining before the session is terminated.</param>
        /// <returns>The response that wraps the original response with the additional error information.</returns>
        static SmtpResponse CreateErrorResponse(SmtpResponse response, int retries)
        {
            return new SmtpResponse(response.ReplyCode, $"{response.Message}, {retries} retry(ies) remaining.");
        }

        /// <summary>
        /// Advances the enumerator to the next command in the stream.
        /// </summary>
        /// <param name="context">The session context to use when making session based transitions.</param>
        /// <param name="segments">The list of array segments to read the command from.</param>
        /// <param name="command">The command that was found.</param>
        /// <param name="errorResponse">The error response that indicates why a command could not be accepted.</param>
        /// <returns>true if a valid command was found, false if not.</returns>
        bool TryMake(SmtpSessionContext context, IReadOnlyList<ArraySegment<byte>> segments, out SmtpCommand command, out SmtpResponse errorResponse)
        {
            var tokenEnumerator = new TokenEnumerator(new ByteArrayTokenReader(segments));

            return _stateMachine.TryMake(context, tokenEnumerator, out command, out errorResponse);
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="context">The execution context to operate on.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which asynchronously performs the execution.</returns>
        bool ExecuteAsync(SmtpCommand command, SmtpSessionContext context)
        {
            context.RaiseCommandExecuting(command);

            return command.ExecuteAsync(context);
        }

        /// <summary>
        /// Output the greeting.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task which performs the operation.</returns>
        void OutputGreetingAsync()
        {
            var version = typeof(SmtpSession).GetTypeInfo().Assembly.GetName().Version;

            _context.NetworkClient.WriteLineAsync($"220 {_context.ServerOptions.ServerName} v{version} ESMTP ready");
            _context.NetworkClient.FlushAsync();
        }
        
        /// <summary>
        /// Returns the completion task.
        /// </summary>
        internal Task<bool> Task => _taskCompletionSource.Task;
    }
}
