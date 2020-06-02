using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Kognifai.OPCUA.Connector.Configuration;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace Kognifai.OPCUA.Connector.Client
{
    public sealed class OpcUaClientSession
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OpcUaClientSession));
        private Session _session;

        public bool IsConnected => !(_session == null || !_session.Connected || _session.KeepAliveStopped);

        public async Task CreateSessionAsync(OpcUaClientConfiguration config)
        {
            Log.Info("Creating the Opcua client session.");
            try
            {
                var endpoint = config.Endpoint;
                if (endpoint != null)
                {
                    var appConfig = await config.LoadOpcUaConfiguration();
                    appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
                    appConfig.CertificateValidator.CertificateValidation += (validator, e) => e.Accept = true;
                    if (endpoint.Description.SecurityPolicyUri != SecurityPolicies.None)
                    {
                        await appConfig.SecurityConfiguration.ApplicationCertificate?.Find(true);
                    }

                    var identity = GetUserIdentity(endpoint);
                    _session = await Session.Create(appConfig, endpoint, true, false, Constants.DefaultSessionName, Constants.DefaultSessionTimeoutMs, identity, null);
                    Log.Info("OpcUa client session successfully created");

                }
                else
                {
                    Log.Warn("Failed to get OPC server url from connector source config.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load application configuration.", ex);
            }
        }

        private UserIdentity GetUserIdentity(ConfiguredEndpoint configuredEndpoint)
        {
            var identity = new UserIdentity();
            if (configuredEndpoint.SelectedUserTokenPolicy?.TokenType == UserTokenType.UserName
                        && configuredEndpoint.UserIdentity is UserNameIdentityToken identityToken)
            {
                if (identityToken.Password == null || identityToken.UserName == null)
                {
                    throw new Exception("Failed to retrieve valid username and password from configured endpoint.");
                }

                var password = Encoding.UTF8.GetString(identityToken.Password);
                var username = identityToken.UserName;
                identity = new UserIdentity(username, password);
            }
            return identity;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }

        public void Close()
        {
            _session?.Close();
        }

        public void AddSubscription(Subscription subscription)
        {
            _session.AddSubscription(subscription);
        }

        public void RemoveSubscription(Subscription subscription)
        {
            _session.RemoveSubscription(subscription);
        }

        public BrowsePathResultCollection GetBrowseResults(List<string> listNodeIds)
        {
            var browseResults = new BrowsePathResultCollection(listNodeIds.Count);

            foreach (var nodeId in listNodeIds)
            {
                try
                {
                    var node = _session?.ReadNode(nodeId);

                    var target = new BrowsePathTarget
                    {
                        TargetId = node?.NodeId
                    };

                    var result = new BrowsePathResult
                    {
                        StatusCode = node?.NodeId is null ? StatusCodes.BadNodeIdUnknown : StatusCodes.Good
                    };
                    result.Targets.Add(target);

                    browseResults.Add(result);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to retrieve node id for item {nodeId}. ", ex);
                }

            }

            return browseResults;
        }
    }
}
