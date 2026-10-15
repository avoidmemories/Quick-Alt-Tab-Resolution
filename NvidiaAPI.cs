using System.Runtime.InteropServices;

namespace Quick_Alt_Tab
{
    public static unsafe class NvidiaAPI
    {
        private const string NvApiDll = "nvapi64.dll";
        private const int NvapiMaxPhysicalGpus = 64;
        private const uint NvFormatA8R8G8B8 = 21;
        private const uint NvTimingOverrideCurrent = 0;
        private const uint NvTimingOverrideAuto = 1;
        private const uint NvTimingOverrideCvt = 4;
        private const uint NvTimingOverrideCvtReducedBlanking = 5;
        private const uint NvTimingOverrideGtf = 6;
        private const uint NvTimingScalingDefault = 1;

        private const uint NvApiInitializeId = 0x0150e828;
        private const uint NvApiGetErrorMessageId = 0x6c2d048c;
        private const uint NvApiEnumPhysicalGpusId = 0xe5ac921f;
        private const uint NvApiGpuGetConnectedDisplayIdsId = 0x0078dba2;
        private const uint NvApiDispGetGdiPrimaryDisplayId = 0x1e9d8a31;
        private const uint NvApiDispGetTimingId = 0x175167e9;
        private const uint NvApiDispEnumCustomDisplayId = 0xa2072d59;
        private const uint NvApiDispTryCustomDisplayId = 0x1f7db630;
        private const uint NvApiDispDeleteCustomDisplayId = 0x552e5b9b;
        private const uint NvApiDispSaveCustomDisplayId = 0x49882876;
        private const uint NvApiDispRevertCustomDisplayTrialId = 0xcbbd40f0;

