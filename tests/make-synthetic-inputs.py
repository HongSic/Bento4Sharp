#!/usr/bin/env python3
# Generates small synthetic MP4 files that exercise atoms and code paths the
# stock Bento4 test files do not cover (HEVC/AV1/VPx/Dolby sample entries,
# 64-bit tables, sample groups, fragments with every optional field, iTunes
# metadata, hint tracks, 64-bit atom sizes, truncated files...).
# The media payload is filler: the files are only meant to be parsed.
#
# usage: make-synthetic-inputs.py <output-dir>
#
# Used by tests/compare-with-reference.sh; deterministic (no randomness, no
# timestamps), so the same files are produced on every run.

import os
import struct
import sys


def u8(v): return struct.pack(">B", v & 0xFF)
def u16(v): return struct.pack(">H", v & 0xFFFF)
def u24(v): return struct.pack(">I", v & 0xFFFFFF)[1:]
def u32(v): return struct.pack(">I", v & 0xFFFFFFFF)
def u64(v): return struct.pack(">Q", v & 0xFFFFFFFFFFFFFFFF)


def box(kind, *payload):
    data = b"".join(payload)
    kind = kind.encode("latin-1") if isinstance(kind, str) else kind
    return u32(8 + len(data)) + kind + data


def full(kind, version, flags, *payload):
    return box(kind, u8(version), u24(flags), *payload)


def box64(kind, *payload):
    data = b"".join(payload)
    return u32(1) + kind.encode("latin-1") + u64(16 + len(data)) + data


class Bits:
    def __init__(self):
        self.bits = []

    def put(self, value, count):
        for i in range(count - 1, -1, -1):
            self.bits.append((value >> i) & 1)
        return self

    def bytes(self):
        bits = self.bits + [0] * ((8 - len(self.bits) % 8) % 8)
        out = bytearray()
        for i in range(0, len(bits), 8):
            byte = 0
            for bit in bits[i:i + 8]:
                byte = (byte << 1) | bit
            out.append(byte)
        return bytes(out)


def filler(seed, size):
    # deterministic pseudo-random bytes (LCG)
    out = bytearray()
    state = seed * 2654435761 % (1 << 32)
    for _ in range(size):
        state = (state * 1103515245 + 12345) % (1 << 31)
        out.append((state >> 16) & 0xFF)
    return bytes(out)


MATRIX = u32(0x10000) + u32(0) * 3 + u32(0x10000) + u32(0) * 3 + u32(0x40000000)


# ----------------------------------------------------------------------
# descriptors and codec configurations
# ----------------------------------------------------------------------
def descriptor(tag, payload, size_bytes=1):
    size = len(payload)
    encoded = bytearray()
    for i in range(size_bytes - 1, -1, -1):
        encoded.append(((size >> (7 * i)) & 0x7F) | (0x80 if i else 0))
    return u8(tag) + bytes(encoded) + payload


def esds(oti, stream_type, dsi, es_id=1, size_bytes=1):
    dcd = u8(oti) + u8((stream_type << 2) | 1) + u24(6144) + u32(128000) + u32(96000)
    if dsi is not None:
        dcd += descriptor(5, dsi, size_bytes)
    es = u16(es_id) + u8(0) + descriptor(4, dcd, size_bytes) + descriptor(6, u8(2), size_bytes)
    return full("esds", 0, 0, descriptor(3, es, size_bytes))


def avcc(profile=100, compat=0, level=31, length_size=4, high_ext=True):
    sps = bytes([0x67, profile, compat, level]) + filler(1, 12)
    pps = bytes([0x68, 0xEE, 0x3C, 0x80])
    data = u8(1) + u8(profile) + u8(compat) + u8(level) + u8(0xFC | (length_size - 1))
    data += u8(0xE0 | 1) + u16(len(sps)) + sps + u8(1) + u16(len(pps)) + pps
    if high_ext:
        data += u8(0xFC | 1) + u8(0xF8 | 0) + u8(0xF8 | 0) + u8(0)
    return box("avcC", data)


def hvcc(kind="hvcC", profile=2, tier=1, level=153, arrays=((32, 1), (33, 1), (34, 2), (39, 1), (50, 1))):
    data = u8(1) + u8((0 << 6) | (tier << 5) | profile) + u32(0x60000000)
    data += bytes([0x90, 0x00, 0x00, 0x00, 0x00, 0x00]) + u8(level)
    data += u16(0xF000 | 0) + u8(0xFC | 0) + u8(0xFC | 1) + u8(0xF8 | 2) + u8(0xF8 | 2)
    data += u16(24 * 256) + u8((1 << 6) | (1 << 3) | (1 << 2) | 3) + u8(len(arrays))
    for nalu_type, count in arrays:
        data += u8(0x80 | nalu_type) + u16(count)
        for i in range(count):
            nalu = u16(nalu_type << 9 | 1) + filler(nalu_type * 7 + i, 5 + i)
            data += u16(len(nalu)) + nalu
    return box(kind, data)


def av1c(profile=1, level=9, tier=1, obus=b"\x0a\x0b\x00\x00\x00\x24\xcf\x7f\x0d\xbf\xff\x30\x08", delay=0x11):
    data = u8(0x81) + u8((profile << 5) | level) + u8((tier << 7) | (1 << 6) | (0 << 5) | (0 << 4) | (1 << 3) | (1 << 2) | 1)
    data += u8(delay) + obus
    return box("av1C", data)


def vpcc(profile=2, level=31, depth=10):
    return full("vpcC", 1, 0, u8(profile), u8(level), u8((depth << 4) | (1 << 1) | 1), u8(9), u8(16), u8(9), u16(0))


def dvcc(kind="dvcC", profile=5, level=6, compat=0):
    bits = Bits().put(1, 8).put(0, 8).put(profile, 7).put(level, 6).put(1, 1).put(0, 1).put(1, 1).put(compat, 4).put(0, 28)
    return box(kind, bits.bytes() + u32(0) * 4)


def dac3():
    return box("dac3", Bits().put(0, 2).put(8, 5).put(0, 3).put(7, 3).put(1, 1).put(15, 5).put(0, 5).bytes())


def dec3(atmos=False, dependent=True):
    bits = Bits().put(640, 13).put(1, 3)
    bits.put(0, 2).put(16, 5).put(0, 1).put(0, 1).put(0, 3).put(7, 3).put(1, 1).put(0, 3)
    if dependent:
        bits.put(1, 4).put(0x101, 9)
    else:
        bits.put(0, 4).put(0, 1)
    bits.put(0, 2).put(16, 5).put(0, 1).put(0, 1).put(1, 3).put(2, 3).put(0, 1).put(0, 3).put(0, 4).put(0, 1)
    data = bits.bytes()
    if atmos:
        data += u8(1) + u8(16)
    return box("dec3", data)


