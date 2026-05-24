// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#ifndef GROOVY_AUDIO_READER_H
#define GROOVY_AUDIO_READER_H

#include <vector>
#include <string>

struct AudioData
{
    std::vector<float> samples;
    unsigned int       sampleRate;
    unsigned int       channels;
    double             duration;
};

AudioData* audio_load(const char* filePath);
void       audio_free(AudioData* data);

AudioData* audio_resample_mono(const AudioData* src, unsigned int targetRate);

#endif
