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
partial class BasicShader {

    Mat4 transform;

    Vec4 Vertex(Vec3 pos) =>
        transform * vec4(pos.x, pos.y, pos.z, 1f);

    RGBA Fragment() =>
        rgba(1, 1, 1, 1);

}
```

The source generator provides a config data structure so that we can create a working shader as follows:

```csharp
var ds = IDrawStuff.StartDrawing(window);
var shader = ds.LoadShader(gl, BasicShader.Config);
```

This shader is type-specialised so that it will only accept a specific type of vertex data, and it will force the user to provide any global shader variables referenced by the shader code. In the case of `BasicShader`, this means it expects an array of `Vec3` structs and a `Mat4` transform value:

```csharp
// the type `Shader<Vec3, Mat4>` is inferred from the config value
var shader = ds.LoadShader(gl, BasicShader.Config);
var transform = Mat4.Identity;

void Draw(TriangleArray<Vec3> array3D) {
    shader.Draw(array3D); // this is a compile error - missing parameter
    shader.Draw(array3D, transform); // this works fine
}

void Draw(TriangleArray<Vec2> array2D) {
    shader.Draw(array2D, transform); // this is a compile error - wrong vertex type
}
```

# Warning

The code is just a proof-of-concept, so it doesn't cover many cases other than those tested in `Samples` (which is currently barely anything).
