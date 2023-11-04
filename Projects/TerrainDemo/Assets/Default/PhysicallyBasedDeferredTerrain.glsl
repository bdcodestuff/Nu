#shader vertex
#version 410 core

const int TexCoordsOffsetVerts = 6;

const vec2 TexCoordsOffsetFilters[TexCoordsOffsetVerts] =
    vec2[TexCoordsOffsetVerts](
        vec2(1,1),
        vec2(0,1),
        vec2(0,0),
        vec2(1,1),
        vec2(0,0),
        vec2(1,0));

const vec2 TexCoordsOffsetFilters2[TexCoordsOffsetVerts] =
    vec2[TexCoordsOffsetVerts](
        vec2(0,0),
        vec2(1,0),
        vec2(1,1),
        vec2(0,0),
        vec2(1,1),
        vec2(0,1));

uniform mat4 view;
uniform mat4 projection;
uniform int layerCount;

layout (location = 0) in vec3 position;
layout (location = 1) in vec2 texCoords;
layout (location = 2) in vec3 normal;
layout (location = 3) in vec4 splat0;
layout (location = 4) in vec4 splat1;
layout (location = 5) in mat4 model;
layout (location = 9) in vec4 texCoordsOffset;
layout (location = 10) in vec4 albedo;
layout (location = 11) in vec4 material;
layout (location = 12) in float height;
layout (location = 13) in int invertRoughness;

out vec4 positionOut;
out vec2 texCoordsOut;
out vec3 normalOut;
out vec4 splat0Out;
flat out vec4 albedoOut;
flat out vec4 materialOut;
flat out float heightOut;
flat out int invertRoughnessOut;

void main()
{
    if (layerCount == 1) {
        splat0Out = vec4(splat0.r, 0.0, 0.0, 0.0);
    } else if (layerCount == 2) {
        splat0Out = vec4(splat0.r, splat0.g, 0.0, 0.0);
    } else if (layerCount == 3) {
        splat0Out = vec4(splat0.r, splat0.g, splat0.b, 0.0);
    } else if (layerCount == 4) {
        splat0Out = vec4(splat0.r, splat0.g, splat0.b, splat0.a);
    }
    
    positionOut = model * vec4(position, 1.0);
    int texCoordsOffsetIndex = gl_VertexID % TexCoordsOffsetVerts;
    vec2 texCoordsOffsetFilter = TexCoordsOffsetFilters[texCoordsOffsetIndex];
    vec2 texCoordsOffsetFilter2 = TexCoordsOffsetFilters2[texCoordsOffsetIndex];
    texCoordsOut = texCoords + texCoordsOffset.xy * texCoordsOffsetFilter + texCoordsOffset.zw * texCoordsOffsetFilter2;
    albedoOut = albedo;
    materialOut = material;
    normalOut = transpose(inverse(mat3(model))) * normal;
    heightOut = height;
    invertRoughnessOut = invertRoughness;
    gl_Position = projection * view * positionOut;
}

#shader fragment
#version 410 core

const float GAMMA = 2.2;

uniform vec3 eyeCenter;
uniform sampler2D albedoTexture0;
uniform sampler2D roughnessTexture0;
uniform sampler2D ambientOcclusionTexture0;
uniform sampler2D normalTexture0;
uniform sampler2D heightTexture0;
uniform sampler2D albedoTexture1;
uniform sampler2D roughnessTexture1;
uniform sampler2D ambientOcclusionTexture1;
uniform sampler2D normalTexture1;
uniform sampler2D heightTexture1;
uniform sampler2D albedoTexture2;
uniform sampler2D roughnessTexture2;
uniform sampler2D ambientOcclusionTexture2;
uniform sampler2D normalTexture2;
uniform sampler2D heightTexture2;
uniform sampler2D albedoTexture3;
uniform sampler2D roughnessTexture3;
uniform sampler2D ambientOcclusionTexture3;
uniform sampler2D normalTexture3;
uniform sampler2D heightTexture3;

