using System;

namespace Sheep.OBSHookLibrary.Devices;

/// <summary>
/// Graphics resource interface.
/// </summary>
public interface IGraphicsTexture: IDisposable
{
	/// <summary>
	/// Texture format.
	/// </summary>
	public uint Format { get; set; }

	/// <summary>
	/// Resource usage.
	/// </summary>
	public uint ResourceUsage { get; set; }

	/// <summary>
	/// Whether the texture is multisampled.
	/// </summary>
	public bool IsMultisampled { get; set; }

	/// <summary>
	/// Texture width.
	/// </summary>
	public uint Width { get; set; }

	/// <summary>
	/// Texture height.
	/// </summary>
	public uint Height { get; set; }

	/// <summary>
	/// Resource native handle.
	/// </summary>
	public IntPtr ResourceHandle { get; set; }

	/// <summary>
	/// Resource shared native handle.
	/// </summary>
	public IntPtr SharedResourceHandle { get; }
}
