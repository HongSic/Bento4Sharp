#!/usr/bin/env python3
# Generates small synthetic MP4 files for tests/compare-tools-T4.sh: movies
# with the codecs that the stock Bento4 test files do not have (HEVC, Dolby
# Vision, AC-3, E-AC-3, AC-4, HE-AAC...) and the structures that mp42ts,
# mp42hls and mp4iframeindex treat specially (NAL unit length sizes of 1 and 2
# bytes, access unit delimiters, large NAL units with start code emulations,
# several sample descriptions, empty samples...).
# The media payload is filler with just enough structure for these tools
# (NAL unit headers, E-AC-3 sync frames): the files cannot be played.
#
# usage: make-synthetic-inputs-T4.py <output-dir>
#
# Builds on tests/make-synthetic-inputs.py; deterministic (no randomness, no
# timestamps), so the same files are produced on every run.

import importlib.util
import os
import sys

_spec = importlib.util.spec_from_file_location(
    "synthetic", os.path.join(os.path.dirname(os.path.abspath(__file__)), "make-synthetic-inputs.py"))
syn = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(syn)

u8, u16, u32, box, filler = syn.u8, syn.u16, syn.u32, syn.box, syn.filler
Track, VMHD, SMHD = syn.Track, syn.VMHD, syn.SMHD
ENG = (5 << 10) | (14 << 5) | 7     # 'eng' as a packed ISO-639-2/T code
FRA = (6 << 10) | (18 << 5) | 1     # 'fra'

AAC_LC = bytes([0x12, 0x10])                  # AAC-LC, 44100 Hz, stereo
AAC_MAIN = bytes([0x0B, 0x88])                # AAC Main, 48000 Hz, mono
AAC_SBR = bytes([0x2B, 0x92, 0x08, 0x00])     # SBR (explicit signaling), 22050 -> 44100 Hz, stereo
AAC_PS = bytes([0xEB, 0x8A, 0x08, 0x00])      # PS (explicit signaling), 22050 -> 44100 Hz, mono


def nalus(units, length_size=4):
    data = b""
    for n in units:
        data += {1: u8, 2: u16, 4: u32}[length_size](len(n)) + n
    return data


def tricky(seed, size):
    """filler with start code emulations (00 00 0x) in it"""
    data = bytearray(filler(seed, size))
    pattern = b"\x00\x00\x00\x01\x00\x00\x02\x00\x00\x03\x00\x00\x00\x00\x04"
    position = 33 + seed % 7
    while position + len(pattern) < size:
        data[position:position + len(pattern)] = pattern
        position += 61
    if size > 3:
        data[-3:] = b"\x00\x00\x00"
    return bytes(data)


