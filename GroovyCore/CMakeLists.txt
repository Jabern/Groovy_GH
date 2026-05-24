cmake_minimum_required(VERSION 3.15)
project(GroovyCore VERSION 1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

option(GROOVY_AVX2 "Enable AVX2 auto-vectorization (requires Haswell+ CPU)" OFF)
option(GROOVY_AVX512 "Enable AVX-512 kernels (requires Skylake-X+ or Zen 4+)" OFF)

add_library(GroovyCore SHARED
    src/api.cpp
    src/audio_reader.cpp
    src/analysis.cpp
    src/fft.cpp
    src/simd.cpp
    src/simd_scalar.cpp
    external/kiss_fft.cpp
)

if(GROOVY_AVX512)
    target_sources(GroovyCore PRIVATE src/simd_avx512.cpp)
    target_compile_definitions(GroovyCore PRIVATE GROOVY_AVX512_ENABLED)
    if(MSVC)
        set_source_files_properties(
            src/simd_avx512.cpp PROPERTIES
            COMPILE_FLAGS "/arch:AVX512F")
    endif()
endif()

target_include_directories(GroovyCore PUBLIC include)
target_include_directories(GroovyCore PRIVATE src external)

target_compile_definitions(GroovyCore PRIVATE GROOVY_EXPORTS)

if(MSVC)
    target_compile_options(GroovyCore PRIVATE /O2 /MT)
    if(GROOVY_AVX2)
        target_compile_options(GroovyCore PRIVATE /arch:AVX2)
    endif()
endif()

set_target_properties(GroovyCore PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Groovy_GH/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Groovy_GH/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Groovy_GH/bin"
)
