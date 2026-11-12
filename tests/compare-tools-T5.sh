#!/bin/bash
# Compares the ported command-line tools of group T5 (mp42aac, mp42avc,
# mp42hevc, avcinfo, hevcinfo, mp4audioclip, mp4diff, mp4rtphintinfo) with the
# original Bento4 tools: both are run with identical arguments, and the
# standard output, the standard error, the exit code and every output file
# are compared byte for byte.
#
# usage: tests/compare-tools-T5.sh [<reference-dir> [<test-data-dir>]]
#
#   <reference-dir>  (or $BENTO4_REFERENCE_DIR) directory with
#                      bin/<tool>.js  the original tools built with emscripten, run with node
#                                     (the T5 tools, and mp4fragment, mp4encrypt to generate inputs)
#   <test-data-dir>  (or $BENTO4_TEST_DATA_DIR) the Test/Data directory of the Bento4 sources (*.mp4)
#
# other environment variables:
#   BENTO4SHARP_ARTIFACTS   where to build (default: a temporary directory, removed on exit)
#   BENTO4SHARP_WORK_DIR    where to put generated inputs and outputs (default: a temporary directory)
#   BENTO4SHARP_SKIP_BUILD  set to 1 to reuse the binaries already in $BENTO4SHARP_ARTIFACTS
#   BENTO4SHARP_FRAMEWORK   target framework of the port to build and run (default: net8.0; net20 runs
#                           the .NET Framework 2.0 executables)
#   BENTO4SHARP_JOBS        number of cases run at the same time (default: 4)
#   BENTO4SHARP_T5_ONLY     only run the cases whose name matches this extended regular expression
#
# Synthetic inputs (HEVC tracks, odd AVC/AAC tracks, RTP hint tracks) need
# python 3 (tests/make-synthetic-inputs.py, tests/make-synthetic-inputs-T5.py);
# without it the cases that use them are skipped.
#
# exit code: 0 if every case passes, 1 otherwise.

REF="${1:-$BENTO4_REFERENCE_DIR}"
DATA="${2:-$BENTO4_TEST_DATA_DIR}"
if [ -z "$REF" ] || [ -z "$DATA" ]; then
    sed -n "2,28p" "$0" >&2
    exit 2
fi
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if ! command -v node > /dev/null || [ ! -f "$REF/bin/mp42aac.js" ]; then
    echo "the original tools ($REF/bin/*.js) and node are required" >&2
    exit 2
fi

CLEANUP=()
cleanup() { for d in "${CLEANUP[@]}"; do rm -rf "$d"; done; }
trap cleanup EXIT

ART="$BENTO4SHARP_ARTIFACTS"
if [ -z "$ART" ]; then ART="$(mktemp -d)"; CLEANUP+=("$ART"); fi
WORK="$BENTO4SHARP_WORK_DIR"
if [ -z "$WORK" ]; then WORK="$(mktemp -d)"; CLEANUP+=("$WORK"); fi
WORK="$WORK/t5"
rm -rf "$WORK"
# the original and the ported tools run in two directories with the same content
RDIR="$WORK/ref"
PDIR="$WORK/port"
RES="$WORK/res"
mkdir -p "$RDIR/out" "$RES"

#----------------------------------------------------------------------
# target framework of the port
#----------------------------------------------------------------------
FW="${BENTO4SHARP_FRAMEWORK:-net8.0}"

# port <tool>.dll [<argument>...]: runs a tool of the port. The builds for
# .NET Framework (net20...) are executables (<tool>.exe); the others are
# started with the dotnet host.
port() {
    case "$FW" in
        *.*) dotnet "$@" ;;
        *)   local exe="${1%.dll}.exe"
             shift
             "$exe" "$@" ;;
    esac
}

#----------------------------------------------------------------------
# build
#----------------------------------------------------------------------
PROJECTS="Mp42Aac Mp42Avc Mp42Hevc AvcInfo HevcInfo Mp4AudioClip Mp4Diff Mp4RtpHintInfo"
if [ "$BENTO4SHARP_SKIP_BUILD" != "1" ]; then
    for project in $PROJECTS; do
        if ! dotnet build "$ROOT/src/Tools/$project/$project.csproj" -c Release -f "$FW" -nologo -v q -nodeReuse:false --artifacts-path "$ART" > "$WORK/build.log" 2>&1; then
            cat "$WORK/build.log"
            echo "BUILD FAILED: $project"
            exit 1
        fi
    done
fi

# port_dll <tool>: sets $DLL to the path of the ported tool
port_dll() {
    case "$1" in
        mp42aac)        DLL="$ART/bin/Mp42Aac/release_$FW/mp42aac.dll" ;;
        mp42avc)        DLL="$ART/bin/Mp42Avc/release_$FW/mp42avc.dll" ;;
        mp42hevc)       DLL="$ART/bin/Mp42Hevc/release_$FW/mp42hevc.dll" ;;
        avcinfo)        DLL="$ART/bin/AvcInfo/release_$FW/avcinfo.dll" ;;
        hevcinfo)       DLL="$ART/bin/HevcInfo/release_$FW/hevcinfo.dll" ;;
        mp4audioclip)   DLL="$ART/bin/Mp4AudioClip/release_$FW/mp4audioclip.dll" ;;
        mp4diff)        DLL="$ART/bin/Mp4Diff/release_$FW/mp4diff.dll" ;;
        mp4rtphintinfo) DLL="$ART/bin/Mp4RtpHintInfo/release_$FW/mp4rtphintinfo.dll" ;;
    esac
}

#----------------------------------------------------------------------
# helpers
#----------------------------------------------------------------------
PASSED=0; FAILED=0; SKIPPED=0
FAILURES=()

# The cases run $JOBS at a time, in the background (every case has its own
# output files); each one leaves its result line in $RES/<case>.result and
# flush prints the results in the order the cases were started.
JOBS="${BENTO4SHARP_JOBS:-4}"
PENDING=()
RUNNING=0

