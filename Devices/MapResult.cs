using System;

namespace Sheep.OBSHookLibrary.Devices;

public class MapResult(bool success, uint rowPitch, IntPtr dataPointer)
{
	public bool Success { get; set; } = success;
	public bool Failure => !this.Success;
	public uint RowPitch { get; set; } = rowPitch;
	public IntPtr DataPointer { get; set; } = dataPointer;

	public static implicit operator bool(MapResult mapResult) => mapResult.Success;
}
