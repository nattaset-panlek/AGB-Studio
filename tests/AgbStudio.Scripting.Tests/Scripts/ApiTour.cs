using System.Threading.Tasks;
using GBA;

class Trail
{
    public int length;
}

// Every member of the Scripting API page (T-P18) and the Figma code samples, used once.
public class ApiTour : Behaviour
{
    public fixed speed = 1;
    PlatformerBody body;
    Health health;
    Trail trail;

    void Start()
    {
        body = Get<PlatformerBody>();
        health = Get<Health>();
        trail = new Trail();
        Camera.Follow(actor);
    }

    void Update()
    {
        if (Input.Pressed(Key.A)) body.Jump();
        if (Input.Held(Key.B)) body.vx = Input.AxisX * speed;
        if (Input.Released(Key.Start)) Scene.Pause();
        body.vy = body.vy + Input.AxisY;
        if (Input.AnyKey && (Input.Held(Key.Select) || Input.Held(Key.Right) || Input.Held(Key.Left) || Input.Held(Key.Up) ||
            Input.Held(Key.Down) || Input.Held(Key.R) || Input.Held(Key.L)))
            Sound.Play("jump", 0.8);
        if (Camera.X > 100) Camera.Shake(2, 10);
        actor.pos.x = fixed.Clamp(actor.pos.x, 0, 480);
        var wave = fixed.Abs(fixed.Sin(64));
        if (wave > 0) Debug.Log("hello");
        int roll = Random.Range(1, 6);
        if (roll == 6) Music.Play(Songs.Help);
        else if (roll == 1) Music.Stop();
        Game.collected[3] = true;
        if (Game.bag[0] == ItemId.Potion) Story.Visits = Story.Visits + 1;
        if (Game.score > 100) Celebrate(3);
    }

    void OnTouch(Actor other)
    {
        if (other.Is<Coin>())
        {
            Game.score += 10;
            other.Destroy();
        }
        other.SetParent(actor);
        other.SetParent(null);
        if (other.parent == actor) other.SetActive(other.activeSelf);
        if (other.tag == "Enemy")
        {
            var potion = Items.Get(ItemId.Potion);
            health.Heal(potion.power);
            if (Game.coins >= potion.price) Shop.Buy(ItemId.Potion);
        }
        Text.Show("forest.help_me");
        if (Save.Write(1) && Save.Read(1)) Scene.Load(Save.scene);
        Save.Checkpoint();
        Scene.Load("Forest_02");
        trail.length++;
    }

    [Cutscene]
    async Task Celebrate(int times)
    {
        await Say("forest.help_me");
        await Wait(times);
    }
}
