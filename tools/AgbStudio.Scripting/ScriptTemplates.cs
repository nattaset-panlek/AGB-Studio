namespace AgbStudio.Scripting;

/// <summary>New Script templates (T-D14): Behaviour, Static helper, Scene script.</summary>
public static class ScriptTemplates
{
    public enum Kind { Behaviour, StaticHelper, SceneScript }

    /// <summary>The text of a new script file in the project namespace (Project Settings · Scripting).</summary>
    public static string Create(Kind kind, string className, string ns)
    {
        var head = "using GBA;\n\n" + (ns.Trim().Length > 0 ? $"namespace {ns.Trim()};\n\n" : "");
        return kind switch
        {
            Kind.Behaviour => head + $$"""
                public class {{className}} : Behaviour
                {
                    public fixed speed = 1;

                    void Start()
                    {
                    }

                    void Update()
                    {
                    }
                }

                """,
            Kind.StaticHelper => head + $$"""
                public static class {{className}}
                {
                    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
                }

                """,
            _ => head + $$"""
                public class {{className}} : SceneScript
                {
                    void OnEnter()
                    {
                    }

                    void OnExit()
                    {
                    }
                }

                """,
        };
    }
}