def dmlp(peak_data_rate=0):
    return box("dmlp", u32(0x12345678), u16((peak_data_rate << 1) & 0xFFFF), u32(0))


def colr(kind="nclx"):
    if kind == "nclx":
        return box("colr", b"nclx", u16(9), u16(16), u16(9), u8(0x80))
    if kind == "nclc":
        return box("colr", b"nclc", u16(1), u16(1), u16(1))
    return box("colr", kind.encode("latin-1"), filler(77, 24))


# ----------------------------------------------------------------------
# sample entries
# ----------------------------------------------------------------------
def visual_entry(kind, *children, width=640, height=360, compressor=b"synthetic"):
    name = u8(len(compressor)) + compressor + b"\0" * (31 - len(compressor))
    return box(kind, b"\0" * 6, u16(1), u16(0), u16(0), u32(0) * 3, u16(width), u16(height),
               u32(0x480000), u32(0x480000), u32(0), u16(1), name, u16(24), u16(0xFFFF), *children)


def audio_entry(kind, *children, channels=2, sample_size=16, sample_rate=48000, version=0):
    data = b"\0" * 6 + u16(1) + u16(version) + u16(0) + u32(0) + u16(channels) + u16(sample_size)
    data += u16(0xFFFE if version else 0) + u16(0) + u32(sample_rate << 16)
    if version == 1:
        data += u32(1024) + u32(2) + u32(4) + u32(2)
    elif version == 2:
        data = b"\0" * 6 + u16(1) + u16(2) + u16(0) + u32(0) + u16(3) + u16(16) + u16(0xFFFE) + u16(0) + u32(0x10000)
        data += u32(72) + struct.pack(">d", 96000.0) + u32(6) + u32(0x7F000000) + u32(24) + u32(0) + u32(0) + u32(0)
    return box(kind, data, *children)


# ----------------------------------------------------------------------
# tracks
# ----------------------------------------------------------------------
class Track:
    def __init__(self, track_id, handler, entries, samples, timescale=1000, duration=100,
                 media_header=None, width=0, height=0, **options):
        self.track_id = track_id
        self.handler = handler
        self.entries = entries
        self.samples = samples
        self.timescale = timescale
        self.duration = duration
        self.media_header = media_header
        self.width = width
        self.height = height
        self.options = options

    def stbl(self, offsets):
        o = self.options
        count = len(self.samples)
        children = [full("stsd", 0, 0, u32(o.get("stsd_count", len(self.entries))), *self.entries)]
        if o.get("stts_runs"):
            runs = o["stts_runs"]
        else:
            runs = [(count, self.duration)] if count else []
        children.append(full("stts", 0, 0, u32(len(runs)), *[u32(c) + u32(d) for c, d in runs]))
        if "ctts" in o:
            version, entries = o["ctts"]
            children.append(full("ctts", version, 0, u32(len(entries)), *[u32(c) + u32(d) for c, d in entries]))
        if "stss" in o:
            children.append(full("stss", 0, 0, u32(len(o["stss"])), *[u32(s) for s in o["stss"]]))
        # two samples per chunk, last chunk may have one
        per_chunk = o.get("samples_per_chunk", 2)
        chunks = [self.samples[i:i + per_chunk] for i in range(0, count, per_chunk)]
        stsc = []
        if chunks:
            stsc.append((1, per_chunk if len(chunks[0]) == per_chunk else len(chunks[0]), 1))
            if len(chunks) > 1 and len(chunks[-1]) != per_chunk:
                stsc.append((len(chunks), len(chunks[-1]), o.get("last_chunk_description", 1)))
        children.append(full("stsc", 0, 0, u32(len(stsc)), *[u32(a) + u32(b) + u32(c) for a, b, c in stsc]))
        field_size = o.get("stz2")
        if field_size:
            if field_size == 4:
                sizes = [len(s) for s in self.samples]
                if len(sizes) % 2:
                    sizes.append(0)
                table = bytes([(sizes[i] << 4) | sizes[i + 1] for i in range(0, len(sizes), 2)])
            elif field_size == 8:
                table = b"".join(u8(len(s)) for s in self.samples)
            else:
                table = b"".join(u16(len(s)) for s in self.samples)
            children.append(full("stz2", 0, 0, u24(0), u8(field_size), u32(count), table))
        elif o.get("fixed_size"):
            children.append(full("stsz", 0, 0, u32(o["fixed_size"]), u32(count)))
        else:
            children.append(full("stsz", 0, 0, u32(0), u32(count), *[u32(len(s)) for s in self.samples]))
        chunk_offsets = []
        position = 0
        for chunk in chunks:
            chunk_offsets.append(offsets[position])
            position += len(chunk)
        if o.get("co64"):
            children.append(full("co64", 0, 0, u32(len(chunk_offsets)), *[u64(x) for x in chunk_offsets]))
        else:
            children.append(full("stco", 0, 0, u32(len(chunk_offsets)), *[u32(x) for x in chunk_offsets]))
        children.extend(o.get("stbl_extra", []))
        return box("stbl", *children)

    def trak(self, offsets):
        o = self.options
        count = len(self.samples)
        media_duration = sum(c * d for c, d in o["stts_runs"]) if o.get("stts_runs") else count * self.duration
        v1 = o.get("version1", False)
        if v1:
            tkhd = full("tkhd", 1, o.get("flags", 7), u64(3600000000), u64(3600000001), u32(self.track_id), u32(0), u64(media_duration))
            mdhd = full("mdhd", 1, 0, u64(3600000000), u64(3600000001), u32(self.timescale), u64(media_duration), u16(o.get("language", 0x15C7)), u16(0))
        else:
            tkhd = full("tkhd", 0, o.get("flags", 7), u32(100), u32(200), u32(self.track_id), u32(0), u32(media_duration))
            mdhd = full("mdhd", 0, 0, u32(100), u32(200), u32(self.timescale), u32(media_duration), u16(o.get("language", 0x55C4)), u16(0))
        tkhd += u32(0) * 2 + u16(0) + u16(o.get("alternate_group", 0)) + u16(0x100 if self.handler == "soun" else 0) + u16(0) + MATRIX
        tkhd += u32(self.width << 16) + u32(self.height << 16)
        tkhd = u32(len(tkhd)) + tkhd[4:]
        hdlr = full("hdlr", 0, 0, u32(0), self.handler.encode("latin-1"), u32(0) * 3, o.get("handler_name", b"Synthetic Handler\0"))
        dinf = box("dinf", full("dref", 0, 0, u32(1), full("url ", 0, 1)))
        minf = box("minf", self.media_header, dinf, self.stbl(offsets))
        children = [tkhd]
        children.extend(o.get("trak_extra", []))
        children.append(box("mdia", mdhd, hdlr, minf))
        children.extend(o.get("trak_extra_after", []))
        return box("trak", *children)


