#!/bin/bash
# Compares the output of the ported tools (mp4dump, mp4info) with the output
# of the original Bento4 tools, byte for byte, and checks that files parsed
# with the port are written back unchanged.
#
# usage: tests/compare-with-reference.sh [<reference-dir> [<test-data-dir>]]
#
#   <reference-dir>  (or $BENTO4_REFERENCE_DIR) directory with
#                      expected/<name>.{dump.txt,dump.v3.txt,dump.json,info.txt,info.json,info.verbose.txt}
#                          outputs of the original tools for each <name>.mp4 of the test data
#                      bin/<tool>.js  (optional) the original tools built with emscripten, run with node:
#                          mp4dump mp4info mp4fragment mp4split mp4edit mp4extract mp4encrypt mp4compact.
#                          When present, extra inputs are generated in a temporary directory (with the
#                          original tools, and with tests/make-synthetic-inputs.py when python is
#                          available) and the original tools are run side by side with the port.
#   <test-data-dir>  (or $BENTO4_TEST_DATA_DIR) the Test/Data directory of the Bento4 sources (*.mp4)
#
# other environment variables:
#   BENTO4SHARP_ARTIFACTS   where to build (default: a temporary directory, removed on exit)
#   BENTO4SHARP_WORK_DIR    where to put generated inputs and outputs (default: a temporary directory)
#   BENTO4SHARP_SKIP_BUILD  set to 1 to reuse the binaries already in $BENTO4SHARP_ARTIFACTS
#   BENTO4SHARP_FRAMEWORK   target framework of the port to build and run (default: net8.0; net20 runs
#                           the .NET Framework 2.0 executables)
#   BENTO4SHARP_SKIP_EXTRA  set to 1 to only run the stock cases
#
# exit code: 0 if every case passes, 1 otherwise.

REF="${1:-$BENTO4_REFERENCE_DIR}"
DATA="${2:-$BENTO4_TEST_DATA_DIR}"
if [ -z "$REF" ] || [ -z "$DATA" ]; then
    sed -n "2,26p" "$0" >&2
    exit 2
fi
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

CLEANUP=()
cleanup() { for d in "${CLEANUP[@]}"; do rm -rf "$d"; done; }
trap cleanup EXIT

ART="$BENTO4SHARP_ARTIFACTS"
if [ -z "$ART" ]; then ART="$(mktemp -d)"; CLEANUP+=("$ART"); fi
WORK="$BENTO4SHARP_WORK_DIR"
if [ -z "$WORK" ]; then WORK="$(mktemp -d)"; CLEANUP+=("$WORK"); fi
mkdir -p "$WORK/out" "$WORK/gen"

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
if [ "$BENTO4SHARP_SKIP_BUILD" != "1" ]; then
    for project in src/Tools/Mp4Dump/Mp4Dump.csproj src/Tools/Mp4Info/Mp4Info.csproj tests/RoundTrip/RoundTrip.csproj; do
        if ! dotnet build "$ROOT/$project" -c Release -f "$FW" -nologo -v q --artifacts-path "$ART" > "$WORK/build.log" 2>&1; then
            cat "$WORK/build.log"
            echo "BUILD FAILED: $project"
            exit 1
        fi
    done
fi
MP4DUMP="$ART/bin/Mp4Dump/release_$FW/mp4dump.dll"
MP4INFO="$ART/bin/Mp4Info/release_$FW/mp4info.dll"
ROUNDTRIP="$ART/bin/RoundTrip/release_$FW/roundtrip.dll"

#----------------------------------------------------------------------
# helpers
#----------------------------------------------------------------------
PASSED=0; FAILED=0; SKIPPED=0
FAILURES=()

VARIANTS="dump.txt dump.v3.txt dump.json info.txt info.json info.verbose.txt"
# more option combinations, only run side by side with the original tools
EXTRA_VARIANTS="$VARIANTS dump.v1.json dump.v2.txt info.data.txt info.vdata.txt info.fast.json"

# Cases where the original tools have undefined behaviour (they print
# uninitialized memory), so there is nothing to compare with:
# - sample data that lies past the end of a truncated file
UNDEFINED_UPSTREAM=" syn-trunc-mdat.info.data.txt syn-trunc-mdat.info.vdata.txt "

# Inputs that the original library does not write back byte for byte either
# (truncated files, an atom of size 0 = "up to the end of the file", atoms that
# are normalized when written): no round-trip check for them.
NO_ROUNDTRIP=" syn-configs syn-odd syn-large-open syn-not-mp4 syn-trunc-moov syn-trunc-stsd syn-trunc-mdat syn-trunc-frag syn-trunc-header "

