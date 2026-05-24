// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#define _USE_MATH_DEFINES
#include "fft.h"
#include "../external/kiss_fft.h"
#include <cmath>
#include <algorithm>

FFTHandle* fft_create(int nfft)
{
    if (nfft < 2) return nullptr;
    auto* h = new FFTHandle();
    h->nfft = nfft;
    h->invNfft = 1.0 / nfft;
    h->cfg = kiss_fft_alloc(nfft, 0, nullptr, nullptr);
    if (!h->cfg) { delete h; return nullptr; }
    h->window.resize(nfft);
    h->fin.resize(nfft);
    h->fout.resize(nfft);
    for (int i = 0; i < nfft; ++i)
    {
        double w = 0.5 * (1.0 - std::cos(2.0 * M_PI * i / (nfft - 1)));
        h->window[i] = static_cast<float>(w);
    }
    return h;
}

void fft_destroy(FFTHandle* h)
{
    if (h)
    {
        if (h->cfg) kiss_fft_free(h->cfg);
        delete h;
    }
}

// window then forward fft then magnitude = sqrt of real squared plus imag squared over nfft
void fft_execute(FFTHandle* h, const float* samples, int count, float* magnitude)
{
    if (!h || !h->cfg) return;
    int nfft = h->nfft;
    int n = std::min(std::max(0, count), nfft);

    for (int i = 0; i < n; ++i)
    {
        h->fin[i].r = samples[i] * h->window[i];
        h->fin[i].i = 0.0f;
    }
    for (int i = n; i < nfft; ++i)
    {
        h->fin[i].r = 0.0f;
        h->fin[i].i = 0.0f;
    }

    kiss_fft(static_cast<kiss_fft_cfg>(h->cfg),
        h->fin.data(), h->fout.data());

    double norm = h->invNfft;
    for (int i = 0; i < nfft / 2 + 1; ++i)
    {
        float re = h->fout[i].r;
        float im = h->fout[i].i;
        magnitude[i] = (float)(std::sqrt(
            (double)re * re + (double)im * im) * norm);
    }
}
