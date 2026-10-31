namespace AgbStudio.Editor.Tests.Support;

/// <summary>
/// The test classes that start the mGBA core. Libretro keeps global state, so the process has one core (D-06): the
/// editors share it through <see cref="AgbStudio.Editor.Services.EmulatorHost"/> and the classes of this collection never
/// run in parallel with each other. A Fixture gives its editor's hold on the core back when it is disposed.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EmulatorCollection
{
    public const string Name = "Emulator core";
}
