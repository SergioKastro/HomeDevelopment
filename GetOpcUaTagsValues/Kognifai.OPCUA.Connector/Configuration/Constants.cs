namespace Kognifai.OPCUA.Connector.Configuration
{
    public static class Constants
    {
        internal const string ConfigFilePath = "OPCUAConfiguration.xml";
        internal const string DefaultSessionName = "Kognifai_Session_ForGetTagsService";
        internal const string DefaultSubscriptionName = "Kognifai_Subscription_ForGetTagsService";
        internal const uint DefaultSessionTimeoutMs = 300 * 1000; //Default value 5 minutes (300000 Msc)
    }
}
