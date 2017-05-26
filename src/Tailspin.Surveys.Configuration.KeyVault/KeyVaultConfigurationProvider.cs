// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Tailspin.Surveys.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.KeyVault.Models;

namespace Tailspin.Surveys.Configuration.KeyVault
{
    /// <summary>
    /// Asp.Net configuration provider to read secrets from key vault. This requires List and Get permissions on the vault
    /// </summary>
    public class KeyVaultConfigurationProvider : ConfigurationProvider
    {
        const int MaxSecrets = 25;
        const string ConfigKey = "ConfigKey";

        private readonly string _appClientId;
        private readonly string _vault;
        private readonly string _certificateThumbprint;
        private readonly bool _validateCertificate;
        private readonly StoreName _storeName;
        private readonly StoreLocation _storeLocation;
        private readonly ILogger _logger;

        private IClientAssertionCertificate _assertion;

        /// <summary>
        /// Creates the Configuration source to read shared secrets from keyvault using cert in My store of CurrentUser
        /// </summary>
        /// <param name="appClientId"></param>
        /// <param name="vaultName"></param>
        /// <param name="certificateThumbprint"></param>
        /// <param name="validateCertificate"></param>
        /// <param name="loggerFactory"></param>
        public KeyVaultConfigurationProvider(string appClientId, string vaultName, string certificateThumbprint, bool validateCertificate, ILoggerFactory loggerFactory)
            : this(appClientId, vaultName, StoreName.My, StoreLocation.CurrentUser, certificateThumbprint, validateCertificate, loggerFactory)
        {
        }

        /// <summary>
        /// Creates the Configuration source to read shared secrets from keyvault using cert in the specified location
        /// </summary>
        /// <param name="appClientId"></param>
        /// <param name="vaultName"></param>
        /// <param name="storeName"></param>
        /// <param name="storeLocation"></param>
        /// <param name="certificateThumbprint"></param>
        /// <param name="validateCertificate"></param>
        /// <param name="loggerFactory"></param>
        public KeyVaultConfigurationProvider(string appClientId, string vaultName, StoreName storeName, StoreLocation storeLocation, string certificateThumbprint, bool validateCertificate, ILoggerFactory loggerFactory)
        {
            Guard.ArgumentNotNullOrWhiteSpace(appClientId, nameof(appClientId));
            Guard.ArgumentNotNullOrWhiteSpace(vaultName, nameof(vaultName));
            Guard.ArgumentNotNullOrWhiteSpace(certificateThumbprint, nameof(certificateThumbprint));
            Guard.ArgumentNotNull(loggerFactory, nameof(loggerFactory));

            _appClientId = appClientId;
            _vault = $"https://{vaultName}.vault.azure.net:443/";
            _storeName = storeName;
            _storeLocation = storeLocation;
            _certificateThumbprint = certificateThumbprint;
            _validateCertificate = validateCertificate;
            _logger = loggerFactory.CreateLogger<KeyVaultConfigurationProvider>();
        }

        /// <summary>
        /// Loads all secrets which are delimited by : so that they can be retrieved by the config system
        /// Since KeyVault does not  allow the : character as delimiter in the share secret name is not used as key for configuration, the Tag properties are used instead
        /// The tag should always be of the form "ConfigKey"="ParentKey1:Child1:.."
        /// </summary>
        public override void Load()
        {
            try
            {
                LoadAsync(CancellationToken.None).Wait();
                _logger.ConfigurationLoadSuccessful(_appClientId);
            }
            catch (Exception exp)
            {
                _logger.ConfigurationLoadFailed(_appClientId, exp);
                throw;
            }
        }
        /// <summary>
        /// Loads all secrets which are delimited by : so that they can be retrieved by the config system
        /// Since KeyVault does not  allow : as delimiters in the share secret name, the actual name is not used as key for configuration.
       ///  The Tag property is used instead
        /// The tag should always be of the form "ConfigKey"="ParentKey1:Child1:.."
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task LoadAsync(CancellationToken token)
        {
            string password;
            var cert = CertificateUtility.FindCertificateByThumbprint(_storeName, _storeLocation, _certificateThumbprint, _validateCertificate);
            var certBytes = CertificateUtility.ExportCertificateWithPrivateKey(cert, out password);
            _assertion = new ClientAssertionCertificate(_appClientId, new X509Certificate2(certBytes, password));
            Data = new Dictionary<string, string>();

            // This returns a list of identifiers which are uris to the secret, you need to use the identifier to get the actual secrets again.
            var kvClient = new KeyVaultClient(GetTokenAsync);
            var secretsResponseList = await kvClient.GetSecretsAsync(_vault, MaxSecrets, token);
            foreach (var secretItem in secretsResponseList ?? new Page<SecretItem>())
            {
                //The actual config key is stored in a tag with the Key "ConfigKey" since : is not supported in a shared secret name by KeyVault
                if (secretItem.Tags != null && secretItem.Tags.ContainsKey(ConfigKey))
                {
                    var secret = await kvClient.GetSecretAsync(secretItem.Id, token);
                    Data.Add(secret.Tags[ConfigKey], secret.Value);
                }
            }
        }

        private async Task<string> GetTokenAsync(string authority, string resource, string scope)
        {
            AuthenticationResult result = null;
            try
            {
                var authContext = new AuthenticationContext(authority);
                result = await authContext.AcquireTokenAsync(resource, _assertion);
            }
            catch (Exception exp)
            {
                _logger.AuthenticationFailed(_appClientId, exp);
                throw;
            }
            if (result == null)
            {
                throw new InvalidOperationException("Bearer token acquisition needed to call KeyVault service failed");
            }

            return result.AccessToken;
        }
    }
}
