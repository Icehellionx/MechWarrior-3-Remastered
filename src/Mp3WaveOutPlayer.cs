// Streams MP3 audio through WinMM waveOut. MP3 decoding is provided by the
// pinned, MIT-licensed NLayer dependency; device output remains a Windows API.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NLayer;

internal sealed class Mp3WaveOutPlayer : IDisposable
{
    private const uint WaveMapper = 0xffffffff;
    private const ushort WaveFormatPcm = 1;
    private const uint WaveHeaderDone = 0x00000001;
    private const uint WaveHeaderPrepared = 0x00000002;
    private const int MaxQueuedBuffers = 3;
    private readonly object stateLock = new object();
    private Thread worker;
    private ManualResetEvent ready;
    private IntPtr device;
    private volatile bool stopping;
    private volatile bool paused;
    private volatile bool completed;
    private volatile bool started;
    private uint volume = 0xffffffff;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSecond;
        public uint AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    private sealed class OutputBuffer : IDisposable
    {
        public readonly byte[] Data;
        public readonly GCHandle DataHandle;
        public readonly IntPtr Header;
        public bool Prepared;

        public OutputBuffer(byte[] data)
        {
            Data = data;
            DataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            WaveHeader value = new WaveHeader
            {
                Data = DataHandle.AddrOfPinnedObject(),
                BufferLength = (uint)data.Length
            };
            Header = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WaveHeader)));
            Marshal.StructureToPtr(value, Header, false);
        }

        public bool IsDone
        {
            get { return (((WaveHeader)Marshal.PtrToStructure(Header, typeof(WaveHeader))).Flags & WaveHeaderDone) != 0; }
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Header);
            if (DataHandle.IsAllocated) DataHandle.Free();
        }
    }

    [DllImport("winmm.dll")]
    private static extern uint waveOutOpen(out IntPtr waveOut, uint deviceId, ref WaveFormat format,
        IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")]
    private static extern uint waveOutPrepareHeader(IntPtr waveOut, IntPtr header, uint headerSize);
    [DllImport("winmm.dll")]
    private static extern uint waveOutWrite(IntPtr waveOut, IntPtr header, uint headerSize);
    [DllImport("winmm.dll")]
    private static extern uint waveOutUnprepareHeader(IntPtr waveOut, IntPtr header, uint headerSize);
    [DllImport("winmm.dll")]
    private static extern uint waveOutPause(IntPtr waveOut);
    [DllImport("winmm.dll")]
    private static extern uint waveOutRestart(IntPtr waveOut);
    [DllImport("winmm.dll")]
    private static extern uint waveOutReset(IntPtr waveOut);
    [DllImport("winmm.dll")]
    private static extern uint waveOutClose(IntPtr waveOut);
    [DllImport("winmm.dll")]
    private static extern uint waveOutSetVolume(IntPtr waveOut, uint volume);

    public bool IsOpen { get { return started; } }
    public bool IsPaused { get { return paused; } }
    public bool Completed { get { return completed; } }

    public bool Start(string path)
    {
        Stop();
        if (ready != null) { ready.Dispose(); ready = null; }
        stopping = false;
        paused = false;
        completed = false;
        started = false;
        ready = new ManualResetEvent(false);
        worker = new Thread(delegate() { StreamFile(path); });
        worker.IsBackground = true;
        worker.Name = "CD audio MP3 decoder/output";
        worker.Start();
        if (!ready.WaitOne(5000) || !started)
        {
            Stop();
            return false;
        }
        return true;
    }

    public bool Pause()
    {
        lock (stateLock)
        {
            if (!started || device == IntPtr.Zero || waveOutPause(device) != 0) return false;
            paused = true;
            return true;
        }
    }

    public bool Resume()
    {
        lock (stateLock)
        {
            if (!started || device == IntPtr.Zero || waveOutRestart(device) != 0) return false;
            paused = false;
            return true;
        }
    }

    public void SetVolume(uint value)
    {
        lock (stateLock)
        {
            volume = value;
            if (device != IntPtr.Zero) waveOutSetVolume(device, value);
        }
    }

    public void Stop()
    {
        Thread active = worker;
        if (active == null) return;
        stopping = true;
        lock (stateLock)
        {
            if (device != IntPtr.Zero) waveOutReset(device);
        }
        if (active != Thread.CurrentThread) active.Join(5000);
        worker = null;
        started = false;
        paused = false;
    }

    private void StreamFile(string path)
    {
        List<OutputBuffer> queued = new List<OutputBuffer>();
        uint headerSize = (uint)Marshal.SizeOf(typeof(WaveHeader));
        bool endOfFile = false;
        try
        {
            using (MpegFile mpeg = new MpegFile(path))
            {
                WaveFormat format = CreateFormat(mpeg.SampleRate, mpeg.Channels);
                IntPtr opened;
                if (waveOutOpen(out opened, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0) return;
                lock (stateLock)
                {
                    device = opened;
                    waveOutSetVolume(device, volume);
                }

                FillQueue(mpeg, queued, headerSize, ref endOfFile);
                if (queued.Count == 0) return;
                started = true;
                ready.Set();

                while (!stopping && (!endOfFile || queued.Count != 0))
                {
                    for (int index = queued.Count - 1; index >= 0; index--)
                    {
                        if (!queued[index].IsDone) continue;
                        ReleaseBuffer(queued[index], headerSize);
                        queued.RemoveAt(index);
                    }
                    if (!paused) FillQueue(mpeg, queued, headerSize, ref endOfFile);
                    Thread.Sleep(5);
                }
                if (!stopping && endOfFile && queued.Count == 0) completed = true;
            }
        }
        catch
        {
            // A decoder or output failure is reported to the wrapper as idle.
        }
        finally
        {
            ready.Set();
            IntPtr closing;
            lock (stateLock) { closing = device; }
            if (closing != IntPtr.Zero)
            {
                waveOutReset(closing);
                foreach (OutputBuffer buffer in queued) ReleaseBuffer(buffer, headerSize);
                waveOutClose(closing);
            }
            lock (stateLock) { device = IntPtr.Zero; }
            started = false;
            paused = false;
        }
    }

    private void FillQueue(MpegFile mpeg, List<OutputBuffer> queued, uint headerSize, ref bool endOfFile)
    {
        int sampleCapacity = Math.Max(4096, mpeg.SampleRate * mpeg.Channels / 4);
        while (!stopping && !endOfFile && queued.Count < MaxQueuedBuffers)
        {
            float[] samples = new float[sampleCapacity];
            int count = mpeg.ReadSamples(samples, 0, samples.Length);
            if (count <= 0) { endOfFile = true; break; }
            byte[] pcm = ConvertToPcm16(samples, count);
            OutputBuffer buffer = new OutputBuffer(pcm);
            if (waveOutPrepareHeader(device, buffer.Header, headerSize) != 0)
            {
                buffer.Dispose();
                throw new InvalidOperationException("Could not prepare a wave output buffer.");
            }
            buffer.Prepared = true;
            if (waveOutWrite(device, buffer.Header, headerSize) != 0)
            {
                ReleaseBuffer(buffer, headerSize);
                throw new InvalidOperationException("Could not write a wave output buffer.");
            }
            queued.Add(buffer);
        }
    }

    private void ReleaseBuffer(OutputBuffer buffer, uint headerSize)
    {
        if (buffer.Prepared && device != IntPtr.Zero)
        {
            for (int tries = 0; tries < 100 && waveOutUnprepareHeader(device, buffer.Header, headerSize) != 0; tries++)
                Thread.Sleep(2);
        }
        buffer.Dispose();
    }

    private static WaveFormat CreateFormat(int sampleRate, int channels)
    {
        ushort channelCount = checked((ushort)channels);
        ushort blockAlign = checked((ushort)(channelCount * 2));
        return new WaveFormat
        {
            FormatTag = WaveFormatPcm,
            Channels = channelCount,
            SamplesPerSecond = checked((uint)sampleRate),
            AverageBytesPerSecond = checked((uint)(sampleRate * blockAlign)),
            BlockAlign = blockAlign,
            BitsPerSample = 16,
            ExtraSize = 0
        };
    }

    private static byte[] ConvertToPcm16(float[] samples, int count)
    {
        byte[] bytes = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            float sample = Math.Max(-1.0f, Math.Min(1.0f, samples[i]));
            short value = sample <= -1.0f ? Int16.MinValue : (short)Math.Round(sample * Int16.MaxValue);
            bytes[i * 2] = (byte)value;
            bytes[i * 2 + 1] = (byte)(value >> 8);
        }
        return bytes;
    }

    public void Dispose()
    {
        Stop();
        if (ready != null) ready.Dispose();
    }
}
