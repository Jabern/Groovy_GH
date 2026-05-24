/*
 *  Internal guts header for KISS FFT.
 *  SPDX-License-Identifier: BSD-3-Clause
 */

#ifndef _KISS_FFT_GUTS_H
#define _KISS_FFT_GUTS_H

#include <stdint.h>
#include <stdlib.h>
#include <math.h>

#define KISS_FFT_MALLOC malloc
#define KISS_FFT_FREE free
#define KISS_FFT_ALIGNED_ALLOC(size, align) malloc(size)

#define KISS_FFT_TMP_ALLOC(nbytes) KISS_FFT_MALLOC(nbytes)
#define KISS_FFT_TMP_FREE(ptr) KISS_FFT_FREE(ptr)
#define KISS_FFT_ALIGN_CHECK(ptr)
#define KISS_FFT_ALIGN_SIZE_UP(size) (size)

#define KISS_FFT_ERROR(msg) /* noop */

struct kiss_fft_state {
    int nfft;
    int inverse;
    int factors[2 * 32];
    kiss_fft_cpx twiddles[1];
};

#define S_MUL(a, b) ((a) * (b))
#define C_MUL(dest, a, b)          \
    do {                            \
        (dest).r = (a).r * (b).r - (a).i * (b).i; \
        (dest).i = (a).r * (b).i + (a).i * (b).r; \
    } while (0)
#define C_FIXDIV(c, div) \
    do { (c).r /= (div); (c).i /= (div); } while (0)
#define C_MULBYSCALAR(c, s) \
    do { (c).r *= (s); (c).i *= (s); } while (0)
#define C_ADD(res, a, b) \
    do { (res).r = (a).r + (b).r; (res).i = (a).i + (b).i; } while (0)
#define C_SUB(res, a, b) \
    do { (res).r = (a).r - (b).r; (res).i = (a).i - (b).i; } while (0)
#define C_ADDTO(res, a) \
    do { (res).r += (a).r; (res).i += (a).i; } while (0)
#define C_SUBFROM(res, a) \
    do { (res).r -= (a).r; (res).i -= (a).i; } while (0)

#define HALF_OF(x) ((x) * 0.5f)

#define kf_cexp(x, phase) \
    do { (x)->r = cosf(phase); (x)->i = sinf(phase); } while (0)

#define KISS_FFT_MAXFACTORS 32

#endif
