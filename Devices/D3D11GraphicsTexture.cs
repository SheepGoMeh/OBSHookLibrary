using System;

using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Sheep.OBSHookLibrary.Devices;

public sealed class D3D11GraphicsTexture: IGraphicsTexture
{
	public D3D11GraphicsTexture(ID3D11Texture2D texture)
	{
		Texture2DDescription description = texture.Description;

		this.Texture = texture;
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
