using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Vanara.PInvoke;

namespace Sheep.OBSHookLibrary;

public class Hook: IDisposable
{
	public const int NumberOfBuffers = 3;
	private uint sharedMemoryIdCounter;
	private Kernel32.SafeMutexHandle? hookMutex;
	private Kernel32.SafeMutexHandle?[] textureMutexes = new Kernel32.SafeMutexHandle?[2];
	private Kernel32.SafeEventHandle? eventHookInit;
	private Kernel32.SafeEventHandle? eventHookExit;
	private Kernel32.SafeEventHandle? eventHookReady;
	private Kernel32.SafeEventHandle? eventCaptureRestart;
	private Kernel32.SafeEventHandle? eventCaptureStop;
	private IntPtr globalHookInfo;
	public unsafe HookInfo* GlobalHookInfo => (HookInfo*)this.globalHookInfo;
	private IntPtr SharedMemoryInfo { get; set; }
	private Kernel32.SafeHSECTION? fileMapHookInfo;
	private Kernel32.SafeHSECTION? sharedMemoryFile;
	private volatile bool active;
	private readonly ThreadData threadData = new();

	public bool Init()
	{
		uint pid = (uint)Environment.ProcessId;
		string mutexName = $"graphics_hook_dup_mutex{pid}";
		if (!(this.hookMutex = Kernel32.OpenMutex(ACCESS_MASK.SYNCHRONIZE, false, mutexName)).IsInvalid)
		{
			this.hookMutex?.Dispose();
			return false;
		}

		try
		{
			this.hookMutex = Kernel32.CreateMutex(null, false, mutexName);
			this.eventHookInit = Kernel32.CreateEvent(null, false, false, $"CaptureHook_Initialize{pid}");
			this.eventHookExit = Kernel32.CreateEvent(null, false, false, $"CaptureHook_Exit{pid}");
			this.eventCaptureRestart = Kernel32.CreateEvent(null, false, false, $"CaptureHook_Restart{pid}");
			this.eventCaptureStop = Kernel32.CreateEvent(null, false, false, $"CaptureHook_Stop{pid}");
			this.eventHookReady = Kernel32.CreateEvent(null, false, false, $"CaptureHook_HookReady{pid}");
			this.textureMutexes =
			[
				Kernel32.CreateMutex(null, false, $"CaptureHook_TextureMutex1{pid}"),
				Kernel32.CreateMutex(null, false, $"CaptureHook_TextureMutex2{pid}")
			];
			this.eventCaptureRestart.Set();

			this.fileMapHookInfo = Kernel32.CreateFileMapping(HFILE.INVALID_HANDLE_VALUE, null,
				Kernel32.MEM_PROTECTION.PAGE_READWRITE, 0, (uint)Unsafe.SizeOf<HookInfo>(),
				$"CaptureHook_HookInfo{pid}");

			if (this.fileMapHookInfo.IsInvalid)
			{
				throw new ArgumentNullException();
			}

			this.globalHookInfo = Kernel32.MapViewOfFile(this.fileMapHookInfo, Kernel32.FILE_MAP.FILE_MAP_ALL_ACCESS, 0,
				0, Unsafe.SizeOf<HookInfo>());
		}
		catch
		{
			return false;
		}

		return true;
	}

	private static ulong lastTime;

	public unsafe bool CaptureReady()
	{
		if (!this.CaptureActive())
		{
			return false;
		}

		if (this.GlobalHookInfo->frame_interval == 0)
		{
			return true;
		}

		ulong timestamp = (ulong)Stopwatch.GetTimestamp();
		ulong elapsed = timestamp - lastTime;

		if (elapsed < this.GlobalHookInfo->frame_interval)
		{
			return false;
		}

		lastTime = (elapsed > this.GlobalHookInfo->frame_interval * 2)
			? timestamp
			: lastTime + this.GlobalHookInfo->frame_interval;
		return true;
	}

	public bool CaptureAlive()
	{
		Kernel32.SafeMutexHandle mutex = Kernel32.OpenMutex(ACCESS_MASK.SYNCHRONIZE, false,
			$"CaptureHook_KeepAlive{Environment.ProcessId}");

		if (mutex.IsInvalid)
		{
			return false;
		}

		mutex.Dispose();
		return true;
	}

