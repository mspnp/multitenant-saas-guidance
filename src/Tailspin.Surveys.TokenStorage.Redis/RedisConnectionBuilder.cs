// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using StackExchange.Redis;
using Tailspin.Surveys.Common;

namespace Tailspin.Surveys.TokenStorage.Redis
{
    /// <summary>
    /// Builder class used to configure an instance of <see cref="StackExchange.Redis.ConfigurationOptions"/>.
    /// </summary>
    public class RedisConnectionBuilder
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.TokenStorage.Redis.RedisConnectionBuilder"/>/
        /// </summary>
        public RedisConnectionBuilder()
            : this(new ConfigurationOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.TokenStorage.Redis.RedisConnectionBuilder"/>/
        /// </summary>
        /// <param name="options">A <see cref="StackExchange.Redis.ConfigurationOptions"/> instance used to configure this instance.</param>
        public RedisConnectionBuilder(ConfigurationOptions options)
        {
            Guard.ArgumentNotNull(options, nameof(options));

            Options = options;
        }

        internal ConfigurationOptions Options { get; private set; }

        public RedisConnectionBuilder AddEndpoint(string hostAndPort)
        {
            Guard.ArgumentNotNullOrWhiteSpace(hostAndPort, nameof(hostAndPort));

            Options.EndPoints.Add(hostAndPort);
            return this;
        }

        public RedisConnectionBuilder UsePassword(string password)
        {
            Guard.ArgumentNotNullOrWhiteSpace(password, nameof(password));

            Options.Password = password;
            return this;
        }

        public RedisConnectionBuilder UseSsl(bool useSsl = true)
        {
            Options.Ssl = useSsl;
            return this;
        }
    }
}
