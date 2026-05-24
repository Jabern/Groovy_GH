// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#include "../include/GroovyCore.h"
#include "analysis.h"
#include "audio_reader.h"
#include "simd.h"
#include <string>
#include <cstring>
#include <cstdlib>
#include <cstdio>

static char* dupstr(const char* s)
{
    if (!s) return nullptr;
    size_t len = strlen(s) + 1;
    char* d = (char*)malloc(len);
    if (d) memcpy(d, s, len);
    return d;
}

static std::string escape_json(const std::string& s)
{
    std::string out;
    out.reserve(s.size() + 16);
    for (char c : s)
    {
        if (c == '"') out += "\\\"";
        else if (c == '\\') out += "\\\\";
        else if (c == '\n') out += "\\n";
        else if (c == '\r') out += "\\r";
        else if (c == '\t') out += "\\t";
        else out += c;
    }
    return out;
}

static std::string analysis_to_json(const AnalysisResult* r)
{
    if (!r || r->frames.empty()) return "{\"error\":\"no analysis data\"}";

    std::string json;
    json.reserve(r->frames.size() * 256);

    char buf[512];

    snprintf(buf, sizeof(buf),
        "{\"duration\":%f,\"sampleRate\":%u,\"numFrames\":%d,\"frameDuration\":%f,\"numBands\":%d,\"frames\":[",
        r->duration, r->sampleRate, (int)r->frames.size(), r->frameDuration, r->numBands);
    json += buf;

    for (size_t i = 0; i < r->frames.size(); ++i)
    {
        const auto& f = r->frames[i];
        snprintf(buf, sizeof(buf),
            "{\"time\":%f,\"rms\":%f,\"onsetStrength\":%f,\"isBeat\":%f,\"bands\":[",
            f.time, f.rms, f.onsetStrength, f.isBeat);
        json += buf;

        for (int b = 0; b < r->numBands; ++b)
        {
            snprintf(buf, sizeof(buf), "%f%s", f.bands[b], (b < r->numBands - 1) ? "," : "");
            json += buf;
        }

        snprintf(buf, sizeof(buf),
            "],\"spectralCentroid\":%f,\"spectralFlux\":%f,\"spectralRolloff\":%f}",
            f.spectralCentroid, f.spectralFlux, f.spectralRolloff);
        json += buf;

        if (i < r->frames.size() - 1) json += ",";
    }

    json += "]}";
    return json;
}

GROOVY_API const char* Groovy_Analyze(const char* filePath, int fftSize, int hopSize, int numBands)
{
    return Groovy_AnalyzeEx(filePath, fftSize, hopSize, numBands, nullptr);
}

GROOVY_API const char* Groovy_AnalyzeEx(const char* filePath,
    int fftSize, int hopSize, int numBands, const char* bandEdgesJson)
{
    // lazy init the simd dispatch on first call, we dont need an explicit dllmain
    groovy_simd_init();

    if (!filePath) return dupstr("{\"error\":\"null file path\"}");

    if (fftSize <= 0) fftSize = 2048;
    if (hopSize <= 0) hopSize = 512;
    if (numBands <= 0) numBands = 3;
    if (numBands > GROOVY_MAX_BANDS) numBands = GROOVY_MAX_BANDS;

    // hand rolled json parser because pulling in nlohmann is too heavy, just eats low high pairs
    BandEdge edges[GROOVY_MAX_BANDS] = {};
    bool hasCustomEdges = false;
    if (bandEdgesJson && bandEdgesJson[0])
    {
        const char* p = bandEdgesJson;
        int edgeIdx = 0;
        while (*p && edgeIdx < numBands)
        {
            while (*p && *p != '{') p++;
            if (!*p) break;
            p++;
            float lo = 0, hi = 0;
            while (*p && *p != ':') p++;
            // horrible hack: atof on a buffer that isnt null terminated, but it works somehow
            if (*p == ':') { p++; lo = (float)atof(p); }
            while (*p && *p != ',') p++;
            if (*p == ',') { p++; while (*p && *p != ':') p++; }
            if (*p == ':') { p++; hi = (float)atof(p); }
            if (lo > 0 && hi > lo)
            {
                edges[edgeIdx].lowHz = lo;
                edges[edgeIdx].highHz = hi;
                hasCustomEdges = true;
            }
            edgeIdx++;
        }
    }

    BandEdge defEdges[GROOVY_MAX_BANDS] = {
        { 20.0f, 250.0f },
        { 250.0f, 4000.0f },
        { 4000.0f, 20000.0f },
    };
    const BandEdge* useEdges = hasCustomEdges ? edges : defEdges;

    AudioData* raw = audio_load(filePath);
    if (!raw) return dupstr("{\"error\":\"failed to load audio file\"}");

    AudioData* mono = audio_resample_mono(raw, raw->sampleRate);
    audio_free(raw);

    AnalysisResult* analysis = nullptr;
    try {
        analysis = analysis_run(
            mono->samples.data(), (int)mono->samples.size(),
            mono->sampleRate, fftSize, hopSize, numBands, useEdges);
    } catch (...) {
        audio_free(mono);
        return dupstr("{\"error\":\"analysis exception\"}");
    }
    audio_free(mono);

    if (!analysis) return dupstr("{\"error\":\"analysis failed\"}");

    std::string json = analysis_to_json(analysis);
    analysis_free(analysis);

    return dupstr(json.c_str());
}

GROOVY_API const char* Groovy_GetDuration(const char* filePath)
{
    if (!filePath) return dupstr("{\"error\":\"null file path\"}");

    AudioData* raw = audio_load(filePath);
    if (!raw) return dupstr("{\"error\":\"failed to load audio file\"}");

    char buf[128];
    snprintf(buf, sizeof(buf), "{\"duration\":%f,\"sampleRate\":%u,\"channels\":%u}",
        raw->duration, raw->sampleRate, raw->channels);
    audio_free(raw);
    return dupstr(buf);
}

GROOVY_API void Groovy_FreeResult(char* result)
{
    free(result);
}
