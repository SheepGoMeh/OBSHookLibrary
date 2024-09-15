namespace Sheep.OBSHookLibrary.Devices;

/// <summary>
/// Graphics device interface.
/// </summary>
public interface IGraphicsDevice
{
	/// <summary>
	/// Creates a texture using provided arguments.
	/// </summary>
	/// <param name="width">Texture width.</param>
	/// <param name="height">Texture height.</param>
	/// <param name="format">Texture format.</param>
	/// <param name="shared">Whether the texture is shared.</param>
	/// <returns><see cref="IGraphicsTexture"/> if successful, null if not.</returns>
	public IGraphicsTexture? CreateTexture(uint width, uint height, uint format, bool shared = false);

	/// <summary>
	/// Maps resource for CPU access.
	/// </summary>
	/// <param name="texture">Resource implementing <see cref="IGraphicsTexture"/>.</param>
	/// <param name="subresource">Subresource if the texture is multisampled.</param>
	/// <returns><see cref="MapResult"/> containing information of success, row pitch and data pointer.</returns>
	public MapResult MapResource(IGraphicsTexture texture, int subresource);

	/// <summary>
	/// Unmaps resource.
	/// </summary>
	/// <param name="texture">Resource implementing <see cref="IGraphicsTexture"/>.</param>
	/// <param name="subresource">Subresource if the texture is multisampled.</param>
	public void UnmapResource(IGraphicsTexture texture, int subresource);

	/// <summary>
	/// Resolves subresource and copies data from one resource to the other.
	/// </summary>
	/// <param name="sourceTexture">Source resource implementing <see cref="IGraphicsTexture"/>.</param>
	/// <param name="destinationTexture">Destination resource implementing <see cref="IGraphicsTexture"/>.</param>
	public void ResolveSubresource(IGraphicsTexture sourceTexture, IGraphicsTexture destinationTexture);

	/// <summary>
	/// Copies data from one resource to the other.
	/// </summary>
	/// <param name="sourceTexture">Source resource implementing <see cref="IGraphicsTexture"/>.</param>
	/// <param name="destinationTexture">Destination resource implementing <see cref="IGraphicsTexture"/>.</param>
	public void CopyResource(IGraphicsTexture sourceTexture, IGraphicsTexture destinationTexture);
}