def avc_frames(count, length_size=4, key_interval=24, big=False):
    """one access unit per sample; key frames start with an access unit delimiter every other time"""
    samples = []
    for i in range(count):
        units = []
        key = i % key_interval == 0
        if key and (i // key_interval) % 2 == 0:
            units.append(bytes([0x09, 0x10]))
        if i % 7 == 3:
            units.append(bytes([0x06, 0x05, 0x01, 0x80]))
        if big:
            # sizes around the limits of the SAMPLE-AES rules: 48, 49, multiples of 16, more than 10 blocks
            size = [48, 49, 64, 65, 80, 208, 209, 400, 47, 1][i % 10] if not key else [720, 368, 49][(i // key_interval) % 3]
        else:
            size = (20 + (i * 11) % 60) if length_size > 1 else (20 + (i * 11) % 200)
            if key:
                size += 40
        header = bytes([0x65, 0x88]) if key else bytes([0x41 if i % 2 else 0x01, 0x9A])
        body = tricky(i, size - 2) if size > 2 else b""
        units.append((header + body)[:size])
        if i % 9 == 4:
            units.append(bytes([0x01, 0x9E]) + tricky(1000 + i, 70 if length_size > 1 else 30))
        samples.append(nalus(units, length_size))
    return samples


def hevc_frames(count, key_interval=24):
    samples = []
    for i in range(count):
        units = []
        key = i % key_interval == 0
        if i % 5 == 0:
            units.append(u16(35 << 9 | 1) + bytes([0x50]))           # AUD
        if key:
            units.append(u16(19 << 9 | 1) + tricky(i, 90))           # IDR_W_RADL
        else:
            units.append(u16(1 << 9 | 1) + tricky(i, 30 + (i * 13) % 50))
        if i % 6 == 2:
            units.append(u16(40 << 9 | 1) + filler(i, 9))            # suffix SEI
        samples.append(nalus(units, 4))
    return samples


def video(track_id, entry, samples, key_interval=24, fps=24, ctts=True, **options):
    count = len(samples)
    stss = list(range(1, count + 1, key_interval))
    options.setdefault("samples_per_chunk", 5)
    if ctts:
        # I P B B pattern: the composition offsets are 1, 3, 0, 0 frames
        runs = []
        for i in range(count):
            runs.append((1, [1000, 3000, 0, 0][i % 4]))
        options["ctts"] = (0, runs)
    return Track(track_id, "vide", entry if isinstance(entry, list) else [entry], samples, timescale=fps * 1000, duration=1000,
                 media_header=VMHD, width=640, height=360, stss=stss, **options)


def audio(track_id, entry, samples, timescale=44100, duration=1024, **options):
    return Track(track_id, "soun", entry if isinstance(entry, list) else [entry], samples, timescale=timescale, duration=duration,
                 media_header=SMHD, samples_per_chunk=7, **options)


def aac_entry(dsi, oti=0x40, sample_rate=44100, channels=2):
    return syn.audio_entry("mp4a", syn.esds(oti, 5, dsi), channels=channels, sample_rate=sample_rate)


def aac_frames(seed, count):
    # sizes below, at and above the 16 byte limits of SAMPLE-AES
    return [filler(seed * 1000 + i, [200, 16, 17, 32, 33, 48, 5, 131][i % 8] + (i * 3) % 5 * 16) for i in range(count)]


def ac3_frames(seed, count, first=96):
    return [filler(seed * 1000 + i, first if i == 0 else 96 + (i % 3) * 32 + (i % 2)) for i in range(count)]


def ec3_syncframe(seed, size, substream=0):
    frmsiz = size // 2 - 1
    return bytes([0x0B, 0x77]) + u16((substream << 11) | frmsiz) + filler(seed, size - 4)


def ec3_frames(seed, count, bad=None):
    samples = []
    for i in range(count):
        sizes = [[64, 40], [128], [16, 18, 256], [34]][i % 4]
        data = b"".join(ec3_syncframe(seed * 1000 + i * 10 + j, size, j) for j, size in enumerate(sizes))
        if i % 5 == 4:
            data += b"\x0B\x77\x00"     # trailing bytes that are too short to be a sync frame
        if bad == i:
            data = data[:64] + b"\x0B\x78" + data[66:]
        samples.append(data)
    return samples


def write(directory, name, tracks, duration_ms):
    data = syn.movie(syn.ftyp("isom", 512, ("isom", "iso2", "mp41")), tracks, timescale=1000, movie_duration=duration_ms)
    with open(os.path.join(directory, name), "wb") as f:
        f.write(data)


def main():
    out = sys.argv[1]
    hvcc = syn.hvcc()
    avc1 = lambda length_size=4, **kw: syn.visual_entry("avc1", syn.avcc(length_size=length_size, **kw))

    # HEVC + AAC-LC
    write(out, "t4-hevc-aac.mp4", [
        video(1, syn.visual_entry("hvc1", hvcc), hevc_frames(96)),
        audio(2, aac_entry(AAC_LC), aac_frames(1, 180)),
    ], 4180)
    # HEVC (hev1) only, without composition offsets
    write(out, "t4-hev1.mp4", [
        video(1, syn.visual_entry("hev1", syn.hvcc(arrays=((33, 2), (34, 1), (32, 1), (39, 1)))), hevc_frames(60, key_interval=20), key_interval=20, ctts=False),
    ], 2500)
    # Dolby Vision (HEVC based) + AC-3
    write(out, "t4-dvh1-ac3.mp4", [
        audio(1, syn.audio_entry("ac-3", syn.dac3(), channels=6), ac3_frames(2, 90), timescale=48000, duration=1536),
        video(2, syn.visual_entry("dvh1", hvcc, syn.dvcc("dvcC", 5, 6, 0)), hevc_frames(72)),
    ], 3000)
    write(out, "t4-dvhe.mp4", [
        video(1, syn.visual_entry("dvhe", hvcc, syn.dvcc("dvcC", 5, 6, 0)), hevc_frames(30, key_interval=10), key_interval=10),
    ], 1250)
    # AVC with 2 byte NAL unit lengths + E-AC-3 (with a dependent substream, language 'eng')
    write(out, "t4-avc2-ec3.mp4", [
        video(1, avc1(2), avc_frames(96, 2)),
        audio(2, syn.audio_entry("ec-3", syn.dec3(), channels=6), ec3_frames(3, 120), timescale=48000, duration=1536, language=ENG),
    ], 4000)
    # AVC with 1 byte NAL unit lengths
    write(out, "t4-avc1-len1.mp4", [
        video(1, avc1(1, high_ext=False), avc_frames(72, 1, key_interval=12), key_interval=12),
    ], 3000)
    # AVC with NAL units of all the sizes that matter for SAMPLE-AES + HE-AAC
    write(out, "t4-avc-big-sbr.mp4", [
        video(1, avc1(), avc_frames(120, 4, big=True)),
        audio(2, aac_entry(AAC_SBR, sample_rate=22050), aac_frames(4, 100), timescale=22050, duration=1024),
    ], 5000)
    # other AVC based formats
    write(out, "t4-avc3.mp4", [
        video(1, syn.visual_entry("avc3", syn.avcc()), avc_frames(48, 4, big=True)),
        audio(2, aac_entry(AAC_LC), aac_frames(5, 80)),
    ], 2000)
    write(out, "t4-dva1.mp4", [
        video(1, syn.visual_entry("dva1", syn.avcc(), syn.dvcc("dvcC", 9, 4, 2)), avc_frames(48, 4)),
    ], 2000)
    # two sample descriptions: the last chunk uses the second one
    write(out, "t4-two-avc.mp4", [
        video(1, [avc1(4), syn.visual_entry("avc1", syn.avcc(profile=77, level=40, length_size=2, high_ext=False))],
              avc_frames(46, 4) + avc_frames(4, 2), key_interval=10, last_chunk_description=2, samples_per_chunk=7),
    ], 2100)
    # audio only
    write(out, "t4-aac-ps.mp4", [audio(1, aac_entry(AAC_PS, sample_rate=22050, channels=1), aac_frames(6, 150), timescale=22050, duration=1024)], 7000)
    write(out, "t4-aac-main.mp4", [audio(1, aac_entry(AAC_MAIN, oti=0x66, sample_rate=48000, channels=1), aac_frames(7, 150), timescale=48000, duration=1024)], 3200)
    write(out, "t4-aac-main-mpeg4.mp4", [audio(1, aac_entry(AAC_MAIN, sample_rate=48000, channels=1), aac_frames(23, 150), timescale=48000, duration=1024)], 3200)
    write(out, "t4-aac-lc-mpeg2.mp4", [audio(1, aac_entry(AAC_LC, oti=0x67), aac_frames(8, 150))], 3500)
    write(out, "t4-aac-no-dsi.mp4", [audio(1, aac_entry(None), aac_frames(9, 100))], 2400)
    write(out, "t4-mp3.mp4", [audio(1, syn.audio_entry("mp4a", syn.esds(0x6B, 5, None)), aac_frames(10, 100), duration=1152)], 2700)
    write(out, "t4-ac3.mp4", [audio(1, syn.audio_entry("ac-3", syn.dac3(), channels=6), ac3_frames(11, 150), timescale=48000, duration=1536, language=FRA)], 4800)
    write(out, "t4-ac3-short.mp4", [audio(1, syn.audio_entry("ac-3", syn.dac3()), ac3_frames(12, 60, first=7), timescale=48000, duration=1536)], 1920)
    write(out, "t4-ec3.mp4", [audio(1, syn.audio_entry("ec-3", syn.dec3(dependent=False)), ec3_frames(13, 150), timescale=48000, duration=1536)], 4800)
    write(out, "t4-ec3-atmos.mp4", [audio(1, syn.audio_entry("ec-3", syn.dec3(atmos=True), channels=6), ec3_frames(14, 100), timescale=48000, duration=1536,
                                           language=0x7FFF)], 3200)
    write(out, "t4-ec3-bad.mp4", [audio(1, syn.audio_entry("ec-3", syn.dec3()), ec3_frames(15, 60, bad=20), timescale=48000, duration=1536)], 1920)
    write(out, "t4-ac4.mp4", [audio(1, syn.audio_entry("ac-4", box("dac4", b"\x20\xA4\x01\x40")), ac3_frames(16, 150), timescale=48000, duration=2048,
                                     stss=list(range(1, 151, 20)))], 6400)
    # formats that the tools refuse
    write(out, "t4-mp4v.mp4", [
        video(1, syn.visual_entry("mp4v", syn.esds(0x20, 4, filler(17, 20))), syn.raw_samples(17, 24), ctts=False),
        audio(2, aac_entry(AAC_LC), aac_frames(18, 40)),
    ], 1000)
    write(out, "t4-hevc-mp3.mp4", [
        video(1, syn.visual_entry("hvc1", hvcc), hevc_frames(24)),
        audio(2, syn.audio_entry("mp4a", syn.esds(0x6B, 5, None)), aac_frames(19, 40)),
    ], 1000)
    write(out, "t4-hevc-ac4.mp4", [
        video(1, syn.visual_entry("hvc1", hvcc), hevc_frames(24)),
        audio(2, syn.audio_entry("ac-4", box("dac4", b"\x20\xA4\x01\x40")), ac3_frames(20, 30), timescale=48000, duration=2048),
    ], 1280)
    # empty samples, and NAL units that do not fit in their sample
    frames = avc_frames(48, 4)
    frames[5] = b""
    frames[9] = u32(100) + bytes([0x41, 0x9A]) + filler(9, 20)
    frames[13] = u32(8) + bytes([0x41, 0x9A]) + filler(13, 6) + b"\x00\x00"
    frames[24] = b"\x00\x00\x00"
    write(out, "t4-avc-odd-samples.mp4", [video(1, avc1(), frames, ctts=False), audio(2, aac_entry(AAC_LC), aac_frames(21, 80))], 2000)
    frames = aac_frames(22, 60)
    frames[30] = b""
    write(out, "t4-aac-empty-sample.mp4", [audio(1, aac_entry(AAC_LC), frames)], 1400)
    # a video track without sync sample table (every sample is a sync sample) and without samples
    track = video(1, avc1(), avc_frames(30, 4), ctts=False)
    del track.options["stss"]
    write(out, "t4-avc-all-sync.mp4", [track], 1250)
    write(out, "t4-no-samples.mp4", [video(1, avc1(), [], ctts=False), audio(2, aac_entry(AAC_LC), [])], 0)
    track = video(1, avc1(), avc_frames(30, 4), ctts=False)
    write(out, "t4-audio-no-samples.mp4", [track, audio(2, aac_entry(AAC_LC), [])], 1250)


main()
