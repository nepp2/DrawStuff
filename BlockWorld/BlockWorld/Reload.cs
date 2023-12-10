using Silk.NET.Windowing;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler( typeof(BlockWorld.ReloadHandler) )]

namespace BlockWorld;

public record GameLoopHandlers(Action<double> OnUpdate, Action<double> OnRender);

public static class ReloadHandler {
    public static void ClearCache(Type[]? updatedTypes) {
        Console.WriteLine("ReloadHandler.ClearCache");
    }

    public static void UpdateApplication(Type[]? updatedTypes) {
        ReloadFlag = true;
        Console.WriteLine("ReloadHandler.UpdateApplication");
    }

    private static volatile bool ReloadFlag = false;

    private static GameLoopHandlers? ActiveLoop = null;

    private static void ClearActiveLoop(IWindow window) {
        if (ActiveLoop != null) {
            window.Update -= ActiveLoop.OnUpdate;
            window.Render -= ActiveLoop.OnRender;
        }
    }

    private static void AddLoopHandlers(IWindow window) {
        window.Update += ActiveLoop!.OnUpdate;
        window.Render += ActiveLoop!.OnRender;
    }

    public static void StartReloadingWindow(Func<IWindow> createWindow, Func<IWindow, GameLoopHandlers> startFunc) {
        var window = createWindow();
        window.Update += _ => {
            if (ReloadFlag) {
                ClearActiveLoop(window);
                ActiveLoop = startFunc(window);
                AddLoopHandlers(window);
                ReloadFlag = false;
            }
        };
        window.Load += () => {
            ActiveLoop = startFunc(window);
            AddLoopHandlers(window);
        };
        window.Run();
    }
}
