using Vortice.DXGI;

namespace Sheep.OBSHookLibrary.Devices;

/// <summary>
/// DXGI format helpers shared by the Direct3D devices.
/// </summary>
internal static class DxgiFormats
{
	/// <summary>
	/// Converts typeless and sRGB formats to the typed linear format OBS expects,
	/// mirrors ReShade's <c>format_to_default_typed(format, 0)</c> for color formats.
	/// </summary>
	public static Format ToDefaultTyped(Format format) => format switch
	{
		Format.R8_Typeless => Format.R8_UNorm,
		Format.R8G8_Typeless => Format.R8G8_UNorm,
		Format.R8G8B8A8_Typeless or Format.R8G8B8A8_UNorm_SRgb => Format.R8G8B8A8_UNorm,
		Format.B8G8R8A8_Typeless or Format.B8G8R8A8_UNorm_SRgb => Format.B8G8R8A8_UNorm,
		Format.B8G8R8X8_Typeless or Format.B8G8R8X8_UNorm_SRgb => Format.B8G8R8X8_UNorm,
		Format.R10G10B10A2_Typeless => Format.R10G10B10A2_UNorm,
		Format.R16_Typeless => Format.R16_Float,
		Format.R16G16_Typeless => Format.R16G16_Float,
		Format.R16G16B16A16_Typeless => Format.R16G16B16A16_Float,
		Format.R32_Typeless => Format.R32_Float,
		Format.R32G32_Typeless => Format.R32G32_Float,
		Format.R32G32B32_Typeless => Format.R32G32B32_Float,
		Format.R32G32B32A32_Typeless => Format.R32G32B32A32_Float,
		_ => format,
	};
}
