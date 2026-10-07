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
	/// Whether another graphics hook, such as OBS's own, owned the process and its capture was taken over.
	/// </summary>
	public bool TookOver => this.hook?.TookOver ?? false;

	/// <summary>
	/// Whether OBS asked for shared memory capture (compatibility mode).
	/// </summary>
	public bool UsesSharedMemory => this.hook?.ForceSharedMemory ?? false;

	/// <summary>
	/// Whether a capture is published to OBS.
	/// </summary>
	public bool IsCapturing => this.session != null;

	/// <summary>
	/// Acquires the hook lock, can be retried until it succeeds.
	/// </summary>
	/// <param name="takeOver">Take the capture from another graphics hook instead of yielding to it.</param>
	/// <returns>False if another graphics hook, such as OBS's own, already owns this process and <paramref name="takeOver"/> is false.</returns>
	public bool TryInit(bool takeOver = false) => (this.hook ??= Hook.TryCreate(takeOver)) != null;

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
				this.session = this.hook.ForceSharedMemory || !device.SupportsSharedTexture
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
