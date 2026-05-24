// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#ifndef GROOVY_FFT_H
#define GROOVY_FFT_H

#include <vector>
#include "../external/kiss_fft.h"

struct FFTHandle
{
    void* cfg;
    int   nfft;
    double invNfft;
    std::vector<float> window;
    std::vector<kiss_fft_cpx> fin;
    std::vector<kiss_fft_cpx> fout;
};

FFTHandle* fft_create(int nfft);
void       fft_destroy(FFTHandle* h);
void       fft_execute(FFTHandle* h, const float* samples, int count,
               float* magnitude);

#endif
