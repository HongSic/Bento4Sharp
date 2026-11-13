#!/bin/bash
# Compares the ported tools of group T4 (mp42ts, mp42hls, mp4iframeindex) with
# the original Bento4 tools, byte for byte: every file written by the tool
# (TS files, segments, playlists, index files...), the standard output, the
# standard error and the exit code.
#
# usage: tests/compare-tools-T4.sh [<reference-dir> [<test-data-dir>]]
#
#   <reference-dir>  (or $BENTO4_REFERENCE_DIR) directory with
#                      bin/<tool>.js  the original tools built with emscripten, run with node:
#                          mp42ts mp42hls mp4iframeindex mp4fragment mp4encrypt mp4edit mp4mux mp42avc mp42aac
#   <test-data-dir>  (or $BENTO4_TEST_DATA_DIR) the Test/Data directory of the Bento4 sources (*.mp4)
#
# other environment variables:
#   BENTO4SHARP_ARTIFACTS   where to build (default: a temporary directory, removed on exit)
#   BENTO4SHARP_WORK_DIR    where to put generated inputs and outputs (default: a temporary directory)
#   BENTO4SHARP_SKIP_BUILD  set to 1 to reuse the binaries already in $BENTO4SHARP_ARTIFACTS
#   BENTO4SHARP_FRAMEWORK   target framework of the port to build and run (default: net6.0; net20 runs
#                           the .NET Framework 2.0 executables)
#   BENTO4SHARP_ONLY        only run the cases whose name matches this (bash) pattern, e.g. 'hls-*'
#   BENTO4SHARP_JOBS        number of cases run at the same time (default: 4)
#   BENTO4SHARP_KEEP        set to 1 to keep the outputs of the cases that pass
#
# Each case runs the original tool and the port with the same arguments, each
# in its own (otherwise identical) working directory, and compares everything.
#
# mp42hls --encryption-iv-mode random cannot be compared directly (random bytes
# are not available in the emscripten build of the original tool, which fails
# with "failed to get random IV"): see compare_random below.
#
# exit code: 0 if every case passes, 1 otherwise.

REF="${1:-$BENTO4_REFERENCE_DIR}"
DATA="${2:-$BENTO4_TEST_DATA_DIR}"
if [ -z "$REF" ] || [ -z "$DATA" ]; then
    sed -n "2,31p" "$0" >&2
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
WORK="$WORK/T4"
rm -rf "$WORK/cases" "$WORK/gen"
mkdir -p "$WORK/cases" "$WORK/gen"

if ! command -v node > /dev/null; then
    echo "node not found"
    exit 2
fi

#----------------------------------------------------------------------
# target framework of the port
#----------------------------------------------------------------------
FW="${BENTO4SHARP_FRAMEWORK:-net6.0}"

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
    for project in src/Tools/Mp42Ts/Mp42Ts.csproj src/Tools/Mp42Hls/Mp42Hls.csproj src/Tools/Mp4IframeIndex/Mp4IframeIndex.csproj; do
        if ! dotnet build "$ROOT/$project" -c Release -f "$FW" -nologo -v q -nodeReuse:false --artifacts-path "$ART" > "$WORK/build.log" 2>&1; then
            cat "$WORK/build.log"
            echo "BUILD FAILED: $project"
            exit 1
        fi
    done
fi

#----------------------------------------------------------------------
# helpers
#----------------------------------------------------------------------
PASSED=0; FAILED=0; SKIPPED=0
FAILURES=()
GEN="$WORK/gen"
JOBS="${BENTO4SHARP_JOBS:-4}"
PENDING=()