VMHD = full("vmhd", 0, 1, u16(0), u16(0) * 3)
SMHD = full("smhd", 0, 0, u16(0), u16(0))
NMHD = full("nmhd", 0, 0)
STHD = full("sthd", 0, 0)
HMHD = full("hmhd", 0, 0, u16(1400), u16(1200), u32(256000), u32(128000), u32(0))


def movie(ftyp, tracks, moov_extra=(), moov_extra_first=(), top_extra=(), mvhd_version=0, mdat64=False, timescale=1000, movie_duration=5000):
    def build(offsets_by_track):
        if mvhd_version == 1:
            mvhd = full("mvhd", 1, 0, u64(3600000000), u64(3600000001), u32(timescale), u64(movie_duration))
        else:
            mvhd = full("mvhd", 0, 0, u32(100), u32(200), u32(timescale), u32(movie_duration))
        mvhd += u32(0x10000) + u16(0x100) + u16(0) + u32(0) * 2 + MATRIX + u32(0) * 6 + u32(len(tracks) + 1)
        mvhd = u32(len(mvhd)) + mvhd[4:]
        traks = [t.trak(offsets_by_track[i]) for i, t in enumerate(tracks)]
        return box("moov", *moov_extra_first, mvhd, *traks, *moov_extra)

    zero = [[0] * len(t.samples) for t in tracks]
    head = ftyp + b"".join(top_extra) + build(zero)
    base = len(head) + (16 if mdat64 else 8)
    offsets = []
    payload = b""
    for t in tracks:
        track_offsets = []
        for s in t.samples:
            track_offsets.append(base + len(payload))
            payload += s
        offsets.append(track_offsets)
    head = ftyp + b"".join(top_extra) + build(offsets)
    mdat = box64("mdat", payload) if mdat64 else box("mdat", payload)
    return head + mdat


def ftyp(major="isom", minor=512, brands=("isom", "iso6", "mp41")):
    return box("ftyp", major.encode("latin-1"), u32(minor), *[b.encode("latin-1") for b in brands])


def avc_samples(count, length_size=4):
    samples = []
    for i in range(count):
        if i % 5 == 0:
            nalus = [bytes([0x09, 0x10]), bytes([0x65, 0x88, 0x84]) + filler(i, 20)]
        elif i % 5 == 1:
            nalus = [bytes([0x41, 0x9A]) + filler(i, 12)]             # slice, first_mb=0, slice_type=5 (P)
        elif i % 5 == 2:
            nalus = [bytes([0x06, 0x05, 0x01, 0x80]), bytes([0x01, 0x9E]) + filler(i, 9)]   # slice_type 1 (B)
        elif i % 5 == 3:
            nalus = [bytes([0x41, 0xB8]) + filler(i, 14)]             # slice_type 2 -> I
        else:
            nalus = [bytes([0x0C, 0xFF, 0xFF]), bytes([0x01, 0x88, 0x40]) + filler(i, 30)]
        data = b""
        for n in nalus:
            if length_size == 4:
                data += u32(len(n))
            elif length_size == 2:
                data += u16(len(n))
            else:
                data += u8(len(n))
            data += n
        samples.append(data)
    return samples


def raw_samples(seed, count, size=24):
    return [filler(seed * 100 + i, size + (i * 7) % 13) for i in range(count)]


