using System;

using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Sheep.OBSHookLibrary.Devices;

public class D3D11GraphicsResource: IGraphicsResource
{
	public D3D11GraphicsResource(ID3D11Resource resource)
	{
		Texture2DDescription description = resource.QueryInterface<ID3D11Texture2D>().Description;

		this.Format = (uint)description.Format;
		this.IsMultisampled = description.SampleDescription.Count > 1;
		this.Width = (uint)description.Width;
		this.Height = (uint)description.Height;
		this.Resource = resource;
		this.DxgiResource = resource.QueryInterface<IDXGIResource>();
	}

	public uint Format { get; set; }
	public uint ResourceUsage { get; set; }
	public bool IsMultisampled { get; set; }
	public uint Width { get; set; }
	public uint Height { get; set; }

	public ID3D11Resource? Resource { get; set; }
	public IDXGIResource? DxgiResource { get; }

	public IntPtr ResourceHandle
	{
		get => this.Resource?.NativePointer ?? IntPtr.Zero;
		set => this.Resource = new ID3D11Resource(value);
	}

	public IntPtr SharedResourceHandle => this.DxgiResource?.SharedHandle ?? IntPtr.Zero;

	public void Dispose()
	{
		this.DxgiResource?.Dispose();
		this.Resource?.Dispose();
	}
}
