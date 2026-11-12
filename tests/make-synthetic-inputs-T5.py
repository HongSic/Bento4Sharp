#!/usr/bin/env python3
# Generates the synthetic MP4 files used by tests/compare-tools-T5.sh, for
# code paths of mp42aac, mp42avc, mp42hevc, mp4audioclip and mp4rtphintinfo
# that the stock Bento4 test files do not reach: HEVC tracks, AVC tracks with
# 1/2/3 byte NAL unit lengths and 'avc3' entries, AAC tracks with unusual
# sample rates and channel counts, RTP hint tracks.
# The media payload is filler: the NAL units only have valid headers.
#
# usage: make-synthetic-inputs-T5.py <output-dir>
#
# Deterministic (no randomness, no timestamps). Builds on the helpers of
# tests/make-synthetic-inputs.py, which must be in the same directory.

import importlib.util
import os
import sys

_spec = importlib.util.spec_from_file_location(
    "synthetic", os.path.join(os.path.dirname(os.path.abspath(__file__)), "make-synthetic-inputs.py"))
syn = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(syn)

u8, u16, u32, box, full, filler = syn.u8, syn.u16, syn.u32, syn.box, syn.full, syn.filler
Track, movie, ftyp = syn.Track, syn.movie, syn.ftyp


def length_prefixed(nalus, length_size):
    data = b""
    for n in nalus:
        if length_size == 4:
            data += u32(len(n))
        elif length_size == 3:
            data += syn.u24(len(n))
        elif length_size == 2:
            data += u16(len(n))
        else:
            data += u8(len(n))
        data += n
    return data


# ----------------------------------------------------------------------
# HEVC
# ----------------------------------------------------------------------
def hevc_nalu(nalu_type, seed, size):
    return u16((nalu_type << 9) | 1) + filler(seed, size)


def hvcc(length_size=4, arrays=((32, 1), (33, 1), (34, 2), (39, 1)), nalu_size=9):
    data = u8(1) + u8(0x21) + u32(0x60000000) + bytes([0x90, 0, 0, 0, 0, 0]) + u8(93)
    data += u16(0xF000) + u8(0xFC) + u8(0xFC | 1) + u8(0xF8) + u8(0xF8)
    data += u16(0) + u8((1 << 3) | (1 << 2) | (length_size - 1)) + u8(len(arrays))
    for nalu_type, count in arrays:
        data += u8(0x80 | nalu_type) + u16(count)
        for i in range(count):
            nalu = hevc_nalu(nalu_type, nalu_type * 11 + i, nalu_size + i)
            data += u16(len(nalu)) + nalu
    return box("hvcC", data)


def hevc_samples(length_size):
    big = 100 if length_size == 1 else 300
    lp = lambda nalus: length_prefixed(nalus, length_size)
    samples = [
        # parameter sets in band: no prefix
        lp([hevc_nalu(32, 1, 8), hevc_nalu(33, 2, 12), hevc_nalu(34, 3, 4), hevc_nalu(19, 4, big)]),
        # starts with an access unit delimiter
        lp([hevc_nalu(35, 5, 1), hevc_nalu(1, 6, 60)]),
        # plain slice
        lp([hevc_nalu(1, 7, 45)]),
        # SEI + two slices
        lp([hevc_nalu(39, 8, 10), hevc_nalu(1, 9, 30), hevc_nalu(1, 10, 31)]),
        # empty NAL unit followed by a slice
        lp([b"", hevc_nalu(0, 11, 20)]),
        # access unit delimiter in second position, then a parameter set
        lp([hevc_nalu(1, 12, 5), hevc_nalu(35, 13, 1), hevc_nalu(34, 14, 6), hevc_nalu(1, 15, 7)]),
        # empty sample
        b"",
        # NAL unit longer than what is left in the sample
        lp([hevc_nalu(1, 16, 10)]) + lp([hevc_nalu(1, 17, 40)])[:-9],
        # fewer bytes left than the size of a NAL unit length
        lp([hevc_nalu(1, 18, 14)]) + (b"\0" * (length_size - 1)),
        lp([hevc_nalu(21, 19, 33)]),
    ]
    return samples


