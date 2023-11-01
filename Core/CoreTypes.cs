
namespace DrawStuff;

public record struct Colour {
    public uint RGBA { get; }

    public Colour(byte r, byte g, byte b, byte a) {
        RGBA = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
    }
    public Colour(float r, float g, float b, float a)
        : this((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255)) { }

    public Colour(double r, double g, double b, double a)
    : this((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255)) { }

    public static Colour White = new(255, 255, 255, 255);
}

public class ValidationException : Exception {
    public ValidationException(string message) : base(message) { }
}

public class Validation {
    public static void Assert(bool condition, string message = "Validation failed") {
        if(!condition) throw new ValidationException(message);
    }
}
