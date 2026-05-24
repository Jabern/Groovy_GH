// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#include "simd.h"

#if defined(_MSC_VER)
#include <intrin.h>
#elif defined(__GNUC__) || defined(__clang__)
#include <cpuid.h>
#endif

rms_func_t    groovy_rms           = groovy_rms_scalar;
flux_func_t   groovy_flux          = groovy_flux_scalar;
window_func_t groovy_window_apply  = groovy_window_apply_scalar;
dot_func_t    groovy_dot           = groovy_dot_scalar;

// wraps the platform specific cpuid call so we dont repeat this mess everywhere
static void cpuid(int info[4], int eax, int ecx)
{
#if defined(_MSC_VER)
    __cpuidex(info, eax, ecx);
#elif defined(__GNUC__) || defined(__clang__)
    __cpuid_count(eax, ecx, info[0], info[1], info[2], info[3]);
#else
    (void)info; (void)eax; (void)ecx;
#endif
}

// bit 16 is avx512, bit 5 is avx2, else we check sse2, else we are screwed
GroovySimdLevel groovy_detect_cpu(void)
{
    int info[4] = {};

    cpuid(info, 0, 0);
    int maxLeaf = info[0];

    if (maxLeaf >= 7)
    {
        cpuid(info, 7, 0);
        if (info[1] & (1 << 16))
            return GROOVY_SIMD_AVX512;
        if (info[1] & (1 << 5))
            return GROOVY_SIMD_AVX2;
    }

    if (maxLeaf >= 1)
    {
        cpuid(info, 1, 0);
        if (info[3] & (1 << 26))
            return GROOVY_SIMD_SSE2;
    }

    return GROOVY_SIMD_NONE;
}

void groovy_simd_init(void)
{
    static int done = 0;
    if (done) return;
    done = 1;

    // i wrote this because without it the analysis took 200ms on a 3min track
    // and it was laggy as hell when tweaking sliders during live playback
    GroovySimdLevel level = groovy_detect_cpu();

    // only wire up avx512 if the compile flag was on, otherwise we crash
    // on missing symbols at load time, this is dumb but i dont have a better way
    // without making the cmake more complicated than it needs to be
    switch (level)
    {
    case GROOVY_SIMD_AVX512:
#if defined(GROOVY_AVX512_ENABLED)
        groovy_rms          = groovy_rms_avx512;
        groovy_flux         = groovy_flux_avx512;
        groovy_window_apply = groovy_window_apply_avx512;
        groovy_dot          = groovy_dot_avx512;
        return;
#endif
    case GROOVY_SIMD_AVX2:
    case GROOVY_SIMD_SSE2:
    case GROOVY_SIMD_NONE:
    default:
        break;
    }
    // if we get here we fall back to scalar, whatever
}
