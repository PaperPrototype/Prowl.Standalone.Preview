// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Prowl.Runtime.Audio.Native;
using Prowl.Runtime.Resources;

namespace Prowl.Runtime.Audio
{
    public delegate void DeviceDataEvent(NativeArray<float> data, UInt32 frameCount);

    /// <summary>
    /// This class is responsible for managing the audio context.
    /// </summary>
    public static class AudioContext
    {
        private static IntPtr audioContext;
        private static ma_device_data_proc deviceDataProc;
        private static Dictionary<UInt64, IntPtr> audioClipHandles = new Dictionary<UInt64, IntPtr>();
        private static AudioBuffer outputBuffer = new AudioBuffer(8192);

        private static UInt32 sampleRate = 44100;
        private static UInt32 channels = 2;
        private static DateTime lastUpdateTime;
        private static float deltaTime;

        public static event DeviceDataEvent DataProcess;

        internal static IntPtr NativeContext
        {
            get
            {
                return audioContext;
            }
        }

        /// <summary>
        /// Gets the chosen sample rate.
        /// </summary>
        /// <value></value>
        public static Int32 SampleRate
        {
            get
            {
                return (int)sampleRate;
            }
        }

        public static Int32 Channels
        {
            get
            {
                return (int)channels;
            }
        }

        /// <summary>
        /// Controls the master volume.
        /// </summary>
        /// <value></value>
        public static float MasterVolume
        {
            get
            {
                return MiniAudioExNative.ma_ex_context_get_master_volume(audioContext);
            }
            set
            {
                MiniAudioExNative.ma_ex_context_set_master_volume(audioContext, value);
            }
        }

        /// <summary>
        /// The elapsed time since last call to 'Update'.
        /// </summary>
        /// <value></value>
        public static float DeltaTime
        {
            get
            {
                return deltaTime;
            }
        }

