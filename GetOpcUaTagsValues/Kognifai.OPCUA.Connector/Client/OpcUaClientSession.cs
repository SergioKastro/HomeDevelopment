using System;
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
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaClientSession));
        private Session _session;

        public bool IsConnected => !(_session == null || !this._session.Connected || this._session.KeepAliveStopped);

        public async Task CreateSessionAsync(OpcUaClientConfiguration config)
        {
            SysLog.Info("Creating the Opcua client session.");
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
                    SysLog.Info("OpcUa client session successfully created");

                }
                else
                {
                    SysLog.Warn("Failed to get OPC server url from connector source config.");
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
            this._session?.Dispose();
        }

        public void Close()
        {
            this._session?.Close();
        }

        public void AddSubscription(Subscription subscription)
        {
            this._session.AddSubscription(subscription);
        }

        public void RemoveSubscription(Subscription subscription)
        {
            this._session.RemoveSubscription(subscription);
        }

        public BrowsePathResult GetBrowseResultForOneNode(string nodeId)
        {
            var result = new BrowsePathResult { StatusCode = StatusCodes.Bad };

            try
            {
                SysLog.Debug($"Trying to locate {nodeId}.\n");
                //This call is a synchronous call (Polling). This type of call for some nodes it is too quickly
                var node = this._session?.ReadNode(nodeId);

                var target = new BrowsePathTarget
                {
                    TargetId = node?.NodeId
                };

                result.StatusCode = node?.NodeId is null ? StatusCodes.Bad : StatusCodes.Good;

                result.Targets.Add(target);
            }
            catch (ServiceResultException ex)
            {
                if (StatusCodes.BadNodeIdUnknown == ex.StatusCode || StatusCodes.BadNodeIdInvalid == ex.StatusCode)
                {
                    //We know the nodeId does not exist because the OPC server has reported that
                    if (SysLog.IsDebugEnabled)
                    {
                        SysLog.Error($"Failed to retrieve node id for item {nodeId}. ");
                    }
                    result.StatusCode = ex.StatusCode;
                }
                //We know the Node is not bad because the OPC Server has not report that .
                //We continue searching for the Node.
                SysLog.Debug($"Node {nodeId} was not located in a normal way.\n");
            }
            catch (Exception ex)
            {
                SysLog.Error($"Failed to retrieve node id for item {nodeId}. ", ex);
            }

            return result;
        }
    }
}
