using System;

namespace GBA;

/// <summary>
/// 24.8 fixed-point number: the number type for positions, speeds and other fractions (the GBA has no FPU).
/// Write it like a float: <c>fixed speed = 1.5;</c> Decimal literals become exact fixed values at build time.
/// In AGB Studio scripts the type is spelled <c>fixed</c>; in an external IDE write <c>Fixed</c> (or <c>@fixed</c>),
/// because <c>fixed</c> is a reserved word in standard C#.
/// </summary>
[Cpp("agb::fixed")]
public struct @fixed : IEquatable<@fixed>
{
    /// <summary>Raw 24.8 value (256 = 1.0).</summary>
    [Cpp("{this.}raw")]
    public int raw;

    /// <summary>Converts a whole number.</summary>
    /// <param name="value">Whole number.</param>
    [Cpp("agb::fixed({0})")]
    public static implicit operator @fixed(int value) => default;

    /// <summary>Converts a decimal constant (for example <c>1.5</c>) at build time. Only constants are allowed.</summary>
    /// <param name="value">Decimal constant.</param>
    [Cpp("agb::fixed::lit({0})")]
    public static implicit operator @fixed(double value) => default;

    /// <summary>Truncates toward zero, like a C# cast from float to int.</summary>
    /// <param name="value">Fixed value.</param>
    [Cpp("{0}.toInt()")]
    public static explicit operator int(@fixed value) => 0;

    /// <summary>Adds two values.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static @fixed operator +(@fixed a, @fixed b) => default;

    /// <summary>Subtracts two values.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static @fixed operator -(@fixed a, @fixed b) => default;

    /// <summary>Multiplies two values.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static @fixed operator *(@fixed a, @fixed b) => default;

    /// <summary>Multiplies by a whole number (exact, no rounding).</summary>
    /// <param name="a">Fixed value.</param>
    /// <param name="b">Whole number.</param>
    public static @fixed operator *(@fixed a, int b) => default;

    /// <summary>Multiplies a whole number by a fixed value.</summary>
    /// <param name="a">Whole number.</param>
    /// <param name="b">Fixed value.</param>
    [Cpp("{1} * {0}", Prec = 14)]
    public static @fixed operator *(int a, @fixed b) => default;

    /// <summary>Divides two values (division by zero gives the largest value).</summary>
    /// <param name="a">Dividend.</param>
    /// <param name="b">Divisor.</param>
    public static @fixed operator /(@fixed a, @fixed b) => default;

    /// <summary>Divides by a whole number.</summary>
    /// <param name="a">Dividend.</param>
    /// <param name="b">Whole divisor.</param>
    public static @fixed operator /(@fixed a, int b) => default;

    /// <summary>Negates a value.</summary>
    /// <param name="a">Value.</param>
    public static @fixed operator -(@fixed a) => default;

    /// <summary>Adds one.</summary>
    /// <param name="a">Value.</param>
    public static @fixed operator ++(@fixed a) => default;

    /// <summary>Subtracts one.</summary>
    /// <param name="a">Value.</param>
    public static @fixed operator --(@fixed a) => default;

    /// <summary>True when both values are equal.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static bool operator ==(@fixed a, @fixed b) => true;

    /// <summary>True when the values differ.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static bool operator !=(@fixed a, @fixed b) => false;

    /// <summary>Less than.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static bool operator <(@fixed a, @fixed b) => false;

    /// <summary>Greater than.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static bool operator >(@fixed a, @fixed b) => false;

    /// <summary>Less than or equal.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static bool operator <=(@fixed a, @fixed b) => false;

    /// <summary>Greater than or equal.</summary>
    /// <param name="a">Left value.</param>
    /// <param name="b">Right value.</param>
    public static bool operator >=(@fixed a, @fixed b) => false;

    /// <summary>Makes a value from its raw 24.8 representation.</summary>
    /// <param name="raw">Raw value (256 = 1.0).</param>
    [Cpp("agb::fixed::fromRaw({0})")]
    public static @fixed FromRaw(int raw) => default;

    /// <summary>Rounds toward negative infinity.</summary>
    [Cpp("{this.}floor()")]
    public int Floor() => 0;

    /// <summary>Rounds to the nearest whole number.</summary>
    [Cpp("{this.}round()")]
    public int Round() => 0;

    /// <summary>Truncates toward zero.</summary>
    [Cpp("{this.}toInt()")]
    public int ToInt() => 0;