# run_case <case name> <tool> <argument>...
# Runs the original tool and the port, each in its own directory
# (<case>/ref/cwd and <case>/port/cwd) that contains a copy of each generated
# input named in the arguments; stdout, stderr and the exit code are stored
# next to it, so that one recursive comparison covers everything.
# The report is printed on stdout, the first line starts with PASS or FAIL.
run_case() {
    local name="$1" tool="$2" arg d files=() dirs=() count=0 f attempt setup=0
    shift 2
    d="$WORK/cases/$name"
    for arg in "$@" $CASE_FILES; do
        if [ -n "$arg" ] && [ -f "$GEN/$arg" ]; then
            for f in "${files[@]}"; do [ "$f" = "$GEN/$arg" ] && continue 2; done
            files+=("$GEN/$arg")
        fi
    done
    # (the working directories of a case have been seen to vanish while they
    # were being filled, on a busy Windows machine with a RAM disk: in that
    # case, and only in that case, the case is run again in another directory)
    for attempt in 1 2 3; do
        dirs=("$d/ref/cwd" "$d/port/cwd")
        for arg in $CASE_DIRS; do dirs+=("$d/ref/cwd/$arg" "$d/port/cwd/$arg"); done
        setup=1
        mkdir -p "${dirs[@]}" 2> /dev/null || setup=0
        if [ $setup = 1 ] && [ ${#files[@]} -ne 0 ]; then
            cp "${files[@]}" "$d/ref/cwd/" 2> /dev/null && cp "${files[@]}" "$d/port/cwd/" 2> /dev/null || setup=0
        fi
        if [ $setup = 1 ]; then
            # the emscripten tools only see the current directory: relative names only
            if cd "$d/ref/cwd" 2> /dev/null; then
                node "$REF/bin/$tool.js" "$@" > ../stdout 2> ../stderr
                echo $? > ../rc
            else
                setup=0
            fi
        fi
        if [ $setup = 1 ]; then
            if cd "$d/port/cwd" 2> /dev/null; then
                port "$PORT_DLL" "$@" > ../stdout 2> ../stderr
                echo $? > ../rc
            else
                setup=0
            fi
        fi
        cd "$WORK" || return
        [ $setup = 1 ] && break
        d="$WORK/cases/$name.attempt$((attempt+1))"
    done
    if [ $setup != 1 ]; then
        echo "FAIL  $name   (cannot prepare the working directories)"
        return
    fi
    if diff -r -q "$d/ref" "$d/port" > "$d/diff.txt" 2>&1; then
        for f in "$d"/ref/cwd/* "$d"/ref/cwd/*/*; do [ -f "$f" ] && count=$((count+1)); done
        echo "PASS  $name   ($count files)"
        [ "$BENTO4SHARP_KEEP" = "1" ] || rm -rf "$d" 2> /dev/null
    else
        echo "FAIL  $name   ($tool $*)"
        while read -r f; do echo "      $f"; done < "$d/diff.txt"
    fi
}

# flush: waits for the running cases and prints their reports in order
flush() {
    local name line first
    wait
    for name in "${PENDING[@]}"; do
        first=1
        while IFS= read -r line; do
            echo "$line"
            if [ $first = 1 ]; then
                first=0
                case "$line" in
                    PASS*) PASSED=$((PASSED+1)) ;;
                    *)     FAILED=$((FAILED+1)); FAILURES+=("$name") ;;
                esac
            fi
        done < "$WORK/cases/$name.report"
        if [ $first = 1 ]; then
            echo "FAIL  $name   (no report)"
            FAILED=$((FAILED+1)); FAILURES+=("$name")
        fi
        rm -f "$WORK/cases/$name.report"
    done
    PENDING=()
}

