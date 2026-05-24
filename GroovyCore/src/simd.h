// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#ifndef GROOVY_SIMD_H
#define GROOVY_SIMD_H
// dispatches audio kernels to whatever simd the cpu actually has

#include <cstddef>
#include <cstdint>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum {
    GROOVY_SIMD_NONE   = 0,
    GROOVY_SIMD_SSE2   = 1,
    GROOVY_SIMD_AVX2   = 2,
    GROOVY_SIMD_AVX512 = 3,
} GroovySimdLevel;

GroovySimdLevel groovy_detect_cpu(void);

typedef float (*rms_func_t)(const float* samples, size_t count);
typedef float (*flux_func_t)(const float* mag, const float* prev,
    size_t n);
typedef void  (*window_func_t)(float* out, const float* in,
    const float* window, size_t n);
typedef float (*dot_func_t)(const float* a, const float* b, size_t n);

extern rms_func_t    groovy_rms;
extern flux_func_t   groovy_flux;
extern window_func_t groovy_window_apply;
extern dot_func_t    groovy_dot;

void groovy_simd_init(void);

float groovy_rms_scalar(const float* samples, size_t count);
float groovy_flux_scalar(const float* mag, const float* prev,
    size_t n);
void  groovy_window_apply_scalar(float* out, const float* in,
    const float* window, size_t n);
float groovy_dot_scalar(const float* a, const float* b, size_t n);

#ifdef GROOVY_AVX512_ENABLED
float groovy_rms_avx512(const float* samples, size_t count);
float groovy_flux_avx512(const float* mag, const float* prev,
    size_t n);
void  groovy_window_apply_avx512(float* out, const float* in,
    const float* window, size_t n);
float groovy_dot_avx512(const float* a, const float* b, size_t n);
#endif

#ifdef __cplusplus
}
#endif

#endif
