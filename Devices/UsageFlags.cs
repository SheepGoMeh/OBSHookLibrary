using System;

namespace Sheep.OBSHookLibrary.Devices;

[Flags]
public enum UsageFlags: uint
{
	CopySource = 2048,
	ResolveSource = 8192
}