    /// <summary>Absolute value.</summary>
    /// <param name="v">Value.</param>
    [Cpp("agb::abs({0})")]
    public static @fixed Abs(@fixed v) => default;

    /// <summary>The smaller of two values.</summary>
    /// <param name="a">First value.</param>
    /// <param name="b">Second value.</param>
    [Cpp("agb::min({0}, {1})")]
    public static @fixed Min(@fixed a, @fixed b) => default;

    /// <summary>The larger of two values.</summary>
    /// <param name="a">First value.</param>
    /// <param name="b">Second value.</param>
    [Cpp("agb::max({0}, {1})")]
    public static @fixed Max(@fixed a, @fixed b) => default;

    /// <summary>Limits a value to a range.</summary>
    /// <param name="v">Value.</param>
    /// <param name="lo">Lowest result.</param>
    /// <param name="hi">Highest result.</param>
    [Cpp("agb::clamp({0}, {1}, {2})")]
    public static @fixed Clamp(@fixed v, @fixed lo, @fixed hi) => default;

    /// <summary>Sine over a 256-step circle (angle 0..255, 64 = a quarter turn).</summary>
    /// <param name="angle">Angle in 1/256 turns.</param>
    [Cpp("agb::math::sin(static_cast<agb::u8>({0}))")]
    public static @fixed Sin(int angle) => default;

    /// <summary>Cosine over a 256-step circle (angle 0..255, 64 = a quarter turn).</summary>
    /// <param name="angle">Angle in 1/256 turns.</param>
    [Cpp("agb::math::cos(static_cast<agb::u8>({0}))")]
    public static @fixed Cos(int angle) => default;

    /// <summary>Square root.</summary>
    /// <param name="v">Value (negative values give 0).</param>
    [Cpp("agb::math::sqrt({0})")]
    public static @fixed Sqrt(@fixed v) => default;

    /// <summary>True when the other value is equal.</summary>
    /// <param name="other">Other value.</param>
    public bool Equals(@fixed other) => raw == other.raw;

    /// <summary>True when the object is an equal fixed value.</summary>
    /// <param name="obj">Object to compare.</param>
    public override bool Equals(object obj) => obj is @fixed f && Equals(f);

    /// <summary>Hash of the raw value.</summary>
    public override int GetHashCode() => raw;
}

/// <summary>Two fixed values: a position or a velocity in pixels.</summary>
[Cpp("agb::fixed2")]
public struct fixed2 : IEquatable<fixed2>
{
    /// <summary>Horizontal part.</summary>
    public @fixed x;

    /// <summary>Vertical part (down is positive).</summary>
    public @fixed y;

    /// <summary>Creates a vector.</summary>
    /// <param name="x">Horizontal part.</param>
    /// <param name="y">Vertical part.</param>
    [Cpp("agb::fixed2{{0}, {1}}")]
    public fixed2(@fixed x, @fixed y) { this.x = x; this.y = y; }

    /// <summary>Adds two vectors.</summary>
    /// <param name="a">Left vector.</param>
    /// <param name="b">Right vector.</param>
    public static fixed2 operator +(fixed2 a, fixed2 b) => default;

    /// <summary>Subtracts two vectors.</summary>
    /// <param name="a">Left vector.</param>
    /// <param name="b">Right vector.</param>
    public static fixed2 operator -(fixed2 a, fixed2 b) => default;

    /// <summary>Scales a vector.</summary>
    /// <param name="a">Vector.</param>
    /// <param name="s">Scale.</param>
    public static fixed2 operator *(fixed2 a, @fixed s) => default;

    /// <summary>True when both parts are equal.</summary>
    /// <param name="a">Left vector.</param>
    /// <param name="b">Right vector.</param>
    public static bool operator ==(fixed2 a, fixed2 b) => true;

    /// <summary>True when a part differs.</summary>
    /// <param name="a">Left vector.</param>
    /// <param name="b">Right vector.</param>
    public static bool operator !=(fixed2 a, fixed2 b) => false;

    /// <summary>True when the other vector is equal.</summary>
    /// <param name="other">Other vector.</param>
    public bool Equals(fixed2 other) => x == other.x && y == other.y;

    /// <summary>True when the object is an equal vector.</summary>
    /// <param name="obj">Object to compare.</param>
    public override bool Equals(object obj) => obj is fixed2 v && Equals(v);

    /// <summary>Hash of both parts.</summary>
    public override int GetHashCode() => x.raw * 31 + y.raw;
}
