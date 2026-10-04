using System;

namespace Sheep.OBSHookLibrary.Devices;

/// <summary>
/// Graphics texture interface.
/// </summary>
public interface IGraphicsTexture: IDisposable
{
	/// <summary>
	/// Texture format, a DXGI_FORMAT value regardless of the graphics API.
	/// </summary>
	public uint Format { get; }

	/// <summary>
	/// Whether the texture is multisampled.
	/// </summary>
	public bool IsMultisampled { get; }

	/// <summary>
	/// Texture width.
	/// </summary>
	public uint Width { get; }

	/// <summary>
	/// Texture height.
	/// </summary>
	public uint Height { get; }

	/// <summary>
	/// Shared native handle, zero if the texture is not shared.
	/// </summary>
	public IntPtr SharedHandle { get; }
}
