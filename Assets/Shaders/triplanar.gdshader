shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

// Triplanar mapping shader - eliminates texture stretching on walls and complex geometry
// Projects texture from 3 axes and blends based on surface normal

// Texture inputs
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;

// Material properties
uniform vec4 albedo_color : source_color = vec4(0.38, 0.33, 0.28, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.88;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float normal_strength : hint_range(0.0, 3.0) = 2.0;

// Triplanar settings
uniform float texture_scale : hint_range(0.1, 10.0) = 1.0;
uniform float blend_sharpness : hint_range(1.0, 16.0) = 4.0;

// Optional detail texture for close-up variation
uniform sampler2D detail_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform float detail_scale : hint_range(1.0, 20.0) = 8.0;
uniform float detail_strength : hint_range(0.0, 1.0) = 0.3;
uniform bool use_detail = false;

// Moss/weathering effect
uniform vec3 moss_color : source_color = vec3(0.15, 0.25, 0.12);
uniform float moss_amount : hint_range(0.0, 1.0) = 0.0;
uniform float moss_height_start : hint_range(-10.0, 10.0) = 0.0;
uniform float moss_height_fade : hint_range(0.1, 5.0) = 2.0;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
    // Pass world space position and normal to fragment shader
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

vec3 triplanar_texture(sampler2D tex, vec3 pos, vec3 normal, float scale) {
    // Calculate blend weights from normal
    vec3 blend = abs(normal);
    // Sharpen the blend to reduce muddy transitions
    blend = pow(blend, vec3(blend_sharpness));
    // Normalize so weights sum to 1
    blend /= (blend.x + blend.y + blend.z);

    // Sample texture from each axis
    vec3 x_projection = texture(tex, pos.zy * scale).rgb;
    vec3 y_projection = texture(tex, pos.xz * scale).rgb;
    vec3 z_projection = texture(tex, pos.xy * scale).rgb;

    // Blend based on normal direction
    return x_projection * blend.x + y_projection * blend.y + z_projection * blend.z;
}

vec3 triplanar_normal(sampler2D tex, vec3 pos, vec3 normal, float scale) {
    // Calculate blend weights
    vec3 blend = abs(normal);
    blend = pow(blend, vec3(blend_sharpness));
    blend /= (blend.x + blend.y + blend.z);

    // Sample normal map from each axis
    vec3 x_normal = texture(tex, pos.zy * scale).rgb * 2.0 - 1.0;
    vec3 y_normal = texture(tex, pos.xz * scale).rgb * 2.0 - 1.0;
    vec3 z_normal = texture(tex, pos.xy * scale).rgb * 2.0 - 1.0;

    // Swizzle normals to world space orientation for each projection axis
    vec3 x_world_normal = vec3(0.0, x_normal.y, -x_normal.x);
    vec3 y_world_normal = vec3(x_normal.x, 0.0, x_normal.y);
    vec3 z_world_normal = vec3(x_normal.x, x_normal.y, 0.0);

    // Blend and add to base normal
    return normalize(
        normal +
        (x_world_normal * blend.x + y_world_normal * blend.y + z_world_normal * blend.z)
    );
}

void fragment() {
    // Get triplanar sampled albedo
    vec3 albedo = triplanar_texture(albedo_texture, world_position, world_normal, texture_scale);
    albedo *= albedo_color.rgb;

    // Add detail texture if enabled (for close-up variation)
    if (use_detail) {
        vec3 detail = triplanar_texture(detail_texture, world_position, world_normal, detail_scale);
        // Overlay blend mode for detail
        albedo = mix(albedo, albedo * detail * 2.0, detail_strength);
    }

    // Apply moss/weathering based on height
    if (moss_amount > 0.0) {
        float height_factor = smoothstep(moss_height_start, moss_height_start + moss_height_fade, world_position.y);
        float moss_mask = (1.0 - height_factor) * moss_amount;
        // Also apply more moss in crevices (upward-facing normals)
        moss_mask *= mix(0.5, 1.0, max(0.0, world_normal.y));
        albedo = mix(albedo, moss_color, moss_mask);
    }

    // Get triplanar sampled normal
    vec3 normal_map = triplanar_normal(normal_texture, world_position, world_normal, texture_scale);

    // Output
    ALBEDO = albedo;
    ROUGHNESS = roughness;
    METALLIC = metallic;
    NORMAL = normalize(mix(NORMAL, normal_map, normal_strength));
}