        /// <summary>
        /// Initializes MiniAudioEx. Call this once at the start of your application.
        /// </summary>
        /// <param name="sampleRate">The sample rate to use. Typical sampling rates are 44100 and 48000.</param>
        /// <param name="channels">The number of channels to use. For most purposes 2 is the best choice (stereo audio).</param>
        /// <param name="periodSizeInFrames">Buffer size for audio processing. This value is a 'hint' so in practice it may be different than what you passed.</param>
        /// <param name="deviceInfo">If left null, a default device is used.</param>
        public static void Initialize(UInt32 sampleRate, UInt32 channels, UInt32 periodSizeInFrames = 2048, DeviceInfo deviceInfo = null)
        {
            if (audioContext != IntPtr.Zero)
                return;

            ma_ex_device_info pDeviceInfo = new ma_ex_device_info();
            pDeviceInfo.index = deviceInfo == null ? -1 : deviceInfo.Index;
            pDeviceInfo.pName = IntPtr.Zero;
            pDeviceInfo.nativeDataFormatCount = 0;
            pDeviceInfo.nativeDataFormats = IntPtr.Zero;

            AudioContext.sampleRate = sampleRate;
            AudioContext.channels = channels;

            ma_ex_context_config contextConfig = MiniAudioExNative.ma_ex_context_config_init(sampleRate, (byte)channels, periodSizeInFrames, ref pDeviceInfo);

            deviceDataProc = OnDeviceDataProc;
            contextConfig.deviceDataProc = deviceDataProc;

            audioContext = MiniAudioExNative.ma_ex_context_init(ref contextConfig);

            if (audioContext == IntPtr.Zero)
            {
                Console.WriteLine("Failed to initialize MiniAudioEx");
            }

            lastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// Deinitializes MiniAudioEx. Call this before closing the application.
        /// </summary>
        public static void Deinitialize()
        {
            if (audioContext == IntPtr.Zero)
                return;

            foreach (var audioClipHandle in audioClipHandles.Values)
            {
                if (audioClipHandle != IntPtr.Zero)
                    Marshal.FreeHGlobal(audioClipHandle);
            }

            audioClipHandles.Clear();

            MiniAudioExNative.ma_ex_context_uninit(audioContext);
            audioContext = IntPtr.Zero;
        }

        /// <summary>
        /// Used to calculate delta time and move messages from the audio thread to the main thread. Call this method from within your main thread loop.
        /// </summary>
        public static void Update()
        {
            if (audioContext == IntPtr.Zero)
                return;

            DateTime currentTime = DateTime.Now;
            TimeSpan dt = currentTime - lastUpdateTime;
            deltaTime = (float)dt.TotalSeconds;

            lastUpdateTime = currentTime;
        }

        /// <summary>
        /// Gets an array of available playback devices. Retrieving devices is a relatively slow operation, so don't call it continuously.
        /// </summary>
        /// <returns>An array with playback devices</returns>
        public static DeviceInfo[] GetDevices()
        {
            IntPtr pDevices = MiniAudioExNative.ma_ex_playback_devices_get(out UInt32 count);

            if (pDevices == IntPtr.Zero)
                return null;

            if (count == 0)
            {
                MiniAudioExNative.ma_ex_playback_devices_free(pDevices, count);
                return null;
            }

            DeviceInfo[] devices = new DeviceInfo[count];

            for (UInt32 i = 0; i < count; i++)
            {
                IntPtr elementPtr = IntPtr.Add(pDevices, (int)i * Marshal.SizeOf<ma_ex_device_info>());
                ma_ex_device_info deviceInfo = Marshal.PtrToStructure<ma_ex_device_info>(elementPtr);
                devices[i] = new DeviceInfo(deviceInfo.pName, deviceInfo.index, deviceInfo.isDefault > 0 ? true : false, deviceInfo.nativeDataFormats, deviceInfo.nativeDataFormatCount);
            }

            MiniAudioExNative.ma_ex_playback_devices_free(pDevices, count);

            return devices;
        }

        internal static void Add(AudioClip clip)
        {
            if (clip.Hash == 0)
                return;

            if (clip.Handle == IntPtr.Zero)
                return;

            if (audioClipHandles.ContainsKey(clip.Hash))
                return;

            audioClipHandles.Add(clip.Hash, clip.Handle);
        }

        internal static void Remove(AudioClip clip)
        {
            if (clip.Hash == 0)
                return;

            if (audioClipHandles.ContainsKey(clip.Hash))
            {
                IntPtr handle = audioClipHandles[clip.Hash];
                if (handle != IntPtr.Zero)
                    Marshal.FreeHGlobal(handle);
                audioClipHandles.Remove(clip.Hash);
            }
        }

        internal static bool GetAudioClipHandle(UInt64 hashcode, out IntPtr handle)
        {
            handle = IntPtr.Zero;

            if (audioClipHandles.ContainsKey(hashcode))
            {
                handle = audioClipHandles[hashcode];
                return true;
            }

            return false;
        }

        private static void OnDeviceDataProc(ma_device_ptr pDevice, IntPtr pOutput, IntPtr pInput, UInt32 frameCount)
        {
            IntPtr pEngine = MiniAudioExNative.ma_ex_device_get_user_data(pDevice.pointer);
            MiniAudioExNative.ma_engine_read_pcm_frames(pEngine, pOutput, frameCount, out _);

            NativeArray<float> buffer = new NativeArray<float>(pOutput, (Int32)(frameCount * channels));

            if (DataProcess != null)
            {
                DataProcess.Invoke(buffer, frameCount);
            }

            outputBuffer.Write(buffer);
        }

        public static bool GetOutputBuffer(ref float[] buffer, out int length)
        {
            length = outputBuffer.Read(ref buffer);
            return length > 0;
        }
    }

    public struct Vector3f
    {
        public float x;
        public float y;
        public float z;

        /// <summary>
        /// Gets the length of the vector.
        /// </summary>
        public float Length
        {
            get
            {
                return (float)Math.Sqrt(x * x + y * y + z * z);
            }
        }

        /// <summary>
        /// Gets the squared length of the vector.
        /// </summary>
        public float LengthSquared
        {
            get
            {
                return x * x + y * y + z * z;
            }
        }

        /// <summary>
        /// Returns a vector with components set to zero.
        /// </summary>
        public static Vector3f Zero
        {
            get
            {
                return new Vector3f(0, 0, 0);
            }
        }

        /// <summary>
        /// Returns a vector with all components set to one.
        /// </summary>
        public static Vector3f One
        {
            get
            {
                return new Vector3f(1, 1, 1);
            }
        }

