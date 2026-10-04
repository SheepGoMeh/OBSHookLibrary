using System;

using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Sheep.OBSHookLibrary.Devices;

public sealed class D3D11GraphicsTexture: IGraphicsTexture
{
	/// <summary>
	/// Wraps the texture, taking a reference of its own. The caller keeps ownership of <paramref name="texture"/>.
	/// </summary>
	public D3D11GraphicsTexture(ID3D11Texture2D texture)
	{
		this.Texture = new ID3D11Texture2D(texture.NativePointer);
		this.Texture.AddRef();

		Texture2DDescription description = this.Texture.Description;
		this.Format = (uint)description.Format;
		this.IsMultisampled = description.SampleDescription.Count > 1;
		this.Width = description.Width;
		this.Height = description.Height;
	}

	public ID3D11Texture2D Texture { get; }
	public uint Format { get; }
	public bool IsMultisampled { get; }
	public uint Width { get; }
	public uint Height { get; }

	public IntPtr SharedHandle
	{
		get
		{
			using IDXGIResource resource = this.Texture.QueryInterface<IDXGIResource>();
			return resource.SharedHandle;
		}
	}

	public void Dispose() => this.Texture.Dispose();
}