def make_hevc(length_size, kind="hvc1", arrays=None):
    config = hvcc(length_size) if arrays is None else hvcc(length_size, arrays)
    samples = hevc_samples(length_size)
    tracks = [Track(1, "vide", [syn.visual_entry(kind, config)], samples, media_header=syn.VMHD, width=640, height=360,
                    timescale=25000, duration=1000, stss=[1, 10]),
              Track(2, "soun", [syn.audio_entry("mp4a", syn.esds(0x40, 5, bytes([0x11, 0x90])))], syn.raw_samples(2, 6),
                    media_header=syn.SMHD, timescale=48000, duration=1024)]
    return movie(ftyp("isom", 1, ("isom", "hvc1")), tracks, movie_duration=400)


# ----------------------------------------------------------------------
# AVC
# ----------------------------------------------------------------------
def avcc(length_size, sps_count=1, pps_count=1):
    data = u8(1) + u8(66) + u8(0xC0) + u8(30) + u8(0xFC | (length_size - 1)) + u8(0xE0 | sps_count)
    for i in range(sps_count):
        sps = bytes([0x67, 66, 0xC0, 30]) + filler(40 + i, 10 + i)
        data += u16(len(sps)) + sps
    data += u8(pps_count)
    for i in range(pps_count):
        pps = bytes([0x68, 0xCE, 0x3C, 0x80]) + filler(50 + i, i)
        data += u16(len(pps)) + pps
    return box("avcC", data)


def avc_samples(length_size):
    big = 100 if length_size == 1 else 300
    lp = lambda nalus: length_prefixed(nalus, length_size)
    samples = [
        lp([bytes([0x65, 0x88, 0x84]) + filler(1, big)]),
        # starts with an access unit delimiter
        lp([bytes([0x09, 0x10]), bytes([0x41, 0x9A]) + filler(2, 40)]),
        lp([bytes([0x06, 0x05, 0x01, 0x80]), bytes([0x01, 0x9E]) + filler(3, 22), bytes([0x01, 0x9E]) + filler(4, 23)]),
        # empty NAL unit followed by a slice
        lp([b"", bytes([0x41, 0xB8]) + filler(5, 14)]),
        # empty sample
        b"",
        # NAL unit longer than what is left in the sample
        lp([bytes([0x41, 0x9A]) + filler(6, 12)]) + lp([bytes([0x41, 0x9A]) + filler(7, 40)])[:-9],
        # fewer bytes left than the size of a NAL unit length
        lp([bytes([0x41, 0x9A]) + filler(8, 17)]) + (b"\0" * (length_size - 1)),
        # access unit delimiter alone
        lp([bytes([0x09, 0xF0])]),
        lp([bytes([0x41, 0x9A]) + filler(9, 31)]),
    ]
    return samples


def make_avc(length_size, kind="avc1", sps_count=1, pps_count=1):
    samples = avc_samples(length_size)
    tracks = [Track(1, "soun", [syn.audio_entry("mp4a", syn.esds(0x40, 5, bytes([0x12, 0x10])), sample_rate=44100)],
                    syn.raw_samples(3, 5), media_header=syn.SMHD, timescale=44100, duration=1024),
              Track(2, "vide", [syn.visual_entry(kind, avcc(length_size, sps_count, pps_count))], samples,
                    media_header=syn.VMHD, width=320, height=240, timescale=30000, duration=1001, stss=[1])]
    return movie(ftyp("isom", 1, ("isom", "avc1")), tracks, movie_duration=300)


