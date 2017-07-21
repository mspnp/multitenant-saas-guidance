// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Extensions.Options;
using Tailspin.Surveys.Common;
using Tailspin.Surveys.Web.Configuration;

#pragma warning disable CS0649

namespace Tailspin.Surveys.Web.Security
{
    /// <summary>
    /// Creates and caches the ADAL ClientAssertionCertificate 
    /// This class exists for performance reasons when using certificates with ADAL 
    /// It read certs from the store once and export it to a byte array representation and creates the ClientAssertionCertificate only once.
    /// </summary>
    public class CertificateCredentialService : ICredentialService
    {
        private Lazy<Task<AdalCredential>> _credential;

        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.Security.CertificateCredentialService"/>.
        /// </summary>
        /// <param name="options">Configuration options for this instance."/></param>
        public CertificateCredentialService(IOptions<ConfigurationOptions> options)
        {
            var aadOptions = options.Value?.AzureAd;
            Guard.ArgumentNotNull(aadOptions, "configOptions.AzureAd");
            Guard.ArgumentNotNull(aadOptions.Asymmetric, "configOptions.AzureAd.Assymetric");

            _credential = new Lazy<Task<AdalCredential>>(() =>
            {
                X509Certificate2 cert = CertificateUtility.FindCertificateByThumbprint(
                    aadOptions.Asymmetric.StoreName,
                    aadOptions.Asymmetric.StoreLocation,
                    aadOptions.Asymmetric.CertificateThumbprint,
                    aadOptions.Asymmetric.ValidationRequired);
                string password = null;
                var certBytes = CertificateUtility.ExportCertificateWithPrivateKey(cert, out password);
                return Task.FromResult(new AdalCredential(new ClientAssertionCertificate(aadOptions.ClientId, new X509Certificate2(certBytes, password))));
            });
        }

        /// <summary>
        /// Returns an instance of an <see cref="Tailspin.Surveys.Web.Security.AdalCredential"/> containing an instance of <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.ClientAssertionCertificate"/>.
        /// </summary>
        /// <returns>An instance of <see cref="Tailspin.Surveys.Web.Security.AdalCredential"/> containing the <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.ClientAssertionCertificate"/> instance.</returns>
        public async Task<AdalCredential> GetCredentialsAsync()
        {
            return await _credential.Value;
        }
    }
}

