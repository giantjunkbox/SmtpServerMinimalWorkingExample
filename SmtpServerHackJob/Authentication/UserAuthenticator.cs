﻿using System.Threading;
using System.Threading.Tasks;

namespace SmtpServer.Authentication
{
    public abstract class UserAuthenticator : IUserAuthenticator, IUserAuthenticatorFactory
    {
        /// <summary>
        /// Creates an instance of the user authenticator for the given session context.
        /// </summary>
        /// <param name="context">The session context.</param>
        /// <returns>The user authenticator instance for the session context.</returns>
        public virtual IUserAuthenticator CreateInstance(ISessionContext context)
        {
            return this;
        }

        /// <summary>
        /// Authenticate a user account.
        /// </summary>
        /// <param name="context">The session context.</param>
        /// <param name="user">The user to authenticate.</param>
        /// <param name="password">The password of the user.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>true if the user is authenticated, false if not.</returns>
        public abstract bool AuthenticateAsync(
            ISessionContext context, 
            string user, 
            string password);
    }
}