in vec4 positionOut;
in vec2 texCoordsOut;
in vec3 normalOut;
in vec4 splat0Out;
flat in vec4 albedoOut;
flat in vec4 materialOut;
flat in float heightOut;
flat in int invertRoughnessOut;

layout (location = 0) out vec4 position;
layout (location = 1) out vec3 albedo;
layout (location = 2) out vec4 material;
layout (location = 3) out vec4 normalAndHeight;

void main()
{
    // forward position
    position = positionOut;

    // compute spatial converters
    vec3 q1 = dFdx(positionOut.xyz);
    vec3 q2 = dFdy(positionOut.xyz);
    vec2 st1 = dFdx(texCoordsOut);
    vec2 st2 = dFdy(texCoordsOut);
    vec3 normal = normalize(normalOut);
    vec3 tangent = normalize(q1 * st2.t - q2 * st1.t);
    vec3 binormal = -normalize(cross(normal, tangent));
    mat3 toWorld = mat3(tangent, binormal, normal);
    mat3 toTangent = transpose(toWorld);

    // blend height
    float heightBlend =
        texture(heightTexture0, texCoordsOut).r * splat0Out.r +
        texture(heightTexture1, texCoordsOut).r * splat0Out.g +
        texture(heightTexture2, texCoordsOut).r * splat0Out.b +
        texture(heightTexture3, texCoordsOut).r * splat0Out.a;
    
    // compute tex coords in parallax space
    vec3 eyeCenterTangent = toTangent * eyeCenter;
    vec3 positionTangent = toTangent * positionOut.xyz;
    vec3 toEyeTangent = normalize(eyeCenterTangent - positionTangent);
    float height = texture(heightTexture0, texCoordsOut).r * heightOut;
    vec2 parallax = toEyeTangent.xy * height;
    vec2 texCoords = texCoordsOut - parallax;

    // blend other materials
    vec4 albedoBlend =
        texture(albedoTexture0, texCoords) * splat0Out.r +
        texture(albedoTexture1, texCoords) * splat0Out.g +
        texture(albedoTexture2, texCoords) * splat0Out.b +
        texture(albedoTexture3, texCoords) * splat0Out.a;

    vec4 roughnessBlend =
        texture(roughnessTexture0, texCoords) * splat0Out.r +
        texture(roughnessTexture1, texCoords) * splat0Out.g +
        texture(roughnessTexture2, texCoords) * splat0Out.b +
        texture(roughnessTexture3, texCoords) * splat0Out.a;

    float ambientOcclusionBlend =
        texture(ambientOcclusionTexture0, texCoords).b * splat0Out.r +
        texture(ambientOcclusionTexture1, texCoords).b * splat0Out.g +
        texture(ambientOcclusionTexture2, texCoords).b * splat0Out.b +
        texture(ambientOcclusionTexture3, texCoords).b * splat0Out.a;

    vec3 normalBlend =
        texture(normalTexture0, texCoords).xyz * splat0Out.r +
        texture(normalTexture1, texCoords).xyz * splat0Out.g +
        texture(normalTexture2, texCoords).xyz * splat0Out.b +
        texture(normalTexture3, texCoords).xyz * splat0Out.a;
    
    // compute albedo, discarding on zero alpha
    vec4 albedoSample = texture(albedoTexture0, texCoords);
    if (albedoSample.a == 0.0f) discard;
    albedo = pow(albedoSample.rgb, vec3(GAMMA)) * albedoOut.rgb;

    // compute material properties
    float metallic = 0.0f;
    float ambientOcclusion = texture(ambientOcclusionTexture0, texCoords).b * materialOut.b;
    vec4 roughnessSample = texture(roughnessTexture0, texCoords);
    float roughness = roughnessSample.a == 1.0f ? roughnessSample.g : roughnessSample.a;
    roughness = (invertRoughnessOut == 0 ? roughness : 1.0f - roughness) * materialOut.g;
    float emission = 0.0f;
    material = vec4(metallic, roughness, ambientOcclusion, emission);

    // compute normal and height
    normalAndHeight.xyz = normalize(toWorld * (texture(normalTexture0, texCoords).xyz * 2.0 - 1.0));
    normalAndHeight.a = height;
}