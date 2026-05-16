namespace EyeRest.Services
{
    /// <summary>
    /// BL-002: opens a URL in the user's default browser. Implemented by
    /// <c>DefaultUrlOpener</c> (Process.Start with UseShellExecute=true), or
    /// by stubs in tests. The interface lets the audio service depend on a
    /// pure-Core abstraction rather than calling <c>Process.Start</c> directly.
    /// </summary>
    public interface IUrlOpener
    {
        void Open(string url);
    }
}
