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
	/// <returns><see cref="IGraphicsResource"/> if successful, null if not.</returns>
	public IGraphicsResource? CreateTexture(uint width, uint height, uint format, bool shared = false);

	/// <summary>
	/// Applies a barrier before or after a copy operation.
	/// </summary>
	/// <param name="resource">Resource implementing <see cref="IGraphicsResource"/>.</param>
	public void Barrier(IGraphicsResource resource);

	/// <summary>
	/// Maps resource for CPU access.
	/// </summary>
	/// <param name="resource">Resource implementing <see cref="IGraphicsResource"/>.</param>
	/// <param name="subresource">Subresource if the texture is multisampled.</param>
	/// <returns><see cref="MapResult"/> containing information of success, row pitch and data pointer.</returns>
	public MapResult MapResource(IGraphicsResource resource, int subresource);

	/// <summary>
	/// Unmaps resource.
	/// </summary>
	/// <param name="resource">Resource implementing <see cref="IGraphicsResource"/>.</param>
	/// <param name="subresource">Subresource if the texture is multisampled.</param>
	public void UnmapResource(IGraphicsResource resource, int subresource);

	/// <summary>
	/// Resolves subresource and copies data from one resource to the other.
	/// </summary>
	/// <param name="sourceResource">Source resource implementing <see cref="IGraphicsResource"/>.</param>
	/// <param name="destinationResource">Destination resource implementing <see cref="IGraphicsResource"/>.</param>
	public void ResolveSubresource(IGraphicsResource sourceResource, IGraphicsResource destinationResource);

	/// <summary>
	/// Copies data from one resource to the other.
	/// </summary>
	/// <param name="sourceResource">Source resource implementing <see cref="IGraphicsResource"/>.</param>
	/// <param name="destinationResource">Destination resource implementing <see cref="IGraphicsResource"/>.</param>
	public void CopyResource(IGraphicsResource sourceResource, IGraphicsResource destinationResource);
}
