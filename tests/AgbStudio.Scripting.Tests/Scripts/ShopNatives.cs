using GBA;

public static class ShopNatives
{
    [ScriptNative(waits: true)]
    public static void OpenShop(int stock)
    {
        Debug.Log($"shop opened with {stock} items");
    }

    [ScriptNative]
    public static int Twice(int value) => value * 2;
}
