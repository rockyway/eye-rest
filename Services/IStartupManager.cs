namespace EyeRest.Services
{
    public interface IStartupManager
    {
        bool IsStartupEnabled();
        void EnableStartup();
        void EnableStartup(bool startMinimized);
        void DisableStartup();
    }
}