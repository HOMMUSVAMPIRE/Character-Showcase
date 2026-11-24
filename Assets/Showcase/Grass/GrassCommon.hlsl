// What the grass's compute shader, its blades' shader and the lawn's shader share: the hash blades are placed and
// varied by, and the clumps (a Voronoi of the world's x and z), so a lawn's tint matches the blades over it.
#ifndef MERIDIAN_GRASS_COMMON_INCLUDED
#define MERIDIAN_GRASS_COMMON_INCLUDED

// A blade, as GrassBlades.compute writes it and Grass.shader reads it: 9 floats (GrassField.BladeStride).
struct Blade
{
    float3 position; // its root, in the world
    float facing;    // which way it bends (radians about the vertical)
    float length;    // m
    float width;     // at the root, m
    float bend;      // 0 straight up, 1 its tip about its length forward
    float hash;      // 0 to 1, the blade's own variation
    float clump;     // 0 to 1, its clump's (see GrassClump)
};

// A well-mixed hash of three integers (Jarzynski and Olano's pcg3d), each output a different random.
uint3 GrassHash(uint3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    return v;
}

// Three randoms, 0 to 1, from three integers.
float3 GrassRandom(uint3 v) { return float3(GrassHash(v)) * (1.0 / 4294967296.0); }

// The clump at p (the world's x and z): a cell of a Voronoi of points jittered on a grid size apart. Its centre, and a
// value 0 to 1 that its blades' length and tint vary by.
void GrassClump(float2 p, float size, out float2 centre, out float value)
{
    float2 cell = floor(p / size);
    float nearest = 1e9;
    centre = p;
    value = 0.5;
    [unroll] for (int y = -1; y <= 1; y++)
    {
        [unroll] for (int x = -1; x <= 1; x++)
        {
            int2 c = int2(cell) + int2(x, y);
            float3 r = GrassRandom(uint3(asuint(c.x), asuint(c.y), 977u));
            float2 seed = (float2(c) + r.xy) * size;
            float2 d = p - seed;
            float d2 = dot(d, d);
            if (d2 < nearest) { nearest = d2; centre = seed; value = r.z; }
        }
    }
}

// A clump's tint, by its value: from a warm, dry green to a cool, lush one, by amount (0: none).
float3 GrassClumpTint(float value, float amount)
{
    float3 tint = lerp(float3(1.14, 1.06, 0.78), float3(0.84, 0.97, 0.96), value);
    return lerp(float3(1.0, 1.0, 1.0), tint, amount);
}

#endif