        public enum NvAPI_Status
        {
            NVAPI_OK = 0,
            NVAPI_ERROR = -1,
            NVAPI_LIBRARY_NOT_FOUND = -2,
            NVAPI_NO_IMPLEMENTATION = -3,
            NVAPI_API_NOT_INITIALIZED = -4,
            NVAPI_INVALID_ARGUMENT = -5,
            NVAPI_NVIDIA_DEVICE_NOT_FOUND = -6,
            NVAPI_END_ENUMERATION = -7,
            NVAPI_INCOMPATIBLE_STRUCT_VERSION = -9,
            NVAPI_HANDLE_INVALIDATED = -10,
            NVAPI_INVALID_DISPLAY_ID = -18,
            NVAPI_EXPECTED_PHYSICAL_GPU_HANDLE = -100,
            NVAPI_INSUFFICIENT_BUFFER = -174,
            NVAPI_ACCESS_DENIED = -175
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr NvApiQueryInterfaceDelegate(uint offset);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiInitializeDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiGetErrorMessageDelegate(NvAPI_Status status, IntPtr message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiEnumPhysicalGpusDelegate(IntPtr[] gpuHandles, ref uint gpuCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiGpuGetConnectedDisplayIdsDelegate(
            IntPtr gpuHandle,
            IntPtr displayIds,
            ref uint displayIdCount,
            uint flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiDispGetGdiPrimaryDisplayIdDelegate(ref uint displayId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiDispGetTimingDelegate(
            uint displayId,
            ref NV_TIMING_INPUT timingInput,
            out NV_TIMING timing);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiDispTryCustomDisplayDelegate(
            uint[] displayIds,
            uint count,
            NV_CUSTOM_DISPLAY[] customDisplays);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiDispEnumCustomDisplayDelegate(
            uint displayId,
            uint index,
            ref NV_CUSTOM_DISPLAY customDisplay);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiDispDeleteCustomDisplayDelegate(
            uint[] displayIds,
            uint count,
            ref NV_CUSTOM_DISPLAY customDisplay);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiDispSaveCustomDisplayDelegate(
            uint[] displayIds,
            uint count,
            uint isThisOutputIdOnly,
            uint isThisMonitorIdOnly);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate NvAPI_Status NvApiDispRevertCustomDisplayTrialDelegate(uint[] displayIds, uint count);

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_GPU_DISPLAYIDS
        {
            public uint version;
            public int connectorType;
            public uint displayId;
            public uint flags;

            public readonly bool IsMultiStreamRootNode => (flags & (1u << 1)) != 0;
            public readonly bool IsActive => (flags & (1u << 2)) != 0;
            public readonly bool IsConnected => (flags & (1u << 6)) != 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_VIEWPORTF
        {
            public float x;
            public float y;
            public float w;
            public float h;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private unsafe struct NV_TIMINGEXT
        {
            public uint flag;
            public ushort rr;
            public uint rrx1k;
            public uint aspect;
            public ushort rep;
            public uint status;
            public fixed byte name[40];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_TIMING
        {
            public ushort HVisible;
            public ushort HBorder;
            public ushort HFrontPorch;
            public ushort HSyncWidth;
            public ushort HTotal;
            public byte HSyncPol;
            public ushort VVisible;
            public ushort VBorder;
            public ushort VFrontPorch;
            public ushort VSyncWidth;
            public ushort VTotal;
            public byte VSyncPol;
            public ushort interlaced;
            public uint pclk;
            public NV_TIMINGEXT etc;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_TIMING_FLAG
        {
            public uint interlacedBits;
            public uint formatBits;
            public uint scalingBits;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_TIMING_INPUT
        {
            public uint version;
            public uint width;
            public uint height;
            public float rr;
            public NV_TIMING_FLAG flag;
            public uint type;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_CUSTOM_DISPLAY
        {
            public uint version;
            public uint width;
            public uint height;
            public uint depth;
            public uint colorFormat;
            public NV_VIEWPORTF srcPartition;
            public float xRatio;
            public float yRatio;
            public NV_TIMING timing;
            public uint flags;
        }

        private static IntPtr nvApiHandle = IntPtr.Zero;
        private static bool isInitialized;

        private static NvApiGetErrorMessageDelegate? nvApiGetErrorMessage;
        private static NvApiEnumPhysicalGpusDelegate? nvApiEnumPhysicalGpus;
        private static NvApiGpuGetConnectedDisplayIdsDelegate? nvApiGpuGetConnectedDisplayIds;
        private static NvApiDispGetGdiPrimaryDisplayIdDelegate? nvApiDispGetGdiPrimaryDisplayId;
        private static NvApiDispGetTimingDelegate? nvApiDispGetTiming;
        private static NvApiDispEnumCustomDisplayDelegate? nvApiDispEnumCustomDisplay;
        private static NvApiDispTryCustomDisplayDelegate? nvApiDispTryCustomDisplay;
        private static NvApiDispDeleteCustomDisplayDelegate? nvApiDispDeleteCustomDisplay;
        private static NvApiDispSaveCustomDisplayDelegate? nvApiDispSaveCustomDisplay;
        private static NvApiDispRevertCustomDisplayTrialDelegate? nvApiDispRevertCustomDisplayTrial;
        private static uint[]? currentTrialDisplayIds;
        private static uint? selectedDisplayId;

        public static string LastErrorMessage { get; private set; } = string.Empty;

        public readonly record struct CustomDisplayMode(int Width, int Height, uint RefreshX1K);
        public readonly record struct DisplayInfo(uint DisplayId, bool IsActive, bool IsConnected);

        public static bool Initialize()
        {
            if (isInitialized)
            {
                return true;
            }

            Logger.Log("Initializing NVAPI...");
            nvApiHandle = LoadLibrary(NvApiDll);
            if (nvApiHandle == IntPtr.Zero)
            {
                Logger.Error($"{NvApiDll} was not found. NVIDIA driver may be missing.");
                return false;
            }

            IntPtr queryPtr = GetProcAddress(nvApiHandle, "nvapi_QueryInterface");
            if (queryPtr == IntPtr.Zero)
            {
                Logger.Error("nvapi_QueryInterface was not found in nvapi64.dll.");
                return false;
            }

            var query = Marshal.GetDelegateForFunctionPointer<NvApiQueryInterfaceDelegate>(queryPtr);

            var initialize = GetRequiredDelegate<NvApiInitializeDelegate>(query, NvApiInitializeId, "NvAPI_Initialize");
            nvApiGetErrorMessage = GetRequiredDelegate<NvApiGetErrorMessageDelegate>(query, NvApiGetErrorMessageId, "NvAPI_GetErrorMessage");
            nvApiEnumPhysicalGpus = GetRequiredDelegate<NvApiEnumPhysicalGpusDelegate>(query, NvApiEnumPhysicalGpusId, "NvAPI_EnumPhysicalGPUs");
            nvApiGpuGetConnectedDisplayIds = GetRequiredDelegate<NvApiGpuGetConnectedDisplayIdsDelegate>(query, NvApiGpuGetConnectedDisplayIdsId, "NvAPI_GPU_GetConnectedDisplayIds");
            nvApiDispGetGdiPrimaryDisplayId = GetOptionalDelegate<NvApiDispGetGdiPrimaryDisplayIdDelegate>(query, NvApiDispGetGdiPrimaryDisplayId, "NvAPI_DISP_GetGDIPrimaryDisplayId");
            nvApiDispGetTiming = GetRequiredDelegate<NvApiDispGetTimingDelegate>(query, NvApiDispGetTimingId, "NvAPI_DISP_GetTiming");
            nvApiDispEnumCustomDisplay = GetRequiredDelegate<NvApiDispEnumCustomDisplayDelegate>(query, NvApiDispEnumCustomDisplayId, "NvAPI_DISP_EnumCustomDisplay");
            nvApiDispTryCustomDisplay = GetRequiredDelegate<NvApiDispTryCustomDisplayDelegate>(query, NvApiDispTryCustomDisplayId, "NvAPI_DISP_TryCustomDisplay");
            nvApiDispDeleteCustomDisplay = GetRequiredDelegate<NvApiDispDeleteCustomDisplayDelegate>(query, NvApiDispDeleteCustomDisplayId, "NvAPI_DISP_DeleteCustomDisplay");
            nvApiDispSaveCustomDisplay = GetRequiredDelegate<NvApiDispSaveCustomDisplayDelegate>(query, NvApiDispSaveCustomDisplayId, "NvAPI_DISP_SaveCustomDisplay");
            nvApiDispRevertCustomDisplayTrial = GetRequiredDelegate<NvApiDispRevertCustomDisplayTrialDelegate>(query, NvApiDispRevertCustomDisplayTrialId, "NvAPI_DISP_RevertCustomDisplayTrial");

            Logger.Log($"NVAPI struct sizes: NV_TIMING_FLAG={Marshal.SizeOf<NV_TIMING_FLAG>()}, NV_TIMING_INPUT={Marshal.SizeOf<NV_TIMING_INPUT>()}, NV_TIMING={Marshal.SizeOf<NV_TIMING>()}, NV_CUSTOM_DISPLAY={Marshal.SizeOf<NV_CUSTOM_DISPLAY>()}.");

            NvAPI_Status status = initialize();
            if (status != NvAPI_Status.NVAPI_OK)
            {
                Logger.Error($"NvAPI_Initialize failed: {FormatStatus(status)}");
                return false;
            }

            isInitialized = true;
            Logger.Log("NVAPI initialized successfully.");
            return true;
        }

        public static bool CreateCustomResolution(int width, int height, double refreshRate)
        {
            Logger.Log("CreateCustomResolution called in safe mode. Starting trial only; immediate save is disabled.");
            if (!BeginCustomResolutionTrial(width, height, refreshRate))
            {
                return false;
            }

            LastErrorMessage = "The custom display was only tested, not saved. Use SaveCurrentTrial() after visible confirmation.";
            RevertCurrentTrial();
            return false;
        }

        public static bool BeginCustomResolutionTrial(int width, int height, double refreshRate)
        {
            return BeginCustomResolutionTrial(width, height, width, height, refreshRate);
        }

        public static bool BeginCustomResolutionTrial(int sourceWidth, int sourceHeight, int outputWidth, int outputHeight, double refreshRate)
        {
            try
            {
                LastErrorMessage = string.Empty;
                RevertCurrentTrial();
                EnsureInitialized();

                uint displayId = GetSelectedNvidiaDisplayId();
                Logger.Log($"Using NVIDIA displayId 0x{displayId:X8}.");

                var displayIds = new[] { displayId };
                var customDisplay = BuildCustomDisplay(sourceWidth, sourceHeight);
                NvAPI_Status status = TryBuildGpuScaledTiming(displayId, outputWidth, outputHeight, refreshRate, out customDisplay.timing);
                ThrowIfFailed(status, $"NvAPI_DISP_GetTiming failed for output {outputWidth}x{outputHeight} @ {refreshRate:F3} Hz");

                customDisplay.timing.etc.rr = (ushort)Math.Round(refreshRate);
                customDisplay.timing.etc.rrx1k = (uint)Math.Round(refreshRate * 1000.0);

                var customDisplays = new[] { customDisplay };
                Logger.Log($"Trying custom display source {sourceWidth}x{sourceHeight}, output timing {customDisplay.timing.HVisible}x{customDisplay.timing.VVisible} @ {refreshRate:F3} Hz...");
                status = nvApiDispTryCustomDisplay!(displayIds, 1, customDisplays);
                ThrowIfFailed(status, $"NvAPI_DISP_TryCustomDisplay failed for source {sourceWidth}x{sourceHeight} @ {refreshRate:F3} Hz");

                currentTrialDisplayIds = displayIds;
                Logger.Log($"Custom resolution trial is active: source {sourceWidth}x{sourceHeight} @ {refreshRate:F3} Hz. It has not been saved.");
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                Logger.Error($"Failed to start custom resolution trial source {sourceWidth}x{sourceHeight} @ {refreshRate:F3} Hz", ex);
                return false;
            }
        }

        public static bool SaveCurrentTrial(bool revertAfterSave = true)
        {
            try
            {
                LastErrorMessage = string.Empty;
                if (currentTrialDisplayIds == null || currentTrialDisplayIds.Length == 0)
                {
                    LastErrorMessage = "No active custom display trial to save.";
                    return false;
                }

                NvAPI_Status status = nvApiDispSaveCustomDisplay!(currentTrialDisplayIds, (uint)currentTrialDisplayIds.Length, 1, 1);
                ThrowIfFailed(status, "NvAPI_DISP_SaveCustomDisplay failed");

                if (revertAfterSave)
                {
                    RevertTrial(currentTrialDisplayIds);
                }

                currentTrialDisplayIds = null;
                Logger.Log(revertAfterSave
                    ? "Current custom display trial saved successfully and reverted to the previous display mode."
                    : "Current custom display trial saved successfully and left active.");
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                Logger.Error("Failed to save current custom display trial.", ex);
                return false;
            }
        }

        public static int DeleteSavedCustomDisplays(IEnumerable<CustomDisplayMode> targetModes)
        {
            try
            {
                LastErrorMessage = string.Empty;
                EnsureInitialized();

                var targets = targetModes
                    .Where(mode => mode.Width > 0 && mode.Height > 0 && mode.RefreshX1K > 0)
                    .Distinct()
                    .ToArray();
                if (targets.Length == 0)
                {
                    Logger.Log("No saved custom display targets were provided for deletion.");
                    return 0;
                }

                uint displayId = GetSelectedNvidiaDisplayId();
                var displayIds = new[] { displayId };
                int deleted = 0;
                bool deletedInPass;

                do
                {
                    deletedInPass = false;

                    for (uint index = 0; ; index++)
                    {
                        var customDisplay = new NV_CUSTOM_DISPLAY
                        {
                            version = MakeNvApiVersion<NV_CUSTOM_DISPLAY>(1)
                        };

                        NvAPI_Status status = nvApiDispEnumCustomDisplay!(displayId, index, ref customDisplay);
                        if (status == NvAPI_Status.NVAPI_END_ENUMERATION)
                        {
                            break;
                        }

                        ThrowIfFailed(status, $"NvAPI_DISP_EnumCustomDisplay failed at index {index}");

                        if (!MatchesAnyTarget(customDisplay, targets))
                        {
                            continue;
                        }

                        status = nvApiDispDeleteCustomDisplay!(displayIds, 1, ref customDisplay);
                        ThrowIfFailed(status, $"NvAPI_DISP_DeleteCustomDisplay failed for {customDisplay.width}x{customDisplay.height}");
                        deleted++;
                        deletedInPass = true;
                        Logger.Log($"Deleted saved custom display {customDisplay.width}x{customDisplay.height} @ {customDisplay.timing.etc.rrx1k / 1000.0:F3} Hz.");
                        break;
                    }
                }
                while (deletedInPass);

                Logger.Log($"Deleted {deleted} matching saved custom display preset(s).");
                return deleted;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                Logger.Error("Failed to delete saved custom display presets.", ex);
                return -1;
            }
        }

        public static List<CustomDisplayMode> EnumSavedCustomDisplays()
        {
            var result = new List<CustomDisplayMode>();

            try
            {
                LastErrorMessage = string.Empty;
                EnsureInitialized();

                uint displayId = GetSelectedNvidiaDisplayId();
                for (uint index = 0; ; index++)
                {
                    var customDisplay = new NV_CUSTOM_DISPLAY
                    {
                        version = MakeNvApiVersion<NV_CUSTOM_DISPLAY>(1)
                    };

                    NvAPI_Status status = nvApiDispEnumCustomDisplay!(displayId, index, ref customDisplay);
                    if (status == NvAPI_Status.NVAPI_END_ENUMERATION)
                    {
                        break;
                    }

                    ThrowIfFailed(status, $"NvAPI_DISP_EnumCustomDisplay failed at index {index}");

                    uint refreshX1K = customDisplay.timing.etc.rrx1k;
                    if (refreshX1K == 0 && customDisplay.timing.etc.rr > 0)
                    {
                        refreshX1K = customDisplay.timing.etc.rr * 1000u;
                    }

                    if (customDisplay.width > 0 && customDisplay.height > 0 && refreshX1K > 0)
                    {
                        result.Add(new CustomDisplayMode((int)customDisplay.width, (int)customDisplay.height, refreshX1K));
                    }
                }
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                Logger.Error("Failed to enumerate saved custom display presets.", ex);
            }

            return result
                .Distinct()
                .OrderByDescending(mode => mode.Width * mode.Height)
                .ThenBy(mode => mode.Width)
                .ThenBy(mode => mode.Height)
                .ThenBy(mode => mode.RefreshX1K)
                .ToList();
        }

        public static void RevertCurrentTrial()
        {
            if (currentTrialDisplayIds == null || currentTrialDisplayIds.Length == 0)
            {
                return;
            }

            RevertTrial(currentTrialDisplayIds);
            currentTrialDisplayIds = null;
        }

        private static bool MatchesAnyTarget(NV_CUSTOM_DISPLAY customDisplay, CustomDisplayMode[] targets)
        {
            foreach (var target in targets)
            {
                if (customDisplay.width != target.Width || customDisplay.height != target.Height)
                {
                    continue;
                }

                uint refreshX1K = customDisplay.timing.etc.rrx1k;
                if (refreshX1K == 0 && customDisplay.timing.etc.rr > 0)
                {
                    refreshX1K = customDisplay.timing.etc.rr * 1000u;
                }

                if (refreshX1K > 0 && Math.Abs((long)refreshX1K - target.RefreshX1K) <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsNvidiaGPUPresent()
        {
            try
            {
                if (!Initialize())
                {
                    return false;
                }

                uint displayId = GetPrimaryNvidiaDisplayId();
                Logger.Log($"NVIDIA GPU/display detected. Primary displayId: 0x{displayId:X8}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("NVIDIA GPU/display detection failed.", ex);
                return false;
            }
        }

        private static T GetRequiredDelegate<T>(NvApiQueryInterfaceDelegate query, uint id, string name) where T : Delegate
        {
            IntPtr pointer = query(id);
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException($"{name} is not available in this NVIDIA driver.");
            }

            return Marshal.GetDelegateForFunctionPointer<T>(pointer);
        }

        private static T? GetOptionalDelegate<T>(NvApiQueryInterfaceDelegate query, uint id, string name) where T : Delegate
        {
            IntPtr pointer = query(id);
            if (pointer == IntPtr.Zero)
            {
                Logger.Log($"{name} is not available; falling back to first active display.");
                return null;
            }

            return Marshal.GetDelegateForFunctionPointer<T>(pointer);
        }

        private static void EnsureInitialized()
        {
            if (!Initialize())
            {
                throw new InvalidOperationException("Failed to initialize NVIDIA API.");
            }
        }

        public static List<DisplayInfo> GetNvidiaDisplays()
        {
            try
            {
                LastErrorMessage = string.Empty;
                EnsureInitialized();
                return GetConnectedDisplayIds()
                    .Where(display => !display.IsMultiStreamRootNode)
                    .Select(display => new DisplayInfo(display.displayId, display.IsActive, display.IsConnected))
                    .ToList();
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                Logger.Error("Failed to enumerate NVIDIA displays.", ex);
                return [];
            }
        }

        public static void SetSelectedDisplayId(uint displayId)
        {
            selectedDisplayId = displayId;
            Logger.Log($"Selected NVIDIA displayId 0x{displayId:X8}.");
        }

        private static uint GetSelectedNvidiaDisplayId()
        {
            var displayIds = GetConnectedDisplayIds();
            if (selectedDisplayId.HasValue)
            {
                foreach (var displayId in displayIds)
                {
                    if (displayId.displayId == selectedDisplayId.Value &&
                        displayId.IsConnected &&
                        !displayId.IsMultiStreamRootNode)
                    {
                        Logger.Log($"Using selected NVIDIA displayId 0x{selectedDisplayId.Value:X8}.");
                        return selectedDisplayId.Value;
                    }
                }

                Logger.Log($"Selected NVIDIA displayId 0x{selectedDisplayId.Value:X8} is unavailable. Falling back to primary display.");
                selectedDisplayId = null;
            }

            return GetPrimaryNvidiaDisplayId(displayIds);
        }

        private static uint GetPrimaryNvidiaDisplayId()
        {
            return GetPrimaryNvidiaDisplayId(GetConnectedDisplayIds());
        }

        private static uint GetPrimaryNvidiaDisplayId(List<NV_GPU_DISPLAYIDS> displayIds)
        {
            uint gdiPrimaryDisplayId = 0;
            if (nvApiDispGetGdiPrimaryDisplayId != null)
            {
                NvAPI_Status status = nvApiDispGetGdiPrimaryDisplayId(ref gdiPrimaryDisplayId);
                if (status == NvAPI_Status.NVAPI_OK)
                {
                    foreach (var displayId in displayIds)
                    {
                        if (displayId.displayId == gdiPrimaryDisplayId && !displayId.IsMultiStreamRootNode)
                        {
                            Logger.Log($"Using GDI primary NVIDIA displayId 0x{gdiPrimaryDisplayId:X8}.");
                            return gdiPrimaryDisplayId;
                        }
                    }

                    Logger.Log($"GDI primary displayId 0x{gdiPrimaryDisplayId:X8} is not in connected NVIDIA display list.");
                }
                else
                {
                    Logger.Log($"NvAPI_DISP_GetGDIPrimaryDisplayId returned {FormatStatus(status)}.");
                }
            }

            uint? firstConnected = null;

            foreach (var displayId in displayIds)
            {
                if (displayId.IsMultiStreamRootNode)
                {
                    continue;
                }

                if (displayId.IsActive)
                {
                    return displayId.displayId;
                }

                firstConnected ??= displayId.displayId;
            }

            if (firstConnected.HasValue)
            {
                return firstConnected.Value;
            }

            throw new InvalidOperationException("No connected NVIDIA displays were found.");
        }

        private static List<NV_GPU_DISPLAYIDS> GetConnectedDisplayIds()
        {
            EnsureDelegates();

            var gpuHandles = new IntPtr[NvapiMaxPhysicalGpus];
            uint gpuCount = 0;
            NvAPI_Status status = nvApiEnumPhysicalGpus!(gpuHandles, ref gpuCount);
            ThrowIfFailed(status, "NvAPI_EnumPhysicalGPUs failed");

            var result = new List<NV_GPU_DISPLAYIDS>();
            for (int gpuIndex = 0; gpuIndex < gpuCount; gpuIndex++)
            {
                uint displayIdCount = 0;
                status = nvApiGpuGetConnectedDisplayIds!(gpuHandles[gpuIndex], IntPtr.Zero, ref displayIdCount, 0);
                if (status != NvAPI_Status.NVAPI_OK || displayIdCount == 0)
                {
                    continue;
                }

                int structSize = Marshal.SizeOf<NV_GPU_DISPLAYIDS>();
                IntPtr buffer = Marshal.AllocHGlobal(structSize * (int)displayIdCount);
                try
                {
                    for (int i = 0; i < displayIdCount; i++)
                    {
                        var item = new NV_GPU_DISPLAYIDS { version = MakeNvApiVersion<NV_GPU_DISPLAYIDS>(3) };
                        Marshal.StructureToPtr(item, buffer + i * structSize, false);
                    }

                    uint allocatedCount = displayIdCount;
                    status = nvApiGpuGetConnectedDisplayIds!(gpuHandles[gpuIndex], buffer, ref allocatedCount, 0);
                    ThrowIfFailed(status, "NvAPI_GPU_GetConnectedDisplayIds failed");

                    for (int i = 0; i < allocatedCount; i++)
                    {
                        var item = Marshal.PtrToStructure<NV_GPU_DISPLAYIDS>(buffer + i * structSize);
                        if (item.IsConnected)
                        {
                            result.Add(item);
                            Logger.Log($"Found displayId 0x{item.displayId:X8}, active={item.IsActive}, connected={item.IsConnected}.");
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return result;
        }

        private static NV_CUSTOM_DISPLAY BuildCustomDisplay(int width, int height)
        {
            return new NV_CUSTOM_DISPLAY
            {
                version = MakeNvApiVersion<NV_CUSTOM_DISPLAY>(1),
                width = (uint)width,
                height = (uint)height,
                depth = 32,
                colorFormat = NvFormatA8R8G8B8,
                srcPartition = new NV_VIEWPORTF { x = 0, y = 0, w = 1, h = 1 },
                xRatio = 1,
                yRatio = 1,
                flags = 0
            };
        }

        private static NvAPI_Status TryBuildTiming(uint displayId, int width, int height, double refreshRate, out NV_TIMING timing)
        {
            uint[] timingTypes =
            [
                NvTimingOverrideCvtReducedBlanking,
                NvTimingOverrideCvt,
                NvTimingOverrideAuto,
                NvTimingOverrideGtf
            ];

            NvAPI_Status lastStatus = NvAPI_Status.NVAPI_ERROR;
            foreach (uint timingType in timingTypes)
            {
                var timingInput = new NV_TIMING_INPUT
                {
                    version = MakeNvApiVersion<NV_TIMING_INPUT>(1),
                    width = (uint)width,
                    height = (uint)height,
                    rr = (float)refreshRate,
                    flag = new NV_TIMING_FLAG { scalingBits = NvTimingScalingDefault },
                    type = timingType
                };

                lastStatus = nvApiDispGetTiming!(displayId, ref timingInput, out timing);
                Logger.Log($"NvAPI_DISP_GetTiming type={timingType} returned {FormatStatus(lastStatus)}.");
                if (lastStatus == NvAPI_Status.NVAPI_OK)
                {
                    return lastStatus;
                }
            }

            timing = default;
            return lastStatus;
        }

        private static NvAPI_Status TryBuildGpuScaledTiming(uint displayId, int outputWidth, int outputHeight, double refreshRate, out NV_TIMING timing)
        {
            var currentTimingInput = new NV_TIMING_INPUT
            {
                version = MakeNvApiVersion<NV_TIMING_INPUT>(1),
                width = 0,
                height = 0,
                rr = 0,
                flag = new NV_TIMING_FLAG { scalingBits = NvTimingScalingDefault },
                type = NvTimingOverrideCurrent
            };

            NvAPI_Status status = nvApiDispGetTiming!(displayId, ref currentTimingInput, out timing);
            Logger.Log($"NvAPI_DISP_GetTiming CURRENT returned {FormatStatus(status)}.");
            uint requestedRefreshX1K = (uint)Math.Round(refreshRate * 1000.0);
            uint currentRefreshX1K = timing.etc.rrx1k;

            if (status == NvAPI_Status.NVAPI_OK &&
                timing.HVisible == outputWidth &&
                timing.VVisible == outputHeight)
            {
                Logger.Log($"Using current physical output timing {timing.HVisible}x{timing.VVisible} @ {currentRefreshX1K / 1000.0:F3} Hz, total {timing.HTotal}x{timing.VTotal}, pclk={timing.pclk}. Only logical refresh metadata will be changed to {refreshRate:F3} Hz.");
                timing.etc.rr = (ushort)Math.Round(refreshRate);
                timing.etc.rrx1k = requestedRefreshX1K;
                return status;
            }

            if (status == NvAPI_Status.NVAPI_OK)
            {
                Logger.Log($"Current output timing is {timing.HVisible}x{timing.VVisible} @ {currentRefreshX1K / 1000.0:F3} Hz; requested output is {outputWidth}x{outputHeight} @ {refreshRate:F3} Hz. Refusing to calculate a new physical timing for safety.");
            }

            timing = default;
            return status == NvAPI_Status.NVAPI_OK ? NvAPI_Status.NVAPI_INVALID_ARGUMENT : status;
        }

        private static uint MakeNvApiVersion<T>(int version) where T : struct
        {
            return (uint)(Marshal.SizeOf<T>() | (version << 16));
        }

        private static void EnsureDelegates()
        {
            if (nvApiEnumPhysicalGpus == null || nvApiGpuGetConnectedDisplayIds == null)
            {
                throw new InvalidOperationException("NVAPI delegates are not initialized.");
            }
        }

        private static void RevertTrial(uint[] displayIds)
        {
            NvAPI_Status status = nvApiDispRevertCustomDisplayTrial!(displayIds, (uint)displayIds.Length);
            if (status == NvAPI_Status.NVAPI_OK)
            {
                Logger.Log("Custom display trial reverted; saved custom mode remains available.");
            }
            else
            {
                Logger.Log($"NvAPI_DISP_RevertCustomDisplayTrial returned {FormatStatus(status)}.");
            }
        }

        private static void ThrowIfFailed(NvAPI_Status status, string message)
        {
            if (status != NvAPI_Status.NVAPI_OK)
            {
                throw new InvalidOperationException($"{message}: {FormatStatus(status)}");
            }
        }

        private static string FormatStatus(NvAPI_Status status)
        {
            string statusName = Enum.IsDefined(typeof(NvAPI_Status), status)
                ? status.ToString()
                : $"NVAPI status {(int)status}";

            if (nvApiGetErrorMessage == null)
            {
                return statusName;
            }

            IntPtr buffer = Marshal.AllocHGlobal(64);
            try
            {
                if (nvApiGetErrorMessage(status, buffer) == NvAPI_Status.NVAPI_OK)
                {
                    string? message = Marshal.PtrToStringAnsi(buffer);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return $"{statusName} ({message})";
                    }
                }
            }
            catch
            {
                // Formatting should never hide the original NVAPI status.
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return statusName;
        }
    }
}
