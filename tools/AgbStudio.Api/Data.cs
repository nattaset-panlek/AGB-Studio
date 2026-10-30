namespace GBA;

/// <summary>A row of on / off values: the type of <c>bitset N</c> fields in Project Settings · Save data.</summary>
[Cpp("agbgen::Bitset")]
public struct Bitset
{
    /// <summary>Gets or sets bit <paramref name="index"/> (out-of-range indexes read false and are not written).</summary>
    /// <param name="index">Bit index from 0.</param>
    [Cpp("{this.}get({0})", Set = "{this.}set({0}, {value})")]
    public bool this[int index] { get => false; set { } }

    /// <summary>Number of bits.</summary>
    [Cpp("{this.}size()")]
    public int Length => 0;

    /// <summary>Number of bits that are on.</summary>
    [Cpp("{this.}count()")]
    public int Count => 0;
}