	public bool CaptureSignalReady() => this.eventHookReady != null && this.eventHookReady.Set();
	public bool CaptureSignalRestart() => this.eventCaptureRestart != null && this.eventCaptureRestart.Set();

	public bool CaptureActive() => this.active;

	public bool CaptureStopped() => this.eventCaptureStop != null &&
	                                Kernel32.WaitForSingleObject(this.eventCaptureStop, 0) ==
	                                Kernel32.WAIT_STATUS.WAIT_OBJECT_0;

	public bool CaptureRestarted() => this.eventCaptureRestart != null &&
	                                  Kernel32.WaitForSingleObject(this.eventCaptureRestart, 0) ==
	                                  Kernel32.WAIT_STATUS.WAIT_OBJECT_0;

	public bool CaptureShouldStop() => this.CaptureActive() && this.CaptureStopped() && !this.CaptureAlive();

	public bool CaptureShouldInit() => !this.CaptureActive() && !this.CaptureRestarted() && this.CaptureAlive();

	public unsafe bool CaptureInitSharedTexture(ref SharedTextureData* data, uint cx, uint cy, uint format,
		bool flip, IntPtr handle, IntPtr windowHandle)
	{
		HWND rootWindow = User32.GetAncestor(windowHandle, User32.GetAncestorFlag.GA_ROOT);

		this.sharedMemoryFile = Kernel32.CreateFileMapping(HFILE.INVALID_HANDLE_VALUE, null,
			Kernel32.MEM_PROTECTION.PAGE_READWRITE, 0, (uint)Unsafe.SizeOf<SharedTextureData>(),
			$"CaptureHook_Texture_{rootWindow.DangerousGetHandle()}_{++this.sharedMemoryIdCounter}");

		if (this.sharedMemoryFile.IsInvalid)
		{
			return false;
		}

		this.SharedMemoryInfo = Kernel32.MapViewOfFile(this.sharedMemoryFile, Kernel32.FILE_MAP.FILE_MAP_ALL_ACCESS, 0,
			0, Unsafe.SizeOf<SharedTextureData>());

		data = (SharedTextureData*)this.SharedMemoryInfo;
		data->tex_handle = ((UIntPtr)handle).ToUInt32();

		this.GlobalHookInfo->hook_ver_major = 1;
		this.GlobalHookInfo->hook_ver_minor = 7;
		this.GlobalHookInfo->window = ((UIntPtr)windowHandle).ToUInt32();
		this.GlobalHookInfo->type = (uint)CaptureType.CaptureTypeTexture;
		this.GlobalHookInfo->format = format;
		this.GlobalHookInfo->flip = flip ? (byte)1 : (byte)0;
		this.GlobalHookInfo->map_id = this.sharedMemoryIdCounter;
		this.GlobalHookInfo->map_size = (uint)Unsafe.SizeOf<SharedTextureData>();
		this.GlobalHookInfo->cx = cx;
		this.GlobalHookInfo->cy = cy;
		this.GlobalHookInfo->UNUSED_base_cx = cx;
		this.GlobalHookInfo->UNUSED_base_cy = cy;

		if (!this.CaptureSignalReady())
		{
			return false;
		}

		return this.active = true;
	}

