using Silk.NET.Windowing;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler( typeof(BlockWorld.ReloadHandler) )]

namespace BlockWorld;

public record GameLoopFunctions(Action<double> OnUpdate, Action<double> OnRender);

public interface GameSetup {
    public IWindow CreateWindow();
    GameLoopFunctions Setup(IWindow w);
}

static class ReloadHandler {
    public static void ClearCache(Type[]? updatedTypes) {
        Console.WriteLine("ReloadHandler.ClearCache");
    }

    public static void UpdateApplication(Type[]? updatedTypes) {
        ReloadFlag = true;
        Console.WriteLine("ReloadHandler.UpdateApplication");
    }

    private static volatile bool ReloadFlag = false;

    private static GameLoopFunctions? ActiveLoop = null;

    private static void ClearActiveLoop(IWindow window) {
        if (ActiveLoop != null) {
            window.Update -= ActiveLoop.OnUpdate;
            window.Render -= ActiveLoop.OnRender;
        }
    }

    private static void DoSetup(IWindow window, GameSetup game) {
        ActiveLoop = game.Setup(window);
        window.Update += ActiveLoop.OnUpdate;
        window.Render += ActiveLoop.OnRender;
    }

    public static void StartReloadingWindow(GameSetup game) {
        var window = game.CreateWindow();
        window.Update += _ => {
            if (ReloadFlag) {
                ClearActiveLoop(window);
                DoSetup(window, game);
                ReloadFlag = false;
            }
        };
        window.Load += () => {
            DoSetup(window, game);
        };
        window.Run();
    }
}

public class Program {

    public static void Main(string[] args) {
        ReloadHandler.StartReloadingWindow(new BlockWorldSetup());
    }
}
