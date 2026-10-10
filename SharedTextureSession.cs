using System;
using System.Collections.Generic;

using Sheep.OBSHookLibrary.Devices;

namespace Sheep.OBSHookLibrary;

/// <summary>
/// Capture through a texture shared with OBS.
/// </summary>
internal sealed unsafe class SharedTextureSession: ICaptureSession
{
	private readonly IGraphicsDevice device;
	private readonly IGraphicsTexture sharedTexture;
	private readonly SharedMemory memory;
	private readonly List<SharedMemory> aliases = [];

	public SharedTextureSession(Hook hook, IGraphicsDevice device, IGraphicsTexture source, IntPtr window)
	{
		this.device = device;

		try
		{
			this.sharedTexture = device.CreateTexture(source.Width, source.Height, source.Format, true);
			uint handle = (uint)(nuint)this.sharedTexture.SharedHandle;
			this.memory = hook.CreateCaptureMemory(window, (uint)sizeof(SharedTextureData));
			((SharedTextureData*)this.memory.Pointer)->tex_handle = handle;
			// Windows the process opens later are covered from the next capture restart
			this.aliases = hook.CreateCaptureAliases(window, (uint)sizeof(SharedTextureData));
			foreach (SharedMemory alias in this.aliases)
			{
				((SharedTextureData*)alias.Pointer)->tex_handle = handle;
			}

			hook.Publish(CaptureType.Texture, window, source.Width, source.Height, this.sharedTexture.Format, 0,
				(uint)sizeof(SharedTextureData));
		}
		catch
		{
			this.Dispose();
			throw;
		}
	}

	public void Capture(IGraphicsTexture texture) => this.device.Copy(texture, this.sharedTexture);

	public void Dispose()
	{
		this.device.WaitIdle();
		foreach (SharedMemory alias in this.aliases)
		{
			alias.Dispose();
		}

		this.memory?.Dispose();
		this.sharedTexture?.Dispose();
	}
}
