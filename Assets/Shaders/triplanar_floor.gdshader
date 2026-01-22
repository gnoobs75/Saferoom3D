shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

// Triplanar mapping shader for floors/ceilings - optimized for horizontal surfaces
// Projects texture primarily from Y axis with blending for angled surfaces

// Texture inputs
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;

// Material properties
uniform vec4 albedo_color : source_color = vec4(0.35, 0.30, 0.25, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.92;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float normal_strength : hint_range(0.0, 3.0) = 2.5;

// Triplanar settings
uniform float texture_scale : hint_range(0.1, 10.0) = 0.25;
uniform float blend_sharpness : hint_range(1.0, 16.0) = 4.0;

// Wet/puddle effect
uniform float wet_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec3 wet_color : source_color = vec3(0.15, 0.12, 0.10);
uniform float wet_roughness : hint_range(0.0, 1.0) = 0.3;

// Dust/dirt accumulation
uniform float dust_amount : hint_range(0.0, 1.0) = 0.1;
uniform vec3 dust_color : source_color = vec3(0.4, 0.35, 0.28);

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

vec3 triplanar_texture(sampler2D tex, vec3 pos, vec3 normal, float scale) {
    vec3 blend = abs(normal);
    blend = pow(blend, vec3(blend_sharpness));
    blend /= (blend.x + blend.y + blend.z);

    vec3 x_projection = texture(tex, pos.zy * scale).rgb;
    vec3 y_projection = texture(tex, pos.xz * scale).rgb;
    vec3 z_projection = texture(tex, pos.xy * scale).rgb;

    return x_projection * blend.x + y_projection * blend.y + z_projection * blend.z;
}

vec3 triplanar_normal(sampler2D tex, vec3 pos, vec3 normal, float scale) {
    vec3 blend = abs(normal);
    blend = pow(blend, vec3(blend_sharpness));
    blend /= (blend.x + blend.y + blend.z);

    vec3 x_normal = texture(tex, pos.zy * scale).rgb * 2.0 - 1.0;
    vec3 y_normal = texture(tex, pos.xz * scale).rgb * 2.0 - 1.0;
    vec3 z_normal = texture(tex, pos.xy * scale).rgb * 2.0 - 1.0;

    vec3 x_world_normal = vec3(0.0, x_normal.y, -x_normal.x);
    vec3 y_world_normal = vec3(x_normal.x, 0.0, x_normal.y);
    vec3 z_world_normal = vec3(x_normal.x, x_normal.y, 0.0);

    return normalize(
        normal +
        (x_world_normal * blend.x + y_world_normal * blend.y + z_world_normal * blend.z)
    );
}

// Simple noise function for variation
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
    // Get triplanar sampled albedo
    vec3 albedo = triplanar_texture(albedo_texture, world_position, world_normal, texture_scale);
    albedo *= albedo_color.rgb;

    float final_roughness = roughness;

    // Add dust accumulation (procedural noise-based)
    if (dust_amount > 0.0) {
        float dust_noise = noise(world_position.xz * 2.0);
        dust_noise = smoothstep(0.3, 0.7, dust_noise) * dust_amount;
        albedo = mix(albedo, dust_color, dust_noise * 0.5);
    }

    // Add wet/puddle effect (also noise-based)
    if (wet_amount > 0.0) {
        float wet_noise = noise(world_position.xz * 0.5 + vec2(42.0, 17.0));
        wet_noise = smoothstep(1.0 - wet_amount, 1.0, wet_noise);
        albedo = mix(albedo, wet_color, wet_noise * 0.6);
        final_roughness = mix(final_roughness, wet_roughness, wet_noise);
    }

    // Get triplanar sampled normal
    vec3 normal_map = triplanar_normal(normal_texture, world_position, world_normal, texture_scale);

    // Output
    ALBEDO = albedo;
    ROUGHNESS = final_roughness;
    METALLIC = metallic;
    NORMAL = normalize(mix(NORMAL, normal_map, normal_strength));
}
