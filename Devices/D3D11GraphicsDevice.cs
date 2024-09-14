using System;

using Vortice.Direct3D11;
using Vortice.DXGI;

using MapFlags = Vortice.Direct3D11.MapFlags;

namespace Sheep.OBSHookLibrary.Devices;

public class D3D11GraphicsDevice(IntPtr deviceHandle): IGraphicsDevice
{
	private readonly ID3D11Device device = new(deviceHandle);

	public IGraphicsResource CreateTexture(uint width, uint height, uint format, bool shared = false)
	{
		ID3D11Device3? device3 = this.device.QueryInterfaceOrNull<ID3D11Device3>();
		bool isDevice3 = device3 != null;

		Texture2DDescription textureDescription = new(
			(Format)format,
			(int)width,
			(int)height,
			1,
			1,
			shared ? BindFlags.ShaderResource : BindFlags.None,
			shared ? ResourceUsage.Default : ResourceUsage.Staging,
			shared ? CpuAccessFlags.None : CpuAccessFlags.Read,
			1,
			0,
			shared ? ResourceOptionFlags.Shared : ResourceOptionFlags.None);

		ID3D11Resource texture = isDevice3
			? device3!.CreateTexture2D1(new Texture2DDescription1(textureDescription))
			: this.device.CreateTexture2D(textureDescription);

		return new D3D11GraphicsResource(texture);
	}

	public MapResult MapResource(IGraphicsResource resource, int subresource = 0) =>
		this.device.ImmediateContext.Map(((D3D11GraphicsResource)resource).Resource, subresource, MapMode.Read,
			MapFlags.None, out MappedSubresource mappedSubresource).Success
			? new MapResult(true, (uint)mappedSubresource.RowPitch, mappedSubresource.DataPointer)
			: new MapResult(false, 0, IntPtr.Zero);

	public void UnmapResource(IGraphicsResource resource, int subresource = 0) =>
		this.device.ImmediateContext.Unmap(((D3D11GraphicsResource)resource).Resource, subresource);

	public void ResolveSubresource(IGraphicsResource sourceResource, IGraphicsResource destinationResource) =>
		this.device.ImmediateContext.ResolveSubresource(((D3D11GraphicsResource)destinationResource).Resource, 0,
			((D3D11GraphicsResource)sourceResource).Resource, 0, (Format)destinationResource.Format);

	public void CopyResource(IGraphicsResource sourceResource, IGraphicsResource destinationResource) =>
		this.device.ImmediateContext.CopyResource(((D3D11GraphicsResource)destinationResource).Resource,
			((D3D11GraphicsResource)sourceResource).Resource);
}
