shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

// Emissive Cracks shader - adds glowing cracks/fissures to stone surfaces
// Great for lava-themed areas, magical corruption, or damaged dungeon floors

// Base texture inputs
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;

// Material properties
uniform vec4 albedo_color : source_color = vec4(0.35, 0.30, 0.25, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.92;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float normal_strength : hint_range(0.0, 3.0) = 2.0;
uniform float texture_scale : hint_range(0.1, 10.0) = 1.0;

// Crack settings
uniform vec3 crack_color : source_color = vec3(1.0, 0.4, 0.1);  // Orange-red lava
uniform vec3 crack_core_color : source_color = vec3(1.0, 0.9, 0.5);  // Hot white-yellow core
uniform float crack_intensity : hint_range(0.0, 5.0) = 2.0;
uniform float crack_scale : hint_range(0.1, 5.0) = 1.0;
uniform float crack_width : hint_range(0.0, 0.5) = 0.15;
uniform float crack_density : hint_range(0.0, 1.0) = 0.4;
uniform float animation_speed : hint_range(0.0, 2.0) = 0.5;

// Procedural noise functions
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

float fbm(vec2 p) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;

    for (int i = 0; i < 5; i++) {
        value += amplitude * noise(p * frequency);
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    return value;
}

// Voronoi for crack pattern
vec2 voronoi(vec2 p) {
    vec2 n = floor(p);
    vec2 f = fract(p);

    float md = 8.0;
    vec2 mr = vec2(0.0);

    for (int j = -1; j <= 1; j++) {
        for (int i = -1; i <= 1; i++) {
            vec2 g = vec2(float(i), float(j));
            vec2 o = vec2(hash(n + g), hash(n + g + vec2(31.0, 17.0)));
            vec2 r = g + o - f;
            float d = dot(r, r);

            if (d < md) {
                md = d;
                mr = r;
            }
        }
    }

    return vec2(sqrt(md), 0.0);
}

// Generate crack pattern
float crack_pattern(vec2 uv, float time) {
    // Use Voronoi edges for crack pattern
    vec2 scaled_uv = uv * crack_scale * 3.0;

    // Get distance to nearest cell edge
    float cell1 = voronoi(scaled_uv).x;
    float cell2 = voronoi(scaled_uv * 0.5 + vec2(5.3, 2.7)).x;

    // Combine for more organic cracks
    float cracks = min(cell1, cell2 * 1.5);

    // Add noise variation to crack width
    float noise_variation = fbm(scaled_uv * 2.0 + time * 0.1) * 0.5 + 0.5;
    float adjusted_width = crack_width * noise_variation;

    // Create crack mask
    float crack_mask = 1.0 - smoothstep(0.0, adjusted_width, cracks);

    // Apply density threshold
    float density_noise = fbm(scaled_uv * 0.5);
    crack_mask *= step(1.0 - crack_density, density_noise);

    return crack_mask;
}

void fragment() {
    vec2 scaled_uv = UV * texture_scale;
    float time = TIME * animation_speed;

    // Sample base textures
    vec3 albedo = texture(albedo_texture, scaled_uv).rgb * albedo_color.rgb;
    vec3 normal_map = texture(normal_texture, scaled_uv).rgb * 2.0 - 1.0;
    normal_map.xy *= normal_strength;
    normal_map = normalize(normal_map);

    // Generate crack pattern
    float crack = crack_pattern(UV, time);

    // Animated pulse for cracks
    float pulse = sin(time * 3.0) * 0.15 + 0.85;

    // Calculate crack emission with hot core
    vec3 emission = vec3(0.0);
    if (crack > 0.0) {
        // Create gradient from edge to core
        float core_factor = smoothstep(0.0, 0.7, crack);
        vec3 crack_emission = mix(crack_color, crack_core_color, core_factor);
        emission = crack_emission * crack * crack_intensity * pulse;

        // Darken albedo where cracks are (burnt stone)
        albedo = mix(albedo, vec3(0.05, 0.02, 0.01), crack * 0.8);
    }

    // Output
    ALBEDO = albedo;
    ROUGHNESS = mix(roughness, 0.2, crack); // Cracks are smoother (molten)
    METALLIC = metallic;
    EMISSION = emission;
    NORMAL_MAP = normal_map * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = 1.0;
}
