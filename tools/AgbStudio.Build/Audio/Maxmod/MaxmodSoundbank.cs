// Ported from Maxmod Utility (mmutil) 1.10.1, main.c (the soundbank mode) and msl.c (MSL_Create).
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>The result of <see cref="MaxmodSoundbank.Write"/>.</summary>
/// <param name="Success">True when there are no errors. The files may be written even when this is false, as mmutil
/// writes them.</param>
/// <param name="Log">What mmutil 1.10.1 prints for the same inputs, followed by the errors and warnings.</param>
/// <param name="Errors">Problems that fail the build, each naming its file.</param>
/// <param name="Warnings">Problems mmutil passes over, each naming its file; the output is still what mmutil writes.</param>
public sealed record MaxmodResult(bool Success, string Log, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

/// <summary>
/// Maxmod soundbank writer, a C# port of the GBA soundbank part of Maxmod Utility (mmutil) 1.10.1. For the same inputs
/// it writes the same soundbank.bin, byte for byte, and the same header as
/// <c>mmutil -o"&lt;bin&gt;" -h"&lt;header&gt;" "&lt;input1&gt;" "&lt;input2&gt;" ...</c>.
/// </summary>
public static class MaxmodSoundbank
{
    /// <summary>
    /// Builds a soundbank from MOD, S3M, XM, IT and WAV files, in the order given (that order sets the MOD_ and SFX_
    /// numbers). <paramref name="headerPath"/> may be null for no header.
    /// </summary>
    public static MaxmodResult Write(IReadOnlyList<string> inputs, string binPath, string? headerPath)
    {
        var log = new MaxmodLog();
        if (inputs.Count == 0)
        {
            log.Error("no input files (mmutil 1.10.1 prints its usage and writes nothing)");
            return Result(log);
        }
        // main.c checks the length of the first input only
        if (inputs[0].Length < 4)
        {
            log.Print("Cannot parse input filename!\n");
            log.File = inputs[0];
            log.Error("the file name is shorter than 4 characters (mmutil 1.10.1 writes nothing)");
            return Result(log);
        }

        var msl = new Msl(log, headerPath != null);
        foreach (var input in inputs)
        {
            if (input.StartsWith('-'))
            {
                log.File = input;
                log.Error("a file name that starts with '-' is an option to mmutil 1.10.1; skipped");
                continue;
            }
            try
            {
                msl.LoadFile(input);
            }
            catch (MaxmodStopException e)
            {
                log.Error(e.Message + "; nothing written");
                return Result(log);
            }
            catch (Exception e) when (e is IndexOutOfRangeException or NullReferenceException or OutOfMemoryException or OverflowException)
            {
                log.Error($"mmutil 1.10.1 reads or writes outside its buffers on this file ({e.GetType().Name}); nothing written");
                return Result(log);
            }
        }

        var bank = msl.Export();
        try
        {
            File.WriteAllBytes(binPath, bank);
            if (headerPath != null)
                File.WriteAllBytes(headerPath, msl.Header());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            log.File = binPath;
            log.Error($"cannot write the soundbank: {e.Message}");
        }
        return Result(log);
    }

    static MaxmodResult Result(MaxmodLog log)
    {
        var text = new System.Text.StringBuilder(log.Text.ToString());
        foreach (var e in log.Errors) text.Append("error: ").Append(e).Append('\n');
        foreach (var w in log.Warnings) text.Append("warning: ").Append(w).Append('\n');
        return new MaxmodResult(log.Errors.Count == 0, text.ToString(), log.Errors.ToArray(), log.Warnings.ToArray());
    }
}
