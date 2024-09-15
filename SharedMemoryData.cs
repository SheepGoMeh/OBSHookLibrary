using System.Runtime.InteropServices;

namespace Sheep.OBSHookLibrary;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct SharedMemoryData
{
	public volatile int last_tex;
	public uint tex1_offset;
	public uint tex2_offset;
}
