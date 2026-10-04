using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Sheep.OBSHookLibrary;

/// <summary>
/// Copies mapped staging textures into the shared memory textures on a separate thread,
/// mirrors the copy thread in obs-studio's graphics-hook.c.
/// </summary>
internal sealed unsafe class MemoryCopier: IDisposable
{
	private readonly Mutex[] textureMutexes;
	private readonly SharedMemoryData* sharedData;
	private readonly byte*[] sharedTextures;
	private readonly uint size;
	private readonly Lock dataLock = new();
	private readonly Lock[] bufferLocks;
	private readonly bool[] lockedBuffers;
	private readonly AutoResetEvent copyEvent = new(false);
	private readonly Thread thread;
	private volatile bool stopping;
	private int currentBuffer = -1;
	private void* currentData;

	public MemoryCopier(int bufferCount, Mutex[] textureMutexes, SharedMemoryData* sharedData, uint size)
	{
		this.textureMutexes = textureMutexes;
		this.sharedData = sharedData;
		this.sharedTextures = [(byte*)sharedData + sharedData->tex1_offset, (byte*)sharedData + sharedData->tex2_offset];
		this.size = size;
		this.bufferLocks = new Lock[bufferCount];
		this.lockedBuffers = new bool[bufferCount];

		for (int i = 0; i < bufferCount; ++i)
		{
			this.bufferLocks[i] = new Lock();
		}

		this.thread = new Thread(this.Run) { IsBackground = true, Name = "OBS hook copy" };
		this.thread.Start();
	}

	/// <summary>
	/// Queues mapped buffer data to be copied to OBS.
	/// </summary>
	public void Copy(int buffer, IntPtr data)
	{
		lock (this.dataLock)
		{
			this.currentBuffer = buffer;
			this.currentData = (void*)data;
			this.lockedBuffers[buffer] = true;
		}

		this.copyEvent.Set();
	}

	/// <summary>
	/// Waits for the copy thread to be done with the buffer.
	/// </summary>
	/// <returns>False if the buffer was never queued, otherwise the buffer must be released with <see cref="Unlock"/>.</returns>
	public bool TryLock(int buffer)
	{
		lock (this.dataLock)
		{
			if (!this.lockedBuffers[buffer])
			{
				return false;
			}
		}

		this.bufferLocks[buffer].Enter();
		return true;
	}

	public void Unlock(int buffer)
	{
		lock (this.dataLock)
		{
			this.lockedBuffers[buffer] = false;
		}

		this.bufferLocks[buffer].Exit();
	}

	private void Run()
	{
		int sharedTexture = 0;

		while (true)
		{
			this.copyEvent.WaitOne();

			if (this.stopping)
			{
				return;
			}

			int buffer;
			void* data;

			lock (this.dataLock)
			{
				buffer = this.currentBuffer;
				data = this.currentData;
			}

			if (buffer < 0 || data == null)
			{
				continue;
			}

			lock (this.bufferLocks[buffer])
			{
				int locked = this.TryLockSharedTexture(sharedTexture);

				if (locked == -1)
				{
					continue;
				}

				Unsafe.CopyBlock(this.sharedTextures[locked], data, this.size);
				this.textureMutexes[locked].ReleaseMutex();
				this.sharedData->last_tex = locked;
				sharedTexture = locked == 0 ? 1 : 0;
			}
		}
	}

	private int TryLockSharedTexture(int preferred)
	{
		int other = preferred == 0 ? 1 : 0;

		if (TryAcquire(this.textureMutexes[preferred]))
		{
			return preferred;
		}

		return TryAcquire(this.textureMutexes[other]) ? other : -1;
	}

	private static bool TryAcquire(Mutex mutex)
	{
		try
		{
			return mutex.WaitOne(0);
		}
		catch (AbandonedMutexException)
		{
			return true;
		}
	}

	public void Dispose()
	{
		this.stopping = true;
		this.copyEvent.Set();
		this.thread.Join();
		this.copyEvent.Dispose();
	}
}
