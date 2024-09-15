using System;

using Vortice.Direct3D11;
using Vortice.DXGI;

using MapFlags = Vortice.Direct3D11.MapFlags;

namespace Sheep.OBSHookLibrary.Devices;

public class D3D11GraphicsDevice(IntPtr deviceHandle): IGraphicsDevice
{
	private readonly ID3D11Device device = new(deviceHandle);

	public IGraphicsTexture CreateTexture(uint width, uint height, uint format, bool shared = false) =>
		new D3D11GraphicsTexture(
			this.device.CreateTexture2D(
				(Format)format,
				(int)width,
				(int)height,
				1,
				1,
				null,
				shared ? BindFlags.ShaderResource : BindFlags.None,
				shared ? ResourceOptionFlags.Shared : ResourceOptionFlags.None,
				shared ? ResourceUsage.Default : ResourceUsage.Staging,
				shared ? CpuAccessFlags.None : CpuAccessFlags.Read)
		);

	public MapResult MapResource(IGraphicsTexture texture, int subresource = 0) =>
		this.device.ImmediateContext.Map(((D3D11GraphicsTexture)texture).TextureResource, subresource, MapMode.Read,
			MapFlags.None, out MappedSubresource mappedSubresource).Success
			? new MapResult(true, (uint)mappedSubresource.RowPitch, mappedSubresource.DataPointer)
			: new MapResult(false, 0, IntPtr.Zero);

	public void UnmapResource(IGraphicsTexture texture, int subresource = 0) =>
		this.device.ImmediateContext.Unmap(((D3D11GraphicsTexture)texture).TextureResource, subresource);

	public void ResolveSubresource(IGraphicsTexture sourceTexture, IGraphicsTexture destinationTexture) =>
		this.device.ImmediateContext.ResolveSubresource(((D3D11GraphicsTexture)destinationTexture).TextureResource, 0,
			((D3D11GraphicsTexture)sourceTexture).TextureResource, 0, (Format)destinationTexture.Format);

	public void CopyResource(IGraphicsTexture sourceTexture, IGraphicsTexture destinationTexture) =>
		this.device.ImmediateContext.CopyResource(((D3D11GraphicsTexture)destinationTexture).TextureResource,
			((D3D11GraphicsTexture)sourceTexture).TextureResource);
}