# ----------------------------------------------------------------------
# files
# ----------------------------------------------------------------------
def make_codecs():
    aac_dsi = bytes([0x11, 0x90])
    he_aac_dsi = bytes([0x2B, 0x92, 0x08, 0x00])            # SBR, explicit hierarchical signaling
    tracks = [
        Track(1, "vide", [visual_entry("avc1", avcc(), colr("nclx"), box("pasp", u32(1), u32(1)), box("btrt", u32(0), u32(500000), u32(400000)))],
              avc_samples(11), media_header=VMHD, width=640, height=360, timescale=30000, duration=1001,
              ctts=(0, [(1, 2002), (2, 0), (3, 1001), (5, 0)]), stss=[1, 6, 11],
              trak_extra=[box("edts", full("elst", 0, 0, u32(2), u32(1000), u32(0xFFFFFFFF), u16(1), u16(0), u32(4000), u32(2002), u16(1), u16(0)))]),
        Track(2, "vide", [visual_entry("avc3", avcc(profile=66, compat=0xC0, level=30, length_size=2, high_ext=False), width=320, height=180)],
              avc_samples(7, 2), media_header=VMHD, width=320, height=180, version1=True,
              ctts=(1, [(2, 0xFFFFFC17), (5, 1001)]), co64=True, samples_per_chunk=3,
              trak_extra=[box("edts", full("elst", 1, 0, u32(1), u64(5000), u64(0xFFFFFFFFFFFFFFFF), u16(1), u16(0))),
                          box("tref", box("cdsc", u32(1)), box("chap", u32(9), u32(10)), box("zzzz", u32(1)))]),
        Track(3, "vide", [visual_entry("hvc1", hvcc(), colr("nclc"))], raw_samples(3, 5), media_header=VMHD, width=1920, height=1080,
              stz2=16, stss=[1]),
        Track(4, "vide", [visual_entry("hev1", hvcc(profile=1, tier=0, level=93, arrays=((33, 1), (34, 1))), colr("nclc"))],
              raw_samples(4, 4), media_header=VMHD, width=1280, height=720, fixed_size=24, samples_per_chunk=1),
        Track(5, "vide", [visual_entry("dvhe", hvcc(), dvcc("dvcC", 5, 6, 0))], raw_samples(5, 3), media_header=VMHD, width=3840, height=2160),
        Track(6, "vide", [visual_entry("hvc1", hvcc(), dvcc("dvvC", 8, 9, 4), hvcc("hvcE", arrays=((33, 1),)))], raw_samples(6, 3),
              media_header=VMHD, width=3840, height=2160),
        Track(7, "vide", [visual_entry("dva1", avcc(), dvcc("dvcC", 4, 3, 2), box("avcE", avcc()[8:]))], avc_samples(3), media_header=VMHD, width=1920, height=1080),
        Track(8, "vide", [visual_entry("av01", av1c(), colr("nclx"))], raw_samples(8, 4), media_header=VMHD, width=854, height=480),
        Track(9, "vide", [visual_entry("vp09", vpcc(), colr("nclx"))], raw_samples(9, 4), media_header=VMHD, width=854, height=480),
        Track(10, "vide", [visual_entry("vp08", vpcc(0, 10, 8))], raw_samples(10, 2), media_header=VMHD, width=426, height=240),
        Track(11, "vide", [visual_entry("mp4v", esds(0x20, 4, bytes([0, 0, 1, 0xB0, 1, 0, 0, 1, 0xB5])))], raw_samples(11, 3), media_header=VMHD, width=352, height=288),
        Track(12, "soun", [audio_entry("mp4a", esds(0x40, 5, aac_dsi))], raw_samples(12, 9), media_header=SMHD, timescale=48000, duration=1024,
              stz2=8, stts_runs=[(4, 1024), (3, 512), (2, 2048)]),
        Track(13, "soun", [audio_entry("mp4a", esds(0x40, 5, he_aac_dsi, size_bytes=4), sample_rate=24000)], raw_samples(13, 7, 3), media_header=SMHD,
              timescale=24000, duration=1024, stz2=4, alternate_group=1, language=0x1144),
        Track(14, "soun", [audio_entry("mp4a", esds(0x6B, 5, None), sample_rate=44100)], raw_samples(14, 3), media_header=SMHD),
        Track(15, "soun", [audio_entry("mp4a", esds(0x67, 5, bytes([0x12, 0x10])), version=1)], raw_samples(15, 3), media_header=SMHD),
        Track(16, "soun", [audio_entry("mp4a", box("wave", box("frma", b"mp4a"), esds(0x40, 5, aac_dsi), box("\0\0\0\0")), version=2)],
              raw_samples(16, 3), media_header=SMHD),
        Track(17, "soun", [audio_entry("ac-3", dac3(), channels=6)], raw_samples(17, 3), media_header=SMHD),
        Track(18, "soun", [audio_entry("ec-3", dec3(atmos=True), channels=6)], raw_samples(18, 3), media_header=SMHD),
        Track(19, "soun", [audio_entry("ec-3", dec3(atmos=False, dependent=False))], raw_samples(19, 3), media_header=SMHD),
        Track(20, "soun", [audio_entry("mlpa", dmlp(), sample_rate=0)], raw_samples(20, 3), media_header=SMHD),
        Track(21, "soun", [audio_entry("Opus", box("dOps", u8(0), u8(2), u16(312), u32(48000), u16(0), u8(0)))], raw_samples(21, 3), media_header=SMHD),
        Track(22, "soun", [audio_entry("fLaC", full("dfLa", 0, 0, filler(5, 38))), audio_entry("alac", full("alac", 0, 0, filler(6, 24)))],
              raw_samples(22, 5), media_header=SMHD, last_chunk_description=2),
        Track(23, "soun", [audio_entry("dtsc", box("ddts", filler(7, 20))), audio_entry("dtsh"), audio_entry("dtsl"), audio_entry("dtse")],
              raw_samples(23, 2), media_header=SMHD),
        Track(24, "subt", [box("stpp", b"\0" * 6, u16(1), b"http://www.w3.org/ns/ttml\0", b"http://example.com/schema.xsd\0", b"image/png\0",
                               box("btrt", u32(0), u32(1000), u32(500)))], raw_samples(24, 2), media_header=STHD),
        Track(25, "text", [box("tx3g", b"\0" * 6, u16(1), filler(8, 30))], raw_samples(25, 2), media_header=NMHD),
        Track(26, "sdsm", [box("mp4s", b"\0" * 6, u16(1), esds(0x01, 3, bytes([0, 0x1F])))], raw_samples(26, 1), media_header=NMHD),
        Track(27, "odsm", [box("mp4s", b"\0" * 6, u16(1), esds(0x01, 1, None))], raw_samples(27, 1), media_header=NMHD, flags=1),
        Track(28, "hint", [box("rtp ", b"\0" * 6, u16(1), u16(1), u16(1), u32(1460), box("tims", u32(90000)), box("tsro", u32(1234)))],
              raw_samples(28, 2), media_header=HMHD,
              trak_extra=[box("tref", box("hint", u32(1)))],
              trak_extra_after=[box("udta", box("hnti", box("sdp ", b"m=video 0 RTP/AVP 96\r\na=control:trackID=28\r\n")))]),
        Track(29, "meta", [box("mett", b"\0" * 6, u16(1), b"\0application/json\0")], raw_samples(29, 1), media_header=NMHD,
              handler_name=b"\x0bPascal Name"),
        Track(30, "jpeg", [visual_entry("jpeg", width=100, height=50)], raw_samples(30, 1), media_header=VMHD, width=100, height=50, handler_name=b""),
        Track(31, "vide", [visual_entry("avc1", avcc())], [], media_header=VMHD, width=16, height=16),
    ]
    iods = full("iods", 0, 0, descriptor(0x10, u16((1 << 6) | 0x0F) + bytes([0xFF, 0xFF, 0xFE, 0x21, 0xFF]) + descriptor(0x0E, u32(26)) + descriptor(0x0E, u32(27))))
    hnti = box("udta", box("hnti", box("rtp ", b"sdp ", b"a=range:npt=0-5.0\r\n")), box("name", b"Top Level Name"))
    return movie(ftyp("mp42", 1, ("isom", "mp42", "avc1", "hvc1", "dby1", "\0\0\0\0")), tracks, moov_extra=[iods, hnti],
                 top_extra=[full("pdin", 0, 0, u32(1000), u32(5), u32(2000), u32(3)), box("free", b"padding!"), box("wide")])


def data_atom(type_indicator, payload, locale=0):
    return box("data", u32(type_indicator), u32(locale), payload)


