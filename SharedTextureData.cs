using System.Runtime.InteropServices;

namespace Sheep.OBSHookLibrary;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct SharedTextureData
{
	public uint tex_handle;
}
