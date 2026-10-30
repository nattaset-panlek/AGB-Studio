using System.Threading.Tasks;
using GBA;

public class Forest : SceneScript
{
    [Cutscene]
    async Task RescueIntro()
    {
        using var _ = LockAll();
        Music.Play(Songs.Help);
        await Say("forest.help_me");
        await Move(Player, Path.EnterScene);
        Story.Forest = ForestState.Rescued;
    }

    [Cutscene]
    static async Task<int> AskHelp(int times)
    {
        var answer = await Choice("forest.yes", "forest.no");
        if (answer == 0)
        {
            Game.score += 5 * times;
            await Wait(30);
        }
        else
        {
            Sound.Play(Sounds.Coin, 0.5);
        }
        return answer;
    }

    void OnEnter()
    {
        Save.Checkpoint();
    }
}