def make_metadata():
    ilst = box("ilst",
               box(b"\xa9nam", data_atom(1, "Synthetic Tïtle ☃".encode("utf-8"))),
               box(b"\xa9ART", data_atom(1, b"Artist \"quoted\" \\ back\tslash")),
               box(b"\xa9alb", data_atom(1, b"Latin-1 \xe9\xe8 bytes")),
               box(b"\xa9day", data_atom(1, b"2020-01-02")),
               box(b"\xa9too", data_atom(1, b"Bento4Sharp tests")),
               box("trkn", data_atom(0, u16(0) + u16(3) + u16(12) + u16(0))),
               box("disk", data_atom(0, u16(0) + u16(1) + u16(2))),
               box("tmpo", data_atom(21, u16(120))),
               box("cpil", data_atom(21, u8(1))),
               box("gnre", data_atom(0, u16(18))),
               box("rtng", data_atom(21, u8(4))),
               box("pgap", data_atom(21, u8(0))),
               box("covr", data_atom(13, b"\xff\xd8\xff\xe0" + filler(3, 40)), data_atom(14, b"\x89PNG\r\n\x1a\n" + filler(4, 20))),
               box("plID", data_atom(21, u64(0x0102030405060708))),
               box("cnID", data_atom(21, u32(123456789))),
               box("stik", data_atom(21, u8(9))),
               box("sonm", data_atom(1, b"sort name")),
               box("tvsh", data_atom(1, b"Show"), box("name", u32(0), b"ignored")),
               box("----", full("mean", 0, 0, b"com.apple.iTunes"), full("name", 0, 0, b"iTunNORM"), data_atom(1, b" 00000A1B 00000A1C")),
               box("----", full("mean", 0, 0, b"org.example"), full("name", 0, 0, b"binary"), data_atom(0, filler(9, 300))),
               box("desc", data_atom(2, "UTF-16 text".encode("utf-16-be"))),
               box("xxxx", data_atom(3, b"\x83\x65\x83\x58\x83\x67")),
               box("zzzz", data_atom(1, b"")),
               box("yyyy", filler(10, 12)))
    meta = full("meta", 0, 0,
                full("hdlr", 0, 0, u32(0), b"mdir", b"appl", u32(0), u32(0), b"\0"),
                ilst,
                box("free", b"\0" * 32))
    udta = box("udta",
               meta,
               full("titl", 0, 0, u16(0x15C7), b"3GPP title\0"),
               full("dscp", 0, 0, u16(0x15C7), "3GPP description é".encode("utf-8") + b"\0"),
               full("auth", 0, 0, u16(0x55C4), b"\xfe\xff" + "UTF-16 author".encode("utf-16-be") + b"\0\0"),
               full("cprt", 0, 0, u16(0x55C4), b"(c) nobody\0"),
               full("yrrc", 0, 0, u16(2021)),
               box("dcfD", full("dcfM", 0, 0, b"dcf metadata")),
               box(b"\xa9xyz", u16(17), u16(0x15C7), b"+48.8577+002.2950/"),
               box("Xtra", filler(11, 64)))
    tracks = [
        Track(1, "soun", [audio_entry("mp4a", esds(0x40, 5, bytes([0x12, 0x10])), sample_rate=44100)], raw_samples(40, 6), media_header=SMHD,
              timescale=44100, duration=1024,
              trak_extra_after=[box("udta", box("name", b"track name"), full("meta", 0, 0, box("ilst", box(b"\xa9nam", data_atom(1, b"Track Title")))))]),
    ]
    uuid_box = box("uuid", bytes(range(16)), b"custom uuid payload")
    return movie(ftyp("M4A ", 0, ("M4A ", "mp42", "isom")), tracks, moov_extra=[udta],
                 top_extra=[uuid_box, box("skip", b"skipped"), box("junk")])


def make_large_and_open():
    tracks = [Track(1, "soun", [audio_entry("mp4a", esds(0x40, 5, bytes([0x12, 0x10])))], raw_samples(50, 5), media_header=SMHD, co64=True)]
    data = movie(ftyp(), tracks, mdat64=True, mvhd_version=1)
    # a last atom with size 0: extends to the end of the file
    data += u32(0) + b"free" + b"trailing data up to the end of the file"
    return data


def make_mdat_first():
    tracks = [Track(1, "vide", [visual_entry("avc1", avcc())], avc_samples(4), media_header=VMHD, width=64, height=64)]
    data = movie(ftyp("qt  ", 0x20050300, ("qt  ",)), tracks)
    # move the mdat before the moov and fix up the chunk offsets
    ftyp_size = struct.unpack(">I", data[:4])[0]
    moov_size = struct.unpack(">I", data[ftyp_size:ftyp_size + 4])[0]
    moov = bytearray(data[ftyp_size:ftyp_size + moov_size])
    mdat = data[ftyp_size + moov_size:]
    position = moov.find(b"stco")
    count = struct.unpack(">I", moov[position + 8:position + 12])[0]
    for i in range(count):
        at = position + 12 + 4 * i
        moov[at:at + 4] = u32(struct.unpack(">I", moov[at:at + 4])[0] - moov_size)
    return data[:ftyp_size] + mdat + bytes(moov)


