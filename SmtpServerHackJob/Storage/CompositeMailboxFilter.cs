using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Mail;

namespace SmtpServer.Storage
{
    internal sealed class CompositeMailboxFilter : IMailboxFilter
    {
        readonly IMailboxFilter[] _filters;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="filters">The list of filters to run in order.</param>
        public CompositeMailboxFilter(params IMailboxFilter[] filters)
        {
            _filters = filters;
        }

        /// <summary>
        /// Returns a value indicating whether the given mailbox can be accepted as a sender.
        /// </summary>
        /// <param name="context">The session context.</param>
        /// <param name="from">The mailbox to test.</param>
        /// <param name="size">The estimated message size to accept.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The acceptance state of the mailbox.</returns>
        public MailboxFilterResult CanAcceptFromAsync(
            ISessionContext context, 
            IMailbox @from, 
            int size)
        {
            if (_filters == null || _filters.Any() == false)
            {
                return MailboxFilterResult.Yes;
            }

            var results = _filters.Select(mailboxFilter => mailboxFilter.CanAcceptFromAsync(context, @from, size));

            return results.Max();
        }

        /// <summary>
        /// Returns a value indicating whether the given mailbox can be accepted as a recipient to the given sender.
        /// </summary>
        /// <param name="context">The session context.</param>
        /// <param name="to">The mailbox to test.</param>
        /// <param name="from">The sender's mailbox.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The acceptance state of the mailbox.</returns>
        public MailboxFilterResult CanDeliverToAsync(
            ISessionContext context, 
            IMailbox to, 
            IMailbox @from)
        {
            if (_filters == null || _filters.Any() == false)
            {
                return MailboxFilterResult.Yes;
            }

            var results = _filters.Select(f => f.CanDeliverToAsync(context, to, @from));

            return results.Max();
        }
    }
}