# variant_tool <variant>: sets $TOOL (mp4dump or mp4info) and $OPTIONS
variant_tool() {
    case "$1" in
        dump.txt)         TOOL=mp4dump; OPTIONS=() ;;
        dump.v3.txt)      TOOL=mp4dump; OPTIONS=(--verbosity 3) ;;
        dump.json)        TOOL=mp4dump; OPTIONS=(--format json) ;;
        info.txt)         TOOL=mp4info; OPTIONS=() ;;
        info.json)        TOOL=mp4info; OPTIONS=(--format json) ;;
        info.verbose.txt) TOOL=mp4info; OPTIONS=(--verbose --show-layout --show-samples) ;;
        info.data.txt)    TOOL=mp4info; OPTIONS=(--show-sample-data) ;;
        info.vdata.txt)   TOOL=mp4info; OPTIONS=(--verbose --show-sample-data) ;;
        info.fast.json)   TOOL=mp4info; OPTIONS=(--fast --format json) ;;
        dump.v1.json)     TOOL=mp4dump; OPTIONS=(--verbosity 1 --format json) ;;
        dump.v2.txt)      TOOL=mp4dump; OPTIONS=(--verbosity 2) ;;
    esac
}

# run_port <variant> <input> <output>
run_port() {
    variant_tool "$1"
    if [ "$TOOL" = mp4dump ]; then
        port "$MP4DUMP" "${OPTIONS[@]}" "$2" > "$3" 2> "$3.stderr"
    else
        port "$MP4INFO" "${OPTIONS[@]}" "$2" > "$3" 2> "$3.stderr"
    fi
    echo $? > "$3.rc"
}

# run_reference <variant> <input (relative to the current directory)> <output>
run_reference() {
    variant_tool "$1"
    node "$REF/bin/$TOOL.js" "${OPTIONS[@]}" "$2" > "$3" 2> "$3.stderr"
    echo $? > "$3.rc"
}

# report <case name> <expected file> <actual file>
# (the exit codes are compared too when the expected one was recorded)
report() {
    if cmp -s "$2" "$3" && { [ ! -f "$2.rc" ] || [ "$(cat "$2.rc")" = "$(cat "$3.rc")" ]; }; then
        echo "PASS  $1"
        PASSED=$((PASSED+1))
    else
        echo "FAIL  $1"
        FAILED=$((FAILED+1))
        FAILURES+=("$1")
    fi
}

# roundtrip <case name> <input>
roundtrip() {
    local out="$WORK/out/$1.roundtrip"
    if port "$ROUNDTRIP" "$2" "$out" 2> "$out.stderr" && cmp -s "$2" "$out"; then
        echo "PASS  $1 roundtrip"
        PASSED=$((PASSED+1))
    else
        echo "FAIL  $1 roundtrip"
        FAILED=$((FAILED+1))
        FAILURES+=("$1 roundtrip")
    fi
    rm -f "$out"
}

# track_dump <case name> <input (in the current directory)> <track option>...
# mp4dump --track: compares the standard output and the track data files
# (<input>.<track id>) written next to the input
track_dump() {
    local name="$1" f="$2" t
    shift 2
    rm -f "$f".[0-9]
    node "$REF/bin/mp4dump.js" "$@" "$f" > "$WORK/out/$name.expected" 2> /dev/null
    echo $? > "$WORK/out/$name.expected.rc"
    for t in 1 2; do rm -f "$WORK/out/$name.track$t.expected"; [ -f "$f.$t" ] && mv "$f.$t" "$WORK/out/$name.track$t.expected"; done
    port "$MP4DUMP" "$@" "$f" > "$WORK/out/$name" 2> /dev/null
    echo $? > "$WORK/out/$name.rc"
    for t in 1 2; do rm -f "$WORK/out/$name.track$t"; [ -f "$f.$t" ] && mv "$f.$t" "$WORK/out/$name.track$t"; done
    report "$name (stdout)" "$WORK/out/$name.expected" "$WORK/out/$name"
    for t in 1 2; do
        if [ -f "$WORK/out/$name.track$t.expected" ] || [ -f "$WORK/out/$name.track$t" ]; then
            report "$name (track $t data)" "$WORK/out/$name.track$t.expected" "$WORK/out/$name.track$t"
        fi
    done
}