def make_fragmented():
    aac = audio_entry("mp4a", esds(0x40, 5, bytes([0x12, 0x10])))
    video = Track(1, "vide", [visual_entry("avc1", avcc())], [], media_header=VMHD, width=640, height=360, timescale=30000,
                  stbl_extra=[full("sgpd", 1, 0, b"roll", u32(2), u32(1), u16(0xFFFF)),
                              full("sbgp", 0, 0, b"roll", u32(1), u32(5), u32(1))])
    audio = Track(2, "soun", [aac], [], media_header=SMHD, timescale=48000)
    mvex = box("mvex",
               full("mehd", 1, 0, u64(123456789012)),
               full("trex", 0, 0, u32(1), u32(1), u32(1001), u32(100), u32(0x01010000)),
               full("trex", 0, 0, u32(2), u32(1), u32(1024), u32(0), u32(0)),
               full("leva", 0, 0, u8(1), u32(1), u8(0x80), u32(0)))
    pssh0 = full("pssh", 0, 0, bytes.fromhex("edef8ba979d64acea3c827dcd51d21ed"), u32(10), filler(60, 10))
    pssh1 = full("pssh", 1, 0, bytes.fromhex("1077efecc0b24d02ace33c1e52e2fb4b"), u32(2), bytes(range(16)), bytes(range(16, 32)), u32(0))
    pssh_pr = full("pssh", 0, 0, bytes.fromhex("9a04f07998404286ab92e65be0885f95"), u32(4), b"\x04\x00\x00\x00")
    head = movie(ftyp("iso5", 1, ("iso5", "iso6", "dash", "cmfc")), [video, audio], moov_extra=[mvex, pssh0, pssh1, pssh_pr], movie_duration=0)
    head = head[:-8]    # drop the empty mdat

    out = bytearray(head)
    out += box("styp", b"msdh", u32(0), b"msdh", b"msix")
    sidx = full("sidx", 1, 0, u32(1), u32(30000), u64(0), u64(0), u16(0), u16(3),
                u32(1000), u32(30030), u32(0x90000000),
                u32(0x80000000 | 2000), u32(30030), u32(0x10000001),
                u32(3000), u32(15015), u32(0xA0001234))
    out += sidx
    out += full("sidx", 0, 0, u32(2), u32(48000), u32(4096), u32(100), u16(0), u16(1), u32(4000), u32(96000), u32(0x90000000))
    out += full("emsg", 0, 0, b"urn:example:scheme\0", b"1\0", u32(1000), u32(0), u32(500), u32(1), b"payload")
    out += full("prft", 1, 0, u32(1), u64(0x0123456789ABCDEF), u64(1000))

    moof_offsets = []

    def fragment(sequence, video_samples, audio_samples, variant):
        moof_offsets.append(len(out))
        video_data = b"".join(video_samples)
        audio_data = b"".join(audio_samples)

        def build(video_offset, audio_offset):
            trafs = []
            if video_samples:
                if variant == 0:
                    # everything explicit
                    tfhd = full("tfhd", 0, 0x02003B, u32(1), u64(moof_offsets[-1]), u32(1), u32(1001), u32(50), u32(0x01010000))
                    tfdt = full("tfdt", 1, 0, u64(1001 * 10 * (sequence - 1)))
                    entries = b"".join(u32(1001) + u32(len(s)) + u32(0x02000000 if i == 0 else 0x01010000) + u32(1001 * (i % 3))
                                       for i, s in enumerate(video_samples))
                    trun = full("trun", 0, 0xF01, u32(len(video_samples)), u32(video_offset), entries)
                    extra = [full("saiz", 0, 1, b"cenc", u32(0), u8(8), u32(len(video_samples))),
                             full("saio", 0, 1, b"cenc", u32(0), u32(1), u32(1234)),
                             full("sbgp", 1, 0, b"seig", u32(7), u32(2), u32(2), u32(0x10001), u32(len(video_samples) - 2), u32(0)),
                             full("sgpd", 1, 0, b"seig", u32(20), u32(1), u8(0), u8(0), u8(1), u8(8), bytes(range(16))),
                             full("sgpd", 2, 0, b"seig", u32(1), u32(29), u32(1), u8(0), u8(0x15), u8(1), u8(0), bytes(range(16)), u8(8), bytes(range(8)))]
                    extra_after = []
                elif variant == 1:
                    # defaults from trex, default-base-is-moof, first-sample-flags, negative composition offsets
                    tfhd = full("tfhd", 0, 0x020000, u32(1))
                    tfdt = full("tfdt", 0, 0, u32(1001 * 10 * (sequence - 1)))
                    entries = b"".join(u32(len(s)) + u32(0xFFFFFC17 if i == 1 else 2002) for i, s in enumerate(video_samples))
                    trun = full("trun", 1, 0xA05, u32(len(video_samples)), u32(video_offset), u32(0x02000000), entries)
                    extra = [full("saiz", 0, 0, u8(0), u32(2), u8(8), u8(16)), full("saio", 1, 0, u32(2), u64(1), u64(2))]
                    extra_after = [full("trun", 0, 0, u32(0))]
                else:
                    # duration-is-empty, several truns
                    tfhd = full("tfhd", 0, 0x010038, u32(1), u32(500), u32(len(video_samples[0])), u32(0))
                    tfdt = None
                    half = len(video_samples) // 2
                    first = sum(len(s) for s in video_samples[:half])
                    trun = full("trun", 0, 0x001, u32(half), u32(video_offset))
                    extra = []
                    extra_after = [full("trun", 0, 0x201, u32(len(video_samples) - half), u32(video_offset + first),
                                        *[u32(len(s)) for s in video_samples[half:]])]
                trafs.append(box("traf", tfhd, *([tfdt] if tfdt else []), *extra, trun, *extra_after))
            if audio_samples:
                tfhd = full("tfhd", 0, 0x020022, u32(2), u32(1), u32(0x02000000))
                tfdt = full("tfdt", 1, 0, u64(1024 * 20 * (sequence - 1)))
                trun = full("trun", 0, 0x301, u32(len(audio_samples)), u32(audio_offset),
                            *[u32(1024) + u32(len(s)) for s in audio_samples])
                trafs.append(box("traf", tfhd, tfdt, trun))
            return box("moof", full("mfhd", 0, 0, u32(sequence)), *trafs)

        size = len(build(0, 0))
        if variant == 0:
            # data_offset relative to the explicit base_data_offset (= moof position)
            moof = build(size + 8, size + 8 + len(video_data))
        else:
            moof = build(size + 8, size + 8 + len(video_data))
        out.extend(moof)
        out.extend(box("mdat", video_data, audio_data))

    fragment(1, avc_samples(6), raw_samples(70, 5), 0)
    fragment(2, avc_samples(5), raw_samples(71, 4), 1)
    same_size = [s[:20] for s in avc_samples(4)]
    fragment(3, [u32(16) + s[4:20] for s in same_size], [], 2)
    fragment(4, [], raw_samples(72, 3), 0)

    mfra_children = [
        full("tfra", 1, 0, u32(1), u32(0x3F), u32(2),
             u64(0), u64(moof_offsets[0]), u32(1), u32(1), u32(1),
             u64(10010), u64(moof_offsets[1]), u32(1), u32(1), u32(1)),
        full("tfra", 0, 0, u32(2), u32(0), u32(2),
             u32(0), u32(moof_offsets[0]), u8(2), u8(1), u8(1),
             u32(20480), u32(moof_offsets[1]), u8(2), u8(1), u8(1)),
        full("tfra", 0, 0, u32(3), u32(0x1B), u32(1), u32(0), u32(moof_offsets[2]), u16(1), u24(1), u32(1)),
    ]
    mfra_size = 8 + sum(len(c) for c in mfra_children) + 16
    out += box("mfra", *mfra_children, full("mfro", 0, 0, u32(mfra_size)))
    return bytes(out)


def make_odd_structures():
    # unusual structures: containers nested in unknown places, empty tables,
    # atoms that upstream does not write back as it read them (no round-trip)
    aac = audio_entry("mp4a", esds(0x40, 5, bytes([0x12, 0x10])))
    stbl_extra = [
        full("sbgp", 0, 0, b"rap ", u32(0)),
        full("sgpd", 0, 0, b"rap ", u32(2), u8(0x80), u8(0x12)),
        full("sgpd", 1, 0, b"sync", u32(0), u32(2), u32(1), u8(5), u32(2), u16(7)),
        full("sdtp", 0, 0, bytes([0x20, 0x10, 0x18, 0x24])),
        full("stsh", 0, 0, u32(0)),
        full("cslg", 0, 0, u32(0) * 5),
        full("padb", 0, 0, u32(0)),
        box("stdp", u32(0)),
    ]
    tracks = [
        Track(1, "soun", [aac], raw_samples(80, 4), media_header=SMHD, stbl_extra=stbl_extra,
              trak_extra=[box("edts"), box("tapt", full("clef", 0, 0, u32(0x10000), u32(0x10000))), box("load", u32(0) * 4)],
              trak_extra_after=[full("meta", 0, 0, full("hdlr", 0, 0, u32(0), b"ID32", u32(0) * 3, b"\0"), full("ID32", 0, 0, u16(0x15C7), b"ID3" + filler(81, 20)))]),
        Track(2, "vide", [visual_entry("s263", box("d263", b"FFMP", u8(0), u8(10), u8(0))), visual_entry("xvid")],
              raw_samples(82, 2), media_header=VMHD, width=176, height=144),
        Track(3, "soun", [audio_entry("samr", box("damr", b"FFMP", u8(0), u16(0x81FF), u8(0), u8(1))),
                          audio_entry("sowt"), audio_entry("twos", version=1)], raw_samples(83, 2), media_header=SMHD, stsd_count=5),
        Track(4, "vide", [visual_entry("avc1", avcc())], raw_samples(84, 2), media_header=VMHD, stsd_count=3),
    ]
    moov_extra = [
        box("udta", box("meta", box("ilst")), box("free"), box("udta", box("name", b"nested"))),
        box("mvex"),
        box("trak"),
        full("meta", 0, 0, full("hdlr", 0, 0, u32(0), b"mdta", u32(0) * 3, b"\0"),
             full("keys", 0, 0, u32(1), u32(8 + 20), b"mdta", b"com.example.key.name"),
             box("ilst", box(u32(1), data_atom(1, b"keyed value")))),
        full("iods", 0, 0, descriptor(0x10, u16(0x004F | 0x20) + b"http://example.com/od")),
        box("ainf", u32(0), b"AAAA", u32(0x01000000), b"urn:dece:apid:org:example:1234\0", filler(90, 8)),
        full("bloc", 0, 0, b"http://example.com/base" + b"\0" * 233, b"http://example.com/purchase" + b"\0" * 229, b"\0" * 512),
        full("8bdl", 0, 0, u32(0x786D6C20), u32(1), b"<bundle/>"),
        box("8id ", b"urn:marlin:organization:example:0001\0"),
        box("mdri", box("free")),
        box("marl", u32(0)),
        box("cmov", box("dcom", b"zlib"), box("cmvd", u32(100), filler(91, 16))),
    ]
    return movie(ftyp("3gp5", 0x100, ("3gp5", "3gp4", "isom")), tracks, moov_extra=moov_extra,
                 top_extra=[box("moof"), box("mfra", full("mfro", 0, 0, u32(24))), full("sidx", 0, 0, u32(1), u32(1000), u32(0), u32(0), u16(0), u16(0))])


