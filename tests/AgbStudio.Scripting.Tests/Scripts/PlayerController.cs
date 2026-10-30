using GBA;

public class PlayerController : Behaviour
{
    [Range(0.5, 3)] public fixed speed = 1.5;
    public fixed jumpPower = 3.5;
    public int lives = 3;
    PlatformerBody body;

    void Start() => body = Get<PlatformerBody>();

    void Update()
    {
        body.vx = Input.AxisX * speed;
        if (Input.Pressed(Key.A) && body.OnGround)
        {
            body.vy = -jumpPower;
            Sound.Play("jump");
        }
    }

    void OnTouch(Actor other)
    {
        if (other.Is<Coin>())
        {
            Game.score += 10;
            other.Destroy();
        }
    }
}
