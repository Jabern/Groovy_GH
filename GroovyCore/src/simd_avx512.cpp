// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#include "simd.h"
#include <immintrin.h>
#include <cmath>

// rms loudness, 16 samples at once with a single fused multiply add
float groovy_rms_avx512(const float* samples, size_t count)
{
    __m512 sum = _mm512_setzero_ps();
    size_t i = 0;
    for (; i + 16 <= count; i += 16)
    {
        __m512 v = _mm512_loadu_ps(samples + i);
        sum = _mm512_fmadd_ps(v, v, sum);
    }
    // collapses all 16 lanes into one float, this is the slow part of the loop
    float result = _mm512_reduce_add_ps(sum);
    for (; i < count; ++i)
        result += samples[i] * samples[i];
    return std::sqrt(result / (float)count);
}

// spectral flux: how much the spectrum moved between frames, used for beat finding
float groovy_flux_avx512(const float* mag, const float* prev, size_t n)
{
    __m512 sum = _mm512_setzero_ps();
    __m512 zero = _mm512_setzero_ps();
    size_t i = 0;
    for (; i + 16 <= n; i += 16)
    {
        __m512 m = _mm512_loadu_ps(mag + i);
        __m512 p = _mm512_loadu_ps(prev + i);
        __m512 diff = _mm512_sub_ps(m, p);
        diff = _mm512_max_ps(diff, zero);
        sum = _mm512_fmadd_ps(diff, diff, sum);
    }
    float result = _mm512_reduce_add_ps(sum);
    for (; i < n; ++i)
    {
        float diff = mag[i] - prev[i];
        if (diff > 0) result += diff * diff;
    }
    return std::sqrt(result);
}

// multiply by hann window, dead simple, memory bound anyway not compute bound
void groovy_window_apply_avx512(float* out, const float* in,
    const float* window, size_t n)
{
    size_t i = 0;
    for (; i + 16 <= n; i += 16)
    {
        __m512 w = _mm512_loadu_ps(window + i);
        __m512 v = _mm512_loadu_ps(in + i);
        _mm512_storeu_ps(out + i, _mm512_mul_ps(v, w));
    }
    for (; i < n; ++i)
        out[i] = in[i] * window[i];
}

// dot product for band energy, same fma pattern as rms without the final sqrt
float groovy_dot_avx512(const float* a, const float* b, size_t n)
{
    __m512 sum = _mm512_setzero_ps();
    size_t i = 0;
    for (; i + 16 <= n; i += 16)
    {
        __m512 va = _mm512_loadu_ps(a + i);
        __m512 vb = _mm512_loadu_ps(b + i);
        sum = _mm512_fmadd_ps(va, vb, sum);
    }
    float result = _mm512_reduce_add_ps(sum);
    for (; i < n; ++i)
        result += a[i] * b[i];
    return result;
}