# compare <case name> <tool> <argument>...
# Schedules a case ($JOBS cases run at the same time).
# $CASE_DIRS: subdirectories to create in the working directories first.
# $CASE_FILES: more input files to copy.
compare() {
    local name="$1" PORT_DLL
    if [ -n "$BENTO4SHARP_ONLY" ]; then
        case "$name" in $BENTO4SHARP_ONLY) ;; *) return ;; esac
    fi
    case "$2" in
        mp42ts)         PORT_DLL="$ART/bin/Mp42Ts/release_$FW/mp42ts.dll" ;;
        mp42hls)        PORT_DLL="$ART/bin/Mp42Hls/release_$FW/mp42hls.dll" ;;
        mp4iframeindex) PORT_DLL="$ART/bin/Mp4IframeIndex/release_$FW/mp4iframeindex.dll" ;;
    esac
    PENDING+=("$name")
    ( run_case "$@" > "$WORK/cases/$name.report" 2>&1 ) &
    if [ ${#PENDING[@]} -ge "$JOBS" ]; then flush; fi
}

# compare_random <case name> <input> <option>...
# mp42hls with a random IV (the port only: random bytes are not available to
# the original tool here): the port is run with `--encryption-iv-mode random`,
# then the original tool is run in 'fps' mode with the IV that the port chose.
# The only expected difference is the IV attribute that the 'random' mode adds
# to the #EXT-X-KEY lines that are not preformatted: it is checked, then
# removed before the comparison.
compare_random() {
    local name="$1" input="$2" d iv ok=1 f
    shift 2
    if [ -n "$BENTO4SHARP_ONLY" ]; then
        case "$name" in $BENTO4SHARP_ONLY) ;; *) return ;; esac
    fi
    flush
    d="$WORK/cases/$name"
    mkdir -p "$d/ref/cwd" "$d/port/cwd"
    cp "$GEN/$input" "$d/ref/cwd/"
    cp "$GEN/$input" "$d/port/cwd/"
    cd "$d/port/cwd" || return
    port "$ART/bin/Mp42Hls/release_$FW/mp42hls.dll" --encryption-key $KEY --encryption-iv-mode random "$@" "$input" > ../stdout 2> ../stderr
    echo $? > ../rc
    iv=""
    while IFS= read -r f; do
        if [[ "$f" =~ ^#EXT-X-KEY:.*IV=0x([0-9A-F]{32}) ]]; then iv="${BASH_REMATCH[1]}"; break; fi
    done < stream.m3u8
    cd "$d/ref/cwd" || return
    node "$REF/bin/mp42hls.js" --encryption-key "$KEY$iv" --encryption-iv-mode fps "$@" "$input" > ../stdout 2> ../stderr
    echo $? > ../rc
    cd "$WORK" || return
    [ ${#iv} = 32 ] || { ok=0; echo "      no IV in the playlist of the port"; }
    [ "$iv" != 00000000000000000000000000000000 ] || { ok=0; echo "      the IV is not random"; }
    for f in "$d"/port/cwd/*.m3u8; do
        grep -q "^#EXT-X-KEY:.*IV=0x$iv" "$f" || { ok=0; echo "      no IV in $f"; }
        sed -e "/^#EXT-X-KEY:/s/,IV=0x$iv//" "$f" > "$f.tmp" && mv "$f.tmp" "$f"
    done
    for f in "$d"/ref/cwd/*.m3u8; do
        sed -e "/^#EXT-X-KEY:/s/,IV=0x$iv//" "$f" > "$f.tmp" && mv "$f.tmp" "$f"
    done
    diff -r -q "$d/ref" "$d/port" > "$d/diff.txt" 2>&1 || ok=0
    if [ $ok = 1 ]; then
        echo "PASS  $name   (IV $iv)"
        PASSED=$((PASSED+1))
        [ "$BENTO4SHARP_KEEP" = "1" ] || rm -rf "$d" 2> /dev/null
    else
        echo "FAIL  $name"
        while read -r f; do echo "      $f"; done < "$d/diff.txt"
        FAILED=$((FAILED+1)); FAILURES+=("$name")
    fi
}

#----------------------------------------------------------------------
# inputs
#----------------------------------------------------------------------
cp "$DATA"/*.mp4 "$GEN/"
: > "$WORK/gen.log"
ref() {
    local tool="$1"; shift
    (cd "$GEN" && node "$REF/bin/$tool.js" "$@") >> "$WORK/gen.log" 2>&1 || echo "generation failed: $tool $*" >> "$WORK/gen.log"
}
# fragmented versions of the stock files
ref mp4fragment video-h264-001.mp4 frag-video.mp4
ref mp4fragment --fragment-duration 500 video-h264-001.mp4 frag-500-video.mp4
ref mp4fragment audio-aac-001.mp4 frag-audio.mp4
ref mp4fragment --fragment-duration 1500 --index audio-aac-001.mp4 frag-1500-audio.mp4
ref mp4fragment --track video video-h264-001.mp4 frag-video-only.mp4
ref mp4fragment --track audio video-h264-001.mp4 frag-audio-only.mp4
# a longer movie with several key frames: 8 times the video of video-h264-001.mp4 (at 24 fps: 18 seconds,
# one key frame every 2.25 seconds) and the audio of audio-aac-001.mp4 (30 seconds)
ref mp42avc video-h264-001.mp4 short.264
ref mp42aac audio-aac-001.mp4 long.aac
cat "$GEN/short.264" "$GEN/short.264" "$GEN/short.264" "$GEN/short.264" "$GEN/short.264" "$GEN/short.264" "$GEN/short.264" "$GEN/short.264" > "$GEN/long.264"
ref mp4mux --track h264:long.264 --track aac:long.aac long-av.mp4
ref mp4mux --track h264:long.264 long-video-only.mp4
ref mp4mux --track aac:long.aac --track h264:long.264#frame_rate=29.97 long-audio-first.mp4
ref mp4fragment long-av.mp4 frag-long-av.mp4
ref mp4fragment --fragment-duration 1000 long-av.mp4 frag-1000-long-av.mp4
ref mp4fragment long-video-only.mp4 frag-long-video-only.mp4
# files that the tools refuse
ref mp4encrypt --method MPEG-CENC --key 1:000102030405060708090a0b0c0d0e0f:0001020304050607 --key 2:101112131415161718191a1b1c1d1e1f:1011121314151617 frag-video.mp4 enc-cenc-frag-video.mp4
ref mp4encrypt --method OMA-PDCF-CBC --key 1:000102030405060708090a0b0c0d0e0f:0001020304050607 audio-aac-001.mp4 enc-oma-audio.mp4
ref mp4edit --remove moov video-h264-001.mp4 no-moov.mp4
ref mp4edit --remove moov/trak --remove moov/trak video-h264-001.mp4 no-tracks.mp4
head -c 20000 "$GEN/video-h264-001.mp4" > "$GEN/truncated-video.mp4"
head -c 100000 "$GEN/frag-long-av.mp4" > "$GEN/truncated-frag-long-av.mp4"
printf 'this is not an mp4 file' > "$GEN/not-mp4.mp4"
: > "$GEN/empty.mp4"

STOCK="audio-aac-001 audio-aac-002 audio-aac-003 video-h264-001 video-h264-002"
GENERATED="frag-video frag-500-video frag-audio frag-1500-audio frag-video-only frag-audio-only long-av long-video-only long-audio-first frag-long-av frag-1000-long-av frag-long-video-only"
for n in $GENERATED enc-cenc-frag-video enc-oma-audio no-moov no-tracks; do
    if [ ! -s "$GEN/$n.mp4" ]; then
        echo "FAIL  $n.mp4 (input was not generated, see $WORK/gen.log)"
        FAILED=$((FAILED+1))
        FAILURES+=("$n.mp4 generation")
    fi
done

# synthetic files: the codecs and structures that the stock files do not have (see tests/make-synthetic-inputs-T4.py)
SYNTHETIC=""
PYTHON="$(command -v python3 || command -v python)"
if [ -n "$PYTHON" ] && "$PYTHON" -c "import sys; sys.exit(0 if sys.version_info[0] >= 3 else 1)" 2> /dev/null; then
    if "$PYTHON" "$ROOT/tests/make-synthetic-inputs-T4.py" "$GEN" >> "$WORK/gen.log" 2>&1; then
        for n in hevc-aac avc2-ec3 dvh1-ac3 avc-big-sbr ac4 two-avc; do
            ref mp4fragment --fragment-duration 700 t4-$n.mp4 t4-frag-$n.mp4
        done
        SYNTHETIC="$(cd "$GEN" && echo t4-*.mp4)"
    else
        echo "FAIL  synthetic inputs (see $WORK/gen.log)"
        FAILED=$((FAILED+1))
        FAILURES+=("synthetic inputs generation")
    fi
else
    echo "(python 3 not found: no synthetic inputs)"
fi

#----------------------------------------------------------------------
# cases
#----------------------------------------------------------------------
echo "=== mp42ts ==="
for n in $STOCK $GENERATED; do
    f=$n.mp4
    compare ts-$n-default      mp42ts $f out.ts
    compare ts-$n-verbose      mp42ts --verbose $f out.ts
    compare ts-$n-pids         mp42ts --pmt-pid 4096 --audio-pid 257 --video-pid 258 $f out.ts
    compare ts-$n-pids2        mp42ts --pmt-pid 32 --audio-pid 8190 --video-pid 33 --pcr-offset 0 $f out.ts
    compare ts-$n-pcr          mp42ts --pcr-offset 123456 $f out.ts
    compare ts-$n-segment      mp42ts --segment 2 --verbose $f seg-%d.ts
    compare ts-$n-segment-pl   mp42ts --segment 4 --playlist list.m3u8 --verbose $f seg-%03d.ts
    compare ts-$n-segment-thr  mp42ts --segment 3 --segment-duration-threshold 800 --playlist list.m3u8 --verbose $f seg-%d.ts
    compare ts-$n-playlist     mp42ts --playlist list.m3u8 $f out.ts
    for v in 1 2 3 4 7; do
        compare ts-$n-segment-plv$v mp42ts --segment 5 --playlist list.m3u8 --playlist-hls-version $v $f seg-%d.ts
    done
done

echo "=== mp4iframeindex ==="
for n in $STOCK $GENERATED; do
    f=$n.mp4
    compare iframe-$n-stdout   mp4iframeindex $f
    compare iframe-$n-file     mp4iframeindex $f index.json
    compare iframe-$n-track1   mp4iframeindex --track 1 $f
    compare iframe-$n-track2   mp4iframeindex --track 2 $f index.json
    compare iframe-$n-track9   mp4iframeindex --track 9 $f index.json
done

echo "=== mp42hls ==="
KEY=000102030405060708090a0b0c0d0e0f
KEYIV=000102030405060708090a0b0c0d0e0ff0e1d2c3b4a5968778695a4b3c2d1e0f
for n in $STOCK $GENERATED; do
    f=$n.mp4
    compare hls-$n-default         mp42hls $f
    compare hls-$n-verbose         mp42hls --verbose $f
    compare hls-$n-info            mp42hls --show-info $f
    compare hls-$n-info-verbose    mp42hls --show-info --verbose --hls-version 4 $f
    compare hls-$n-seg2            mp42hls --segment-duration 2 --verbose $f
    compare hls-$n-seg1-thr        mp42hls --segment-duration 1 --segment-duration-threshold 600 --verbose --show-info $f
    compare hls-$n-seg0            mp42hls --segment-duration 0 --verbose $f
    for v in 1 2 3 4 5 6 12; do
        compare hls-$n-v$v         mp42hls --hls-version $v --segment-duration 3 $f
    done
    compare hls-$n-single          mp42hls --output-single-file --verbose --segment-duration 2 $f
    compare hls-$n-single-v3       mp42hls --output-single-file --hls-version 3 --show-info $f
    compare hls-$n-packed          mp42hls --audio-format packed --verbose --segment-duration 4 $f
    compare hls-$n-packed-single   mp42hls --audio-format packed --output-single-file --hls-version 2 $f
    compare hls-$n-ts              mp42hls --audio-format ts --segment-duration 10 $f
    compare hls-$n-iframes         mp42hls --iframe-index-filename if.m3u8 --hls-version 3 --segment-duration 2 $f
    compare hls-$n-pcr0            mp42hls --pcr-offset 0 $f
    compare hls-$n-pcr             mp42hls --pcr-offset 900000 --segment-duration 3 $f
    compare hls-$n-pids            mp42hls --pmt-pid 500 --audio-pid 501 --video-pid 8000 $f
    CASE_DIRS=out \
    compare hls-$n-names           mp42hls --index-filename out/index.m3u8 --iframe-index-filename out/i.m3u8 --segment-filename-template out/s%03d.ts \
                                           --segment-url-template "http://example.com/media/s%03d.ts?x=%%" --segment-duration 2 --hls-version 4 $f
    compare hls-$n-names-single    mp42hls --index-filename index.m3u8 --segment-filename-template all.bin --segment-url-template media/all.bin --output-single-file $f
    compare hls-$n-cache-yes       mp42hls --allow-cache YES $f
    compare hls-$n-cache-no        mp42hls --allow-cache NO --hls-version 2 $f
    compare hls-$n-no-audio        mp42hls --audio-track-id 0 --verbose $f
    compare hls-$n-no-video        mp42hls --video-track-id 0 --verbose $f
    compare hls-$n-tracks12        mp42hls --video-track-id 1 --audio-track-id 2 --segment-duration 2 $f
    compare hls-$n-tracks21        mp42hls --video-track-id 2 --audio-track-id 1 --segment-duration 2 $f
    compare hls-$n-audio1          mp42hls --audio-track-id 1 --video-track-id 0 $f
    compare hls-$n-video9          mp42hls --video-track-id 9 $f
    # encryption (the IV is never random here)
    compare hls-$n-aes128          mp42hls --encryption-key $KEY --segment-duration 2 $f
    compare hls-$n-aes128-seq      mp42hls --encryption-mode AES-128 --encryption-iv-mode sequence --encryption-key $KEY \
                                           --encryption-key-uri "https://example.com/keys/key1.bin" --hls-version 4 --verbose --segment-duration 3 $f
    compare hls-$n-aes128-fps      mp42hls --encryption-iv-mode fps --encryption-key $KEYIV --segment-duration 2 --verbose $f
    compare hls-$n-aes128-single   mp42hls --encryption-key $KEY --output-single-file --verbose --segment-duration 2 --show-info $f
    compare hls-$n-aes128-packed   mp42hls --encryption-key $KEY --audio-format packed --segment-duration 5 $f
    compare hls-$n-aes128-keyfmt   mp42hls --encryption-key $KEY --encryption-key-format com.example.keys --encryption-key-format-versions 1/2 --hls-version 3 $f
    compare hls-$n-aes128-keyline  mp42hls --encryption-key $KEY --encryption-key-line "URI=\"key://one\"" --encryption-key-line "!URI=\"key://two\",KEYFORMAT=\"two\"" --encryption-key-line "!" $f
    compare hls-$n-saes-seq        mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode sequence --encryption-key $KEY --segment-duration 2 --verbose $f
    compare hls-$n-saes-fps        mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV --verbose $f
    compare hls-$n-saes-fps-v3     mp42hls --hls-version 3 --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV --segment-duration 3 \
                                           --encryption-key-format com.apple.streamingkeydelivery --encryption-key-uri skd://key1 $f
    compare hls-$n-saes-single     mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV --output-single-file --segment-duration 2 $f
    compare hls-$n-saes-packed     mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode sequence --encryption-key $KEY --audio-format packed --segment-duration 4 $f
    compare hls-$n-saes-packed-one mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV --audio-format packed --output-single-file $f
    compare hls-$n-saes-keyline    mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV \
                                           --encryption-key-line "URI=\"skd://one\",KEYFORMAT=\"com.apple.streamingkeydelivery\",KEYFORMATVERSIONS=\"1\"" \
                                           --encryption-key-line "!URI=\"data:text/plain;base64,AAAA\"" $f
    compare hls-$n-saes-no-video   mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode sequence --encryption-key $KEY --video-track-id 0 --segment-duration 5 $f
    compare hls-$n-saes-no-audio   mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode sequence --encryption-key $KEY --audio-track-id 0 --segment-duration 2 $f
done

echo "=== mp42hls, random IV ==="
compare_random hls-random-aes128        long-av.mp4 --segment-duration 2 --hls-version 4 --verbose
compare_random hls-random-aes128-single audio-aac-001.mp4 --output-single-file --encryption-key-uri "https://example.com/key"
compare_random hls-random-saes          long-av.mp4 --encryption-mode SAMPLE-AES --segment-duration 3 --show-info
compare_random hls-random-saes-packed   frag-long-av.mp4 --encryption-mode SAMPLE-AES --audio-format packed --encryption-key-format-versions 1
compare_random hls-random-saes-keyline  video-h264-001.mp4 --encryption-mode SAMPLE-AES --encryption-key-line "URI=\"skd://one\"" --encryption-key-line "!URI=\"skd://two\""

echo "=== synthetic inputs ==="
for f in $SYNTHETIC; do
    n="${f%.mp4}"
    compare ts-$n-default          mp42ts --verbose $f out.ts
    compare ts-$n-segment          mp42ts --segment 1 --playlist list.m3u8 --verbose $f seg-%d.ts
    compare iframe-$n              mp4iframeindex $f
    compare hls-$n-verbose         mp42hls --verbose --segment-duration 1 $f
    compare hls-$n-info            mp42hls --show-info $f
    compare hls-$n-single          mp42hls --hls-version 4 --output-single-file --show-info --segment-duration 1 $f
    compare hls-$n-packed          mp42hls --audio-format packed --segment-duration 2 --verbose $f
    compare hls-$n-packed-single   mp42hls --audio-format packed --output-single-file --segment-duration 1 $f
    compare hls-$n-aes128          mp42hls --encryption-key $KEY --segment-duration 1 --verbose $f
    compare hls-$n-saes-seq        mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode sequence --encryption-key $KEY --segment-duration 1 --verbose $f
    compare hls-$n-saes-fps-single mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV --output-single-file --segment-duration 1 $f
    compare hls-$n-saes-packed     mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV --audio-format packed --segment-duration 1 $f
    compare hls-$n-saes-no-audio   mp42hls --encryption-mode SAMPLE-AES --encryption-iv-mode sequence --encryption-key $KEY --audio-track-id 0 --show-info $f
done

echo "=== error cases ==="
A=audio-aac-001.mp4
V=video-h264-001.mp4
compare err-ts-usage             mp42ts
compare err-ts-usage1            mp42ts $V
compare err-ts-no-output         mp42ts --verbose $V
compare err-ts-no-input          mp42ts --verbose --verbose
compare err-ts-extra             mp42ts $V out.ts extra
for o in --segment --segment-duration-threshold --pmt-pid --audio-pid --video-pid --pcr-offset --playlist --playlist-hls-version; do
    compare err-ts-missing$o     mp42ts $V out.ts $o
done
compare err-ts-plv0              mp42ts --playlist-hls-version 0 $V out.ts
compare err-ts-plvx              mp42ts --playlist-hls-version x $V out.ts
compare err-ts-numbers           mp42ts --segment " +2x" --pmt-pid -1 --audio-pid 99999999999 --video-pid 0x20 --pcr-offset abc --verbose long-av.mp4 "s%d.ts"
compare err-ts-big-pids          mp42ts --pmt-pid 70000 --audio-pid 65537 --video-pid 8191 $V out.ts
compare err-ts-same-pids         mp42ts --pmt-pid 0 --audio-pid 256 --video-pid 256 $V out.ts
compare err-ts-nofile            mp42ts missing.mp4 out.ts
compare err-ts-nodir             mp42ts $V missing/out.ts
compare err-ts-nodir-playlist    mp42ts --playlist missing/list.m3u8 $V out.ts
compare err-ts-playlist-pattern  mp42ts --playlist "list-%d.m3u8" --segment 1 long-av.mp4 "s-%#06x.ts"
compare err-ts-pattern-u         mp42ts --playlist "list-%%.m3u8" --segment 2 long-av.mp4 "s-%-4u-.ts"
compare err-ts-pattern-c         mp42ts --playlist list.m3u8 $V "out%c.ts"
compare err-ts-no-pattern        mp42ts --segment 2 --playlist list.m3u8 --verbose long-av.mp4 out.ts
compare err-ts-threshold-big     mp42ts --segment 2 --segment-duration-threshold 5000 --playlist list.m3u8 --verbose long-av.mp4 "s%d.ts"
compare err-ts-segment-big       mp42ts --segment 1000 --playlist list.m3u8 --playlist-hls-version 2 --verbose long-av.mp4 "s%d.ts"
for n in not-mp4 empty no-moov no-tracks truncated-video truncated-frag-long-av enc-cenc-frag-video enc-oma-audio; do
    compare err-ts-$n            mp42ts --verbose $n.mp4 out.ts
    compare err-hls-$n           mp42hls --verbose $n.mp4
    compare err-hls-$n-packed    mp42hls --audio-format packed --show-info $n.mp4
    compare err-iframe-$n        mp4iframeindex $n.mp4
done

compare err-iframe-usage         mp4iframeindex
compare err-iframe-missing-track mp4iframeindex $V --track
compare err-iframe-no-input      mp4iframeindex --track 1
compare err-iframe-extra         mp4iframeindex $V out.json extra
compare err-iframe-nofile        mp4iframeindex missing.mp4
compare err-iframe-nofile-out    mp4iframeindex missing.mp4 out.json
compare err-iframe-nodir         mp4iframeindex $V missing/out.json
compare err-iframe-track-x       mp4iframeindex --track x $V
compare err-iframe-track-neg     mp4iframeindex --track -1 $V
compare err-iframe-audio         mp4iframeindex $A
compare err-iframe-audio-track   mp4iframeindex --track 2 --track 1 long-av.mp4 out.json
compare iframe-fragments-info    mp4iframeindex --fragments-info frag-video.mp4 frag-video.mp4
CASE_FILES=$V \
compare iframe-fragments-info2   mp4iframeindex --fragments-info $V --track 1 frag-video.mp4 out.json
CASE_FILES=frag-long-av.mp4 \
compare iframe-fragments-info3   mp4iframeindex --fragments-info frag-long-av.mp4 no-moov.mp4
compare err-iframe-fragments-info-nofile mp4iframeindex --fragments-info missing.mp4 $V

compare err-hls-usage            mp42hls
compare err-hls-no-input         mp42hls --verbose
compare err-hls-extra            mp42hls $V extra
for o in --hls-version --segment-duration --segment-duration-threshold --segment-filename-template --segment-url-template --allow-cache \
         --pmt-pid --audio-pid --video-pid --audio-track-id --audio-format --video-track-id --pcr-offset --index-filename \
         --iframe-index-filename --encryption-key --encryption-mode --encryption-iv-mode --encryption-key-uri --encryption-key-format \
         --encryption-key-format-versions --encryption-key-line; do
    compare err-hls-missing$o    mp42hls $V $o
done
compare err-hls-version0         mp42hls --hls-version 0 $V
compare err-hls-allow-cache      mp42hls --allow-cache yes $V
compare err-hls-audio-format     mp42hls --audio-format aac $V
compare err-hls-key-short        mp42hls --encryption-key 0011 $V
compare err-hls-key-nothex       mp42hls --encryption-key 000102030405060708090a0b0c0d0e0g $V
compare err-hls-key-long         mp42hls --encryption-key ${KEY}ffzz --segment-duration 2 $V
compare err-hls-key-upper        mp42hls --encryption-key 000102030405060708090A0B0C0D0E0F --encryption-mode AES-128-CBC --encryption-iv-mode sequence-numbers $V
compare err-hls-mode             mp42hls --encryption-mode aes-128 --encryption-key $KEY $V
compare err-hls-mode-prefix      mp42hls --encryption-mode SAMPLE-AES-CTR --encryption-iv-mode fpsx --encryption-key $KEYIV $V
compare err-hls-iv-mode          mp42hls --encryption-iv-mode none --encryption-key $KEY $V
compare err-hls-mode-no-key      mp42hls --encryption-mode AES-128 $V
compare err-hls-keyline-no-key   mp42hls --encryption-key-line "URI=\"x\"" $V
compare err-hls-fps-short        mp42hls --encryption-iv-mode fps --encryption-key $KEY $V
compare err-hls-fps-long         mp42hls --encryption-iv-mode fps --encryption-key ${KEYIV}00 $V
compare err-hls-fps-bad-iv       mp42hls --encryption-iv-mode fps --encryption-key ${KEY}f0e1d2c3b4a5968778695a4b3c2d1exx $V
compare err-hls-iv-mode-only     mp42hls --encryption-iv-mode sequence $V
compare err-hls-key-twice        mp42hls --encryption-key $KEYIV --encryption-key $KEY --encryption-mode SAMPLE-AES --encryption-mode AES-128 --segment-duration 2 $V
compare err-hls-saes-v4-iframes  mp42hls --hls-version 2 --encryption-mode SAMPLE-AES --encryption-iv-mode fps --encryption-key $KEYIV --iframe-index-filename i.m3u8 \
                                         --output-single-file --encryption-key-format-versions 1 --show-info --verbose $V
compare err-hls-nofile           mp42hls missing.mp4
compare err-hls-nodir-index      mp42hls --index-filename missing/index.m3u8 $V
compare err-hls-nodir-iframes    mp42hls --iframe-index-filename missing/i.m3u8 $V
compare err-hls-nodir-segments   mp42hls --segment-filename-template "missing/s%d.ts" --show-info $V
compare err-hls-audio-track-bad  mp42hls --audio-track-id 1 $V
compare err-hls-video-track-bad  mp42hls --video-track-id 2 $V
compare err-hls-audio-track-none mp42hls --audio-track-id 7 $V
compare err-hls-tracks-none      mp42hls --audio-track-id 0 --video-track-id 0 $V
compare err-hls-track-negative   mp42hls --audio-track-id -1 --video-track-id -1 $V
compare err-hls-track-negative2  mp42hls --audio-track-id -2 $V
compare err-hls-packed-no-audio  mp42hls --audio-format packed --audio-track-id 0 $V
compare err-hls-packed-video     mp42hls --audio-format packed long-video-only.mp4
compare err-hls-numbers          mp42hls --segment-duration " 2abc" --segment-duration-threshold -1 --pmt-pid 99999999999 --audio-pid x --video-pid 0x30 --pcr-offset -5 --verbose long-av.mp4
compare err-hls-big-pids         mp42hls --pmt-pid 70000 --audio-pid 65537 --video-pid 8191 $V
compare err-hls-patterns         mp42hls --segment-filename-template "s-%+5d%%.ts" --segment-url-template "u-%#o" --segment-duration 2 long-av.mp4
compare err-hls-patterns2        mp42hls --segment-filename-template "s-%.3i.ts" --segment-url-template "u-% d-%%d" --segment-duration 2 long-av.mp4
compare err-hls-patterns3        mp42hls --segment-filename-template "s-%X.ts" --segment-url-template "u-%-6u|" --segment-duration 1 --audio-track-id 0 long-av.mp4
compare err-hls-pattern-c        mp42hls --segment-filename-template "seg%c.ts" --segment-url-template "url%3c.ts" $V
compare err-hls-pattern-single   mp42hls --output-single-file --segment-filename-template "s%d.ts" --segment-url-template "u%d.ts" --segment-duration 2 long-av.mp4
compare err-hls-index-pattern    mp42hls --index-filename "index-%d.m3u8" --iframe-index-filename "i-%d.m3u8" --hls-version 4 $V
compare err-hls-threshold-big    mp42hls --segment-duration 2 --segment-duration-threshold 5000 --verbose long-av.mp4
compare err-hls-same-names       mp42hls --index-filename same.m3u8 --iframe-index-filename same.m3u8 --hls-version 4 $V

#----------------------------------------------------------------------
# summary
#----------------------------------------------------------------------
flush
echo "=== summary: $PASSED passed, $FAILED failed, $SKIPPED skipped ==="
if [ $FAILED -ne 0 ]; then
    for f in "${FAILURES[@]}"; do echo "  failed: $f"; done
    exit 1
fi
exit 0