flush() {
    local name line
    wait
    for name in "${PENDING[@]}"; do
        line="FAIL  $name   (no result)"
        [ -f "$RES/$name.result" ] && read -r line < "$RES/$name.result"
        echo "$line"
        case "$line" in
            PASS*) PASSED=$((PASSED+1)) ;;
            *)     FAILED=$((FAILED+1)); FAILURES+=("$name") ;;
        esac
    done
    PENDING=()
    RUNNING=0
}

# launch <case> <function> <arguments>...
launch() {
    local name="$1"
    shift
    PENDING+=("$name")
    ( "$@" ) &
    RUNNING=$((RUNNING+1))
    if [ $RUNNING -ge $JOBS ]; then wait; RUNNING=0; fi
}

fail() { flush; echo "FAIL  $1   ($2)"; FAILED=$((FAILED+1)); FAILURES+=("$1"); }
skip() { flush; echo "SKIP  $1   ($2)"; SKIPPED=$((SKIPPED+1)); }

selected() { [ -z "$BENTO4SHARP_T5_ONLY" ] || [[ "$1" =~ $BENTO4SHARP_T5_ONLY ]]; }

# run_both <case> <tool> <arguments>...
# runs the original tool in $RDIR and the port in $PDIR; stdout, stderr and
# exit code go to $RES/<case>.{ref,port}.{out,err,rc}
run_both() {
    local name="$1" tool="$2"
    shift 2
    port_dll "$tool"
    (cd "$RDIR" && node "$REF/bin/$tool.js" "$@" > "$RES/$name.ref.out" 2> "$RES/$name.ref.err"; echo $? > "$RES/$name.ref.rc") &
    (cd "$PDIR" && port "$DLL" "$@" > "$RES/$name.port.out" 2> "$RES/$name.port.err"; echo $? > "$RES/$name.port.rc")
    wait
}

# differences <case> <output files>: sets $DIFFS to the list of what differs
differences() {
    local name="$1" f rc1="?" rc2="?"
    DIFFS=""
    cmp -s "$RES/$name.ref.out" "$RES/$name.port.out" || DIFFS="$DIFFS stdout"
    cmp -s "$RES/$name.ref.err" "$RES/$name.port.err" || DIFFS="$DIFFS stderr"
    read -r rc1 < "$RES/$name.ref.rc"
    read -r rc2 < "$RES/$name.port.rc"
    [ "$rc1" = "$rc2" ] || DIFFS="$DIFFS exit-code($rc1/$rc2)"
    for f in $2; do
        if [ -e "$RDIR/$f" ] && [ -e "$PDIR/$f" ]; then
            cmp -s "$RDIR/$f" "$PDIR/$f" || DIFFS="$DIFFS $f"
        elif [ -e "$RDIR/$f" ] || [ -e "$PDIR/$f" ]; then
            DIFFS="$DIFFS $f(exists-in-one-only)"
        fi
    done
}

# result <case>: writes the result line of the case, from $DIFFS
result() {
    if [ -z "$DIFFS" ]; then
        echo "PASS  $1" > "$RES/$1.result"
    else
        echo "FAIL  $1   (differs:$DIFFS)" > "$RES/$1.result"
    fi
}

# t5_case <mode> <case> <tool> <output files> <arguments>...
t5_case() {
    local mode="$1" name="$2" tool="$3" outputs="$4"
    shift 4
    run_both "$name" "$tool" "$@"
    [ "$mode" = crash ] && sed -i -e '/^exception thrown: /,$d' "$RES/$name.ref.err"
    differences "$name" "$outputs"
    result "$name"
}

# t5 <case> <tool> <output files (relative, space separated)> <arguments>...
t5() {
    selected "$1" || return 0
    launch "$1" t5_case normal "$@"
}

# t5_crash: same as t5 for cases where the original tool crashes after its
# error message (it dereferences a NULL stream): the port stops instead, with
# the same exit code. What the wasm runtime prints about the crash is ignored.
t5_crash() {
    selected "$1" || return 0
    launch "$1" t5_case crash "$@"
}

# t5_rtp <case> <input> <track id>
# mp4rtphintinfo: the RTP sequence numbers and timestamps start at random
# values (srand(time)), in the original tool as in the port. The packets
# written to <input>-track-<id>.rtp are compared after subtracting the
# sequence number and timestamp of the first packet from all the packets
# (the packet sizes are those printed on the standard output).
t5_rtp() {
    selected "$1" || return 0
    launch "$1" t5_rtp_case "$@"
}

t5_rtp_case() {
    local name="$1" input="$2" id="$3" rtp="$2-track-$3.rtp" side
    rm -f "$RDIR/$rtp" "$PDIR/$rtp"
    run_both "$name" mp4rtphintinfo --trackid "$id" "$input"
    differences "$name" ""
    if [ -e "$RDIR/$rtp" ] || [ -e "$PDIR/$rtp" ]; then
        for side in ref port; do
            local dir="$RDIR"; [ $side = port ] && dir="$PDIR"
            "$PYTHON" -c '
import re, sys
sizes = [int(x) for x in re.findall(rb"packet contains (-?\d+) bytes", open(sys.argv[1], "rb").read())]
try:
    data = open(sys.argv[2], "rb").read()
except IOError:
    sys.stdout.write("no rtp file\n"); sys.exit(0)
out = []
pos = 0
seq0 = ts0 = None
for size in sizes:
    packet = data[pos:pos + size]
    pos += size
    if len(packet) < 12:
        out.append("short packet " + packet.hex()); continue
    seq = int.from_bytes(packet[2:4], "big"); ts = int.from_bytes(packet[4:8], "big")
    if seq0 is None: seq0, ts0 = seq, ts
    out.append("%s seq+%d ts+%d %s" % (packet[:2].hex(), (seq - seq0) & 0xFFFF, (ts - ts0) & 0xFFFFFFFF, packet[8:].hex()))
out.append("trailing %s" % data[pos:].hex())
sys.stdout.write("\n".join(out) + "\n")
' "$RES/$name.$side.out" "$dir/$rtp" > "$RES/$name.$side.rtp.txt" 2>&1
        done
        cmp -s "$RES/$name.ref.rtp.txt" "$RES/$name.port.rtp.txt" || DIFFS="$DIFFS rtp-packets"
    fi
    result "$name"
}

