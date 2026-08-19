namespace DesktopFly.Rendering;

public static class ShaderSources
{
    public const string MeshVertexShader = @"#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vTexCoord;

void main()
{
    vec4 worldPos = uModel * vec4(aPos, 1.0);
    vWorldPos = worldPos.xyz;
    vNormal = normalize(mat3(transpose(inverse(uModel))) * aNormal);
    vTexCoord = aTexCoord;
    gl_Position = uProjection * uView * worldPos;
}
";

    public const string MeshFragmentShader = @"#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vTexCoord;

out vec4 FragColor;

uniform vec4 uDiffuse;
uniform vec4 uSpecular;
uniform vec4 uEmission;
uniform float uShininess;
uniform float uOpacity;
uniform bool uUseTexture;
uniform sampler2D uTexture;

uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uAmbientColor;
uniform vec3 uCameraPos;

void main()
{
    vec4 baseColor = uDiffuse;
    if (uUseTexture)
    {
        baseColor *= texture(uTexture, vTexCoord);
    }

    vec3 N = normalize(vNormal);
    vec3 L = normalize(-uLightDir);
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 H = normalize(L + V);

    float diff = max(dot(N, L), 0.0);
    vec3 diffuse = diff * uLightColor * baseColor.rgb;
    vec3 ambient = uAmbientColor * baseColor.rgb;

    float spec = 0.0;
    if (diff > 0.0)
    {
        spec = pow(max(dot(N, H), 0.0), uShininess * 128.0);
    }
    vec3 specular = spec * uSpecular.rgb * uLightColor;

    vec3 finalRgb = ambient + diffuse + specular + uEmission.rgb;
    float finalAlpha = baseColor.a * uOpacity;

    FragColor = vec4(finalRgb, finalAlpha);
}
";

    public const string PointVertexShader = @"#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec4 aColor;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform float uPointSize;

out vec4 vColor;

void main()
{
    vec4 worldPos = uModel * vec4(aPos, 1.0);
    vec4 clipPos = uProjection * uView * worldPos;
    gl_Position = clipPos;
    gl_PointSize = uPointSize;
    vColor = aColor;
}
";

    public const string PointFragmentShader = @"#version 330 core
in vec4 vColor;
out vec4 FragColor;

uniform float uOpacity;

void main()
{
    vec2 coord = gl_PointCoord - vec2(0.5);
    if (length(coord) > 0.5)
        discard;

    FragColor = vec4(vColor.rgb, vColor.a * uOpacity);
}
";
}
