/* StealthVoice: thin C API over Opus (voice codec) and RNNoise (noise suppression)
 * for the Unity game. All audio is mono float PCM in [-1, 1] at 48000 Hz. */
#include <stdlib.h>
#include <opus.h>
#include <rnnoise.h>

#ifdef _WIN32
#  define SV_API __declspec(dllexport)
#  define SV_CALL __cdecl
#else
#  define SV_API __attribute__((visibility("default")))
#  define SV_CALL
#endif

#define SV_RATE 48000
#define SV_DENOISE_FRAME 480

#ifdef __cplusplus
extern "C" {
#endif

SV_API void* SV_CALL sv_encoder_create(int bitrate)
{
    int err = 0;
    OpusEncoder* enc = opus_encoder_create(SV_RATE, 1, OPUS_APPLICATION_VOIP, &err);
    if (err != OPUS_OK || !enc)
        return NULL;
    opus_encoder_ctl(enc, OPUS_SET_BITRATE(bitrate));
    opus_encoder_ctl(enc, OPUS_SET_INBAND_FEC(1));
    opus_encoder_ctl(enc, OPUS_SET_PACKET_LOSS_PERC(10));
    opus_encoder_ctl(enc, OPUS_SET_VBR(1));
    opus_encoder_ctl(enc, OPUS_SET_COMPLEXITY(8));
    opus_encoder_ctl(enc, OPUS_SET_SIGNAL(OPUS_SIGNAL_VOICE));
    return enc;
}

SV_API void SV_CALL sv_encoder_set_bitrate(void* enc, int bitrate)
{
    if (enc) opus_encoder_ctl((OpusEncoder*)enc, OPUS_SET_BITRATE(bitrate));
}

SV_API void SV_CALL sv_encoder_set_loss(void* enc, int percent)
{
    if (!enc) return;
    if (percent < 0) percent = 0;
    if (percent > 100) percent = 100;
    opus_encoder_ctl((OpusEncoder*)enc, OPUS_SET_PACKET_LOSS_PERC(percent));
}

SV_API int SV_CALL sv_encode(void* enc, const float* pcm, int frame, unsigned char* out, int max_bytes)
{
    if (!enc || !pcm || !out || max_bytes <= 0)
        return OPUS_BAD_ARG;
    return opus_encode_float((OpusEncoder*)enc, pcm, frame, out, max_bytes);
}

SV_API void SV_CALL sv_encoder_destroy(void* enc)
{
    if (enc) opus_encoder_destroy((OpusEncoder*)enc);
}

SV_API void* SV_CALL sv_decoder_create(void)
{
    int err = 0;
    OpusDecoder* dec = opus_decoder_create(SV_RATE, 1, &err);
    if (err != OPUS_OK || !dec)
        return NULL;
    return dec;
}

/* data NULL / len 0: packet loss concealment for one frame.
 * fec = 1: decode the FEC copy of the lost previous frame carried in this packet. */
SV_API int SV_CALL sv_decode(void* dec, const unsigned char* data, int len, float* out, int frame, int fec)
{
    if (!dec || !out || frame <= 0)
        return OPUS_BAD_ARG;
    if (!data || len <= 0)
        return opus_decode_float((OpusDecoder*)dec, NULL, 0, out, frame, 0);
    return opus_decode_float((OpusDecoder*)dec, data, len, out, frame, fec ? 1 : 0);
}

SV_API void SV_CALL sv_decoder_destroy(void* dec)
{
    if (dec) opus_decoder_destroy((OpusDecoder*)dec);
}

SV_API void* SV_CALL sv_denoise_create(void)
{
    return rnnoise_create(NULL);
}

/* In place, 480 samples (10 ms). RNNoise works on the 16-bit scale. Returns voice probability. */
SV_API float SV_CALL sv_denoise(void* st, float* frame)
{
    float buf[SV_DENOISE_FRAME];
    float vad;
    int i;
    if (!st || !frame)
        return 0.0f;
    for (i = 0; i < SV_DENOISE_FRAME; i++)
        buf[i] = frame[i] * 32768.0f;
    vad = rnnoise_process_frame((DenoiseState*)st, buf, buf);
    for (i = 0; i < SV_DENOISE_FRAME; i++) {
        float v = buf[i] * (1.0f / 32768.0f);
        frame[i] = v > 1.0f ? 1.0f : (v < -1.0f ? -1.0f : v);
    }
    return vad;
}

SV_API void SV_CALL sv_denoise_destroy(void* st)
{
    if (st) rnnoise_destroy((DenoiseState*)st);
}

SV_API int SV_CALL sv_version(void)
{
    return 1;
}

#ifdef __cplusplus
}
#endif
