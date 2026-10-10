using System;
using System.Collections.Generic;
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
	private readonly bool takeOver;
	private readonly EventWaitHandle? quitEvent;
	private readonly Thread? restartWaiter;
	private int restartPending;
	private uint mapIdCounter;
	private long lastKeepAliveCheck;
	private long lastFrameTime;

	private Hook(Mutex hookLock, bool takeOver, bool otherHook)
	{
		int pid = Environment.ProcessId;
		this.hookLock = hookLock;
		this.keepAliveName = $"CaptureHook_KeepAlive{pid}";
		this.takeOver = takeOver;
		this.TookOver = otherHook;

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

		if (!takeOver)
		{
			this.restartEvent.Set();
			return;
		}

		// The other hook frees its capture on its next present
		if (otherHook)
		{
			this.stopEvent.Set();
		}

		// A blocked waiter gets every auto reset restart before a hook that polls on present
		this.restartPending = 1;
		this.quitEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
		this.restartWaiter = new Thread(this.WaitForRestarts) { IsBackground = true, Name = "OBS restart waiter" };
		this.restartWaiter.Start();
	}

	public Mutex[] TextureMutexes { get; }

	/// <summary>
	/// Whether another graphics hook owned the process when this one was created.
	/// </summary>
	public bool TookOver { get; }

	public bool ForceSharedMemory => this.Info->force_shmem != 0;

	private HookInfo* Info => (HookInfo*)this.hookInfo.Pointer;

	/// <summary>
	/// Acquires the hook lock and sets up the hook.
	/// </summary>
	/// <param name="takeOver">Take the capture from another graphics hook, such as OBS's own, instead of yielding.</param>
	/// <returns>The hook, or null if another graphics hook already owns this process and <paramref name="takeOver"/> is false.</returns>
	public static Hook? TryCreate(bool takeOver = false)
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

		if (!createdNew && !takeOver)
		{
			hookLock.Dispose();
			return null;
		}

		return new Hook(hookLock, takeOver, !createdNew);
	}

	public bool ShouldInit()
	{
		bool restart = this.takeOver
			? Interlocked.Exchange(ref this.restartPending, 0) == 1
			: this.restartEvent.WaitOne(0);
		return restart && this.IsCaptureAlive();
	}

	private void WaitForRestarts()
	{
		WaitHandle[] handles = [this.restartEvent, this.quitEvent!];
		while (WaitHandle.WaitAny(handles) == 0)
		{
			Interlocked.Exchange(ref this.restartPending, 1);
		}
	}

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

	public void SignalRestart()
	{
		// Kept private while taking over, the other hook would take the shared event
		if (this.takeOver)
		{
			Interlocked.Exchange(ref this.restartPending, 1);
		}
		else
		{
			this.restartEvent.Set();
		}
	}

	/// <summary>
	/// Creates the shared memory OBS reads the next capture from.
	/// </summary>
	public SharedMemory CreateCaptureMemory(IntPtr window, uint size) =>
		new($"CaptureHook_Texture_{(ulong)GetAncestor(window, GaRoot)}_{++this.mapIdCounter}", size);

	/// <summary>
	/// The last <see cref="CreateCaptureMemory"/> capture again under the process's other top level windows.
	/// OBS opens the capture under the window its source matched first and only falls back to the published one, so a
	/// plugin window of the same executable (Dalamud viewports) would otherwise get the window's own capture, or none.
	/// An existing mapping of that name, left by another hook, is opened and overwritten.
	/// </summary>
	public List<SharedMemory> CreateCaptureAliases(IntPtr window, uint size)
	{
		IntPtr root = GetAncestor(window, GaRoot);
		List<SharedMemory> aliases = [];
		foreach (IntPtr other in ProcessWindows())
		{
			if (other == root)
			{
				continue;
			}

			try
			{
				aliases.Add(new SharedMemory($"CaptureHook_Texture_{(ulong)other}_{this.mapIdCounter}", size));
			}
			catch (Exception)
			{
				// Taken by something else with another size, OBS falls back to the published window there
			}
		}

		return aliases;
	}

	private static List<IntPtr> ProcessWindows()
	{
		List<IntPtr> windows = [];
		GCHandle handle = GCHandle.Alloc(windows);
		try
		{
			EnumWindows(&CollectWindow, GCHandle.ToIntPtr(handle));
		}
		finally
		{
			handle.Free();
		}

		return windows;
	}

	[UnmanagedCallersOnly]
	private static int CollectWindow(IntPtr window, IntPtr state)
	{
		GetWindowThreadProcessId(window, out uint pid);
		if (pid == Environment.ProcessId)
		{
			((List<IntPtr>)GCHandle.FromIntPtr(state).Target!).Add(window);
		}

		return 1;
	}

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

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool EnumWindows(delegate* unmanaged<IntPtr, IntPtr, int> callback, IntPtr state);

	[LibraryImport("user32.dll")]
	private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

	public void Dispose()
	{
		if (this.restartWaiter != null)
		{
			this.quitEvent!.Set();
			this.restartWaiter.Join();
			// The other hook, or a fresh injection, takes the capture back
			this.restartEvent.Set();
		}

		this.quitEvent?.Dispose();
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
