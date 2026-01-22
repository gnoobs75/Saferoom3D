shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

// Parallax Occlusion Mapping shader for stone surfaces
// Creates fake depth by offsetting UVs based on view angle and height map
// Much better visual depth than normal maps alone

// Texture inputs
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D height_texture : hint_default_white, filter_linear_mipmap, repeat_enable;

// Material properties
uniform vec4 albedo_color : source_color = vec4(0.38, 0.33, 0.28, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.88;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float normal_strength : hint_range(0.0, 3.0) = 2.0;

// Parallax settings
uniform float height_scale : hint_range(0.0, 0.2) = 0.05;
uniform int parallax_steps : hint_range(4, 32) = 16;
uniform float texture_scale : hint_range(0.1, 10.0) = 1.0;

// Edge wear effect
uniform float edge_wear : hint_range(0.0, 1.0) = 0.3;
uniform vec3 edge_color : source_color = vec3(0.25, 0.22, 0.18);

varying vec3 tangent_view_dir;

void vertex() {
    // Calculate view direction in tangent space for parallax
    vec3 binormal = cross(NORMAL, TANGENT) * BINORMAL.x;
    mat3 tbn = mat3(TANGENT, binormal, NORMAL);
    vec3 view_dir = (inverse(MODELVIEW_MATRIX) * vec4(0.0, 0.0, 0.0, 1.0)).xyz - VERTEX;
    tangent_view_dir = normalize(tbn * view_dir);
}

// Parallax Occlusion Mapping
vec2 parallax_mapping(vec2 uv, vec3 view_dir) {
    // Number of layers for raymarching
    float num_layers = mix(float(parallax_steps), float(parallax_steps / 2), abs(view_dir.z));
    float layer_depth = 1.0 / num_layers;
    float current_layer_depth = 0.0;

    // Direction and amount to shift UVs per layer
    vec2 p = view_dir.xy * height_scale;
    vec2 delta_uv = p / num_layers;

    vec2 current_uv = uv;
    float current_depth = texture(height_texture, current_uv * texture_scale).r;

    // Raymarch through height layers
    for (int i = 0; i < parallax_steps; i++) {
        if (current_layer_depth >= current_depth) {
            break;
        }
        current_uv -= delta_uv;
        current_depth = texture(height_texture, current_uv * texture_scale).r;
        current_layer_depth += layer_depth;
    }

    // Linear interpolation for smoother result
    vec2 prev_uv = current_uv + delta_uv;
    float after_depth = current_depth - current_layer_depth;
    float before_depth = texture(height_texture, prev_uv * texture_scale).r - current_layer_depth + layer_depth;
    float weight = after_depth / (after_depth - before_depth);

    return mix(current_uv, prev_uv, weight);
}

void fragment() {
    // Apply parallax mapping to UVs
    vec2 parallax_uv = parallax_mapping(UV, tangent_view_dir);

    // Sample textures with parallax-adjusted UVs
    vec3 albedo = texture(albedo_texture, parallax_uv * texture_scale).rgb;
    albedo *= albedo_color.rgb;

    // Sample height for edge wear calculation
    float height = texture(height_texture, parallax_uv * texture_scale).r;

    // Add edge wear on raised areas
    if (edge_wear > 0.0) {
        float wear_mask = smoothstep(0.6, 0.9, height) * edge_wear;
        albedo = mix(albedo, edge_color, wear_mask);
    }

    // Get normal from normal map
    vec3 normal_map = texture(normal_texture, parallax_uv * texture_scale).rgb;
    normal_map = normal_map * 2.0 - 1.0;
    normal_map.xy *= normal_strength;
    normal_map = normalize(normal_map);

    // Output
    ALBEDO = albedo;
    ROUGHNESS = roughness;
    METALLIC = metallic;
    NORMAL_MAP = normal_map * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = 1.0;
}
