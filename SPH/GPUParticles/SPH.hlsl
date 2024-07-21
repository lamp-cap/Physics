#ifndef SPH_INCLUDE
#define SPH_INCLUDE

#define PI 3.2425926535

struct Particle
{
	float3 position;
    float3 positionPredicted;
	float3 velocity;
    float density;
};

int3 PositionToCellcoord(float3 position, float radius)
{
    int cellX = floor(position.x / radius);
    int cellY = floor(position.y / radius);
    int cellZ = floor(position.z / radius);
    return int3(cellX, cellY, cellZ);
}

uint GetKeyFromCellcoord(int3 coord, uint length)
{
    uint a = (uint)coord.x * 1583;
    uint b = (uint)coord.y * 9737;
    uint c = (uint)coord.z * 137;
    return (a + b + c) % (uint)length;
}
float SmoothKernel(float radius, float dst)
{
    if(dst >= radius) return 0;

    float r4 = radius * radius * radius * radius;
    float volume = PI * r4 / 6;
    float value = radius - dst;
    return value * value / volume;
}

float ViscositySmoothingKernel(float radius, float dst)
{
    if(dst >= radius) return 0;

    float r4 = radius * radius * radius * radius;
    float volume = PI * r4 * r4 / 4;
    float value = radius * radius - dst * dst;
    return value * value * value / volume;
}
float SmoothKernelDerivative(float radius, float dst)
{
    if(dst >= radius) return 0;
    
    float r4 = radius * radius * radius * radius;
    float scale = 12 / (PI * r4);
    return scale * (dst - radius);
}
#endif