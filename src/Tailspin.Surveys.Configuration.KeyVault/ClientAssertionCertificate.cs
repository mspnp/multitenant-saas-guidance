using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Tailspin.Surveys.Configuration.KeyVault
{
    internal class ClientAssertionCertificate : IClientAssertionCertificate
    {
        private X509Certificate2 certificate;

        public string ClientId { get; private set; }

        public string Thumbprint
        {
            get
            {
                return Base64UrlEncoder.Encode(certificate.GetCertHash());
            }
        }

        public ClientAssertionCertificate(string clientId, X509Certificate2 certificate)
        {
            ClientId = clientId;
            this.certificate = certificate;
        }

        public byte[] Sign(string message)
        {
            using (var key = certificate.GetRSAPrivateKey())
            {
                return key.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }

}
