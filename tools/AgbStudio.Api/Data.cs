namespace GBA;

/// <summary>A row of numbers from 0 to 65535: the type of <c>short[N]</c> fields in Project Settings · Save data.</summary>
[Cpp("unsigned short*")]
public struct ShortList
{
    /// <summary>Gets or sets the number at a position.</summary>
    /// <param name="index">Position from 0.</param>
    [Cpp("static_cast<int>({this}[{0}])", Set = "{this}[{0}] = static_cast<unsigned short>({value})")]
    public int this[int index] { get => 0; set { } }
}

/// <summary>A row of on / off values: the type of <c>bitset N</c> fields in Project Settings · Save data.</summary>
[Cpp("unsigned char*")]
public struct Bitset
{
    /// <summary>Gets or sets bit <paramref name="index"/> (out-of-range indexes read false and are not written).</summary>
    /// <param name="index">Bit index from 0.</param>
    [Cpp("agb_bit_get({this}, {0})", Set = "agb_bit_set({this}, {0}, {value})")]
    public bool this[int index] { get => false; set { } }

    /// <summary>Number of bits.</summary>
    [Cpp("agb_bit_size({this})")]
    public int Length => 0;

    /// <summary>Number of bits that are on.</summary>
    [Cpp("agb_bit_count({this})")]
    public int Count => 0;
}