        /// <summary>
        /// Gets the unit vector along the X-axis.
        /// </summary>
        public static Vector3f UnitX
        {
            get
            {
                return new Vector3f(1, 0, 0);
            }
        }

        /// <summary>
        /// Gets the unit vector along the Y-axis.
        /// </summary>
        public static Vector3f UnitY
        {
            get
            {
                return new Vector3f(0, 1, 0);
            }
        }

        /// <summary>
        /// Gets the unit vector along the Z-axis.
        /// </summary>
        public static Vector3f UnitZ
        {
            get
            {
                return new Vector3f(0, 0, 1);
            }
        }

        /// <summary>
        /// Constructs a new Vector3f with the specified components.
        /// </summary>
        public Vector3f(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// Normalizes this vector.
        /// </summary>
        public void Normalize()
        {
            float scale = 1.0f / Length;
            x *= scale;
            y *= scale;
            z *= scale;
        }

        /// <summary>
        /// Returns the normalized vector of the input vector.
        /// </summary>
        public static Vector3f Normalize(Vector3f v)
        {
            float scale = 1.0f / v.Length;
            v.x = v.x *= scale;
            v.y = v.y *= scale;
            v.z = v.z *= scale;
            return v;
        }

        /// <summary>
        /// Calculates the distance between two vectors.
        /// </summary>
        public static float Distance(Vector3f a, Vector3f b)
        {
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float dz = b.z - a.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Calculates the squared distance between two vectors.
        /// </summary>
        public static float DistanceSquared(Vector3f a, Vector3f b)
        {
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            float dz = b.z - a.z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// Calculates the dot product of two vectors.
        /// </summary>
        public static float Dot(Vector3f a, Vector3f b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        /// <summary>
        /// Calculates the cross product of two vectors.
        /// </summary>
        public static Vector3f Cross(Vector3f a, Vector3f b)
        {
            float x = a.y * b.z - a.z * b.y;
            float y = a.z * b.x - a.x * b.z;
            float z = a.x * b.y - a.y * b.x;
            return new Vector3f(x, y, z);
        }

        /// <summary>
        /// Performs linear interpolation between two vectors.
        /// </summary>
        public static Vector3f Lerp(Vector3f a, Vector3f b, float t)
        {
            float x = a.x + (b.x - a.x) * t;
            float y = a.y + (b.y - a.y) * t;
            float z = a.z + (b.z - a.z) * t;
            return new Vector3f(x, y, z);
        }

        // Helper method for clamping values between min and max.
        private static float Clamp(float n, float min, float max)
        {
            return Math.Max(Math.Min(n, max), min);
        }

        /// <summary>
        /// Calculates the angle between two vectors in radians.
        /// </summary>
        public static float Angle(Vector3f a, Vector3f b)
        {
            float temp = Dot(a, b);
            return (float)Math.Acos(Clamp(temp / (a.Length * b.Length), -1.0f, 1.0f));
        }

        public static Vector3f operator +(Vector3f a, Vector3f b)
        {
            return new Vector3f(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Vector3f operator -(Vector3f a, Vector3f b)
        {
            return new Vector3f(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Vector3f operator -(Vector3f a)
        {
            return new Vector3f(-a.x, -a.y, -a.z);
        }

        public static Vector3f operator *(Vector3f a, float scalar)
        {
            return new Vector3f(a.x * scalar, a.y * scalar, a.z * scalar);
        }

        public static Vector3f operator *(float scalar, Vector3f a)
        {
            return a * scalar;
        }

        public static Vector3f operator /(Vector3f a, float scalar)
        {
            a.x /= scalar;
            a.y /= scalar;
            a.z /= scalar;
            return a;
        }

        public static bool operator ==(Vector3f a, Vector3f b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static bool operator !=(Vector3f a, Vector3f b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Vector3f))
                return false;

            Vector3f other = (Vector3f)obj;
            return this == other;
        }

        public override string ToString()
        {
            return "(" + x + "," + y + "," + z + ")";
        }

        public override int GetHashCode()
        {
            int hash = 42;
            hash = hash ^ x.GetHashCode();
            hash = hash ^ y.GetHashCode();
            hash = hash ^ z.GetHashCode();
            return hash;
        }
    }

    public enum AttenuationModel
    {
        None,
        Inverse,
        Linear,
        Exponential
    }

    public enum PanMode
    {
        Balance,
        Pan
    }

    public struct DeviceDataFormat
    {
        public ma_format format;       /* Sample format. If set to ma_format_unknown, all sample formats are supported. */
        public UInt32 channels;     /* If set to 0, all channels are supported. */
        public UInt32 sampleRate;   /* If set to 0, all sample rates are supported. */
        public UInt32 flags;        /* A combination of MA_DATA_FORMAT_FLAG_* flags. */
    }

    public sealed class DeviceInfo
    {
        private string name;
        private Int32 index;
        private bool isDefault;
        private DeviceDataFormat[] formats;

        public string Name
        {
            get => name;
        }

        public Int32 Index
        {
            get => index;
        }

        public bool IsDefault
        {
            get => isDefault;
        }

        public DeviceDataFormat[] Formats
        {
            get => formats;
        }

        public DeviceInfo(IntPtr pName, Int32 index, bool isDefault, IntPtr pFormats, UInt32 formatCount)
        {
            if (pName != IntPtr.Zero)
                name = Marshal.PtrToStringAnsi(pName);
            else
                name = string.Empty;

            this.index = index;
            this.isDefault = isDefault;

            formats = (formatCount > 0 && pFormats != IntPtr.Zero) ? new DeviceDataFormat[formatCount] : null;

            if (formats != null)
            {
                for (int i = 0; i < formats.Length; i++)
                {
                    IntPtr elementPtr = IntPtr.Add(pFormats, i * Marshal.SizeOf<ma_ex_native_data_format>());
                    ma_ex_native_data_format f = Marshal.PtrToStructure<ma_ex_native_data_format>(elementPtr);
                    formats[i] = new DeviceDataFormat();
                    formats[i].channels = f.channels;
                    formats[i].flags = f.flags;
                    formats[i].format = f.format;
                    formats[i].sampleRate = f.sampleRate;
                }
            }
        }
    }

    public sealed class ConcurrentList<T>
    {
        private readonly List<T> items;
        private readonly object syncRoot = new object();

        public int Count
        {
            get
            {
                lock (syncRoot)
                {
                    return items.Count;
                }
            }
        }

        public T this[int index]
        {
            get
            {
                lock (syncRoot)
                {
                    return items[index];
                }
            }
            set
            {
                lock (syncRoot)
                {
                    items[index] = value;
                }
            }
        }

        public ConcurrentList()
        {
            this.items = new List<T>();
        }

        public void Clear()
        {
            lock (syncRoot)
            {
                items.Clear();
            }
        }

        public void Add(T item)
        {
            lock (syncRoot)
            {
                items.Add(item);
            }
        }

        public void Remove(T item)
        {
            lock (syncRoot)
            {
                items.Remove(item);
            }
        }

        public void Remove(List<T> items)
        {
            lock (syncRoot)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    this.items.Remove(items[i]);
                }
            }
        }

        public void RemoveAt(int index)
        {
            lock (syncRoot)
            {
                items.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// A thread safe class storing audio data.
    /// </summary>
    public sealed class AudioBuffer
    {
        private readonly float[] buffer;
        private readonly object sync = new();
        private int currentLength = 0;

        public AudioBuffer(int capacityPowerOfTwo)
        {
            if (capacityPowerOfTwo <= 0 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
                throw new ArgumentException("capacityPowerOfTwo must be power of two");
            int capacity = capacityPowerOfTwo;
            buffer = new float[capacity];
        }

        public int Write(NativeArray<float> src)
        {
            lock (sync)
            {
                unsafe
                {
                    fixed (float* pBuffer = &buffer[0])
                    {
                        NativeArray<float> b = new NativeArray<float>(pBuffer, src.Length);
                        src.CopyTo(b);
                        currentLength = src.Length;
                    }

                }
                return src.Length;
            }
        }

        public int Read(ref float[] output)
        {
            lock (sync)
            {
                unsafe
                {
                    if (output?.Length < buffer.Length)
                        output = new float[buffer.Length];

                    fixed (float* pSrc = &buffer[0], pDst = &output[0])
                    {
                        NativeArray<float> src = new NativeArray<float>(pSrc, buffer.Length);
                        NativeArray<float> dst = new NativeArray<float>(pDst, buffer.Length);
                        src.CopyTo(dst);
                        return currentLength;
                    }
                }
            }
        }
    }
}
