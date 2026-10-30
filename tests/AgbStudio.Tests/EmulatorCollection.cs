using AgbStudio.Emu;

namespace AgbStudio.Tests;

// The libretro core keeps global state, so every test that runs ROMs shares one core and runs serially.
[CollectionDefinition("emulator", DisableParallelization = true)]
public sealed class EmulatorCollection : ICollectionFixture<EmulatorFixture>;

public sealed class EmulatorFixture : IDisposable
{
    public LibretroCore Core { get; } = new();
    public void Dispose() => Core.Dispose();
}
