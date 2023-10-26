# DrawStuff

A library attempting to make GPU rendering code much easier to write.

Can generate shaders from C# source, as well as the C# boilerplate needed to bind and invoke them correctly.

## Overview

The main goal is to turn mysterious rendering bugs into compile errors, so it's easier to experiment with GPU rendering code.

The library uses a C# [source generator](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/) to avoid a lot of the repetitive & error-prone code that is usually needed to interact with the GPU. Unlike reflection it happens during the build phase, so there's no impact on runtime performance.

The shader code is written in C#, so it's easy to extract type information from it and generate boilerplate code that hooks up to the runtime graphics library.

## Example

This is an example of some C# shader code:

```csharp
[ShaderProgram]
public static partial class BasicShader {

    static Mat4 transform;

    public static void Vertex(in Vec3 pos, out VertexPos vertPos) {
        vertPos = transform * new Vec4(pos.x, pos.y, pos.z, 1f);
    }

    public static void Fragment(out RGBA colour) {
        colour = new(1, 1, 1, 1);
    }
}
```

The source generator provides a config data structure so that we can create a working graphics pipeline as follows:

```csharp
    var pipeline = RenderPipeline.Create(gl, BasicShader.PipelineConfig);
```

This automatically creates the GPU vertex array and configures its data layout to match the shader. If I use this pipeline to render an array of `Vec3` structs, everything will line up correctly. If I try to pass in `Vec4` structs, I'll get a compile error.

The pipeline type is also aware that there's a global variable called `transform`, and expects to receive it as a mandatory argument to the render method. The method signature makes this obvious, and will change if I modify the shader:

```csharp
    pipeline.Render(); // this is a compile error

    pipeline.Render(Mat4.Identity); // this works fine
```

# Warning

The code is just a proof-of-concept, so it doesn't cover many cases other than those tested in `Samples` (which is currently barely anything).
