using AgbStudio.Build;

namespace AgbStudio.Tests;

public sealed class LintTests
{
    [Fact]
    public void PlantedViolationsAreReported()
    {
        var root = Directory.CreateTempSubdirectory("agblint").FullName;
        Directory.CreateDirectory(Path.Combine(root, "runtime", "src", "render"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "docs", "note.md"), "Done \U0001F680\n");
        File.WriteAllText(Path.Combine(root, "runtime", "src", "render", "bad.cpp"),
            "void f() {\n    REG_DISPCNT = 0;  // should go through commit\n    float speed = 1.5f;\n    oam_mem[0].attr0 = 1;\n}\n");
        File.WriteAllText(Path.Combine(root, "runtime", "src", "render", "good.cpp"), "void g() { commit::setReg(commit::DispCnt, 0); if (x == REG_VCOUNT) {} }\n");
        var issues = Lint.Run(root);
        Assert.Contains(issues, i => i.Rule == "no-emoji" && i.File == "docs/note.md");
        Assert.Contains(issues, i => i.Rule == "hardware-through-commit" && i.Line == 2);
        Assert.Contains(issues, i => i.Rule == "hardware-through-commit" && i.Line == 4);
        Assert.Contains(issues, i => i.Rule == "no-float" && i.Line == 3);
        Assert.DoesNotContain(issues, i => i.File.EndsWith("good.cpp"));
    }

    [Fact]
    public void PokeemeraldNamesAndWrongProductNamesAreReported()
    {
        var root = Directory.CreateTempSubdirectory("agblint").FullName;
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        File.WriteAllText(Path.Combine(root, "tools", "Copied.cs"), "var s = gSaveBlock1Ptr->pos;\nint v = VAR_RESULT;\nPlay(MUS_ROUTE101);\n");
        File.WriteAllText(Path.Combine(root, "tools", "Title.cs"), "Title = \"GBA Studio\";\nvar ok = \"AGB Studio for Game Boy Advance\";\n");
        File.WriteAllText(Path.Combine(root, "tools", "Fine.cs"), "var main = gMainMenu; int speciesCount = 3;\n");
        var issues = Lint.Run(root);
        Assert.Equal(3, issues.Count(i => i.Rule == "no-pokeemerald" && i.File == "tools/Copied.cs"));
        Assert.Contains(issues, i => i.Rule == "product-name" && i.File == "tools/Title.cs" && i.Line == 1);
        Assert.DoesNotContain(issues, i => i.File == "tools/Fine.cs" || (i.File == "tools/Title.cs" && i.Line == 2));
    }

    [Fact]
    public void RepositoryIsClean() => Assert.Empty(Lint.Run(NativeBuild.RepoRoot));
}
