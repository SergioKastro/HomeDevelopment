namespace Kognifai.OPCUA.Connector.Interfaces
{
    public interface IOpcUaProcessor
    {
        void Start();

        void Stop();

        void Shutdown();
    }
}