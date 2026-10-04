using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sheep.OBSHookLibrary;

/// <summary>
/// Process wide side of the OBS graphics hook protocol, mirrors obs-studio's graphics-hook.c.
/// Owns the hook lock, the signals OBS game capture waits on and the hook info it reads.
/// </summary>
internal sealed unsafe partial class Hook: IDisposable
{
	private const uint VersionMajor = 1;
	private const uint VersionMinor = 8;
	private const long KeepAliveCheckInterval = 5_000_000_000;
	private const uint GaRoot = 2;

	private readonly Mutex hookLock;
	private readonly EventWaitHandle restartEvent;
	private readonly EventWaitHandle stopEvent;
	private readonly EventWaitHandle readyEvent;
	private readonly EventWaitHandle exitEvent;
	private readonly EventWaitHandle initEvent;
	private readonly SharedMemory hookInfo;
	private readonly string keepAliveName;
	private uint mapIdCounter;
	private long lastKeepAliveCheck;
	private long lastFrameTime;

	private Hook(Mutex hookLock)
	{
		int pid = Environment.ProcessId;
		this.hookLock = hookLock;
		this.keepAliveName = $"CaptureHook_KeepAlive{pid}";

		try
		{
			this.restartEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"CaptureHook_Restart{pid}");
			this.stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"CaptureHook_Stop{pid}");
			this.readyEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"CaptureHook_HookReady{pid}");
			this.exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"CaptureHook_Exit{pid}");
			this.initEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"CaptureHook_Initialize{pid}");
			this.TextureMutexes =
			[
				new Mutex(false, $"CaptureHook_TextureMutex1{pid}"),
				new Mutex(false, $"CaptureHook_TextureMutex2{pid}")
			];
			this.hookInfo = new SharedMemory($"CaptureHook_HookInfo{pid}", (uint)sizeof(HookInfo));
		}
		catch
		{
			this.Dispose();
			throw;
		}

		this.restartEvent.Set();
	}

	public Mutex[] TextureMutexes { get; }

	public bool ForceSharedMemory => this.Info->force_shmem != 0;

	private HookInfo* Info => (HookInfo*)this.hookInfo.Pointer;

	/// <summary>
	/// Acquires the hook lock and sets up the hook.
	/// </summary>
	/// <returns>The hook, or null if another graphics hook already owns this process.</returns>
	public static Hook? TryCreate()
	{
		Mutex hookLock;
		bool createdNew;

		try
		{
			hookLock = new Mutex(false, $"graphics_hook_dup_mutex{Environment.ProcessId}", out createdNew);
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}

		if (!createdNew)
		{
			hookLock.Dispose();
			return null;
		}

		return new Hook(hookLock);
	}

	public bool ShouldInit() => this.restartEvent.WaitOne(0) && this.IsCaptureAlive();

	public bool ShouldStop()
	{
		bool alive = true;
		long now = Now();

		if (now - this.lastKeepAliveCheck > KeepAliveCheckInterval)
		{
			alive = this.IsCaptureAlive();
			this.lastKeepAliveCheck = now;
		}

		return this.stopEvent.WaitOne(0) || !alive;
	}

	public bool FrameReady()
	{
		long interval = (long)this.Info->frame_interval;

		if (interval == 0)
		{
			return true;
		}

		long now = Now();
		long elapsed = now - this.lastFrameTime;

		if (elapsed < interval)
		{
			return false;
		}

		this.lastFrameTime = elapsed > interval * 2 ? now : this.lastFrameTime + interval;
		return true;
	}

	public void SignalRestart() => this.restartEvent.Set();

	/// <summary>
	/// Creates the shared memory OBS reads the next capture from.
	/// </summary>
	public SharedMemory CreateCaptureMemory(IntPtr window, uint size) =>
		new($"CaptureHook_Texture_{(ulong)GetAncestor(window, GaRoot)}_{++this.mapIdCounter}", size);

	/// <summary>
	/// Publishes the capture created with <see cref="CreateCaptureMemory"/> and tells OBS it is ready.
	/// </summary>
	public void Publish(CaptureType type, IntPtr window, uint cx, uint cy, uint format, uint pitch, uint mapSize)
	{
		HookInfo* info = this.Info;
		info->hook_ver_major = VersionMajor;
		info->hook_ver_minor = VersionMinor;
		info->window = (uint)(nuint)window;
		info->type = (uint)type;
		info->format = format;
		info->flip = 0;
		info->map_id = this.mapIdCounter;
		info->map_size = mapSize;
		info->pitch = pitch;
		info->cx = cx;
		info->cy = cy;
		info->UNUSED_base_cx = cx;
		info->UNUSED_base_cy = cy;

		this.readyEvent.Set();
	}

	private bool IsCaptureAlive()
	{
		try
		{
			if (!Mutex.TryOpenExisting(this.keepAliveName, out Mutex? mutex))
			{
				return false;
			}

			mutex.Dispose();
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			// Exists, but OBS runs elevated
			return true;
		}
	}

	private static long Now() => (long)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));

	[LibraryImport("user32.dll")]
	private static partial IntPtr GetAncestor(IntPtr window, uint flags);

	public void Dispose()
	{
		this.hookInfo?.Dispose();

		foreach (Mutex mutex in this.TextureMutexes ?? [])
		{
			mutex.Dispose();
		}

		this.initEvent?.Dispose();
		this.exitEvent?.Dispose();
		this.readyEvent?.Dispose();
		this.stopEvent?.Dispose();
		this.restartEvent?.Dispose();
		this.hookLock.Dispose();
	}
}
