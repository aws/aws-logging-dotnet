namespace AWS.Logger.Core
{
    public interface IAWSLoggerCore
    {
        void Flush();

        void Close();

        void AddMessage(string message);

        void StartMonitor();
    }
}
