using System;

using Sheep.OBSHookLibrary.Devices;

namespace Sheep.OBSHookLibrary;

public class Capture: IDisposable
{
	private readonly Hook hook;
	private bool usingSharedTexture;
	private unsafe SharedTextureData* sharedTextureData;
	private IGraphicsTexture? sharedTexture;
	private readonly IGraphicsTexture?[] copySurfaces = new IGraphicsTexture?[Hook.NumberOfBuffers];
	private readonly bool[] textureReady = new bool[Hook.NumberOfBuffers];
	private readonly bool[] textureMapped = new bool[Hook.NumberOfBuffers];
	private uint pitch;
	private unsafe SharedMemoryData* sharedMemoryData;
	private int currentTexture;
	private int copyWait;

	public Capture()
	{
		this.hook = new Hook();
		if (!this.hook.Init())
		{
			throw new Exception("Failed to initialize hook!");
		}
	}

	public unsafe bool CaptureImplementationInit(IGraphicsDevice device, IntPtr windowHandle, uint width, uint height,
		uint format)
	{
		if (this.hook.GlobalHookInfo->force_shmem == 0)
		{
			this.usingSharedTexture = true;

			IGraphicsTexture texture = device.CreateTexture(width, height, format, true);

			this.sharedTexture = texture;
			return this.hook.CaptureInitSharedTexture(ref this.sharedTextureData, width, height,
				format, false, texture.SharedHandle, windowHandle);
		}

		this.usingSharedTexture = false;

		for (int i = 0; i < Hook.NumberOfBuffers; ++i)
		{
			IGraphicsTexture texture = device.CreateTexture(width, height, format);

			this.copySurfaces[i] = texture;
		}

		if (device.TryMap(this.copySurfaces[0]!, out _, out this.pitch))
		{
			device.Unmap(this.copySurfaces[0]!);
		}

		return this.hook.CaptureInitSharedMemory(ref this.sharedMemoryData, width, height, this.pitch,
			format, false, windowHandle);
	}

	public void CaptureImplementationFree(IGraphicsDevice device)
	{
		this.hook.CaptureFree();

		if (this.usingSharedTexture)
		{
			this.sharedTexture?.Dispose();
		}
		else
		{
			for (int i = 0; i < Hook.NumberOfBuffers; ++i)
			{
				if (this.copySurfaces[i] == null)
				{
					continue;
				}

				if (this.textureMapped[i])
				{
					device.Unmap(this.copySurfaces[i]!);
				}

				this.copySurfaces[i]!.Dispose();
			}
		}
	}

	public void CaptureImplementationSharedTexture(IGraphicsDevice device, IGraphicsTexture texture)
	{
		device.Copy(texture, this.sharedTexture!);
	}

	public void CaptureImplementationSharedMemory(IGraphicsDevice device, IGraphicsTexture texture)
	{
		int nextTexture = (this.currentTexture + 1) % Hook.NumberOfBuffers;

		if (this.textureReady[nextTexture])
		{
			this.textureReady[nextTexture] = false;

			if (device.TryMap(this.copySurfaces[nextTexture]!, out IntPtr data, out _))
			{
				this.textureMapped[nextTexture] = true;
				this.hook.SharedMemoryCopyData((uint)nextTexture, data);
			}
		}

		if (this.copyWait < Hook.NumberOfBuffers - 1)
		{
			this.copyWait++;
		}
		else
		{
			if (this.hook.SharedMemoryTextureDataLock(this.currentTexture))
			{
				device.Unmap(this.copySurfaces[this.currentTexture]!);
				this.textureMapped[this.currentTexture] = false;
				this.hook.SharedMemoryTextureUnlock(this.currentTexture);
			}

			device.Copy(texture, this.copySurfaces[this.currentTexture]!);

			this.textureReady[this.currentTexture] = true;
		}

		this.currentTexture = nextTexture;
	}

	public void CaptureImplementationFrame(IGraphicsDevice device, IGraphicsTexture texture)
	{
		if (!this.hook.CaptureReady())
		{
			return;
		}

		if (this.usingSharedTexture)
		{
			this.CaptureImplementationSharedTexture(device, texture);
		}
		else
		{
			this.CaptureImplementationSharedMemory(device, texture);
		}
	}

	public void Present(IGraphicsDevice device, IGraphicsTexture texture, IntPtr windowHandle)
	{
		unsafe
		{
			if (this.hook.GlobalHookInfo == null)
			{
				return;
			}
		}

		if (this.hook.CaptureShouldStop())
		{
			this.CaptureImplementationFree(device);
		}

		if (this.hook.CaptureShouldInit())
		{
			this.CaptureImplementationInit(device, windowHandle, texture.Width, texture.Height, texture.Format);
		}

		this.CaptureImplementationFrame(device, texture);
	}

	public void Dispose()
	{
		this.hook.Dispose();
	}
}
