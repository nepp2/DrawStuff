using Silk.NET.Input;
using System.Numerics;

namespace BlockWorld; 

public class KeyboardCamera {

    public float ZoomPower = 0;
    public float ZoomSpeed = 1;
    public float Speed = 1;
    public float Angle = 0;
    public Vector2 Pos = Vector2.Zero;

    public float ZoomFactor => MathF.Pow(2, ZoomPower);

    public void Update(IKeyboard keyboard, double seconds) {
        var zoomedSpeed = Speed / MathF.Pow(2, ZoomPower);
        if (keyboard.IsKeyPressed(Key.Left))
            Angle += (float)seconds;
        if (keyboard.IsKeyPressed(Key.Right))
            Angle -= (float)seconds;
        if (keyboard.IsKeyPressed(Key.Up))
            Pos -= new Vector2(MathF.Sin(-Angle), MathF.Cos(-Angle)) * (float)seconds * zoomedSpeed;
        if (keyboard.IsKeyPressed(Key.Down))
            Pos += new Vector2(MathF.Sin(-Angle), MathF.Cos(-Angle)) * (float)seconds * zoomedSpeed;
        if (keyboard.IsKeyPressed(Key.W))
            ZoomPower += ZoomSpeed * (float)seconds;
        if (keyboard.IsKeyPressed(Key.S))
            ZoomPower -= ZoomSpeed * (float)seconds;
        if (keyboard.IsKeyPressed(Key.A))
            Pos += new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * (float)seconds * zoomedSpeed;
        if (keyboard.IsKeyPressed(Key.D))
            Pos -= new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * (float)seconds * zoomedSpeed;
    }
}
