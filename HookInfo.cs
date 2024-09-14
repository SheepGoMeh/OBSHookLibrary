using System.Runtime.InteropServices;

namespace Sheep.OBSHookLibrary;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
unsafe public struct HookInfo
{
	/* hook version */
	public uint hook_ver_major;
	public uint hook_ver_minor;

	/* capture info */
	public uint type;
	public uint window;
	public uint format;
	public uint cx;
	public uint cy;
	public uint UNUSED_base_cx;
	public uint UNUSED_base_cy;
	public uint pitch;
	public uint map_id;
	public uint map_size;
	public byte flip;

	/* additional options */
	public ulong frame_interval;
	public byte UNUSED_use_scale;
	public byte force_shmem;
	public byte capture_overlay;
	public byte allow_srgb_alias;

	public fixed byte reserved[574];
}