#----------------------------------------------------------------------
# inputs (generated with the original tools, then copied for the port)
#----------------------------------------------------------------------
echo "=== generating inputs ==="
cp "$DATA"/*.mp4 "$RDIR/"
cd "$RDIR" || exit 1
: > "$WORK/gen.log"
ref() {
    local tool="$1"; shift
    node "$REF/bin/$tool.js" "$@" >> "$WORK/gen.log" 2>&1 || echo "generation failed: $tool $*" >> "$WORK/gen.log"
}
K1=000102030405060708090a0b0c0d0e0f
K2=101112131415161718191a1b1c1d1e1f
KEY1=1:$K1:0001020304050607
KEY2=2:$K2:1011121314151617

PYTHON="$(command -v python3 || command -v python)"
if [ -n "$PYTHON" ] && "$PYTHON" -c "import sys; sys.exit(0 if sys.version_info[0] >= 3 else 1)" 2> /dev/null; then
    "$PYTHON" "$ROOT/tests/make-synthetic-inputs.py" . >> "$WORK/gen.log" 2>&1 || echo "generation failed: synthetic inputs" >> "$WORK/gen.log"
    "$PYTHON" "$ROOT/tests/make-synthetic-inputs-T5.py" . >> "$WORK/gen.log" 2>&1 || echo "generation failed: T5 synthetic inputs" >> "$WORK/gen.log"
else
    PYTHON=""
    echo "(python 3 not found: no synthetic inputs)"
fi

# fragmented files
for f in audio-aac-001 audio-aac-002 audio-aac-003 video-h264-001 video-h264-002; do
    ref mp4fragment $f.mp4 frag-$f.mp4
done
ref mp4fragment --fragment-duration 500 video-h264-001.mp4 frag-500-video-h264-001.mp4
ref mp4fragment --fragment-duration 500 video-h264-002.mp4 frag-500-video-h264-002.mp4
ref mp4fragment --track audio video-h264-001.mp4 frag-audio-only-video-h264-001.mp4
# encrypted files: fragmented
for m in CENC CENS CBC1 CBCS; do
    l=$(echo $m | tr A-Z a-z)
    ref mp4encrypt --method MPEG-$m --key $KEY1 --key $KEY2 frag-video-h264-001.mp4 enc-$l-frag-video.mp4
    ref mp4encrypt --method MPEG-$m --key $KEY1 frag-audio-aac-001.mp4 enc-$l-frag-audio.mp4
done
ref mp4encrypt --method PIFF-CTR --key $KEY1 --key $KEY2 frag-video-h264-001.mp4 enc-piff-ctr-frag-video.mp4
ref mp4encrypt --method PIFF-CBC --key $KEY1 --key $KEY2 frag-video-h264-001.mp4 enc-piff-cbc-frag-video.mp4
ref mp4encrypt --method MPEG-CENC --key $KEY1 --key $KEY2 frag-500-video-h264-001.mp4 enc-cenc-frag-500-video.mp4
# encrypted files: not fragmented
for m in CENC CENS CBC1 CBCS; do
    l=$(echo $m | tr A-Z a-z)
    ref mp4encrypt --method MPEG-$m --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-$l-video.mp4
    ref mp4encrypt --method MPEG-$m --key $KEY1 audio-aac-001.mp4 enc-$l-audio.mp4
done
ref mp4encrypt --method OMA-PDCF-CBC --key $KEY1 audio-aac-001.mp4 enc-oma-cbc-audio.mp4
ref mp4encrypt --method OMA-PDCF-CTR --key $KEY1 audio-aac-001.mp4 enc-oma-ctr-audio.mp4
ref mp4encrypt --method OMA-PDCF-CBC --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-oma-cbc-video.mp4
ref mp4encrypt --method OMA-PDCF-CTR --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-oma-ctr-video.mp4
ref mp4encrypt --method ISMA-IAEC --key $KEY1 --kms-uri https://kms.example.com/key audio-aac-001.mp4 enc-isma-audio.mp4
ref mp4encrypt --method ISMA-IAEC --key $KEY1 --key $KEY2 --kms-uri urn:example:kms video-h264-001.mp4 enc-isma-video.mp4
ref mp4encrypt --method MARLIN-IPMP-ACBC --key $KEY1 audio-aac-001.mp4 enc-marlin-acbc-audio.mp4
ref mp4encrypt --method MARLIN-IPMP-ACBC --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-marlin-acbc-video.mp4
ref mp4encrypt --method MARLIN-IPMP-ACGK --key $KEY1 --key $KEY2 --key 0:202122232425262728292a2b2c2d2e2f:0000000000000000 video-h264-001.mp4 enc-marlin-acgk-video.mp4
# only the video track, or only the audio track, is encrypted
ref mp4encrypt --method MPEG-CENC --key $KEY1 video-h264-001.mp4 enc-cenc-video-only.mp4
ref mp4encrypt --method OMA-PDCF-CBC --key $KEY2 video-h264-001.mp4 enc-oma-cbc-audio-only.mp4
if [ -n "$PYTHON" ]; then
    # (t5-hevc-*: track 1 = HEVC, track 2 = AAC; t5-avc-*: track 1 = AAC, track 2 = AVC)
    for f in t5-hevc-l4 t5-hevc-l2 t5-hevc-l1 t5-avc-l4 t5-avc-l2 t5-avc-l1 t5-avc3-l4; do
        ref mp4encrypt --method MPEG-CENC --key $KEY1 --key $KEY2 $f.mp4 enc-cenc-$f.mp4
        ref mp4encrypt --method MPEG-CBCS --key $KEY1 --key $KEY2 $f.mp4 enc-cbcs-$f.mp4
        ref mp4encrypt --method OMA-PDCF-CBC --key $KEY1 --key $KEY2 $f.mp4 enc-oma-cbc-$f.mp4
        ref mp4encrypt --method ISMA-IAEC --key $KEY1 --key $KEY2 --kms-uri urn:example:kms $f.mp4 enc-isma-$f.mp4
    done
    for f in t5-aac-7350-1 t5-aac-96000-6 t5-aac-12345-8; do
        ref mp4encrypt --method MPEG-CENC --key $KEY1 $f.mp4 enc-cenc-$f.mp4
        ref mp4encrypt --method OMA-PDCF-CTR --key $KEY1 $f.mp4 enc-oma-ctr-$f.mp4
        ref mp4encrypt --method ISMA-IAEC --key $KEY1 --kms-uri urn:example:kms $f.mp4 enc-isma-$f.mp4
    done
    ref mp4fragment t5-hevc-l4.mp4 frag-t5-hevc-l4.mp4
    ref mp4fragment t5-avc-l2.mp4 frag-t5-avc-l2.mp4
    ref mp4fragment t5-aac-96000-6.mp4 frag-t5-aac-96000-6.mp4
fi
# elementary streams for avcinfo and hevcinfo
ref mp42avc video-h264-001.mp4 es-video-h264-001.264
ref mp42avc video-h264-002.mp4 es-video-h264-002.264
ref mp42aac audio-aac-001.mp4 es-audio-aac-001.aac
if [ -n "$PYTHON" ]; then
    for f in t5-avc-l4 t5-avc-l2 t5-avc-l1 t5-avc3-l4 t5-avc-noparams syn-codecs; do ref mp42avc $f.mp4 es-$f.264; done
    for f in t5-hevc-l4 t5-hevc-l2 t5-hevc-l1 t5-hevc-noparams t5-hevc-order; do ref mp42hevc $f.mp4 es-$f.hevc; done
fi
# small hand-made Annex-B streams (every NAL unit that is a slice has at least 9 bytes,
# every access unit delimiter has 2: the original tools read that many bytes unchecked)
: > es-empty.bin
printf '\x00' > es-one-zero.bin
printf '\x00\x00\x01' > es-start-code-only.bin
printf '\x00\x00\x00\x01\x09\xf0' > es-aud.bin
printf '\x00\x00\x00\x01\x09\x10\x00\x00\x01\x67\x42\x00\x00\x01\x68\x00\x00\x00\x01\x06\x05' > es-short-nalus.bin
printf '\x67\x42\x00\x1e\x00\x00\x01\x09\x30\x00\x00\x00\x00\x00\x01\x09\x50\x00\x00\x01\x09\x70\x00\x00\x01\x09\x90\x00\x00\x01\x09\xb0\x00\x00\x01\x09\xd0' > es-leading-garbage.bin
printf '\x00\x00\x01\x65\x88\x84\x21\x43\x65\x87\x09\x21\x43\x00\x00\x01\x41\x9a\x21\x43\x65\x87\x09\x21\x43\x00\x00\x01\x01\x9e\x21\x43\x65\x87\x09\x21\x43\x00\x00\x01\x41\xb8\x21\x43\x65\x87\x09\x21\x43\x00\x00\x01\x05\xa6\x21\x43\x65\x87\x09\x21\x43\x00\x00\x01\x02\xa7\x21\x43\x65\x87\x09\x21\x43\x00\x00\x01\x13\x88\x21\x43\x65\x87\x09\x21\x43\x00\x00\x01\x41\x00\x40\x43\x65\x87\x09\x21\x43\x00\x00\x01\x1f\x01\x02\x03\x04\x05\x06\x07\x08\x09\x00\x00\x01\x0e\x01\x02\x03\x04\x05\x06\x07\x08\x09' > es-slices.bin
printf '\x00\x00\x01\x46\x01\x10\x00\x00\x01\x46\x01\x30\x00\x00\x01\x46\x01\x50\x00\x00\x01\x46\x01\x70\x00\x00\x01\x46\x01\xff\x00\x00\x01\x40\x01\x0c\x00\x00\x01\x42\x01\x01\x00\x00\x01\x44\x01\xc0\x00\x00\x01\x4e\x01\x05\x00\x00\x01\x50\x01\x05\x00\x00\x01\x26\x01\xaf\x00\x00\x01\x02\x01\xd0\x11\x22\x33\x44\x55\x66\x77\x00\x00\x01\x7e\x01\x00\x00\x00\x01\x60\x01\x00\x00\x00\x01\x62\x01\x00' > es-hevc-types.bin
head -c 10000 video-h264-001.mp4 > es-mp4-head.bin
# file names that are not ASCII (UTF-8: e acute, two CJK characters)
UNI="$(printf 't5-\303\251\346\227\245\346\234\254')"
cp audio-aac-001.mp4 "$UNI.mp4"
cp frag-video-h264-001.mp4 "$UNI-frag.mp4"
cp es-video-h264-001.264 "$UNI.264"
[ -n "$PYTHON" ] && cp t5-hint.mp4 "$UNI-hint.mp4"

cd "$ROOT" || exit 1
cp -r "$RDIR" "$PDIR"

# file lists
STOCK="audio-aac-001 audio-aac-002 audio-aac-003 video-h264-001 video-h264-002"
ALL_MP4=""
for f in "$RDIR"/*.mp4; do ALL_MP4="$ALL_MP4 $(basename "$f" .mp4)"; done
NAMES=" $ALL_MP4 "
have() { case "$NAMES" in *" $1 "*) [ -s "$RDIR/$1.mp4" ] ;; *) false ;; esac; }
for f in $ALL_MP4; do
    if [ ! -s "$RDIR/$f.mp4" ] && [ "$f" != syn-empty ]; then
        fail "$f.mp4 generation" "input was not generated, see $WORK/gen.log"
    fi
done

#----------------------------------------------------------------------
# mp42aac, mp42avc, mp42hevc
#----------------------------------------------------------------------
# Inputs for which the original tool has undefined behaviour (nothing to compare with):
# (none of the inputs of this script)
UNDEFINED_UPSTREAM=" "

for tool in mp42aac mp42avc mp42hevc; do
    flush
    echo "=== $tool ==="
    case $tool in mp42aac) ext=aac ;; mp42avc) ext=264 ;; mp42hevc) ext=hevc ;; esac
    for f in $ALL_MP4; do
        case "$UNDEFINED_UPSTREAM" in *" $tool:$f "*) skip "$tool $f" "undefined behaviour in the original tool"; continue ;; esac
        t5 "$tool.$f" $tool "out/$tool.$f.$ext" $f.mp4 out/$tool.$f.$ext
        relevant=no
        case $tool:$f in
            mp42aac:enc-*)                                   relevant=yes ;;
            mp42avc:enc-*video*|mp42avc:enc-*avc*)           relevant=yes ;;
            mp42hevc:enc-*hevc*|mp42hevc:enc-cenc-video|mp42hevc:enc-oma-cbc-video) relevant=yes ;;
        esac
        case $relevant in
            yes)
                t5 "$tool.$f.key1" $tool "out/$tool.$f.key1.$ext" --key $K1 $f.mp4 out/$tool.$f.key1.$ext
                t5 "$tool.$f.key2" $tool "out/$tool.$f.key2.$ext" --key $K2 $f.mp4 out/$tool.$f.key2.$ext
                ;;
        esac
    done
    # a key that is not needed
    t5 "$tool.clear-with-key" $tool "out/$tool.clear-with-key.$ext" --key $K1 video-h264-001.mp4 out/$tool.clear-with-key.$ext
    t5 "$tool.clear-with-key-uppercase" $tool "out/$tool.clear-with-key-uc.$ext" --key 000102030405060708090A0B0C0D0E0F audio-aac-001.mp4 out/$tool.clear-with-key-uc.$ext
    # usage and errors
    t5 "$tool.usage.no-args" $tool ""
    t5 "$tool.usage.one-arg" $tool "" video-h264-001.mp4
    t5 "$tool.error.key-only" $tool "" --key $K1
    t5 "$tool.error.key-missing-output" $tool "" --key $K1 video-h264-001.mp4
    t5 "$tool.error.key-extra-arg" $tool "out/$tool.x1.$ext" --key $K1 video-h264-001.mp4 out/$tool.x1.$ext extra
    t5 "$tool.error.key-short" $tool "out/$tool.x2.$ext" --key 0001020304 video-h264-001.mp4 out/$tool.x2.$ext
    t5 "$tool.error.key-long" $tool "out/$tool.x3.$ext" --key ${K1}0011 video-h264-001.mp4 out/$tool.x3.$ext
    t5 "$tool.error.key-not-hex" $tool "out/$tool.x4.$ext" --key 000102030405060708090a0b0c0d0eXX video-h264-001.mp4 out/$tool.x4.$ext
    t5 "$tool.error.key-option-last" $tool "out/$tool.x5.$ext" video-h264-001.mp4 out/$tool.x5.$ext --key
    t5 "$tool.extra-args-ignored" $tool "out/$tool.x6.$ext" video-h264-001.mp4 out/$tool.x6.$ext extra1 extra2
    if [ $tool = mp42aac ]; then
        t5 "$tool.error.no-input" $tool "out/$tool.x8.$ext" no-such-file.mp4 out/$tool.x8.$ext
        t5 "$tool.error.no-output-dir" $tool "" video-h264-001.mp4 no-such-dir/x9.$ext
    else
        # the original tool goes on with a NULL stream and crashes
        t5_crash "$tool.error.no-input" $tool "out/$tool.x8.$ext" no-such-file.mp4 out/$tool.x8.$ext
        t5_crash "$tool.error.no-output-dir" $tool "" video-h264-001.mp4 no-such-dir/x9.$ext
        t5_crash "$tool.error.no-output-dir-no-video" $tool "" audio-aac-001.mp4 no-such-dir/x9.$ext
        t5_crash "$tool.error.no-output-dir-not-mp4" $tool "" es-audio-aac-001.aac no-such-dir/x9.$ext
        t5_crash "$tool.error.no-output-dir-no-key" $tool "" enc-oma-cbc-video.mp4 no-such-dir/x9.$ext
        t5_crash "$tool.error.no-output-dir-key" $tool "" --key $K1 enc-oma-cbc-video.mp4 no-such-dir/x9.$ext
        t5_crash "$tool.error.no-output-dir-cenc-key" $tool "" --key $K1 enc-cenc-video.mp4 no-such-dir/x9.$ext
        t5_crash "$tool.error.no-output-dir-no-samples" $tool "" frag-video-h264-001.mp4 no-such-dir/x9.$ext
        if [ -n "$PYTHON" ]; then
            t5_crash "$tool.error.no-output-dir-hevc" $tool "" t5-hevc-l4.mp4 no-such-dir/x9.$ext
            t5_crash "$tool.error.no-output-dir-hevc-key" $tool "" --key $K1 enc-isma-t5-hevc-l4.mp4 no-such-dir/x9.$ext
            t5_crash "$tool.error.no-output-dir-hevc-wrong-key" $tool "" --key $K2 enc-oma-cbc-t5-hevc-l4.mp4 no-such-dir/x9.$ext
        fi
    fi
    t5 "$tool.error.input-not-mp4" $tool "out/$tool.x10.$ext" es-video-h264-001.264 out/$tool.x10.$ext
    t5 "$tool.error.input-empty" $tool "out/$tool.x11.$ext" es-empty.bin out/$tool.x11.$ext
done

#----------------------------------------------------------------------
# avcinfo, hevcinfo
#----------------------------------------------------------------------
for tool in avcinfo hevcinfo; do
    flush
    echo "=== $tool ==="
    for f in "$RDIR"/es-*; do
        f="$(basename "$f")"
        t5 "$tool.$f" $tool "" $f
    done
    t5 "$tool.mp4-file" $tool "" audio-aac-003.mp4
    t5 "$tool.verbose-first" $tool "" --verbose es-video-h264-001.264
    t5 "$tool.verbose-last" $tool "" es-video-h264-002.264 --verbose
    t5 "$tool.verbose-twice" $tool "" --verbose es-aud.bin --verbose
    t5 "$tool.usage.no-args" $tool ""
    t5 "$tool.error.verbose-only" $tool "" --verbose
    t5 "$tool.error.two-files" $tool "" es-video-h264-001.264 es-video-h264-002.264
    t5 "$tool.error.three-files" $tool "" es-video-h264-001.264 es-video-h264-002.264 third
    t5 "$tool.error.unknown-option" $tool "" --nalus es-video-h264-001.264
    t5 "$tool.error.no-input" $tool "" no-such-file.264
    t5 "$tool.error.no-input-verbose" $tool "" --verbose no-such-file.264
done

#----------------------------------------------------------------------
# mp4audioclip
#----------------------------------------------------------------------
flush
echo "=== mp4audioclip ==="
# clip <case suffix> <input> <options>...
clip() {
    local suffix="$1" f="$2"
    shift 2
    t5 "mp4audioclip.$f.$suffix" mp4audioclip "out/clip.$f.$suffix.mp4" "$@" $f.mp4 out/clip.$f.$suffix.mp4
}
for f in $ALL_MP4; do
    clip default $f
    clip s1000-d1500 $f --start 1000 --duration 1500
done
for f in audio-aac-002 audio-aac-003 video-h264-002 enc-cenc-audio enc-oma-cbc-audio enc-isma-video t5-aac-7350-1 t5-aac-12345-8 t5-hevc-l4 t5-avc-l2 syn-codecs; do
    case $f in t5-*|syn-*) have $f || continue ;; esac
    clip s500 $f --start 500
    clip s3000 $f --start 3000
    clip s-huge $f --start 4294967295
    clip d1 $f --duration 1
    clip d1000 $f --duration 1000
    clip s2000-d99999 $f --start 2000 --duration 99999
    clip s20-d40 $f --start 20 --duration 40
done
for f in audio-aac-001 video-h264-001 t5-aac-96000-6; do
    case $f in t5-*|syn-*) have $f || continue ;; esac
    clip s0 $f --start 0
    clip s1 $f --start 1
    clip s23 $f --start 23
    clip s24 $f --start 24
    clip s500 $f --start 500
    clip s3000 $f --start 3000
    clip s3599 $f --start 3599
    clip s3600 $f --start 3600
    clip s3622 $f --start 3622
    clip s3623 $f --start 3623
    clip s29999 $f --start 29999
    clip s30046 $f --start 30046
    clip s30047 $f --start 30047
    clip s30048 $f --start 30048
    clip s-huge $f --start 4294967295
    clip s-overflow $f --start 4294967296
    clip s-negative $f --start -1
    clip s-text $f --start abc
    clip s-decimal $f --start 12.75
    clip s-hex $f --start 0x10
    clip s-empty $f --start ""
    clip d0 $f --duration 0
    clip d1 $f --duration 1
    clip d21 $f --duration 21
    clip d22 $f --duration 22
    clip d1000 $f --duration 1000
    clip d3600 $f --duration 3600
    clip d30047 $f --duration 30047
    clip d100000 $f --duration 100000
    clip d-huge $f --duration 4294967295
    clip d-text $f --duration 1s
    clip d-negative $f --duration -500
    clip s100-d0 $f --start 100 --duration 0
    clip d700-s1300 $f --duration 700 --start 1300
    clip s2000-d99999 $f --start 2000 --duration 99999
    clip s3000-d700 $f --start 3000 --duration 700
    clip repeated $f --start 5 --start 2000 --duration 5 --duration 800
done
t5 "mp4audioclip.options-last" mp4audioclip "out/clip.x1.mp4" audio-aac-001.mp4 out/clip.x1.mp4 --start 4000 --duration 2000
t5 "mp4audioclip.options-between" mp4audioclip "out/clip.x2.mp4" audio-aac-001.mp4 --duration 2000 out/clip.x2.mp4
t5 "mp4audioclip.usage.no-args" mp4audioclip ""
t5 "mp4audioclip.error.input-only" mp4audioclip "" audio-aac-001.mp4
t5 "mp4audioclip.error.options-only" mp4audioclip "" --start 5 --duration 6
t5 "mp4audioclip.error.start-missing-value" mp4audioclip "out/clip.x3.mp4" audio-aac-001.mp4 out/clip.x3.mp4 --start
t5 "mp4audioclip.error.duration-missing-value" mp4audioclip "out/clip.x4.mp4" audio-aac-001.mp4 out/clip.x4.mp4 --duration
t5 "mp4audioclip.error.start-value-is-option" mp4audioclip "out/clip.x5.mp4" --start --duration audio-aac-001.mp4 out/clip.x5.mp4
t5 "mp4audioclip.error.extra-arg" mp4audioclip "out/clip.x6.mp4" audio-aac-001.mp4 out/clip.x6.mp4 extra
t5 "mp4audioclip.error.unknown-option" mp4audioclip "out/clip.x7.mp4" --verbose audio-aac-001.mp4 out/clip.x7.mp4
t5 "mp4audioclip.error.no-input" mp4audioclip "out/clip.x8.mp4" no-such-file.mp4 out/clip.x8.mp4
t5 "mp4audioclip.error.no-output-dir" mp4audioclip "" audio-aac-001.mp4 no-such-dir/clip.mp4
t5 "mp4audioclip.error.input-not-mp4" mp4audioclip "out/clip.x9.mp4" es-audio-aac-001.aac out/clip.x9.mp4
t5 "mp4audioclip.error.input-empty" mp4audioclip "out/clip.x10.mp4" es-empty.bin out/clip.x10.mp4
flush
# a clip of a clip
if [ -e "$RDIR/out/clip.audio-aac-001.s1000-d1500.mp4" ]; then
    t5 "mp4audioclip.clip-of-clip" mp4audioclip "out/clip.x11.mp4" --start 300 --duration 400 out/clip.audio-aac-001.s1000-d1500.mp4 out/clip.x11.mp4
fi

#----------------------------------------------------------------------
# mp4diff
#----------------------------------------------------------------------
flush
echo "=== mp4diff ==="
# diff <file 1> <file 2>
diff2() { t5 "mp4diff.$1.$2" mp4diff "" $1.mp4 $2.mp4; }
# identical files
for f in $STOCK frag-audio-aac-001 frag-video-h264-001 frag-500-video-h264-001 enc-cenc-frag-video enc-cenc-video; do diff2 $f $f; done
# not fragmented
diff2 audio-aac-001 video-h264-001
diff2 video-h264-001 audio-aac-001
diff2 video-h264-001 enc-cenc-video
diff2 audio-aac-001 enc-oma-cbc-audio
# fragmented / not fragmented
diff2 video-h264-001 frag-video-h264-001
diff2 frag-video-h264-001 video-h264-001
diff2 audio-aac-001 audio-aac-002
diff2 audio-aac-002 audio-aac-001
# fragmented, same samples
diff2 frag-video-h264-001 frag-500-video-h264-001
diff2 frag-500-video-h264-001 frag-video-h264-001
diff2 audio-aac-002 frag-audio-aac-002
diff2 audio-aac-002 audio-aac-003
diff2 audio-aac-003 audio-aac-002
diff2 frag-audio-aac-001 audio-aac-002
diff2 audio-aac-003 frag-audio-aac-001
diff2 video-h264-002 frag-video-h264-002
diff2 frag-500-video-h264-002 video-h264-002
# fragmented, different samples
diff2 frag-video-h264-001 enc-cenc-frag-video
diff2 enc-cenc-frag-video frag-video-h264-001
diff2 enc-cenc-frag-video enc-cbcs-frag-video
diff2 enc-cens-frag-video enc-cbc1-frag-video
diff2 enc-piff-ctr-frag-video enc-piff-cbc-frag-video
diff2 enc-cenc-frag-500-video enc-cenc-frag-video
diff2 frag-audio-aac-001 enc-cenc-frag-audio
diff2 enc-cbcs-frag-audio enc-cenc-frag-audio
diff2 frag-video-h264-001 frag-video-h264-002
diff2 frag-video-h264-002 frag-video-h264-001
diff2 video-h264-002 frag-video-h264-001
diff2 frag-audio-aac-001 frag-audio-aac-002
diff2 frag-audio-aac-002 frag-audio-aac-003
# different tracks
diff2 frag-audio-aac-001 frag-video-h264-001
diff2 frag-video-h264-001 frag-audio-aac-001
diff2 frag-audio-only-video-h264-001 frag-video-h264-001
diff2 frag-video-h264-001 frag-audio-only-video-h264-001
diff2 frag-audio-only-video-h264-001 frag-audio-aac-001
diff2 video-h264-002 audio-aac-002
if [ -n "$PYTHON" ]; then
    diff2 syn-fragmented syn-fragmented
    diff2 syn-fragmented frag-video-h264-001
    diff2 frag-video-h264-001 syn-fragmented
    diff2 syn-fragmented syn-trunc-frag
    diff2 syn-trunc-frag syn-fragmented
    diff2 frag-t5-hevc-l4 frag-t5-avc-l2
    diff2 frag-t5-avc-l2 frag-t5-hevc-l4
    diff2 frag-t5-aac-96000-6 frag-t5-avc-l2
    diff2 syn-codecs syn-trunc-moov
    diff2 syn-not-mp4 syn-codecs
    diff2 syn-empty syn-ftyp-only
fi
t5 "mp4diff.not-mp4" mp4diff "" es-video-h264-001.264 es-audio-aac-001.aac
t5 "mp4diff.not-mp4-second" mp4diff "" frag-video-h264-001.mp4 es-audio-aac-001.aac
t5 "mp4diff.usage.no-args" mp4diff ""
t5 "mp4diff.usage.one-arg" mp4diff "" video-h264-001.mp4
t5 "mp4diff.error.three-args" mp4diff "" video-h264-001.mp4 video-h264-002.mp4 audio-aac-001.mp4
t5 "mp4diff.error.option" mp4diff "" --verbose frag-video-h264-001.mp4 frag-video-h264-002.mp4
t5 "mp4diff.error.option-as-file" mp4diff "" --verbose frag-video-h264-001.mp4
t5 "mp4diff.error.no-input-1" mp4diff "" no-such-file.mp4 video-h264-001.mp4
t5 "mp4diff.error.no-input-2" mp4diff "" video-h264-001.mp4 no-such-file.mp4
t5 "mp4diff.error.no-input-both" mp4diff "" no-such-file-1.mp4 no-such-file-2.mp4

#----------------------------------------------------------------------
# mp4rtphintinfo
#----------------------------------------------------------------------
flush
echo "=== mp4rtphintinfo ==="
t5 "mp4rtphintinfo.usage.no-args" mp4rtphintinfo ""
t5 "mp4rtphintinfo.usage.one-arg" mp4rtphintinfo "" video-h264-001.mp4
t5 "mp4rtphintinfo.usage.two-args" mp4rtphintinfo "" --trackid 1
t5 "mp4rtphintinfo.usage.four-args" mp4rtphintinfo "" --trackid 1 video-h264-001.mp4 extra
t5 "mp4rtphintinfo.not-hint-track" mp4rtphintinfo "video-h264-001.mp4-track-1.rtp" --trackid 1 video-h264-001.mp4
t5 "mp4rtphintinfo.not-hint-track-2" mp4rtphintinfo "video-h264-001.mp4-track-2.rtp" --trackid 2 video-h264-001.mp4
t5 "mp4rtphintinfo.no-such-track" mp4rtphintinfo "video-h264-001.mp4-track-7.rtp" --trackid 7 video-h264-001.mp4
t5 "mp4rtphintinfo.track-id-0" mp4rtphintinfo "" --trackid 0 video-h264-001.mp4
t5 "mp4rtphintinfo.track-id-negative" mp4rtphintinfo "" --trackid -1 video-h264-001.mp4
t5 "mp4rtphintinfo.track-id-text" mp4rtphintinfo "" --trackid abc video-h264-001.mp4
t5 "mp4rtphintinfo.track-id-prefix" mp4rtphintinfo "" --trackid " +1x" video-h264-001.mp4
t5 "mp4rtphintinfo.track-id-after-file" mp4rtphintinfo "" video-h264-001.mp4 --trackid 1
t5 "mp4rtphintinfo.fragmented" mp4rtphintinfo "" --trackid 1 frag-video-h264-001.mp4
t5 "mp4rtphintinfo.error.three-files" mp4rtphintinfo "" video-h264-001.mp4 video-h264-002.mp4 audio-aac-001.mp4
t5 "mp4rtphintinfo.error.trackid-last" mp4rtphintinfo "" video-h264-001.mp4 video-h264-002.mp4 --trackid
t5 "mp4rtphintinfo.error.trackid-twice" mp4rtphintinfo "" --trackid --trackid --trackid
t5 "mp4rtphintinfo.error.no-input" mp4rtphintinfo "" --trackid 1 no-such-file.mp4
t5 "mp4rtphintinfo.error.not-mp4" mp4rtphintinfo "" --trackid 1 es-video-h264-001.264
t5 "mp4rtphintinfo.error.empty" mp4rtphintinfo "" --trackid 1 es-empty.bin
if [ -n "$PYTHON" ]; then
    for f in t5-hint t5-hint-long-sdp t5-hint-binary-sdp t5-hint-long-binary-sdp t5-hint-no-sdp-no-tref t5-hint-sample-desc t5-hint-bad-sample-number t5-hint-bad-sample-range; do
        t5_rtp "mp4rtphintinfo.$f" $f.mp4 2
        t5 "mp4rtphintinfo.$f.media-track" mp4rtphintinfo "" --trackid 1 $f.mp4
    done
    # the hint track of syn-codecs.mp4 has filler instead of hint samples
    t5 "mp4rtphintinfo.syn-codecs.not-hint-track" mp4rtphintinfo "" --trackid 12 syn-codecs.mp4
else
    skip "mp4rtphintinfo on hint tracks" "python 3 not found"
fi

#----------------------------------------------------------------------
# summary
#----------------------------------------------------------------------
#----------------------------------------------------------------------
# file names that are not ASCII
#----------------------------------------------------------------------
flush
echo "=== non-ASCII file names ==="
t5 "unicode.mp42aac" mp42aac "out/$UNI.aac" "$UNI.mp4" "out/$UNI.aac"
t5 "unicode.mp42avc" mp42avc "out/$UNI.264" "$UNI-frag.mp4" "out/$UNI.264"
t5 "unicode.mp42hevc" mp42hevc "out/$UNI.hevc" "$UNI-frag.mp4" "out/$UNI.hevc"
t5 "unicode.avcinfo" avcinfo "" "$UNI.264"
t5 "unicode.hevcinfo" hevcinfo "" "$UNI.264"
t5 "unicode.mp4audioclip" mp4audioclip "out/$UNI.clip.mp4" --duration 1000 "$UNI.mp4" "out/$UNI.clip.mp4"
t5 "unicode.mp4diff" mp4diff "" "$UNI-frag.mp4" "$UNI-frag.mp4"
t5 "unicode.mp4rtphintinfo.not-hint" mp4rtphintinfo "" --trackid 1 "$UNI.mp4"
t5 "unicode.avcinfo.no-input" avcinfo "" "$UNI-no-such-file.264"
t5 "unicode.avcinfo.extra-arg" avcinfo "" "$UNI.264" "$UNI-extra"
t5 "unicode.hevcinfo.no-input" hevcinfo "" "$UNI-no-such-file.264"
t5 "unicode.hevcinfo.extra-arg" hevcinfo "" "$UNI.264" "$UNI-extra"
t5 "unicode.mp4audioclip.no-input" mp4audioclip "" "$UNI-no-such-file.mp4" "out/$UNI.x1.mp4"
t5 "unicode.mp4audioclip.no-output-dir" mp4audioclip "" "$UNI.mp4" "$UNI-no-such-dir/x2.mp4"
t5 "unicode.mp4audioclip.extra-arg" mp4audioclip "" "$UNI.mp4" "out/$UNI.x3.mp4" "$UNI-extra"
t5 "unicode.mp4diff.no-input-1" mp4diff "" "$UNI-no-such-file.mp4" "$UNI.mp4"
t5 "unicode.mp4diff.no-input-2" mp4diff "" "$UNI.mp4" "$UNI-no-such-file.mp4"
t5 "unicode.mp4diff.extra-arg" mp4diff "" "$UNI.mp4" "$UNI.mp4" "$UNI-extra"
t5 "unicode.mp4rtphintinfo.extra-arg" mp4rtphintinfo "" "$UNI.mp4" "$UNI-extra" third
t5 "unicode.mp4rtphintinfo.no-input" mp4rtphintinfo "" --trackid 1 "$UNI-no-such-file.mp4"
if [ -n "$PYTHON" ]; then
    t5_rtp "unicode.mp4rtphintinfo" "$UNI-hint.mp4" 2
fi

flush
echo "=== summary: $PASSED passed, $FAILED failed, $SKIPPED skipped ==="
if [ $FAILED -ne 0 ]; then
    for f in "${FAILURES[@]}"; do echo "  failed: $f"; done
    exit 1
fi
exit 0
