using System.Threading;
using System.Threading.Tasks;

using Vanara.PInvoke;

namespace Sheep.OBSHookLibrary;

internal unsafe class ThreadData
{
	public readonly Kernel32.CRITICAL_SECTION[] Mutexes = new Kernel32.CRITICAL_SECTION[3];
	public Kernel32.CRITICAL_SECTION DataMutex;
	public volatile void* CurrentData = null;
	public readonly byte*[] SharedMemoryTextures = new byte*[2];
	public Task? CopyThread;
	public CancellationTokenSource? CopyThreadCancellationTokenSource;
	public Kernel32.SafeEventHandle? CopyEvent;
	public Kernel32.SafeEventHandle? StopEvent;
	public volatile int CurrentTexture;
	public uint Pitch;
	public uint Cy;
	public volatile bool[] LockedTextures = new bool[3];
}
