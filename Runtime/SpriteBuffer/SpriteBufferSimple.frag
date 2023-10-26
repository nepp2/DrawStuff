#version 330 core

in vec2 frag_texCoords;
flat in uint frag_tint;

out vec4 out_color;

uniform sampler2D uTexture;

vec4 toRGBA(uint v) {
    return vec4(v >> 24, (v >> 16) & 255u, (v >> 8) & 255u, v & 255u) / 255;
}

void main()
{
    vec4 tint = toRGBA(frag_tint);
    vec4 tex = texture(uTexture, frag_texCoords);
    if(tex.w < 0.5)
        discard;
    out_color = vec4(tint.x * tex.x, tint.y * tex.y, tint.z * tex.z, tex.w);
}
