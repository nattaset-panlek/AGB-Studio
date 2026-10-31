using AgbStudio.Core.Project;

namespace AgbStudio.Build.Pipeline;

/// <summary>G-HW.06: the bytes one save slot needs (the generated <c>struct GameState</c>) and the bytes a slot has on
/// each save chip, as the runtime lays them out (runtime/src/core/save.cpp): every slot keeps two copies, each copy
/// starts with a 16-byte header, Flash copies take whole 4 KB sectors, EEPROM copies whole 8-byte blocks, and a slot
/// holds at most 4 KB.</summary>
public static class SaveLayout
{
    public const int CopyHeaderBytes = 16;
    public const int MaxSlotBytes = 4096;
    public const int SaveHeaderBytes = 8;      // agb::SaveHeader: scene, x, y, flags
    public const int OptionsBytes = 8;         // agb::save::kOptionsBytes: the global options block outside the slots
    public const int ScriptStoreBytes = 160;   // agb::script::PersistentStore: 256 flag bits + 32 vars

    public static int ChipBytes(SaveType type) => type switch
    {
        SaveType.Sram32K => 32 * 1024,
        SaveType.Flash64K => 64 * 1024,
        SaveType.Flash128K => 128 * 1024,
        SaveType.Eeprom8K => 8 * 1024,
        _ => 0,
    };

    public static string ChipName(SaveType type) => type switch
    {
        SaveType.Sram32K => "SRAM 32 KB",
        SaveType.Flash64K => "Flash 64 KB",
        SaveType.Flash128K => "Flash 128 KB",
        SaveType.Eeprom8K => "EEPROM 8 KB",
        _ => "No save",
    };

    /// <summary>Size and alignment of a save field as GameBuilder declares it in C++.</summary>
    public static (int Size, int Align) Field(string type)
    {
        var t = type.Trim();
        if (t.StartsWith("bitset", StringComparison.Ordinal)) return ((int.Parse(t[6..].Trim()) + 7) / 8, 1);
        var open = t.IndexOf('[');
        if (open > 0) return (2 * int.Parse(t[(open + 1)..t.IndexOf(']')]), 2);
        return t switch
        {
            "bool" or "byte" => (1, 1),
            "short" => (2, 2),
            "fixed2" => (8, 4),
            _ => (4, 4),  // int, fixed and anything else (declared as int)
        };
    }

    // The order GameBuilder writes the fields in ("largest fields first to avoid padding").
    static int Order(string type)
    {
        var t = type.Trim();
        return t is "int" or "fixed" or "fixed2" ? 4 : t == "short" || t.Contains('[') ? 2 : 1;
    }

    /// <summary>sizeof(GameState): the save header, the fields and, when scripts are compiled, the saved script store.</summary>
    public static int SlotBytes(IEnumerable<SaveField> fields, bool scriptStore)
    {
        var offset = SaveHeaderBytes;
        var align = 2;
        foreach (var f in fields.OrderByDescending(f => Order(f.Type)))
        {
            var (size, a) = Field(f.Type);
            offset = (offset + a - 1) / a * a + size;
            align = Math.Max(align, a);
        }
        if (scriptStore)
        {
            offset = (offset + 3) / 4 * 4 + ScriptStoreBytes;
            align = 4;
        }
        return (offset + align - 1) / align * align;
    }

    /// <summary>Bytes at the end of the chip kept for the options block (music and effects volume, language, button
    /// remap, scope log T-D08, P4.12, P2.10): a whole sector on Flash, else <see cref="OptionsBytes"/>.</summary>
    public static int OptionsReserve(SaveType type) => type switch
    {
        SaveType.Flash64K or SaveType.Flash128K => 4096,
        SaveType.None => 0,
        _ => OptionsBytes,
    };

    /// <summary>The bytes one slot can hold with this save type and slot count (0 when the chip has no room).</summary>
    public static int SlotCapacity(SaveType type, int slots)
    {
        var chip = ChipBytes(type);
        if (chip == 0) return 0;
        var per = (chip - OptionsReserve(type)) / (Math.Max(1, slots) * 2);
        per &= type switch
        {
            SaveType.Flash64K or SaveType.Flash128K => ~0xFFF,
            SaveType.Eeprom8K => ~7,
            _ => ~3,
        };
        return Math.Clamp(per - CopyHeaderBytes, 0, MaxSlotBytes);
    }

    /// <summary>The build error when the save data does not fit, else null.</summary>
    public static string? Problem(SaveSettings save, bool scriptStore)
    {
        if (save.Type == SaveType.None) return null;
        var need = SlotBytes(save.Fields, scriptStore);
        var room = SlotCapacity(save.Type, save.Slots);
        if (need <= room) return null;
        var slots = Math.Max(1, save.Slots);
        return $"Save data needs {need} B per slot, but {ChipName(save.Type)} with {slots} {(slots == 1 ? "slot" : "slots")} has room for {room} B per slot " +
               "(each slot keeps two copies). Use fewer slots, a bigger save type or fewer save fields (Project Settings · Save data).";
    }
}
