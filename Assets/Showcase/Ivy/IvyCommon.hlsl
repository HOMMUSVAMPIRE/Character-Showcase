// What the ivy's compute shader (IvyLeaves.compute) and its leaves' shader (Ivy.shader) share: a leaf, and a wall face.
#ifndef MERIDIAN_IVY_COMMON_INCLUDED
#define MERIDIAN_IVY_COMMON_INCLUDED

// A leaf, as IvyLeaves.compute writes it and Ivy.shader reads it: 14 floats (IvyField.LeafStride).
struct Leaf
{
    float3 position; // where its stalk meets it, in the world
    float3 normal;   // its wall's outward normal
    float3 down;     // the way it hangs, in its wall's plane (from its base toward its tip, before it tilts out)
    float size;      // its length (m)
    float tilt;      // how far its tip comes out from the wall (radians)
    float hash;      // 0 to 1, its own variation
    float shade;     // 1 lit in full, less deep in the growth, where the leaves over it shade it
    float sway;      // how much it flutters: 1 on ivy, more on a vine, most toward its tip
};

// A wall face the ivy grows on, in the world (IvyField.FaceStride: 20 floats): its corner (its foot's start) and width
// (w), the way along it and its height (w), the way up it and its mask's layer (w), the way out from it and whether its
// growth hangs (w: 1 for vines, 0 for ivy), and the share of its mask's layer its mask fills (x, y: a layer's as big as the
// largest face's mask).
struct IvyFace
{
    float4 corner;
    float4 along;
    float4 up;
    float4 outward;
    float4 mask;
};

#endif
