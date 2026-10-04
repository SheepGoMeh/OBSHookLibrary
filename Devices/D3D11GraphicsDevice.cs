using System;

using Vortice.Direct3D11;
using Vortice.DXGI;

using MapFlags = Vortice.Direct3D11.MapFlags;

namespace Sheep.OBSHookLibrary.Devices;

public sealed class D3D11GraphicsDevice: IGraphicsDevice
{
	private readonly ID3D11Device device;
	private readonly ID3D11DeviceContext context;

	/// <summary>
	/// Wraps the device, taking a reference of its own. The caller keeps ownership of the device.
	/// </summary>
	public D3D11GraphicsDevice(IntPtr deviceHandle)
	{
		this.device = new ID3D11Device(deviceHandle);
		this.device.AddRef();
		this.context = this.device.ImmediateContext;
	}

	public IGraphicsTexture CreateTexture(uint width, uint height, uint format, bool shared = false)
	{
		using ID3D11Texture2D texture = this.device.CreateTexture2D(
			(Format)format,
			width,
			height,
			1,
			1,
			null,
			shared ? BindFlags.ShaderResource : BindFlags.None,
			shared ? ResourceOptionFlags.Shared : ResourceOptionFlags.None,
			shared ? ResourceUsage.Default : ResourceUsage.Staging,
			shared ? CpuAccessFlags.None : CpuAccessFlags.Read);

		return new D3D11GraphicsTexture(texture);
	}

	public bool TryMap(IGraphicsTexture texture, out IntPtr data, out uint rowPitch)
	{
		bool success = this.context.Map(Unwrap(texture), 0, MapMode.Read, MapFlags.None,
			out MappedSubresource mappedSubresource).Success;
		data = mappedSubresource.DataPointer;
		rowPitch = mappedSubresource.RowPitch;
		return success;
	}

	public void Unmap(IGraphicsTexture texture) => this.context.Unmap(Unwrap(texture), 0);

	public void Copy(IGraphicsTexture source, IGraphicsTexture destination)
	{
		if (source.IsMultisampled)
		{
			this.context.ResolveSubresource(Unwrap(destination), 0, Unwrap(source), 0, (Format)destination.Format);
		}
		else
		{
			this.context.CopyResource(Unwrap(destination), Unwrap(source));
		}
	}

	private static ID3D11Texture2D Unwrap(IGraphicsTexture texture) => ((D3D11GraphicsTexture)texture).Texture;

	public void Dispose()
	{
		this.context.Dispose();
		this.device.Dispose();
	}
}
