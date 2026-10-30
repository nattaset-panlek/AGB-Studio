using System.Threading.Tasks;
using GBA;

public class Shopkeeper : SceneScript
{
    [Cutscene]
    static async Task Visit()
    {
        ShopNatives.OpenShop(3);
        Game.coins = ShopNatives.Twice(21);
    }
}
