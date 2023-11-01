namespace DrawStuff;

public class Framerate {
    double totalFrames = 0;
    double totalSeconds = 0;

    public void LogFrame(double deltaSeconds) {
        if (totalSeconds > 1) {
            totalFrames /= 2;
            totalSeconds /= 2;
        }
        totalFrames += 1;
        totalSeconds += deltaSeconds;
    }

    public int GetFramesPerSecond() {
        return (int)(totalFrames / totalSeconds);
    }
}