#----------------------------------------------------------------------
# stock cases: port vs pre-generated expected outputs
#----------------------------------------------------------------------
echo "=== stock cases ==="
for f in "$DATA"/*.mp4; do
    n="$(basename "$f" .mp4)"
    for v in $VARIANTS; do
        run_port $v "$f" "$WORK/out/$n.$v"
        report "$n.$v" "$REF/expected/$n.$v" "$WORK/out/$n.$v"
    done
    roundtrip "$n" "$f"
done

#----------------------------------------------------------------------
# extra cases: generated inputs, original vs port
#----------------------------------------------------------------------
if [ "$BENTO4SHARP_SKIP_EXTRA" != "1" ] && [ -f "$REF/bin/mp4fragment.js" ] && command -v node > /dev/null; then
    echo "=== extra cases ==="
    GEN="$WORK/gen"
    cp "$DATA"/*.mp4 "$GEN/"
    # the emscripten tools only see the current directory: use relative names
    cd "$GEN" || exit 1
    : > "$WORK/gen.log"
    ref() {
        local tool="$1"; shift
        node "$REF/bin/$tool.js" "$@" >> "$WORK/gen.log" 2>&1 || echo "generation failed: $tool $*" >> "$WORK/gen.log"
    }
    K1=000102030405060708090a0b0c0d0e0f
    K2=101112131415161718191a1b1c1d1e1f
    KEY1=1:$K1:0001020304050607
    KEY2=2:$K2:1011121314151617

    # fragmented files
    ref mp4fragment video-h264-001.mp4 frag-default-video.mp4
    ref mp4fragment audio-aac-001.mp4 frag-default-audio.mp4
    ref mp4fragment --fragment-duration 500 video-h264-001.mp4 frag-500-video.mp4
    ref mp4fragment --fragment-duration 2000 --index audio-aac-001.mp4 frag-2000-index-audio.mp4
    ref mp4fragment --index video-h264-001.mp4 frag-index-video.mp4
    ref mp4fragment --track video --trun-version-zero --no-tfdt --timescale 10000000 video-h264-001.mp4 frag-smooth-video.mp4
    ref mp4fragment --copy-udta --sequence-number-start 7 --tfdt-start 10.5 video-h264-001.mp4 frag-udta-video.mp4
    # segments
    ref mp4split --init-segment split-init.mp4 --media-segment "split-seg-%llu.%04llu.m4s" frag-500-video.mp4
    ref mp4split --init-segment split-audio-init.mp4 --media-segment "split-audio-seg-%llu.%04llu.m4s" frag-2000-index-audio.mp4
    # edited files
    ref mp4extract moov/udta video-h264-001.mp4 udta.atom
    ref mp4extract moov/trak/mdia/minf/stbl/stsd audio-aac-001.mp4 stsd.atom
    ref mp4edit --remove moov/udta video-h264-001.mp4 edit-remove-udta.mp4
    ref mp4edit --insert moov:udta.atom audio-aac-001.mp4 edit-insert-udta.mp4
    ref mp4edit --insert moov/trak:udta.atom:0 --remove moov/trak/edts video-h264-001.mp4 edit-insert-remove.mp4
    ref mp4edit --replace moov/udta:stsd.atom video-h264-001.mp4 edit-replace.mp4
    # compacted files
    ref mp4compact audio-aac-001.mp4 compact-audio.mp4
    ref mp4compact video-h264-001.mp4 compact-video.mp4
    # encrypted files
    ref mp4encrypt --method MPEG-CENC --key $KEY1 --key $KEY2 frag-default-video.mp4 enc-cenc-frag-video.mp4
    ref mp4encrypt --method MPEG-CENC --key $KEY1 frag-default-audio.mp4 enc-cenc-frag-audio.mp4
    ref mp4encrypt --method MPEG-CENC --key $KEY1 --key $KEY2 --property 1:KID:00112233445566778899aabbccddeeff \
                   --pssh edef8ba979d64acea3c827dcd51d21ed:udta.atom frag-500-video.mp4 enc-cenc-pssh-video.mp4
    ref mp4encrypt --method MPEG-CENS --key $KEY1 --key $KEY2 frag-default-video.mp4 enc-cens-frag-video.mp4
    ref mp4encrypt --method MPEG-CBC1 --key $KEY1 --key $KEY2 frag-default-video.mp4 enc-cbc1-frag-video.mp4
    ref mp4encrypt --method MPEG-CBCS --key $KEY1 --key $KEY2 frag-default-video.mp4 enc-cbcs-frag-video.mp4
    ref mp4encrypt --method MPEG-CBCS --key $KEY1 frag-default-audio.mp4 enc-cbcs-frag-audio.mp4
    ref mp4encrypt --method MPEG-CENC --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-cenc-video.mp4
    ref mp4encrypt --method PIFF-CTR --key $KEY1 --key $KEY2 frag-default-video.mp4 enc-piff-ctr-frag-video.mp4
    ref mp4encrypt --method PIFF-CBC --key $KEY1 --key $KEY2 frag-default-video.mp4 enc-piff-cbc-frag-video.mp4
    ref mp4encrypt --method OMA-PDCF-CBC --key $KEY1 --property 1:ContentId:cid-1234 --property 1:RightsIssuerUrl:http://example.com/ri \
                   --property 1:Custom:value audio-aac-001.mp4 enc-oma-cbc-audio.mp4
    ref mp4encrypt --method OMA-PDCF-CTR --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-oma-ctr-video.mp4
    ref mp4encrypt --method ISMA-IAEC --key $KEY1 --kms-uri https://kms.example.com/key audio-aac-001.mp4 enc-isma-audio.mp4
    ref mp4encrypt --method ISMA-IAEC --key $KEY1 --key $KEY2 --kms-uri urn:example:kms video-h264-001.mp4 enc-isma-video.mp4
    ref mp4encrypt --method MARLIN-IPMP-ACBC --key $KEY1 audio-aac-001.mp4 enc-marlin-acbc-audio.mp4
    ref mp4encrypt --method MARLIN-IPMP-ACBC --key $KEY1 --key $KEY2 --property 1:ContentId:urn:marlin:organization:example:cid-1 video-h264-001.mp4 enc-marlin-acbc-video.mp4
    ref mp4encrypt --method MARLIN-IPMP-ACGK --key $KEY1 --key $KEY2 --key 0:202122232425262728292a2b2c2d2e2f:0000000000000000 video-h264-001.mp4 enc-marlin-acgk-video.mp4
    # synthetic files
    PYTHON="$(command -v python3 || command -v python)"
    if [ -n "$PYTHON" ] && "$PYTHON" -c "import sys; sys.exit(0 if sys.version_info[0] >= 3 else 1)" 2> /dev/null; then
        "$PYTHON" "$ROOT/tests/make-synthetic-inputs.py" . >> "$WORK/gen.log" 2>&1 || echo "generation failed: synthetic inputs" >> "$WORK/gen.log"
    else
        echo "(python 3 not found: no synthetic inputs)"
    fi

    for f in *.mp4 split-seg-1.0001.m4s split-seg-2.0001.m4s split-audio-seg-1.0001.m4s split-audio-seg-1.0003.m4s; do
        case "$f" in audio-aac-00[23].mp4|video-h264-002.mp4) continue ;; esac   # nothing new compared to the stock cases
        n="${f%.*}"
        if [ ! -s "$f" ] && [ "$n" != syn-empty ]; then
            echo "FAIL  $f (input was not generated, see $WORK/gen.log)"
            FAILED=$((FAILED+1))
            FAILURES+=("$f generation")
            continue
        fi
        for v in $EXTRA_VARIANTS; do
            case "$UNDEFINED_UPSTREAM" in *" $n.$v "*)
                echo "SKIP  $n.$v   (undefined behaviour in the original tool)"
                SKIPPED=$((SKIPPED+1))
                continue ;;
            esac
            run_reference $v "$f" "$WORK/out/$n.$v.expected"
            run_port $v "$f" "$WORK/out/$n.$v"
            report "$n.$v" "$WORK/out/$n.$v.expected" "$WORK/out/$n.$v"
        done
        case "$NO_ROUNDTRIP" in
            *" $n "*) ;;
            *) roundtrip "$n" "$f" ;;
        esac
    done

    # mp4dump --track (with and without decryption)
    for f in video-h264-001.mp4 audio-aac-002.mp4 frag-500-video.mp4; do
        track_dump "${f%.*}.dump.track" "$f" --track 1 --track 2 --format json
    done
    for f in enc-*.mp4; do
        [ -s "$f" ] || continue
        track_dump "${f%.*}.dump.track-decrypt" "$f" --track 1:$K1 --track 2:$K2
        track_dump "${f%.*}.dump.track-wrong-key" "$f" --track 1:$K2
        track_dump "${f%.*}.dump.track-no-key" "$f" --track 1
    done
    cd "$ROOT" || exit 1
else
    echo "=== extra cases skipped (no reference tools) ==="
fi

#----------------------------------------------------------------------
# summary
#----------------------------------------------------------------------
echo "=== summary: $PASSED passed, $FAILED failed, $SKIPPED skipped ==="
if [ $FAILED -ne 0 ]; then
    for f in "${FAILURES[@]}"; do echo "  failed: $f"; done
    exit 1
fi
exit 0
