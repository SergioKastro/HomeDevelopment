using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Kognifai.OPCUA.Connector.Configuration;
using log4net;
using Opc.Ua;
using Opc.Ua.Configuration;

namespace Kognifai.OPCUA.Connector.Client
{
    public class OpcUaClientConfiguration
    {
        private static readonly ILog SysLog = LogManager.GetLogger(typeof(OpcUaClientConfiguration));
        private readonly string _opcServerUrl;

        public OpcUaClientConfiguration(string opcServerUrl)
        {
            _opcServerUrl = opcServerUrl;
        }

        private ConfiguredEndpoint _endpoint;

        public ConfiguredEndpoint Endpoint
        {
            get
            {
                if (_endpoint != null)
                {
                    return _endpoint;
                }

                try
                {
                    var selectedUserTokenType = UserTokenType.Anonymous;
                    var userName = string.Empty;
                    var password = string.Empty;
                    var userAuthentication = new UserAuthentication
                    {
                        ServerUrl = _opcServerUrl,
                        SelectedUserTokenType = selectedUserTokenType,
                        UserName = userName,
                        Password = password
                    };
                    _endpoint = ParseConfiguredEndpoint(userAuthentication);
                }
                catch (Exception ex)
                {
                    SysLog.Error("Invalid opcua configuration", ex);
                }
                return _endpoint;
            }
        }

        public async Task<ApplicationConfiguration> LoadOpcUaConfiguration()
        {
            if (!System.IO.File.Exists(Constants.ConfigFilePath))
                throw new FileNotFoundException($"Failed to locate application configuration. Looking fo file: {Constants.ConfigFilePath}");
            return await ApplicationInstance.LoadAppConfig(false, Constants.ConfigFilePath, ApplicationType.ClientAndServer, typeof(ApplicationConfiguration), false);
        }

        private ConfiguredEndpoint ParseConfiguredEndpoint(UserAuthentication userAuthentication)
        {
            string parameters = null;
            if (userAuthentication.ServerUrl == null)
                return null;

            var index = userAuthentication.ServerUrl.IndexOf("- [", StringComparison.Ordinal);
            if (index != -1)
            {
                parameters = userAuthentication.ServerUrl.Substring(index + 3);
                userAuthentication.ServerUrl = userAuthentication.ServerUrl.Substring(0, index).Trim();
            }

            var securityMode = MessageSecurityMode.None;
            var securityPolicyUri = SecurityPolicies.None;
            var useBinaryEncoding = true;

            if (!string.IsNullOrEmpty(parameters))
            {
                var fields = parameters.Split(new[] { '-', '[', ':', ']' }, StringSplitOptions.RemoveEmptyEntries);
                securityMode = SetSecurityModeProperty(fields);
                securityPolicyUri = SetSecurityPolicyUriProperty(fields);
                useBinaryEncoding = SetUserBinaryEncodingProperty(fields);
            }

            try
            {
                var uri = new Uri(userAuthentication.ServerUrl);
                var description = new EndpointDescription
                {
                    EndpointUrl = uri.ToString(),
                    SecurityMode = securityMode,
                    SecurityPolicyUri = securityPolicyUri,
                    Server =
                    {
                        ApplicationUri = Utils.UpdateInstanceUri(uri.ToString()),
                        ApplicationName = uri.AbsolutePath
                    }
                };

                if (description.EndpointUrl.StartsWith(Utils.UriSchemeOpcTcp, StringComparison.Ordinal))
                {
                    description.TransportProfileUri = Profiles.UaTcpTransport;
                    description.Server.DiscoveryUrls.Add(description.EndpointUrl);
                }
                else if (description.EndpointUrl.StartsWith(Utils.UriSchemeHttps, StringComparison.Ordinal))
                {
                    description.TransportProfileUri = Profiles.HttpsBinaryTransport;
                    description.Server.DiscoveryUrls.Add(description.EndpointUrl);
                }
                else
                {
                    description.TransportProfileUri = Profiles.UaTcpTransport;
                    description.Server.DiscoveryUrls.Add(description.EndpointUrl + "/discovery");
                }

                return SetUserAuthenticationToEndpoint(userAuthentication, useBinaryEncoding, description);
            }
            catch (Exception ex)
            {
                SysLog.Warn("failed retrieve endpoint ", ex);
                return null;
            }
        }

        private ConfiguredEndpoint SetUserAuthenticationToEndpoint(UserAuthentication userAuthentication, bool useBinaryEncoding, EndpointDescription description)
        {
            IEnumerable<UserTokenPolicy> userTokenPolicy = new List<UserTokenPolicy> { new UserTokenPolicy(userAuthentication.SelectedUserTokenType) };
            description.UserIdentityTokens = new UserTokenPolicyCollection(userTokenPolicy);
            var endpoint = new ConfiguredEndpoint(new ConfiguredEndpointCollection(), description, null)
            {
                Configuration = { UseBinaryEncoding = useBinaryEncoding, OperationTimeout = 5000 },
                UpdateBeforeConnect = true,
                UserIdentity = new UserNameIdentityToken
                {
                    UserName = userAuthentication.UserName,
                    Password = Encoding.ASCII.GetBytes(userAuthentication.Password)
                }
            };
            return endpoint;
        }

        private bool SetUserBinaryEncodingProperty(IReadOnlyList<string> fields)
        {
            bool useBinaryEncoding;
            try
            {
                useBinaryEncoding = fields.Count > 2 && fields[2] == "Binary";
            }
            catch
            {
                useBinaryEncoding = false;
            }

            return useBinaryEncoding;
        }

        private string SetSecurityPolicyUriProperty(IReadOnlyList<string> fields)
        {
            string securityPolicyUri;
            try
            {
                securityPolicyUri = fields.Count > 1 ? SecurityPolicies.GetUri(fields[1]) : SecurityPolicies.None;
            }
            catch
            {
                securityPolicyUri = SecurityPolicies.None;
            }

            return securityPolicyUri;
        }

        private MessageSecurityMode SetSecurityModeProperty(IReadOnlyList<string> fields)
        {
            MessageSecurityMode securityMode;
            try
            {
                securityMode = fields.Count > 0
                    ? (MessageSecurityMode)Enum.Parse(typeof(MessageSecurityMode), fields[0], false)
                    : MessageSecurityMode.None;
            }
            catch
            {
                securityMode = MessageSecurityMode.None;
            }

            return securityMode;
        }

        private class UserAuthentication
        {
            public UserTokenType SelectedUserTokenType { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            public string ServerUrl { get; set; }
        }
    }
}
