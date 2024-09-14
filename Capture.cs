using System;

using Sheep.OBSHookLibrary.Devices;

namespace Sheep.OBSHookLibrary;

public class Capture: IDisposable
{
	private readonly Hook hook;
	private bool usingSharedTexture;
	private bool multisampled;
	private unsafe SharedTextureData* sharedTextureData;
	private IGraphicsResource? sharedTexture;
	private readonly IGraphicsResource?[] copySurfaces = new IGraphicsResource?[Hook.NumberOfBuffers];
	private readonly bool[] textureReady = new bool[Hook.NumberOfBuffers];
	private readonly bool[] textureMapped = new bool[Hook.NumberOfBuffers];
	private uint pitch;
	private unsafe SharedMemoryData* sharedMemoryData;
	private int currentTexture;
	private int copyWait;

	public Capture()
	{
		this.hook = new Hook();
		this.hook.Init();
	}

	public unsafe bool CaptureImplementationInit(IGraphicsDevice device, IntPtr windowHandle, uint width, uint height,
		uint format)
	{
		if (this.hook.GlobalHookInfo->force_shmem == 0)
		{
			this.usingSharedTexture = true;

			IGraphicsResource? texture = device.CreateTexture(width, height, format, true);

			if (texture == null)
			{
				return false;
			}

			this.sharedTexture = texture;
			return this.hook.CaptureInitSharedTexture(ref this.sharedTextureData, width, height,
				format, false, texture.SharedResourceHandle, windowHandle);
		}

		this.usingSharedTexture = false;

		for (int i = 0; i < Hook.NumberOfBuffers; ++i)
		{
			IGraphicsResource? texture = device.CreateTexture(width, height, format);

			if (texture == null)
			{
				return false;
			}

			this.copySurfaces[i] = texture;
		}

		MapResult result = device.MapResource(this.copySurfaces[0]!, 0);

		if (result)
		{
			this.pitch = result.RowPitch;
			device.UnmapResource(this.copySurfaces[0]!, 0);
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
					device.UnmapResource(this.copySurfaces[i]!, 0);
				}

				this.copySurfaces[i]!.Dispose();
			}
		}
	}

	public void CaptureImplementationSharedTexture(IGraphicsDevice device, IGraphicsResource resource)
	{
		if (this.multisampled)
		{
			device.ResolveSubresource(resource, this.sharedTexture!);
		}
		else
		{
			device.CopyResource(resource, this.sharedTexture!);
		}
	}

	public void CaptureImplementationSharedMemory(IGraphicsDevice device, IGraphicsResource resource)
	{
		int nextTexture = (this.currentTexture + 1) % Hook.NumberOfBuffers;

		if (this.textureReady[nextTexture])
		{
			this.textureReady[nextTexture] = false;

			MapResult result = device.MapResource(this.copySurfaces[nextTexture]!, 0);
			if (result)
			{
				this.textureMapped[nextTexture] = true;
				this.hook.SharedMemoryCopyData((uint)nextTexture, result.DataPointer);
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
				device.UnmapResource(this.copySurfaces[this.currentTexture]!, 0);
				this.textureMapped[this.currentTexture] = false;
				this.hook.SharedMemoryTextureUnlock(this.currentTexture);
			}

			if (this.multisampled)
			{
				device.ResolveSubresource(resource, this.copySurfaces[this.currentTexture]!);
			}
			else
			{
				device.CopyResource(resource, this.copySurfaces[this.currentTexture]!);
			}

			this.textureReady[this.currentTexture] = true;
		}

		this.currentTexture = nextTexture;
	}

	public void CaptureImplementationFrame(IGraphicsDevice device, IGraphicsResource resource)
	{
		if (!this.hook.CaptureReady())
		{
			return;
		}

		if (this.usingSharedTexture)
		{
			this.CaptureImplementationSharedTexture(device, resource);
		}
		else
		{
			this.CaptureImplementationSharedMemory(device, resource);
		}
	}

	public void Present(IGraphicsDevice device, IGraphicsResource texture, IntPtr windowHandle)
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
			this.multisampled = texture.IsMultisampled;
			this.CaptureImplementationInit(device, windowHandle, texture.Width, texture.Height, texture.Format);
		}

		this.CaptureImplementationFrame(device, texture);
	}

	public void Dispose()
	{
		this.hook.Dispose();
	}
}
