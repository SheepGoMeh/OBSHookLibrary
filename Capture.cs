using System;

using Sheep.OBSHookLibrary.Devices;

namespace Sheep.OBSHookLibrary;

/// <summary>
/// Exposes frames to OBS game capture, call <see cref="TryInit"/> once and <see cref="Present"/> every frame.
/// </summary>
public sealed class Capture: IDisposable
{
	private Hook? hook;
	private ICaptureSession? session;
	private (uint Width, uint Height, uint Format) sessionShape;

	/// <summary>
	/// Whether the hook lock is held and frames can be captured.
	/// </summary>
	public bool IsHooked => this.hook != null;

	/// <summary>
	/// Acquires the hook lock, can be retried until it succeeds.
	/// </summary>
	/// <returns>False if another graphics hook, such as OBS's own, already owns this process.</returns>
	public bool TryInit() => (this.hook ??= Hook.TryCreate()) != null;

	/// <summary>
	/// Starts, stops and feeds the capture as OBS requests, does nothing until <see cref="TryInit"/> succeeds.
	/// </summary>
	/// <param name="device">Device the texture belongs to.</param>
	/// <param name="texture">Frame to capture.</param>
	/// <param name="windowHandle">Window OBS is capturing.</param>
	public void Present(IGraphicsDevice device, IGraphicsTexture texture, IntPtr windowHandle)
	{
		if (this.hook == null)
		{
			return;
		}

		// Resizing takes a new capture, same as upstream freeing on ResizeBuffers
		if (this.session != null &&
			(this.hook.ShouldStop() || this.sessionShape != (texture.Width, texture.Height, texture.Format)))
		{
			this.Free();
		}

		if (this.session == null && this.hook.ShouldInit())
		{
			try
			{
				this.session = this.hook.ForceSharedMemory
					? new SharedMemorySession(this.hook, device, texture, windowHandle)
					: new SharedTextureSession(this.hook, device, texture, windowHandle);
				this.sessionShape = (texture.Width, texture.Height, texture.Format);
			}
			catch
			{
				// Retry on the next frame
				this.hook.SignalRestart();
				throw;
			}
		}

		if (this.session != null && this.hook.FrameReady())
		{
			this.session.Capture(texture);
		}
	}

	private void Free()
	{
		this.session?.Dispose();
		this.session = null;
		this.hook?.SignalRestart();
	}

	public void Dispose()
	{
		this.session?.Dispose();
		this.session = null;
		this.hook?.Dispose();
		this.hook = null;
	}
}
