shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// Dissolve parameters
uniform float dissolve_amount : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D noise_texture : hint_default_white;
uniform float noise_scale : hint_range(0.1, 10.0) = 2.0;
uniform vec3 edge_color : source_color = vec3(1.0, 0.5, 0.0);
uniform float edge_width : hint_range(0.0, 0.3) = 0.1;
uniform float edge_glow : hint_range(0.0, 5.0) = 2.0;

// Original material properties (passed from code)
uniform vec4 albedo_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.8;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    // Sample noise texture for dissolve pattern
    vec2 scaled_uv = UV * noise_scale;
    float noise = texture(noise_texture, scaled_uv).r;

    // Discard pixels based on dissolve amount
    if (noise < dissolve_amount) {
        discard;
    }

    // Calculate distance from dissolve edge
    float edge_distance = noise - dissolve_amount;

    // Glowing edge effect
    float edge_factor = 1.0 - smoothstep(0.0, edge_width, edge_distance);

    // Base albedo
    ALBEDO = albedo_color.rgb;
    ROUGHNESS = roughness;
    METALLIC = metallic;

    // Add glowing edge emission
    EMISSION = edge_color * edge_factor * edge_glow;
}
