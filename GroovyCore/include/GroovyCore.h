// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

#ifndef GROOVY_CORE_H
#define GROOVY_CORE_H

#ifdef GROOVY_EXPORTS
#define GROOVY_API __declspec(dllexport)
#else
#define GROOVY_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

GROOVY_API const char* Groovy_Analyze(
    const char* filePath,
    int fftSize,
    int hopSize,
    int numBands);

GROOVY_API const char* Groovy_AnalyzeEx(
    const char* filePath,
    int fftSize,
    int hopSize,
    int numBands,
    const char* bandEdgesJson);

GROOVY_API const char* Groovy_GetDuration(const char* filePath);

GROOVY_API void Groovy_FreeResult(char* result);

#ifdef __cplusplus
}
#endif

#endif