	public unsafe bool CaptureInitSharedMemory(ref SharedMemoryData* data, uint cx, uint cy, uint pitch,
		uint format, bool flip, IntPtr windowHandle)
	{
		uint textureSize = cy * pitch;
		uint alignedHeader = ((uint)Unsafe.SizeOf<SharedMemoryData>() + (32u - 1u)) & ~(32u - 1u);
		uint alignedTexture = (textureSize + (32u - 1u)) & ~(32u - 1u);
		uint totalSize = alignedHeader + alignedTexture * 2u + 32u;

		HWND rootWindow = User32.GetAncestor(windowHandle, User32.GetAncestorFlag.GA_ROOT);

		this.sharedMemoryFile = Kernel32.CreateFileMapping(HFILE.INVALID_HANDLE_VALUE, null,
			Kernel32.MEM_PROTECTION.PAGE_READWRITE, 0, totalSize,
			$"CaptureHook_Texture_{rootWindow.DangerousGetHandle()}_{++this.sharedMemoryIdCounter}");

		if (this.sharedMemoryFile.IsInvalid)
		{
			return false;
		}

		this.SharedMemoryInfo = Kernel32.MapViewOfFile(this.sharedMemoryFile, Kernel32.FILE_MAP.FILE_MAP_ALL_ACCESS, 0,
			0, totalSize);

		data = (SharedMemoryData*)this.SharedMemoryInfo;

		uint alignedPosition = (uint)data;
		alignedPosition += alignedHeader;
		alignedPosition &= ~(32u - 1u);
		alignedPosition -= (uint)data;

		if (alignedPosition < Unsafe.SizeOf<SharedMemoryData>())
		{
			alignedPosition += 32;
		}

		data->last_tex = -1;
		data->tex1_offset = alignedPosition;
		data->tex2_offset = data->tex1_offset + alignedTexture;

		this.GlobalHookInfo->hook_ver_major = 1;
		this.GlobalHookInfo->hook_ver_minor = 7;
		this.GlobalHookInfo->window = ((UIntPtr)windowHandle).ToUInt32();
		this.GlobalHookInfo->type = (uint)CaptureType.CaptureTypeMemory;
		this.GlobalHookInfo->format = format;
		this.GlobalHookInfo->flip = flip ? (byte)1 : (byte)0;
		this.GlobalHookInfo->map_id = this.sharedMemoryIdCounter;
		this.GlobalHookInfo->map_size = totalSize;
		this.GlobalHookInfo->pitch = pitch;
		this.GlobalHookInfo->cx = cx;
		this.GlobalHookInfo->cy = cy;
		this.GlobalHookInfo->UNUSED_base_cx = cx;
		this.GlobalHookInfo->UNUSED_base_cy = cy;

		this.threadData.Pitch = pitch;
		this.threadData.Cy = cy;
		this.threadData.SharedMemoryTextures[0] = ((byte*)this.SharedMemoryInfo) + data->tex1_offset;
		this.threadData.SharedMemoryTextures[1] = ((byte*)this.SharedMemoryInfo) + data->tex2_offset;
		this.threadData.CopyEvent = Kernel32.CreateEvent();
		this.threadData.StopEvent = Kernel32.CreateEvent();

		for (int i = 0; i != NumberOfBuffers; ++i)
		{
			Kernel32.InitializeCriticalSection(out this.threadData.Mutexes[i]);
		}

		Kernel32.InitializeCriticalSection(out this.threadData.DataMutex);

		this.threadData.CopyThreadCancellationTokenSource = new CancellationTokenSource();
		this.threadData.CopyThread = Task.Run(this.CopyThread, this.threadData.CopyThreadCancellationTokenSource.Token);

		if (!this.CaptureSignalReady())
		{
			return false;
		}

		return this.active = true;
	}

	private unsafe void CopyThread()
	{
		uint pitch = this.threadData.Pitch;
		uint cy = this.threadData.Cy;
		int sharedMemoryId = 0;
		ISyncHandle[] events =
		[
			new Kernel32.SafeEventHandle(this.threadData.CopyEvent!.Duplicate()),
			new Kernel32.SafeEventHandle(this.threadData.StopEvent!.Duplicate())
		];

		while (Kernel32.WaitForMultipleObjects(events, false, Kernel32.INFINITE) ==
		       Kernel32.WAIT_STATUS.WAIT_OBJECT_0)
		{
			if (this.threadData.CopyThreadCancellationTokenSource!.IsCancellationRequested)
			{
				return;
			}

			Kernel32.EnterCriticalSection(ref this.threadData.DataMutex);
			Kernel32.LeaveCriticalSection(ref this.threadData.DataMutex);

			if (this.threadData.CurrentTexture >= NumberOfBuffers || this.threadData.CurrentData == null)
			{
				continue;
			}

			Kernel32.EnterCriticalSection(ref this.threadData.Mutexes[this.threadData.CurrentTexture]);
			int lockId = -1;
			int nextId = sharedMemoryId == 0 ? 1 : 0;

			Kernel32.WAIT_STATUS waitResult = Kernel32.WaitForSingleObject(this.textureMutexes[sharedMemoryId]!, 0);

			if (waitResult is Kernel32.WAIT_STATUS.WAIT_OBJECT_0 or Kernel32.WAIT_STATUS.WAIT_ABANDONED)
			{
				lockId = sharedMemoryId;
			}
			else
			{
				waitResult = Kernel32.WaitForSingleObject(this.textureMutexes[nextId]!, 0);
				if (waitResult is Kernel32.WAIT_STATUS.WAIT_OBJECT_0 or Kernel32.WAIT_STATUS.WAIT_ABANDONED)
				{
					lockId = nextId;
				}
			}

			if (lockId != -1)
			{
				Unsafe.CopyBlock(this.threadData.SharedMemoryTextures[lockId], this.threadData.CurrentData, pitch * cy);
				Kernel32.ReleaseMutex(this.textureMutexes[lockId]!);
				((SharedMemoryData*)this.SharedMemoryInfo)->last_tex = lockId;
				sharedMemoryId = lockId == 0 ? 1 : 0;
			}

			Kernel32.LeaveCriticalSection(ref this.threadData.Mutexes[this.threadData.CurrentTexture]);
		}
	}

