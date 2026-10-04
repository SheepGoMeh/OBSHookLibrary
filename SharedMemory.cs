using System;
using System.IO.MemoryMappedFiles;

namespace Sheep.OBSHookLibrary;

/// <summary>
/// Named shared memory mapped for the lifetime of the object.
/// </summary>
internal sealed unsafe class SharedMemory: IDisposable
{
	private readonly MemoryMappedFile file;
	private readonly MemoryMappedViewAccessor view;

	public SharedMemory(string name, uint size)
	{
		this.file = MemoryMappedFile.CreateOrOpen(name, size);

		try
		{
			this.view = this.file.CreateViewAccessor(0, size);
		}
		catch
		{
			this.file.Dispose();
			throw;
		}

		byte* pointer = null;
		this.view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
		this.Pointer = pointer;
	}

	/// <summary>
	/// Start of the view, mapped views are allocation granularity aligned.
	/// </summary>
	public byte* Pointer { get; }

	public void Dispose()
	{
		this.view.SafeMemoryMappedViewHandle.ReleasePointer();
		this.view.Dispose();
		this.file.Dispose();
	}
}
