// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#ifndef GROOVY_ANALYSIS_H
#define GROOVY_ANALYSIS_H

#include <vector>

#define GROOVY_MAX_BANDS 8

struct AnalysisFrame
{
    double time;
    float  rms;
    float  onsetStrength;
    float  isBeat;
    float  bands[GROOVY_MAX_BANDS];
    float  spectralCentroid;
    float  spectralFlux;
    float  spectralRolloff;
};

struct AnalysisResult
{
    std::vector<AnalysisFrame> frames;
    double                     duration;
    unsigned int               sampleRate;
    double                     frameDuration;
    int                        numBands;
};

struct BandEdge { float lowHz; float highHz; };

AnalysisResult* analysis_run(const float* samples, int count,
    unsigned int sampleRate, int fftSize, int hopSize, int numBands,
    const BandEdge* edges);

AnalysisResult* analysis_run_default(const float* samples, int count,
    unsigned int sampleRate, int fftSize, int hopSize, int numBands);

void analysis_free(AnalysisResult* r);

#endif
