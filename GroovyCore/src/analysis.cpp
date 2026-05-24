// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#define _USE_MATH_DEFINES
#include "analysis.h"
#include "fft.h"
#include <cmath>
#include <cstring>
#include <algorithm>

static float compute_rms(const float* samples, int count)
{
    double sum = 0.0;
    for (int i = 0; i < count; ++i)
        sum += (double)samples[i] * samples[i];
    return (float)std::sqrt(sum / count);
}

static float compute_band_centroid(const float* mag, int nBins,
    int sampleRate, int nfft)
{
    double wsum = 0.0, msum = 0.0;
    float binHz = (float)sampleRate / (float)nfft;
    for (int i = 0; i < nBins; ++i)
    {
        wsum += mag[i] * (float)i * binHz;
        msum += mag[i];
    }
    if (msum < 1e-10f) return 0.0f;
    return (float)(wsum / msum);
}

static float compute_spectral_flux(const float* mag, const float* prev,
    int nBins)
{
    double flux = 0.0;
    for (int i = 0; i < nBins; ++i)
    {
        float diff = mag[i] - prev[i];
        if (diff > 0) flux += diff * diff;
    }
    return (float)std::sqrt(flux);
}

static float compute_spectral_rolloff(const float* mag, int nBins,
    float threshold)
{
    double total = 0.0;
    for (int i = 0; i < nBins; ++i) total += mag[i];
    double accum = 0.0, target = total * threshold;
    for (int i = 0; i < nBins; ++i)
    {
        accum += mag[i];
        if (accum >= target) return (float)i / (float)nBins;
    }
    return 1.0f;
}

// splits the spectrum into bands defined by the config, like bass 20hz to 250hz
static void compute_band_energies(const float* mag, int nBins,
    int sampleRate, int nfft, float* bandEnergies, int numBands,
    const BandEdge* edges, float* globalMax)
{
    float nyquist = sampleRate / 2.0f;
    float binHz = (float)sampleRate / (float)nfft;

    for (int b = 0; b < numBands; ++b)
    {
        float lo = edges[b].lowHz;
        float hi = edges[b].highHz;
        if (hi > nyquist) hi = nyquist;
        if (lo >= hi) { bandEnergies[b] = 0; continue; }

        int loBin = (int)(lo / binHz);
        int hiBin = (int)(hi / binHz);
        if (loBin < 0) loBin = 0;
        if (hiBin > nBins) hiBin = nBins;
        if (hiBin <= loBin) hiBin = loBin + 1;

        double energy = 0.0;
        for (int i = loBin; i < hiBin; ++i)
            energy += (double)mag[i] * mag[i];
        bandEnergies[b] = (float)std::sqrt(
            energy / std::max(1, hiBin - loBin));
    }

    for (int b = 0; b < numBands; ++b)
        // tracks global max for each band so we can normalize over the whole track later
        if (bandEnergies[b] > globalMax[b])
            globalMax[b] = bandEnergies[b];
}

AnalysisResult* analysis_run(const float* samples, int count,
    unsigned int sampleRate, int fftSize, int hopSize, int numBands,
    const BandEdge* edges)
{
    if (!samples || count <= 0 || fftSize <= 0 || hopSize <= 0)
        return nullptr;
    if (!edges) return analysis_run_default(
        samples, count, sampleRate, fftSize, hopSize, numBands);
    if (numBands < 1) numBands = 3;
    if (numBands > GROOVY_MAX_BANDS) numBands = GROOVY_MAX_BANDS;

    int nBins = fftSize / 2 + 1;
    FFTHandle* fft = fft_create(fftSize);
    if (!fft) return nullptr;

    int numFrames = std::max(0, (count - fftSize) / hopSize + 1);
    if (numFrames <= 0) { fft_destroy(fft); return nullptr; }

    auto* result = new AnalysisResult();
    result->frames.resize(numFrames);
    result->sampleRate = sampleRate;
    result->frameDuration = (double)hopSize / sampleRate;
    result->duration = (double)count / sampleRate;
    result->numBands = numBands;

    std::vector<float> magnitude(nBins);
    std::vector<float> prevMagnitude(nBins, 0.0f);
    float globalMax[GROOVY_MAX_BANDS] = {};

    for (int f = 0; f < numFrames; ++f)
    {
        int offset = f * hopSize;
        auto& frame = result->frames[f];
        frame.time = (double)offset / sampleRate;

        frame.rms = compute_rms(samples + offset,
            std::min(fftSize, count - offset));

        fft_execute(fft, samples + offset,
            std::min(fftSize, count - offset), magnitude.data());

        frame.spectralCentroid = compute_band_centroid(
            magnitude.data(), nBins, sampleRate, fftSize);
        frame.spectralFlux = (f == 0) ? 0.0f : compute_spectral_flux(
            magnitude.data(), prevMagnitude.data(), nBins);
        frame.spectralRolloff = compute_spectral_rolloff(
            magnitude.data(), nBins, 0.85f);

        compute_band_energies(magnitude.data(), nBins, sampleRate,
            fftSize, frame.bands, numBands, edges, globalMax);

        frame.onsetStrength = frame.spectralFlux;
        memcpy(prevMagnitude.data(), magnitude.data(),
            nBins * sizeof(float));
    }

    for (int f = 0; f < numFrames; ++f)
    {
        // normalize by global max instead of per frame, otherwise every frame hits 1.0 and the sliders do nothing
        auto& frame = result->frames[f];
        for (int b = 0; b < numBands; ++b)
            if (globalMax[b] > 1e-10f)
                frame.bands[b] /= globalMax[b];
    }

    for (int f = 0; f < numFrames; ++f)
    {
        // cheap onset detection: compare each frame to local average of 3 frames on each side
        float current = result->frames[f].onsetStrength;
        int halfWin = 3;
        int start = std::max(0, f - halfWin);
        int end = std::min(numFrames - 1, f + halfWin);
        float localMean = 0.0f; int cnt = 0;
        for (int i = start; i <= end; ++i)
        {
            if (i != f)
            {
                localMean += result->frames[i].onsetStrength;
                cnt++;
            }
        }
        if (cnt > 0) localMean /= cnt;
        float threshold = localMean * 1.5f + 0.01f;
        result->frames[f].isBeat = (current > threshold
            && current > 0.01f) ? 1.0f : 0.0f;
    }

    fft_destroy(fft);
    return result;
}

AnalysisResult* analysis_run_default(const float* samples, int count,
    unsigned int sampleRate, int fftSize, int hopSize, int numBands)
{
    BandEdge defEdges[GROOVY_MAX_BANDS] = {
        { 20.0f, 250.0f },
        { 250.0f, 4000.0f },
        { 4000.0f, 20000.0f },
    };
    return analysis_run(samples, count, sampleRate,
        fftSize, hopSize, numBands, defEdges);
}

void analysis_free(AnalysisResult* r) { delete r; }
