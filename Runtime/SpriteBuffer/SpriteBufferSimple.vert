#version 330 core

layout (location = 0) in vec2 aPosition;

layout (location = 1) in vec2 aTexCoords;

layout (location = 2) in uint aTint;

out vec2 frag_texCoords;
flat out uint frag_tint;

uniform mat4 uTransform;

void main()
{
    gl_Position = uTransform * vec4(aPosition.x, -aPosition.y, 0.0, 1.0);

    frag_texCoords = aTexCoords;
    frag_tint = aTint;
}
