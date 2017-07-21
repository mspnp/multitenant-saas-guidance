using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Tailspin.Surveys.Common;

namespace Tailspin.Surveys.Web.Security
{
    /// <summary>
    /// This class is needed as a workaround for the design of the ADAL credentials
    /// </summary>
    public class AdalCredential
    {
        /// <summary>
        /// Creates a new instance of the <see cref="Tailspin.Surveys.Security.AdalCredential"/>
        /// </summary>
        /// <param name="clientCredential">A <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential"/> instance to store in this credential.</param>
        public AdalCredential(ClientCredential clientCredential)
        {
            Guard.ArgumentNotNull(clientCredential, nameof(clientCredential));

            ClientCredential = clientCredential;
            CredentialType = AdalCredentialType.ClientCredential;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Tailspin.Surveys.Security.AdalCredential"/>
        /// </summary>
        /// <param name="clientAssertionCertificate">A <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.ClientAssertionCertificate"/> instance containing an X509 certificate that identifies the client.</param>
        public AdalCredential(ClientAssertionCertificate clientAssertionCertificate)
        {
            Guard.ArgumentNotNull(clientAssertionCertificate, nameof(clientAssertionCertificate));

            ClientAssertionCertificate = clientAssertionCertificate;
            CredentialType = AdalCredentialType.ClientAssertionCertificate;
        }

        /// <summary>
        /// Credential type stored in this <see cref="Tailspin.Surveys.Security.AdalCredential"/> instance.
        /// </summary>
        public AdalCredentialType CredentialType { get; private set; }

        /// <summary>
        /// A <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential"/> containing a client id and secret.
        /// </summary>
        public ClientCredential ClientCredential { get; private set; }

        /// <summary>
        /// A <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.ClientAssertionCertificate"/> instance containing an X509 certificate that identifies the client.
        /// </summary>
        public ClientAssertionCertificate ClientAssertionCertificate { get; private set; }
    }
}
