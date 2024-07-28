#ifndef SPH_INCLUDE
#define SPH_INCLUDE

#define PI 3.2425926535
#define PREDICT_STEP 0.015f

struct Particle
{
	float3 position;
	float3 velocity;
    float density;
};

int3 PositionToCellCoord(float3 position, float radius)
{
    int cellX = floor(position.x / radius);
    int cellY = floor(position.y / radius);
    int cellZ = floor(position.z / radius);
    return int3(cellX, cellY, cellZ);
}

int GetKeyFromCellCoord(int3 coord, uint length)
{
    uint a = (coord.x + 91257) * 1583;
    uint b = (coord.y + 91257) * 9737;
    uint c = (coord.z + 91257) * 137;
    return (a + b + c) % length;
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
