// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#include "audio_reader.h"
#include <cstring>
#include <cstdlib>
#include <cstdint>
#include <algorithm>

static bool ends_with(const char* str, const char* suffix)
{
    size_t lenStr = strlen(str);
    size_t lenSuf = strlen(suffix);
    if (lenSuf > lenStr) return false;
    return _stricmp(str + lenStr - lenSuf, suffix) == 0;
}

static unsigned int read_u16le(const unsigned char* p)
{
    return (unsigned int)p[0] | ((unsigned int)p[1] << 8);
}

static unsigned int read_u32le(const unsigned char* p)
{
    return (unsigned int)p[0] | ((unsigned int)p[1] << 8)
         | ((unsigned int)p[2] << 16) | ((unsigned int)p[3] << 24);
}

// hand rolled wav parser, assumes standard pcm, dies on exotic formats
static AudioData* load_wav(const char* filePath)
{
    FILE* f = nullptr;
    if (fopen_s(&f, filePath, "rb") != 0 || !f) return nullptr;

    unsigned char header[44];
    if (fread(header, 1, 44, f) != 44) { fclose(f); return nullptr; }

    if (memcmp(header, "RIFF", 4) != 0 || memcmp(header + 8, "WAVE", 4) != 0)
    {
        fclose(f);
        return nullptr;
    }

    unsigned int channels = read_u16le(header + 22);
    unsigned int sampleRate = read_u32le(header + 24);
    unsigned int bitsPerSample = read_u16le(header + 34);
    unsigned int audioFormat = read_u16le(header + 20);

    if (channels == 0 || sampleRate == 0) { fclose(f); return nullptr; }

    if (audioFormat == 3)
    {
        fclose(f);
        return nullptr;
    }

    if (bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
    {
        fclose(f);
        return nullptr;
    }

    unsigned int dataOffset = 0;
    unsigned char chunkId[4];
    unsigned int chunkSize;
    int loopGuard = 0;

    fseek(f, 12, SEEK_SET);
    while (fread(chunkId, 1, 4, f) == 4 && loopGuard < 1000)
    {
        loopGuard++;
        // reads chunk size as raw little endian, big endian goes straight in the trash
        if (fread(&chunkSize, 4, 1, f) != 1) break;
        if (memcmp(chunkId, "data", 4) == 0)
        {
            dataOffset = (unsigned int)ftell(f);
            break;
        }
        if (chunkSize == 0) break;
        fseek(f, (long)chunkSize, SEEK_CUR);
    }

    if (dataOffset == 0) { fclose(f); return nullptr; }

    fseek(f, 0, SEEK_END);
    long fileEnd = ftell(f);
    if (fileEnd < 0 || (unsigned long)fileEnd <= dataOffset)
    {
        fclose(f);
        return nullptr;
    }
    unsigned int dataSize = (unsigned int)((unsigned long)fileEnd - dataOffset);

    unsigned int bytesPerSample = bitsPerSample / 8;
    unsigned int bytesPerFrame = channels * bytesPerSample;
    unsigned int totalFrames = dataSize / bytesPerFrame;

    if (totalFrames == 0) { fclose(f); return nullptr; }

    size_t sampleCount = (size_t)totalFrames * channels;

    auto* audio = new AudioData();
    audio->channels = channels;
    audio->sampleRate = sampleRate;
    audio->samples.resize(sampleCount);
    audio->duration = (double)totalFrames / sampleRate;

    fseek(f, dataOffset, SEEK_SET);
    unsigned char* raw = new unsigned char[dataSize];
    size_t bytesRead = fread(raw, 1, dataSize, f);
    if (bytesRead < (size_t)dataSize)
        memset(raw + bytesRead, 0, (size_t)dataSize - bytesRead);
    fclose(f);

    double normFactor = 1.0;
    if (bitsPerSample == 16) normFactor = 1.0 / 32768.0;
    else if (bitsPerSample == 24) normFactor = 1.0 / 8388608.0;
    else if (bitsPerSample == 32) normFactor = 1.0 / 2147483648.0;

    for (size_t i = 0; i < sampleCount; ++i)
    {
        size_t sampleIndex = (size_t)i * bytesPerSample;
        int64_t value = 0;
        if (bitsPerSample == 16)
        {
            int16_t s = 0;
            memcpy(&s, raw + sampleIndex, 2);
            value = s;
        }
        else if (bitsPerSample == 24)
        {
            uint32_t u = (uint32_t)raw[sampleIndex]
                       | ((uint32_t)raw[sampleIndex + 1] << 8)
                       | ((uint32_t)raw[sampleIndex + 2] << 16);
            if (u & 0x800000u) u |= 0xFF000000u;
            value = (int32_t)u;
        }
        else if (bitsPerSample == 32)
        {
            int32_t s = 0;
            memcpy(&s, raw + sampleIndex, 4);
            value = s;
        }
        audio->samples[i] = (float)((double)value * normFactor);
    }

    delete[] raw;
    return audio;
}

AudioData* audio_load(const char* filePath)
{
    if (ends_with(filePath, ".wav") || ends_with(filePath, ".WAV"))
        return load_wav(filePath);
    return nullptr;
}

void audio_free(AudioData* data)
{
    delete data;
}

AudioData* audio_resample_mono(const AudioData* src, unsigned int targetRate)
{
    auto* dst = new AudioData();
    dst->sampleRate = targetRate;
    dst->channels = 1;
    if (src->channels == 0) return dst;

    unsigned int srcFrames = (unsigned int)src->samples.size() / src->channels;
    if (srcFrames == 0 || src->sampleRate == 0) return dst;

    double ratio = (double)targetRate / src->sampleRate;
    unsigned int dstFrames = (unsigned int)(srcFrames * ratio);
    if (dstFrames == 0) dstFrames = 1;
    dst->samples.resize(dstFrames);
    dst->duration = (double)dstFrames / targetRate;

    for (unsigned int i = 0; i < dstFrames; ++i)
    {
        double srcPos = (double)i / ratio;
        unsigned int srcIndex = (unsigned int)srcPos;
        double frac = srcPos - srcIndex;

        float sumLeft = 0.0f, sumRight = 0.0f;
        if (srcIndex + 1 < srcFrames)
        {
            float a0 = src->samples[(size_t)srcIndex * src->channels];
            float a1 = src->samples[(size_t)(srcIndex + 1) * src->channels];
            sumLeft = (float)(a0 * (1.0 - frac) + a1 * frac);

            if (src->channels >= 2)
            {
                float b0 = src->samples[(size_t)srcIndex * src->channels + 1];
                float b1 = src->samples[(size_t)(srcIndex + 1) * src->channels + 1];
                sumRight = (float)(b0 * (1.0 - frac) + b1 * frac);
            }
        }
        else
        {
            sumLeft = src->samples[(size_t)srcIndex * src->channels];
            if (src->channels >= 2)
                sumRight = src->samples[(size_t)srcIndex * src->channels + 1];
        }

        dst->samples[i] = (sumLeft + sumRight) / (src->channels >= 2 ? 2.0f : 1.0f);
    }

    return dst;
}