	public unsafe void SharedMemoryCopyData(uint index, IntPtr data)
	{
		Kernel32.EnterCriticalSection(ref this.threadData.DataMutex);
		this.threadData.CurrentTexture = (int)index;
		this.threadData.CurrentData = (void*)data;
		this.threadData.LockedTextures[index] = true;
		Kernel32.LeaveCriticalSection(ref this.threadData.DataMutex);

		this.threadData.CopyEvent!.Set();
	}

	public bool SharedMemoryTextureDataLock(int index)
	{
		Kernel32.EnterCriticalSection(ref this.threadData.DataMutex);
		bool locked = this.threadData.LockedTextures[index];
		Kernel32.LeaveCriticalSection(ref this.threadData.DataMutex);

		if (!locked)
		{
			return false;
		}

		Kernel32.EnterCriticalSection(ref this.threadData.Mutexes[index]);
		return true;
	}

	public void SharedMemoryTextureUnlock(int index)
	{
		Kernel32.EnterCriticalSection(ref this.threadData.DataMutex);
		this.threadData.LockedTextures[index] = false;
		Kernel32.LeaveCriticalSection(ref this.threadData.DataMutex);

		Kernel32.LeaveCriticalSection(ref this.threadData.Mutexes[index]);
	}

	public void CaptureFree()
	{
		if (this.threadData.CopyThread != null)
		{
			this.threadData.CopyThreadCancellationTokenSource!.Cancel();
			this.threadData.StopEvent!.Set();
			this.threadData.CopyThread.Wait(500);
			this.threadData.CopyThread.Dispose();
			this.threadData.CopyThreadCancellationTokenSource.Dispose();
		}

		this.threadData.StopEvent?.Dispose();
		this.threadData.CopyEvent?.Dispose();

		for (int i = 0; i < this.threadData.Mutexes.Length; ++i)
		{
			Kernel32.DeleteCriticalSection(ref this.threadData.Mutexes[i]);
		}

		Kernel32.DeleteCriticalSection(ref this.threadData.DataMutex);

		this.CaptureSignalRestart();
		this.active = false;
	}

	public void Dispose()
	{
		if (this.SharedMemoryInfo != IntPtr.Zero)
		{
			Kernel32.UnmapViewOfFile(this.SharedMemoryInfo);
		}

		if (this.globalHookInfo != IntPtr.Zero)
		{
			Kernel32.UnmapViewOfFile(this.globalHookInfo);
		}

		this.fileMapHookInfo?.Dispose();
		this.sharedMemoryFile?.Dispose();
		this.hookMutex?.Dispose();

		foreach (Kernel32.SafeMutexHandle? mutex in this.textureMutexes)
		{
			mutex?.Dispose();
		}

		this.eventHookInit?.Dispose();
		this.eventHookExit?.Dispose();
		this.eventHookReady?.Dispose();
		this.eventCaptureRestart?.Dispose();
		this.eventCaptureStop?.Dispose();
	}
}
