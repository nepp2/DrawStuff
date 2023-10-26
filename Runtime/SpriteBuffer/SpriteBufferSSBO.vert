#version 430 core

struct Sprite
{
    float x;
    float y;
    float w;
    float h;
    float tx;
    float ty;
    float tw;
    float th;
    uint tint;
};


layout (std430, binding=3) readonly buffer spriteData
{ 
    Sprite sprite[];
};

out vec2 frag_texCoords;
flat out uint frag_tint;

uniform mat4 uTransform;

void main()
{
    int spriteID = gl_VertexID / 4;
    int vert = gl_VertexID % 4;
    float xf = vert < 2 ? 1 : 0;
    float yf = (vert % 3) == 0 ? 1 : 0;
    Sprite s = sprite[spriteID];

    vec2 pos = vec2(s.x + s.w * xf, s.y + s.h * yf);
    gl_Position = uTransform * vec4(pos.x, -pos.y, 0.0, 1.0);

    frag_texCoords = vec2(s.tx + s.tw * xf, s.ty + s.th * yf);
    frag_tint = s.tint;
}