def make_protected_entries():
    # protected sample entries without / with protection info
    def sinf(original, scheme, *schi):
        return box("sinf", box("frma", original.encode("latin-1")), full("schm", 0, 0, scheme.encode("latin-1"), u32(0x10000)), box("schi", *schi))
    tenc = full("tenc", 0, 0, u16(0), u8(1), u8(8), bytes(range(16)))
    tracks = [
        Track(1, "vide", [visual_entry("encv"), visual_entry("drmi"), visual_entry("encv", avcc(), sinf("avc1", "cenc", tenc))],
              raw_samples(140, 2), media_header=VMHD, width=176, height=144),
        Track(2, "soun", [audio_entry("enca"), audio_entry("drms"),
                          audio_entry("enca", esds(0x40, 5, bytes([0x12, 0x10])), sinf("mp4a", "cbcs", tenc))], raw_samples(141, 2), media_header=SMHD),
        Track(3, "soun", [audio_entry("enca", esds(0x40, 5, bytes([0x12, 0x10])), sinf("mp4a", "cenc", tenc))], raw_samples(142, 2), media_header=SMHD),
    ]
    return movie(ftyp(), tracks)


def make_config_edge_cases():
    # codec configuration atoms with short, empty or unusual payloads
    # (upstream normalizes several of them when writing: this file does not round-trip)
    def v(kind, *children): return visual_entry(kind, *children, width=32, height=32)
    entries_video = [
        v("avc1", box("avcC")),
        v("avc1", box("avcC", u8(1), u8(100), u8(0), u8(40), u8(0xFF))),
        v("avc1", box("avcC", u8(1), u8(244), u8(0), u8(51), u8(0xFF), u8(0xE2), u16(3), b"\x67\x01\x02", u16(0), u8(1), u16(1), b"\x68")),
        v("avc1", avcc(profile=110), avcc(profile=77, high_ext=False)),
        v("dva1", avcc(), dvcc("dvcC", 9, 3, 2)),
        v("hvc1", box("hvcC", u8(1), u8(0x83), u32(0xFFFFFFFF), filler(100, 6), u8(186), filler(101, 9), u8(0))),
        v("hev1", hvcc(arrays=())),
        v("av01", box("av1C")),
        v("av01", box("av1C", u8(0x81), u8(0x1F), u8(0xFF), u8(0x1F))),
        v("av01", av1c(profile=0, level=0, tier=0, obus=b"", delay=0x13)),
        v("vp09", box("vpcC")),
        v("vp09", full("vpcC", 0, 0, u8(0), u8(10), u8(0x80), u8(1), u16(2), b"\xAA\xBB")),
        v("vp10", full("vpcC", 1, 0, u8(3), u8(62), u8(0xC5), u8(1), u8(1), u8(1), u16(4), b"\x01\x02\x03\x04")),
        v("dvh1", hvcc(), box("dvcC")),
        v("dvav", avcc(), dvcc("dvcC", 0, 1, 0)),
        v("dvhe", hvcc(), dvcc("dvcC", 127, 63, 15)),
        v("avc1", avcc(), dvcc("dvvC", 4, 5, 2), colr("nclx"), colr("nclc")),
        v("hev1", hvcc(), dvcc("dvcC", 7, 5, 6)),
        v("avc2", avcc()), v("avc4", avcc()),
        v("mp4v", esds(0x21, 4, b"")), v("mp4v", full("esds", 0, 0)), v("mp4v", full("esds", 0, 0, descriptor(3, b"")))
    ]
    entries_audio = [
        audio_entry("ac-3", box("dac3")),
        audio_entry("ac-3", box("dac3", b"\x50")),
        audio_entry("ac-3", box("dac3", b"\xFF\xFF\xFF\xFF")),
        audio_entry("ec-3", box("dec3")),
        audio_entry("ec-3", box("dec3", b"\x07")),
        audio_entry("ec-3", box("dec3", b"\xFF\xFF")),
        audio_entry("ec-3", box("dec3", Bits().put(8191, 13).put(7, 3).bytes() + filler(110, 5))),
        audio_entry("ec-3", box("dec3", Bits().put(100, 13).put(2, 3).put(0x20F1FE, 24).put(0x400E00, 24).put(0x20F1E1, 24).put(0x1FF, 9).bytes() + b"\x01\x05")),
        audio_entry("mlpa", box("dmlp")),
        audio_entry("mlpa", dmlp(30000)),
        audio_entry("mlpa", box("dmlp", b"\x01\x02\x03")),
        audio_entry("mp4a", esds(0x40, 5, b"")),
        audio_entry("mp4a", esds(0x40, 5, b"\xF8")),
        audio_entry("mp4a", esds(0x40, 5, bytes([0xF9, 0x66, 0x00, 0x00]))),
        audio_entry("mp4a", esds(0x40, 5, bytes([0x17, 0x80, 0x00, 0xBB, 0x80, 0x10]))),
        audio_entry("mp4a", esds(0x40, 5, bytes([0xEB, 0x8A, 0x08, 0x00]))),
        audio_entry("mp4a", esds(0x40, 5, bytes([0x12, 0x08, 0x56, 0xE5, 0x00]))),
        audio_entry("mp4a", esds(0x40, 5, bytes([0x13, 0x90, 0x56, 0xE5, 0xA5, 0x48, 0x80]))),
        audio_entry("mp4a", esds(0x40, 5, bytes([0x07, 0x80 | 0x10, 0x00]))),
        audio_entry("mp4a", esds(0x69, 5, None)), audio_entry("mp4a", esds(0xA5, 5, None)), audio_entry("mp4a", esds(0xDD, 5, b"\x01")),
        audio_entry("mp4a", esds(0x66, 5, bytes([0x0A, 0x10]))), audio_entry("mp4a", esds(0x68, 6, bytes([0x1A, 0x10]))),
        audio_entry("mp4a"),
        audio_entry("mp4a", box("esds")),
    ]
    tracks = [
        Track(1, "vide", entries_video, raw_samples(120, 2), media_header=VMHD, width=32, height=32),
        Track(2, "soun", entries_audio, raw_samples(121, 2), media_header=SMHD),
    ]
    return movie(ftyp(), tracks)


