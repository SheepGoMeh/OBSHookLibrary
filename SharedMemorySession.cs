using System;

using Sheep.OBSHookLibrary.Devices;

namespace Sheep.OBSHookLibrary;

/// <summary>
/// Capture through CPU shared memory, used when OBS forces it.
/// Frames go through a ring of staging textures so mapping never stalls on the frame just copied.
/// </summary>
internal sealed unsafe class SharedMemorySession: ICaptureSession
{
	private const int BufferCount = 3;
	private const uint Alignment = 32;

	private readonly IGraphicsDevice device;
	private readonly IGraphicsTexture[] buffers = new IGraphicsTexture[BufferCount];
	private readonly bool[] bufferReady = new bool[BufferCount];
	private readonly bool[] bufferMapped = new bool[BufferCount];
	private readonly SharedMemory memory;
	private readonly MemoryCopier copier;
	private int currentBuffer;
	private int copyWait;

	public SharedMemorySession(Hook hook, IGraphicsDevice device, IGraphicsTexture source, IntPtr window)
	{
		this.device = device;

		try
		{
			for (int i = 0; i < BufferCount; ++i)
			{
				this.buffers[i] = device.CreateTexture(source.Width, source.Height, source.Format);
			}

			if (!device.TryMap(this.buffers[0], out _, out uint pitch))
			{
				throw new InvalidOperationException("Failed to map staging texture.");
			}

			device.Unmap(this.buffers[0]);

			uint textureSize = Align(source.Height * pitch);
			uint headerSize = Align((uint)sizeof(SharedMemoryData));
			uint totalSize = headerSize + textureSize * 2 + Alignment;

			this.memory = hook.CreateCaptureMemory(window, totalSize);

			// The view is allocation granularity aligned, so the header size alone keeps the textures aligned
			SharedMemoryData* data = (SharedMemoryData*)this.memory.Pointer;
			data->last_tex = -1;
			data->tex1_offset = headerSize;
			data->tex2_offset = headerSize + textureSize;

			this.copier = new MemoryCopier(BufferCount, hook.TextureMutexes, data, source.Height * pitch);
			hook.Publish(CaptureType.Memory, window, source.Width, source.Height, this.buffers[0].Format, pitch, totalSize);
		}
		catch
		{
			this.Dispose();
			throw;
		}
	}

	public void Capture(IGraphicsTexture texture)
	{
		int nextBuffer = (this.currentBuffer + 1) % BufferCount;

		if (this.bufferReady[nextBuffer])
		{
			this.bufferReady[nextBuffer] = false;

			if (this.device.TryMap(this.buffers[nextBuffer], out IntPtr data, out _))
			{
				this.bufferMapped[nextBuffer] = true;
				this.copier.Copy(nextBuffer, data);
			}
		}

		if (this.copyWait < BufferCount - 1)
		{
			this.copyWait++;
		}
		else
		{
			if (this.copier.TryLock(this.currentBuffer))
			{
				this.device.Unmap(this.buffers[this.currentBuffer]);
				this.bufferMapped[this.currentBuffer] = false;
				this.copier.Unlock(this.currentBuffer);
			}

			this.device.Copy(texture, this.buffers[this.currentBuffer]);
			this.bufferReady[this.currentBuffer] = true;
		}

		this.currentBuffer = nextBuffer;
	}

	private static uint Align(uint size) => (size + (Alignment - 1)) & ~(Alignment - 1);

	public void Dispose()
	{
		// Stop copying before anything it reads from goes away
		this.copier?.Dispose();
		this.device.WaitIdle();

		for (int i = 0; i < BufferCount; ++i)
		{
			if (this.bufferMapped[i])
			{
				this.device.Unmap(this.buffers[i]);
			}

			this.buffers[i]?.Dispose();
		}

		this.memory?.Dispose();
	}
}
