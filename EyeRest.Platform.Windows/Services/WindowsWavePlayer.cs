using System;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using PortAudioSharp;

namespace EyeRest.Services
{
    /// <summary>
    /// Plays WAV files through PortAudio's MME host (DirectSound fallback) instead of
    /// winmm <c>PlaySound</c> (which <see cref="System.Media.SoundPlayer"/> wraps).
    ///
    /// Why not SoundPlayer / winmm PlaySound: on some Windows audio endpoints every
    /// PlaySound playback — sync or async, any sample rate — comes out distorted, while
    /// PortAudio's MME/DirectSound streams on the same endpoint play the identical PCM
    /// data cleanly (the same host-API split Audacity shows: MME/DirectSound clean,
    /// WASAPI distorts). macOS (NSSound) is unaffected. Ported from inkspoke's
    /// WindowsSoundEffectService. SoundPlayer is kept only as a last-resort fallback when
    /// no PortAudio output stream can be opened.
    /// </summary>
    internal sealed class WindowsWavePlayer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly object _paInitLock = new();
        private bool _paInitialized;
        private volatile bool _disposed;

        public WindowsWavePlayer(ILogger logger) => _logger = logger;

        /// <summary>
        /// Decodes <paramref name="filePath"/> and plays it synchronously. Honours
        /// <paramref name="ct"/> at each safe point. Falls back to winmm SoundPlayer if
        /// PortAudio playback fails for any reason.
        /// </summary>
        public void Play(string filePath, CancellationToken ct)
        {
            if (_disposed) return;
            ct.ThrowIfCancellationRequested();

            var (samples, sampleRate, channels) = DecodeWav(filePath);

            try
            {
                EnsurePortAudioInitialized();
                var device = ResolveOutputDevice();
                if (device < 0)
                    throw new InvalidOperationException("No PortAudio output device available");
                PlayViaPortAudio(device, samples, sampleRate, channels, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PortAudio WAV playback failed — falling back to winmm SoundPlayer");
                ct.ThrowIfCancellationRequested();
                using var player = new SoundPlayer(filePath);
                player.Load();
                ct.ThrowIfCancellationRequested();
                player.PlaySync();
            }
        }

        #region PortAudio Playback

        private void EnsurePortAudioInitialized()
        {
            lock (_paInitLock)
            {
                if (_paInitialized) return;
                PortAudio.Initialize();
                _paInitialized = true;
            }
        }

        /// <summary>
        /// Picks the default output device of the MME host, then DirectSound, then the
        /// PortAudio global default. Resolved per play because USB replug shifts indices.
        /// </summary>
        private int ResolveOutputDevice()
        {
            foreach (var hostType in new[] { PaHostApiType.Mme, PaHostApiType.DirectSound })
            {
                try
                {
                    var hostIdx = Pa_HostApiTypeIdToHostApiIndex((int)hostType);
                    if (hostIdx < 0) continue;
                    var info = Marshal.PtrToStructure<PaHostApiInfo>(Pa_GetHostApiInfo(hostIdx));
                    if (info.defaultOutputDevice >= 0) return info.defaultOutputDevice;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Host API {Host} lookup failed", hostType);
                }
            }

            return PortAudio.DefaultOutputDevice;
        }

        private void PlayViaPortAudio(int device, float[] samples, int sampleRate, int channels, CancellationToken ct)
        {
            var deviceInfo = PortAudio.GetDeviceInfo(device);
            var outParams = new StreamParameters
            {
                device = device,
                channelCount = channels,
                sampleFormat = SampleFormat.Float32,
                suggestedLatency = deviceInfo.defaultLowOutputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero,
            };

            var pos = 0; // index into the interleaved float buffer
            using var done = new ManualResetEventSlim(false);

            StreamCallbackResult Callback(IntPtr input, IntPtr output, uint frameCount,
                ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
            {
                var floatsRequested = (int)frameCount * channels;
                var toCopy = Math.Min(floatsRequested, samples.Length - pos);
                if (toCopy > 0) Marshal.Copy(samples, pos, output, toCopy);
                if (toCopy < floatsRequested)
                {
                    var silence = new float[floatsRequested - toCopy];
                    Marshal.Copy(silence, 0, output + toCopy * sizeof(float), silence.Length);
                }
                pos += toCopy;
                return pos >= samples.Length ? StreamCallbackResult.Complete : StreamCallbackResult.Continue;
            }

            PortAudioSharp.Stream? stream = null;
            try
            {
                stream = new PortAudioSharp.Stream(
                    inParams: null,
                    outParams: outParams,
                    sampleRate: sampleRate,
                    framesPerBuffer: 0, // let PortAudio choose
                    streamFlags: StreamFlags.NoFlag,
                    callback: Callback,
                    userData: IntPtr.Zero);

                stream.SetFinishedCallback(_ => done.Set());
                stream.Start();

                // Wait for the stream to drain naturally; generous timeout as a safety net.
                var cueDuration = TimeSpan.FromSeconds((double)samples.Length / channels / sampleRate + 2);
                while (!done.Wait(50))
                {
                    if (ct.IsCancellationRequested) break;
                    if (cueDuration <= TimeSpan.Zero) break;
                    cueDuration -= TimeSpan.FromMilliseconds(50);
                }
            }
            finally
            {
                if (stream != null)
                {
                    try { stream.Stop(); } catch { /* already stopped */ }
                    try { stream.Dispose(); } catch { /* best effort */ }
                    // Prevent the finalizer from closing a potentially dangling native
                    // handle on the GC thread (fatal) if Dispose didn't fully release it.
                    try { GC.SuppressFinalize(stream); } catch { /* best effort */ }
                }
            }

            ct.ThrowIfCancellationRequested();
        }

        #endregion

        #region Win32 / PortAudio Interop

        // PaHostApiTypeId values from portaudio.h
        private enum PaHostApiType
        {
            DirectSound = 1,
            Mme = 2,
        }

        // "portaudio" matches PortAudioSharp's own DllImport name, so this binds to the
        // exact native module the binding already loaded (no second copy).
        [DllImport("portaudio")]
        private static extern int Pa_HostApiTypeIdToHostApiIndex(int type);

        [DllImport("portaudio")]
        private static extern IntPtr Pa_GetHostApiInfo(int hostApi);

        [StructLayout(LayoutKind.Sequential)]
        private struct PaHostApiInfo
        {
#pragma warning disable CS0649 // fields populated by Marshal.PtrToStructure
            public int structVersion;
            public int type;
            public IntPtr name;
            public int deviceCount;
            public int defaultInputDevice;
            public int defaultOutputDevice;
#pragma warning restore CS0649
        }

        #endregion

        #region WAV decoding

        /// <summary>
        /// Minimal RIFF/WAVE PCM decoder → interleaved float samples. Scans the chunk list
        /// (a real WAV often has LIST/INFO or fact chunks before <c>data</c>, so the data
        /// chunk is NOT at a fixed offset). Supports PCM 8/16/24/32-bit and 32-bit IEEE
        /// float, any channel count / sample rate (including WAVE_FORMAT_EXTENSIBLE).
        /// </summary>
        private static (float[] samples, int sampleRate, int channels) DecodeWav(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 12 ||
                bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F' ||
                bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
                throw new InvalidDataException("Not a RIFF/WAVE file");

            int format = 0, channels = 0, sampleRate = 0, bitsPerSample = 0;
            int dataOffset = -1, dataLength = 0;
            bool haveFmt = false;

            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                var id = Encoding.ASCII.GetString(bytes, pos, 4);
                long size = BitConverter.ToUInt32(bytes, pos + 4);
                int body = pos + 8;
                if (size < 0 || body + size > bytes.Length)
                    size = bytes.Length - body; // tolerate a truncated/over-stated size

                if (id == "fmt " && size >= 16)
                {
                    format = BitConverter.ToUInt16(bytes, body + 0);
                    channels = BitConverter.ToUInt16(bytes, body + 2);
                    sampleRate = (int)BitConverter.ToUInt32(bytes, body + 4);
                    bitsPerSample = BitConverter.ToUInt16(bytes, body + 14);
                    // WAVE_FORMAT_EXTENSIBLE: the real format tag is the first 2 bytes of
                    // the SubFormat GUID (at body + 24).
                    if (format == 0xFFFE && size >= 26)
                        format = BitConverter.ToUInt16(bytes, body + 24);
                    haveFmt = true;
                }
                else if (id == "data")
                {
                    dataOffset = body;
                    dataLength = (int)size;
                }

                if (haveFmt && dataOffset >= 0) break;
                pos = body + (int)size + ((int)size & 1); // chunks are word-aligned
            }

            if (!haveFmt || dataOffset < 0)
                throw new InvalidDataException("WAV file missing fmt or data chunk");
            if (channels <= 0 || sampleRate <= 0)
                throw new InvalidDataException("WAV file has invalid channel count / sample rate");

            var samples = ConvertToFloat(bytes, dataOffset, dataLength, format, bitsPerSample);
            return (samples, sampleRate, channels);
        }

        private static float[] ConvertToFloat(byte[] data, int offset, int length, int format, int bits)
        {
            // format: 1 = PCM (integer), 3 = IEEE float
            if (format == 3 && bits == 32)
            {
                var n = length / 4;
                var f = new float[n];
                Buffer.BlockCopy(data, offset, f, 0, n * 4);
                return f;
            }

            if (format == 1)
            {
                switch (bits)
                {
                    case 16:
                    {
                        var n = length / 2;
                        var f = new float[n];
                        for (var i = 0; i < n; i++)
                        {
                            var s = (short)(data[offset + i * 2] | (data[offset + i * 2 + 1] << 8));
                            f[i] = s / 32768f;
                        }
                        return f;
                    }
                    case 8:
                    {
                        // 8-bit PCM is unsigned, centred at 128.
                        var f = new float[length];
                        for (var i = 0; i < length; i++)
                            f[i] = (data[offset + i] - 128) / 128f;
                        return f;
                    }
                    case 24:
                    {
                        var n = length / 3;
                        var f = new float[n];
                        for (var i = 0; i < n; i++)
                        {
                            var b = offset + i * 3;
                            var s = data[b] | (data[b + 1] << 8) | (data[b + 2] << 16);
                            if ((s & 0x800000) != 0) s |= unchecked((int)0xFF000000); // sign-extend
                            f[i] = s / 8388608f;
                        }
                        return f;
                    }
                    case 32:
                    {
                        var n = length / 4;
                        var f = new float[n];
                        for (var i = 0; i < n; i++)
                        {
                            var b = offset + i * 4;
                            var s = data[b] | (data[b + 1] << 8) | (data[b + 2] << 16) | (data[b + 3] << 24);
                            f[i] = s / 2147483648f;
                        }
                        return f;
                    }
                }
            }

            throw new NotSupportedException($"Unsupported WAV format (tag {format}, {bits}-bit)");
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_paInitLock)
            {
                if (_paInitialized)
                {
                    _paInitialized = false;
                    try { PortAudio.Terminate(); } catch { /* ref may already be drained */ }
                }
            }
        }
    }
}
