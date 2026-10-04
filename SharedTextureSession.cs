using System;

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

	public SharedTextureSession(Hook hook, IGraphicsDevice device, IGraphicsTexture source, IntPtr window)
	{
		this.device = device;

		try
		{
			this.sharedTexture = device.CreateTexture(source.Width, source.Height, source.Format, true);
			this.memory = hook.CreateCaptureMemory(window, (uint)sizeof(SharedTextureData));
			((SharedTextureData*)this.memory.Pointer)->tex_handle = (uint)(nuint)this.sharedTexture.SharedHandle;
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
		this.memory?.Dispose();
		this.sharedTexture?.Dispose();
	}
}
