# StealthVoice: one shared library with Opus + RNNoise linked in statically.
# Built by Tools/Voice/build_native.sh, which passes the extracted source trees:
#   -DOPUS_SRC_DIR=<opus-1.5.2> -DRNNOISE_SRC_DIR=<rnnoise-0.2>
cmake_minimum_required(VERSION 3.16)
project(StealthVoice C)

set(OPUS_SRC_DIR "" CACHE PATH "Extracted opus-1.5.2 source tree")
set(RNNOISE_SRC_DIR "" CACHE PATH "Extracted rnnoise-0.2 source tree")
if(NOT EXISTS "${OPUS_SRC_DIR}/CMakeLists.txt")
  message(FATAL_ERROR "OPUS_SRC_DIR is not an opus source tree: '${OPUS_SRC_DIR}'")
endif()
if(NOT EXISTS "${RNNOISE_SRC_DIR}/src/denoise.c")
  message(FATAL_ERROR "RNNOISE_SRC_DIR is not an rnnoise source tree: '${RNNOISE_SRC_DIR}'")
endif()

# Static CRT everywhere (/MT): the DLL needs no VC++ redistributable.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>" CACHE STRING "" FORCE)

# Opus as a static library from its own CMakeLists.
set(OPUS_BUILD_SHARED_LIBRARY OFF CACHE BOOL "" FORCE)
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_TESTING OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_PROGRAMS OFF CACHE BOOL "" FORCE)
set(OPUS_STATIC_RUNTIME ON CACHE BOOL "" FORCE)
set(OPUS_INSTALL_PKG_CONFIG_MODULE OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_CMAKE_CONFIG_MODULE OFF CACHE BOOL "" FORCE)
add_subdirectory("${OPUS_SRC_DIR}" opus EXCLUDE_FROM_ALL)

# RNNoise: library sources only (no tools, no x86 RTCD). Its generic C path does not build
# (vec.h includes a missing os_support.h); GCC on x86-64 takes the SSE2 path in vec_avx.h,
# because __SSE2__ is predefined there. MSVC x64 always has SSE2 but never defines the macro,
# so define it to get the same baseline-SSE2 code.
set(RNN "${RNNOISE_SRC_DIR}/src")
add_library(rnnoise STATIC
  ${RNN}/denoise.c ${RNN}/rnn.c ${RNN}/pitch.c ${RNN}/kiss_fft.c ${RNN}/celt_lpc.c
  ${RNN}/nnet.c ${RNN}/nnet_default.c ${RNN}/parse_lpcnet_weights.c
  ${RNN}/rnnoise_data.c ${RNN}/rnnoise_tables.c)
target_include_directories(rnnoise PUBLIC "${RNNOISE_SRC_DIR}/include" PRIVATE "${RNN}")
target_compile_definitions(rnnoise PRIVATE _USE_MATH_DEFINES _CRT_SECURE_NO_WARNINGS)
if(MSVC)
  target_compile_options(rnnoise PRIVATE /wd4244 /wd4305 /wd4267 /wd4018)
  if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    target_compile_definitions(rnnoise PRIVATE __SSE2__)
  endif()
endif()

add_library(StealthVoice SHARED stealth_voice.c)
target_link_libraries(StealthVoice PRIVATE opus rnnoise)
target_compile_definitions(StealthVoice PRIVATE SV_BUILD)