def make_ac4():
    # AC-4 sample entries: a v1 DSI built field by field, a v0 DSI and short payloads
    def dsi_v1(presentation_version, channel_coded, multi_pid, language, atmos):
        b = Bits()
        b.put(1, 3)                 # ac4_dsi_version
        b.put(2, 7)                 # bitstream_version
        b.put(1, 1)                 # fs_index
        b.put(3, 4)                 # frame_rate_index
        b.put(1, 9)                 # n_presentations
        b.put(1, 1)                 # b_program_id
        b.put(0x1234, 16)           # short_program_id
        b.put(0, 1)                 # b_uuid
        # ac4_bitrate_dsi
        b.put(2, 2).put(256000, 32).put(0, 32)
        while len(b.bits) % 8:
            b.put(0, 1)
        p = Bits()
        p.put(1 if presentation_version == 2 else 0, 5)     # presentation_config_v1 (single substream group / 1)
        if presentation_version == 2:
            pass
        p.put(3, 3)                 # mdcompat
        p.put(1, 1)                 # b_presentation_id
        p.put(5, 5)                 # presentation_id
        p.put(0, 2)                 # dsi_frame_rate_multiply_info
        p.put(0, 2)                 # dsi_frame_rate_fraction_info
        p.put(0, 5)                 # presentation_emdf_version
        p.put(0, 10)                # presentation_key_id
        p.put(channel_coded, 1)     # b_presentation_channel_coded
        if channel_coded:
            p.put(4, 5)             # dsi_presentation_ch_mode
            p.put(0x47, 24)         # presentation_channel_mask_v1
        p.put(0, 1)                 # b_presentation_core_differs
        p.put(0, 1)                 # b_presentation_filter
        p.put(multi_pid, 1)         # b_multi_pid
        # one substream group
        for _ in range(2 if presentation_version == 2 else 1):
            p.put(1, 1)             # b_substreams_present
            p.put(0, 1)             # b_hsf_ext
            p.put(channel_coded, 1) # b_channel_coded
            p.put(1, 8)             # n_substreams
            p.put(0, 2)             # dsi_sf_multiplier
            p.put(0, 1)             # b_substream_bitrate_indicator
            if channel_coded:
                p.put(0x47, 24)     # dsi_substream_channel_mask
            else:
                p.put(1, 1).put(0, 1).put(1, 1).put(0, 1).put(1, 4)
            p.put(1, 1)             # b_content_type
            p.put(0, 3)             # content_classifier
            p.put(1 if language else 0, 1)
            if language:
                p.put(len(language), 6)
                for c in language:
                    p.put(c, 8)
        p.put(0, 1)                 # b_pre_virtualized
        p.put(0, 1)                 # b_add_emdf_substreams
        p.put(0, 1)                 # b_presentation_bitrate_info
        p.put(0, 1)                 # b_alternative
        while len(p.bits) % 8:
            p.put(0, 1)
        p.put(1, 1)                 # de_indicator
        p.put(atmos, 1)             # dolby_atmos_indicator
        p.put(0, 4).put(0, 1)       # reserved, b_extended_presentation_id
        while len(p.bits) % 8:
            p.put(0, 1)
        body = p.bytes()
        b.put(presentation_version, 8)
        b.put(len(body), 8)
        return b.bytes() + body

    entries = [
        audio_entry("ac-4", box("dac4", dsi_v1(1, 1, 0, b"en", 0))),
        audio_entry("ac-4", box("dac4", dsi_v1(1, 0, 1, b"fr-CA", 1)), channels=6),
        audio_entry("ac-4", box("dac4", dsi_v1(2, 1, 0, b"", 1))),
        audio_entry("ac-4", box("dac4", Bits().put(0, 3).put(1, 7).put(1, 1).put(3, 4).put(1, 9).bytes() + filler(130, 12))),
        audio_entry("ac-4", box("dac4")),
        audio_entry("ac-4", box("dac4", b"\x20")),
        audio_entry("ac-4", box("dac4", b"\x20\xA4\x01\x40")),
        audio_entry("ac-4", box("dac4", b"\xE0\x00")),
        audio_entry("ac-4"),
    ]
    tracks = [Track(1, "soun", entries, raw_samples(131, 2), media_header=SMHD)]
    return movie(ftyp("mp42", 0, ("dby1", "isom")), tracks)


def main():
    if len(sys.argv) != 2:
        sys.stderr.write("usage: make-synthetic-inputs.py <output-dir>\n")
        return 1
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    codecs = make_codecs()
    fragmented = make_fragmented()
    files = {
        "syn-codecs.mp4": codecs,
        "syn-metadata.mp4": make_metadata(),
        "syn-large-open.mp4": make_large_and_open(),
        "syn-mdat-first.mp4": make_mdat_first(),
        "syn-fragmented.mp4": fragmented,
        "syn-odd.mp4": make_odd_structures(),
        "syn-configs.mp4": make_config_edge_cases(),
        "syn-ac4.mp4": make_ac4(),
        # protected sample entries
        "enc-syn-entries.mp4": make_protected_entries(),
        # truncated files
        "syn-trunc-moov.mp4": codecs[:len(codecs) * 2 // 5],
        "syn-trunc-stsd.mp4": codecs[:codecs.find(b"avcC") + 10],
        "syn-trunc-mdat.mp4": codecs[:len(codecs) - 300],
        "syn-trunc-frag.mp4": fragmented[:len(fragmented) - 150],
        "syn-trunc-header.mp4": codecs[:36 + 5],
        "syn-empty.mp4": b"",
        "syn-ftyp-only.mp4": ftyp(),
        "syn-not-mp4.mp4": b"This is not an MP4 file at all, just some text.\n" * 3,
    }
    for name, data in files.items():
        with open(os.path.join(out, name), "wb") as f:
            f.write(data)
    return 0


if __name__ == "__main__":
    sys.exit(main())
