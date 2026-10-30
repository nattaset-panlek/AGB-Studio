using GBA;

public enum SlimeMood { Calm, Angry }

public class SlimeController : Behaviour
{
    public fixed speed = 0.5;
    public int turnEvery = 60;
    public SlimeMood mood = SlimeMood.Calm;
    PlatformerBody body;
    int timer;
    int dir = -1;

    void Start()
    {
        body = Get<PlatformerBody>();
    }

    void Update()
    {
        timer++;
        if (timer >= turnEvery || body.HitWall)
        {
            timer = 0;
            dir = -dir;
        }
        body.vx = speed * dir;
        actor.facingLeft = dir < 0;
    }

    void OnTouch(Actor other)
    {
        if (other.tag == "Player")
        {
            var health = other.Get<Health>();
            if (health != null && health.Damage(1))
            {
                mood = SlimeMood.Angry;
                Debug.Log($"slime hit the player, hearts {health.hearts}, speed {speed}");
            }
        }
    }
}
