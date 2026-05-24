// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#include "simd.h"
#include <cmath>
#include <cstring>
// fallback for potato cpus that dont have any simd at all

// accumulates in double to avoid float drift over thousands of samples
float groovy_rms_scalar(const float* samples, size_t count)
{
    double sum = 0.0;
    for (size_t i = 0; i < count; ++i)
        sum += (double)samples[i] * samples[i];
    return (float)std::sqrt(sum / count);
}

float groovy_flux_scalar(const float* mag, const float* prev, size_t n)
{
    double flux = 0.0;
    for (size_t i = 0; i < n; ++i)
    {
        float diff = mag[i] - prev[i];
        if (diff > 0) flux += diff * diff;
    }
    return (float)std::sqrt(flux);
}

void groovy_window_apply_scalar(float* out, const float* in,
    const float* window, size_t n)
{
    for (size_t i = 0; i < n; ++i)
        out[i] = in[i] * window[i];
}

float groovy_dot_scalar(const float* a, const float* b, size_t n)
{
    double sum = 0.0;
    for (size_t i = 0; i < n; ++i)
        sum += (double)a[i] * b[i];
    return (float)sum;
}
