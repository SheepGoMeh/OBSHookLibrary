using System;

using Sheep.OBSHookLibrary.Devices;

namespace Sheep.OBSHookLibrary;

/// <summary>
/// A single published capture, owns every resource OBS reads it through.
/// </summary>
internal interface ICaptureSession: IDisposable
{
	public void Capture(IGraphicsTexture texture);
}
