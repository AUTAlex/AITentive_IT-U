using System;
using Unity.Collections;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Rendering;

public class AsyncRenderTextureSensorComponent : SensorComponent
{
    [Header("Source")]
    public RenderTexture RenderTexture;

    [Header("Observation")]
    public string SensorName = "AsyncRT";
    public bool grayscale = false;     // keep false if color matters
    public bool normalize01 = true;    // outputs 0..1 floats
    [Range(2, 8)] public int ringSize = 3;

    [Header("Behavior")]
    public bool reuseLastFrameIfNotReady = true;
    public bool logWarnings = true;

    AsyncRenderTextureSensor _sensor;

    public override ISensor[] CreateSensors()
    {
        if (RenderTexture == null)
            throw new InvalidOperationException($"{nameof(AsyncRenderTextureSensorComponent)}: source is null");

        if (RenderTexture.antiAliasing != 1 && logWarnings)
            Debug.LogWarning($"[{SensorName}] RenderTexture antiAliasing is {RenderTexture.antiAliasing}. Prefer 1 (None) to avoid extra resolves/stalls.");

        _sensor = new AsyncRenderTextureSensor(
            RenderTexture,
            string.IsNullOrEmpty(SensorName) ? name : SensorName,
            grayscale,
            normalize01,
            ringSize,
            reuseLastFrameIfNotReady,
            logWarnings
        );

        return new ISensor[] { _sensor };
    }

    void OnDisable()
    {
        _sensor?.Dispose();
        _sensor = null;
    }

    private sealed class AsyncRenderTextureSensor : ISensor, IDisposable
    {
        readonly RenderTexture _rt;
        readonly string _name;
        readonly bool _grayscale;
        readonly bool _normalize01;
        readonly bool _reuseLast;
        readonly bool _logWarnings;

        readonly int _w, _h, _c;
        readonly ObservationSpec _spec;

        AsyncGPUReadbackRequest[] _req;
        NativeArray<byte>[] _cpuBytes;
        int _head;
        bool _hasValidFrame;

        float[] _lastFrame; // CHW

        public AsyncRenderTextureSensor(
            RenderTexture rt,
            string name,
            bool grayscale,
            bool normalize01,
            int ringSize,
            bool reuseLast,
            bool logWarnings)
        {
            _rt = rt;
            _name = name;
            _grayscale = grayscale;
            _normalize01 = normalize01;
            _reuseLast = reuseLast;
            _logWarnings = logWarnings;

            _w = _rt.width;
            _h = _rt.height;
            _c = _grayscale ? 1 : 3;

            // ML-Agents expects Visual(height, width, channels)
            _spec = ObservationSpec.Visual(_h, _w, _c);

            ringSize = Mathf.Clamp(ringSize, 2, 8);
            _req = new AsyncGPUReadbackRequest[ringSize];
            _cpuBytes = new NativeArray<byte>[ringSize];

            int byteCount = _w * _h * 4; // RGBA32
            for (int i = 0; i < ringSize; i++)
                _cpuBytes[i] = new NativeArray<byte>(byteCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            _lastFrame = new float[_w * _h * _c];

            // prime pipeline
            KickRequest();
            KickRequest();
        }

        public string GetName() => _name;

        public ObservationSpec GetObservationSpec() => _spec;

        public int Write(ObservationWriter writer)
        {
            // poll oldest request slot (roughly)
            int tail = (_head + 1) % _req.Length;

            if (_req[tail].done)
            {
                if (_req[tail].hasError)
                {
                    if (_logWarnings)
                        Debug.LogWarning($"[{_name}] AsyncGPUReadback hadError=true. Reusing last frame.");
                }
                else
                {
                    ConvertRGBABytesToCHW(_cpuBytes[tail], _lastFrame, _w, _h, _c, _grayscale, _normalize01);
                    _hasValidFrame = true;
                }

                // re-issue into this slot
                KickRequest(tail);
            }

            if (!_hasValidFrame && !_reuseLast)
            {
                // write zeros (rarely useful; reuseLast=true is usually what you want)
                int n0 = _w * _h * _c;
                for (int i = 0; i < n0; i++) writer[i] = 0f;
                return n0;
            }

            // write last available frame
            int n = _lastFrame.Length;
            for (int i = 0; i < n; i++) writer[i] = _lastFrame[i];
            return n;
        }

        public byte[] GetCompressedObservation() => null;

        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public void Update() { }

        public void Reset() { }

        void KickRequest(int slot = -1)
        {
            if (slot < 0)
            {
                slot = _head;
                _head = (_head + 1) % _req.Length;
            }

            // Read back RT into a preallocated CPU buffer.
            // TextureFormat.RGBA32 is the common denominator; your RT must be compatible.
            _req[slot] = AsyncGPUReadback.RequestIntoNativeArray(
                ref _cpuBytes[slot],
                _rt,
                0,
                TextureFormat.RGBA32
            );
        }

        static void ConvertRGBABytesToCHW(
            NativeArray<byte> rgba, float[] dstCHW, int w, int h, int c,
            bool grayscale, bool normalize01)
        {
            float scale = normalize01 ? (1f / 255f) : 1f;
            int pixels = w * h;

            if (grayscale)
            {
                for (int p = 0; p < pixels; p++)
                {
                    int si = p * 4;
                    float r = rgba[si + 0] * scale;
                    float g = rgba[si + 1] * scale;
                    float b = rgba[si + 2] * scale;
                    dstCHW[p] = 0.299f * r + 0.587f * g + 0.114f * b;
                }
            }
            else
            {
                int plane = pixels;
                for (int p = 0; p < pixels; p++)
                {
                    int si = p * 4;
                    dstCHW[p + 0 * plane] = rgba[si + 0] * scale;
                    dstCHW[p + 1 * plane] = rgba[si + 1] * scale;
                    dstCHW[p + 2 * plane] = rgba[si + 2] * scale;
                }
            }
        }

        public void Dispose()
        {
            if (_cpuBytes != null)
            {
                for (int i = 0; i < _cpuBytes.Length; i++)
                    if (_cpuBytes[i].IsCreated) _cpuBytes[i].Dispose();
            }

            _cpuBytes = null;
            _req = null;
            _lastFrame = null;
        }
    }
}