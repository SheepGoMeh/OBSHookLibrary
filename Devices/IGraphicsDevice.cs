using System;

namespace Sheep.OBSHookLibrary.Devices;

/// <summary>
/// Graphics device interface.
/// </summary>
public interface IGraphicsDevice: IDisposable
{
	/// <summary>
	/// Whether textures can be shared with OBS directly, otherwise capture goes through shared memory.
	/// OBS opens the legacy shared handle with a Direct3D 11 device, so only Direct3D 10/11 can share.
	/// </summary>
	public bool SupportsSharedTexture { get; }

	/// <summary>
	/// Creates a texture using provided arguments.
	/// The format is converted to its typed linear variant, the result's <see cref="IGraphicsTexture.Format"/> is
	/// what was actually created.
	/// </summary>
	/// <param name="width">Texture width.</param>
	/// <param name="height">Texture height.</param>
	/// <param name="format">Texture format, a DXGI_FORMAT value.</param>
	/// <param name="shared">Whether the texture is shared, otherwise it is a CPU readable staging texture.</param>
	/// <returns>The created <see cref="IGraphicsTexture"/>.</returns>
	public IGraphicsTexture CreateTexture(uint width, uint height, uint format, bool shared = false);

	/// <summary>
	/// Maps texture for CPU read access.
	/// </summary>
	/// <param name="texture">Texture to map.</param>
	/// <param name="data">Pointer to the mapped data.</param>
	/// <param name="rowPitch">Row pitch of the mapped data.</param>
	/// <returns>Whether the texture was mapped.</returns>
	public bool TryMap(IGraphicsTexture texture, out IntPtr data, out uint rowPitch);

	/// <summary>
	/// Unmaps texture.
	/// </summary>
	/// <param name="texture">Texture to unmap.</param>
	public void Unmap(IGraphicsTexture texture);

	/// <summary>
	/// Copies data from one texture to the other, resolving it if the source is multisampled.
	/// Implementations with explicit resource states transition the source as needed and restore its state afterwards.
	/// </summary>
	/// <param name="source">Source texture.</param>
	/// <param name="destination">Destination texture.</param>
	public void Copy(IGraphicsTexture source, IGraphicsTexture destination);

	/// <summary>
	/// Waits for the GPU to finish submitted work, called before capture textures are released.
	/// </summary>
	public void WaitIdle();
}