# ----------------------------------------------------------------------
# AAC
# ----------------------------------------------------------------------
def make_aac(sample_rate, channels, sample_sizes, kind="mp4a", oti=0x40):
    samples = [filler(1000 + i, size) for i, size in enumerate(sample_sizes)]
    if kind == "mp4a":
        entry = syn.audio_entry("mp4a", syn.esds(oti, 5, bytes([0x12, 0x10])), sample_rate=sample_rate, channels=channels)
    else:
        entry = syn.audio_entry(kind, syn.dac3(), sample_rate=sample_rate, channels=channels)
    tracks = [Track(1, "soun", [entry], samples, media_header=syn.SMHD, timescale=sample_rate or 1000, duration=1024,
                    samples_per_chunk=3)]
    return movie(ftyp("M4A ", 0, ("isom", "mp42")), tracks, movie_duration=len(samples) * 1024 * 1000 // (sample_rate or 1000))


# ----------------------------------------------------------------------
# RTP hint tracks
# ----------------------------------------------------------------------
def rtp_packet(relative_time, payload_type, sequence, constructors, p=0, x=0, m=0, bframe=0, repeat=0, extra=None):
    data = u32(relative_time) + u8(0x80 | (p << 5) | (x << 4)) + u8((m << 7) | payload_type) + u16(sequence)
    data += u8(0) + u8(((1 if extra is not None else 0) << 2) | (bframe << 1) | repeat) + u16(len(constructors))
    if extra is not None:
        data += u32(4 + len(extra)) + extra
    return data + b"".join(constructors)


def c_noop():
    return u8(0) + filler(77, 15)


def c_immediate(data, declared=None):
    return u8(1) + u8(len(data) if declared is None else declared) + data + b"\0" * (14 - len(data))


def c_sample(track_ref_index, length, sample_number, offset):
    return u8(2) + u8(track_ref_index) + u16(length) + u32(sample_number) + u32(offset) + u16(1) + u16(1)


def c_sample_desc(track_ref_index, length, index, offset):
    return u8(3) + u8(track_ref_index) + u16(length) + u32(index) + u32(offset) + u32(0)


def rtpo(offset):
    return u32(12) + b"rtpo" + u32(offset)


def hint_sample(packets, extra=b""):
    return u16(len(packets)) + u16(0) + b"".join(packets) + extra


def hint_movie(hint_samples, sdp=b"m=audio 0 RTP/AVP 96\r\na=rtpmap:96 mpeg4-generic/48000/2\r\na=control:trackID=2\r\n",
               tref=True, media_first=True, stts_runs=None):
    media = Track(1, "soun", [syn.audio_entry("mp4a", syn.esds(0x40, 5, bytes([0x11, 0x90])))],
                  [filler(500 + i, 40 + 13 * i) for i in range(6)], media_header=syn.SMHD, timescale=48000, duration=1024)
    options = {}
    if tref:
        options["trak_extra"] = [box("tref", box("hint", u32(1)))]
    if sdp is not None:
        options["trak_extra_after"] = [box("udta", box("hnti", box("sdp ", sdp)))]
    if stts_runs:
        options["stts_runs"] = stts_runs
    entry = box("rtp ", b"\0" * 6, u16(1), u16(1), u16(1), u32(1460), box("tims", u32(90000)), box("tsro", u32(1234)))
    hint = Track(2, "hint", [entry], hint_samples, media_header=syn.HMHD, timescale=90000, duration=1920, **options)
    hnti = box("udta", box("hnti", box("rtp ", b"sdp ", b"a=range:npt=0-0.128\r\n")))
    tracks = [media, hint] if media_first else [hint, media]
    return movie(ftyp("isom", 1, ("isom", "mp42")), tracks, moov_extra=[hnti], movie_duration=128)


def make_hint():
    samples = [
        # one packet: immediate header + whole media sample
        hint_sample([rtp_packet(0, 96, 1, [c_immediate(bytes([0x00, 0x10, 0x01, 0x40])), c_sample(0, 40, 1, 0)], m=1)]),
        # two packets, the second one with a timestamp offset
        hint_sample([rtp_packet(0, 96, 2, [c_immediate(bytes([0x00, 0x10, 0x01, 0xA8])), c_sample(0, 30, 2, 0)]),
                     rtp_packet(10, 96, 3, [c_noop(), c_sample(0, 23, 2, 30), c_immediate(b"")], m=1, p=1, x=1, extra=rtpo(4500))]),
        # no packet at all
        hint_sample([]),
        # data taken from the hint sample itself, unknown and known extra entries, flags
        hint_sample([rtp_packet(-5, 97, 4, [c_sample(0xFF, 16, 4, 48), c_sample(0, 10, 3, 5), c_sample(0, 0, 3, 0)],
                                bframe=1, repeat=1, extra=u32(16) + b"abcd" + filler(3, 8) + rtpo(0xFFFFFFF6))],
                    extra=b"immediate-data-in-hint-sample!!!"),
        # a packet without constructors and immediate data of the maximum size
        hint_sample([rtp_packet(0, 127, 0xFFFF, []),
                     rtp_packet(0, 0, 0, [c_immediate(filler(9, 14)), c_sample(0, 105, 6, 0)], m=1)]),
    ]
    return hint_movie(samples, stts_runs=[(2, 1920), (1, 960), (2, 3840)])


def make_hint_long_sdp():
    # the SDP text is longer than the 1024 byte buffer of AP4_Debug
    sdp = b"m=audio 0 RTP/AVP 96\r\n" + b"".join(b"a=x-line-%03d:%s\r\n" % (i, b"0123456789abcdef" * 2) for i in range(40))
    samples = [hint_sample([rtp_packet(0, 96, 1, [c_sample(0, 20, 1, 0)])])]
    return hint_movie(samples, sdp=sdp)


def make_hint_binary_sdp():
    # SDP text that is not ASCII: UTF-8 (2, 3 and 4 byte sequences), Latin-1,
    # bytes that are not valid UTF-8 and a truncated sequence
    sdp = (b"s=caf" + bytes([0xC3, 0xA9, 0x20, 0xE6, 0x97, 0xA5, 0xE6, 0x9C, 0xAC, 0x20, 0xF0, 0x9F, 0x8E, 0xB5]) + b"\r\n" +
           b"i=latin1 " + bytes([0xE9, 0xE8]) + b" invalid " + bytes([0xFF, 0xFE, 0x80]) + b" " + bytes([0xC3]) + b"\r\n")
    samples = [hint_sample([rtp_packet(0, 96, 1, [c_sample(0, 20, 1, 0)])])]
    return hint_movie(samples, sdp=sdp)


def make_hint_long_binary_sdp():
    # more than 1023 bytes, with a multi-byte UTF-8 sequence across the limit
    sdp = b"s=" + bytes([0xE6, 0x97, 0xA5]) * 400 + b"\r\n"
    samples = [hint_sample([rtp_packet(0, 96, 1, [c_sample(0, 20, 1, 0)])])]
    return hint_movie(samples, sdp=sdp)


def make_hint_no_sdp_no_tref():
    samples = [hint_sample([rtp_packet(0, 96, 1, [c_immediate(b"abc")])]),
               hint_sample([rtp_packet(0, 96, 2, [c_immediate(b"defg"), c_sample(0xFF, 4, 1, 0)])]),
               # media track sample, but there is no track reference: stops here
               hint_sample([rtp_packet(0, 96, 3, [c_immediate(b"hij"), c_sample(0, 10, 1, 0)])]),
               hint_sample([rtp_packet(0, 96, 4, [c_immediate(b"never")])])]
    return hint_movie(samples, sdp=None, tref=False, media_first=False)


def make_hint_sample_desc():
    samples = [hint_sample([rtp_packet(0, 96, 1, [c_immediate(b"ok"), c_sample(0, 12, 1, 3)])]),
               hint_sample([rtp_packet(0, 96, 2, [c_immediate(b"sd"), c_sample_desc(0, 8, 1, 0)])]),
               hint_sample([rtp_packet(0, 96, 3, [c_immediate(b"never")])])]
    return hint_movie(samples)


def make_hint_bad_sample_number():
    samples = [hint_sample([rtp_packet(0, 96, 1, [c_immediate(b"ok"), c_sample(0, 12, 6, 3)])]),
               hint_sample([rtp_packet(0, 96, 2, [c_immediate(b"bad"), c_sample(0, 8, 7, 0)])]),
               hint_sample([rtp_packet(0, 96, 3, [c_immediate(b"never")])])]
    return hint_movie(samples)


def make_hint_bad_sample_range():
    samples = [hint_sample([rtp_packet(0, 96, 1, [c_immediate(b"ok"), c_sample(0, 12, 1, 28)])]),
               # 30 bytes at offset 20 of a 40 byte sample
               hint_sample([rtp_packet(0, 96, 2, [c_immediate(b"bad"), c_sample(0, 30, 1, 20)])]),
               hint_sample([rtp_packet(0, 96, 3, [c_immediate(b"never")])])]
    return hint_movie(samples)


def main():
    if len(sys.argv) != 2:
        sys.stderr.write("usage: make-synthetic-inputs-T5.py <output-dir>\n")
        return 1
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    files = {
        "t5-hevc-l4.mp4": make_hevc(4),
        "t5-hevc-l2.mp4": make_hevc(2, "hev1"),
        "t5-hevc-l1.mp4": make_hevc(1),
        "t5-hevc-l3.mp4": make_hevc(3),
        "t5-hevc-noparams.mp4": make_hevc(4, "hev1", arrays=()),
        "t5-hevc-order.mp4": make_hevc(4, "hvc1", arrays=((34, 2), (39, 1), (33, 2), (32, 1), (34, 1), (32, 2))),
        "t5-avc-l4.mp4": make_avc(4),
        "t5-avc-l2.mp4": make_avc(2, sps_count=2, pps_count=3),
        "t5-avc-l1.mp4": make_avc(1),
        "t5-avc-l3.mp4": make_avc(3),
        "t5-avc3-l4.mp4": make_avc(4, "avc3"),
        "t5-avc-noparams.mp4": make_avc(4, "avc1", sps_count=0, pps_count=0),
        "t5-aac-7350-1.mp4": make_aac(7350, 1, [10, 200, 0, 3000]),
        "t5-aac-96000-6.mp4": make_aac(96000, 6, [100, 2040, 2041, 5000, 8184, 8185, 8191, 9000]),
        "t5-aac-12345-8.mp4": make_aac(12345, 8, [64, 65, 66, 67, 68]),
        "t5-aac-22050-0.mp4": make_aac(22050, 0, [30, 31]),
        "t5-aac-nosamples.mp4": make_aac(48000, 2, []),
        "t5-mp3-in-mp4a.mp4": make_aac(32000, 2, [417, 418, 417], oti=0x6B),
        "t5-ac3.mp4": make_aac(48000, 6, [80, 80, 80], kind="ac-3"),
        "t5-hint.mp4": make_hint(),
        "t5-hint-long-sdp.mp4": make_hint_long_sdp(),
        "t5-hint-binary-sdp.mp4": make_hint_binary_sdp(),
        "t5-hint-long-binary-sdp.mp4": make_hint_long_binary_sdp(),
        "t5-hint-no-sdp-no-tref.mp4": make_hint_no_sdp_no_tref(),
        "t5-hint-sample-desc.mp4": make_hint_sample_desc(),
        "t5-hint-bad-sample-number.mp4": make_hint_bad_sample_number(),
        "t5-hint-bad-sample-range.mp4": make_hint_bad_sample_range(),
    }
    for name, data in files.items():
        with open(os.path.join(out, name), "wb") as f:
            f.write(data)
    return 0


if __name__ == "__main__":
    sys.exit(main())
