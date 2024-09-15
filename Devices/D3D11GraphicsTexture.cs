using System;

using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Sheep.OBSHookLibrary.Devices;

public class D3D11GraphicsTexture: IGraphicsTexture
{
	public D3D11GraphicsTexture(ID3D11Texture2D resource) => this.Init(resource);

	private void Init(ID3D11Texture2D resource)
	{
		Texture2DDescription description = resource.QueryInterface<ID3D11Texture2D>().Description;

		this.Format = (uint)description.Format;
		this.IsMultisampled = description.SampleDescription.Count > 1;
		this.Width = (uint)description.Width;
		this.Height = (uint)description.Height;
		this.TextureResource = resource;
		this.DxgiResource = resource.QueryInterface<IDXGIResource>();
	}

	public uint Format { get; set; }
	public uint ResourceUsage { get; set; }
	public bool IsMultisampled { get; set; }
	public uint Width { get; set; }
	public uint Height { get; set; }

	public ID3D11Texture2D? TextureResource { get; set; }
	public IDXGIResource? DxgiResource { get; set; }

	public IntPtr ResourceHandle
	{
		get => this.TextureResource?.NativePointer ?? IntPtr.Zero;
		set => this.Init(new ID3D11Texture2D(value));
	}

	public IntPtr SharedResourceHandle => this.DxgiResource?.SharedHandle ?? IntPtr.Zero;

	public void Dispose()
	{
		this.DxgiResource?.Dispose();
		this.TextureResource?.Dispose();
	}
}
