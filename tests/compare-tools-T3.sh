#!/bin/bash
# Compares the ported tools of group T3 (mp4mux, aac2mp4, fixaacsampledescription)
# with the original Bento4 tools: same arguments, then the output file, the
# standard output, the standard error and the exit code are compared byte for
# byte.
#
# usage: tests/compare-tools-T3.sh [<reference-dir> [<test-data-dir>]]
#
#   <reference-dir>  (or $BENTO4_REFERENCE_DIR) directory with
#                      bin/<tool>.js  the original tools built with emscripten, run with node:
#                          mp4mux aac2mp4 fixaacsampledescription mp42aac mp42avc
#   <test-data-dir>  (or $BENTO4_TEST_DATA_DIR) the Test/Data directory of the Bento4 sources (*.mp4)
#
# other environment variables:
#   BENTO4SHARP_ARTIFACTS   where to build (default: a temporary directory, removed on exit)
#   BENTO4SHARP_WORK_DIR    where to put generated inputs and outputs (default: a temporary directory)
#   BENTO4SHARP_SKIP_BUILD  set to 1 to reuse the binaries already in $BENTO4SHARP_ARTIFACTS
#   BENTO4SHARP_FRAMEWORK   target framework of the port to build and run (default: net6.0; net20 runs
#                           the .NET Framework 2.0 executables)
#
# The inputs (H.264 Annex B and AAC ADTS elementary streams) are extracted from
# the test data with the original mp42avc and mp42aac. A few more are derived
# from them (truncated, corrupted and concatenated streams, hand-made AC-3 and
# E-AC-3 frames, an MP4 file that declares MPEG-2 AAC LC audio).
#
# mp4mux stores the current time in the creation and modification times of the
# 'mvhd' atom (and nowhere else: the track and media times are 0 or copied from
# the input), so differences in those 8 bytes, and only those, are accepted
# when the times written by the two tools are close to each other.
#
# exit code: 0 if every case passes, 1 otherwise.

REF="${1:-$BENTO4_REFERENCE_DIR}"
DATA="${2:-$BENTO4_TEST_DATA_DIR}"
if [ -z "$REF" ] || [ -z "$DATA" ]; then
    sed -n "2,31p" "$0" >&2
    exit 2
fi
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REF="$(cd "$REF" && pwd)"
DATA="$(cd "$DATA" && pwd)"

CLEANUP=()
cleanup() { for d in "${CLEANUP[@]}"; do rm -rf "$d"; done; }
trap cleanup EXIT

ART="$BENTO4SHARP_ARTIFACTS"
if [ -z "$ART" ]; then ART="$(mktemp -d)"; CLEANUP+=("$ART"); fi
WORK="$BENTO4SHARP_WORK_DIR"
if [ -z "$WORK" ]; then WORK="$(mktemp -d)"; CLEANUP+=("$WORK"); fi
OUT="$WORK/t3-out"
GEN="$WORK/t3-gen"
rm -rf "$OUT" "$GEN"
mkdir -p "$OUT/expected" "$OUT/actual" "$GEN"

for tool in mp4mux aac2mp4 fixaacsampledescription mp42aac mp42avc; do
    if [ ! -f "$REF/bin/$tool.js" ]; then
        echo "original tool not found: $REF/bin/$tool.js"
        exit 1
    fi
done
if ! command -v node > /dev/null; then
    echo "node not found"
    exit 1
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
    for project in src/Tools/Mp4Mux/Mp4Mux.csproj src/Tools/Aac2Mp4/Aac2Mp4.csproj src/Tools/FixAacSampleDescription/FixAacSampleDescription.csproj; do
        if ! dotnet build "$ROOT/$project" -c Release -f "$FW" -nologo -v q --artifacts-path "$ART" > "$WORK/t3-build.log" 2>&1; then
            cat "$WORK/t3-build.log"
            echo "BUILD FAILED: $project"
            exit 1
        fi
    done
fi

# port_dll <tool>: sets $DLL
port_dll() {
    case "$1" in
        mp4mux)                  DLL="$ART/bin/Mp4Mux/release_$FW/mp4mux.dll" ;;
        aac2mp4)                 DLL="$ART/bin/Aac2Mp4/release_$FW/aac2mp4.dll" ;;
        fixaacsampledescription) DLL="$ART/bin/FixAacSampleDescription/release_$FW/fixaacsampledescription.dll" ;;
    esac
}

#----------------------------------------------------------------------
# helpers
#----------------------------------------------------------------------
PASSED=0; FAILED=0; SKIPPED=0
FAILURES=()

# (few external commands are used here: starting one is slow on Windows. This
# is why the results are only compared, and printed, once all the cases have run)
CASES=()            # case names, and section titles, in order
declare -A MASK     # case name -> yes when the mvhd times must be ignored

# collect <prefix> <output name>: moves what a tool wrote to <prefix>.out and
# records in <prefix>.files which files it left behind
collect() {
    local prefix="$1" output="$2" files=""
    if [ -n "$output" ]; then
        if [ -e "${output}_" ]; then files="$files temporary-file"; rm -rf "${output}_"; fi
        if [ -f "$output" ]; then files="$files output"; mv "$output" "$prefix.out"; fi
    fi
    echo "$files" > "$prefix.files"
    [ -f "$prefix.out" ] || : > "$prefix.out"
}

# compare_case <case name> <tool> <mask: yes|no> <output name> <argument>...
# runs the original tool and the port in the current directory with the same
# arguments; <output name> is the file that the arguments tell the tool to
# write ('' when there is none)
compare_case() {
    local name="$1" tool="$2" mask="$3" output="$4"
    shift 4
    local e="$OUT/expected/$name" a="$OUT/actual/$name"
    port_dll "$tool"
    CASES+=("$name")
    MASK[$name]="$mask"

    [ -n "$output" ] && [ -f "$output" ] && rm -f "$output"
    node "$REF/bin/$tool.js" "$@" > "$e.stdout" 2> "$e.stderr"
    echo $? > "$e.rc"
    collect "$e" "$output"
    port "$DLL" "$@" > "$a.stdout" 2> "$a.stderr"
    echo $? > "$a.rc"
    collect "$a" "$output"
}

section() {
    CASES+=("=== $1 ===")
}

skip() {
    CASES+=("SKIP  $1   ($2)")
}

# mvhd_times <file>: sets $MVHD to the offset of the 'mvhd' atom of a file that
# starts with 'ftyp' 'moov'('mvhd' ...), and $CREATION and $MODIFICATION to its
# times; $MVHD is empty for other files
mvhd_times() {
    local hex ftyp
    MVHD=""
    hex=$(od -An -v -tx1 -N 128 "$1")
    hex="${hex//[^0-9a-f]/}"
    [ "${hex:8:8}" = 66747970 ] || return                 # 'ftyp'
    ftyp=$((16#${hex:0:8}))
    [ $ftyp -ge 8 ] && [ ${#hex} -ge $(((ftyp+28)*2)) ] || return
    [ "${hex:$(((ftyp+4)*2)):8}" = 6d6f6f76 ] || return   # 'moov'
    [ "${hex:$(((ftyp+12)*2)):8}" = 6d766864 ] || return  # 'mvhd'
    [ "${hex:$(((ftyp+16)*2)):8}" = 00000000 ] || return  # version 0: 32-bit times
    MVHD=$((ftyp+8))
    CREATION=$((16#${hex:$(((ftyp+20)*2)):8}))
    MODIFICATION=$((16#${hex:$(((ftyp+24)*2)):8}))
}

# same_but_mvhd_times <expected> <actual>: succeeds when the two files only
# differ by the creation and modification times of the 'mvhd' atom, and those
# are plausible (creation == modification, the two tools ran within minutes)
same_but_mvhd_times() {
    local eo et em offset rest
    mvhd_times "$1"; eo="$MVHD"; et="$CREATION"; em="$MODIFICATION"
    mvhd_times "$2"
    [ -n "$eo" ] && [ "$eo" = "$MVHD" ] || return 1
    [ "$et" = "$em" ] && [ "$CREATION" = "$MODIFICATION" ] || return 1
    [ $((CREATION-et)) -ge 0 ] && [ $((CREATION-et)) -le 600 ] || return 1
    # cmp -l: one line per differing byte (offset from 1), or a message when the sizes differ
    while read -r offset rest; do
        case "$offset" in *[!0-9]*|"") return 1 ;; esac
        [ "$offset" -gt $((eo+12)) ] && [ "$offset" -le $((eo+20)) ] || return 1
    done < <(cmp -l "$1" "$2" 2>&1)
    return 0
}

# report_cases: compares what the two tools wrote in each case (out: the output
# file, stdout, stderr, rc: the exit code, files: which files were left behind)
report_cases() {
    local -A differ
    local line file name kind what
    while IFS= read -r line; do
        case "$line" in
            "Files $OUT/expected/"*" differ")
                line="${line#Files $OUT/expected/}"
                file="${line%% and *}"
                differ[${file%.*}]="${differ[${file%.*}]} ${file##*.}" ;;
            *)
                echo "$line"
                FAILED=$((FAILED+1))
                FAILURES+=("$line") ;;
        esac
    done < <(diff -rq "$OUT/expected" "$OUT/actual" 2>&1)
    for name in "${CASES[@]}"; do
        case "$name" in
            "==="*) echo "$name"; continue ;;
            "SKIP"*) echo "$name"; SKIPPED=$((SKIPPED+1)); continue ;;
        esac
        what=""
        for kind in ${differ[$name]}; do
            if [ "$kind" = out ] && [ "${MASK[$name]}" = yes ] &&
               same_but_mvhd_times "$OUT/expected/$name.out" "$OUT/actual/$name.out"; then
                continue
            fi
            what="$what $kind"
        done
        if [ -z "$what" ]; then
            echo "PASS  $name"
            PASSED=$((PASSED+1))
        else
            echo "FAIL  $name   (differences:$what)"
            FAILED=$((FAILED+1))
            FAILURES+=("$name")
        fi
    done
}

# mux <case name> <mp4mux arguments, without the output name>...
mux() {
    local name="$1"
    shift
    compare_case "mp4mux.$name" mp4mux yes out.mp4 "$@" out.mp4
}

#----------------------------------------------------------------------
# inputs
#----------------------------------------------------------------------
cp "$DATA"/*.mp4 "$GEN/"
# the emscripten tools only see the current directory: use relative names
cd "$GEN" || exit 1
: > "$WORK/t3-gen.log"
ref() {
    local tool="$1"; shift
    node "$REF/bin/$tool.js" "$@" >> "$WORK/t3-gen.log" 2>&1 || echo "generation failed: $tool $*" >> "$WORK/t3-gen.log"
}
# elementary streams
ref mp42avc video-h264-001.mp4 video.h264
ref mp42aac audio-aac-001.mp4 audio.aac
ref mp42aac video-h264-001.mp4 audio2.aac
for f in video.h264 audio.aac audio2.aac; do
    if [ ! -s "$f" ]; then
        echo "FAIL  $f (input was not generated, see $WORK/t3-gen.log)"
        echo "=== summary: 0 passed, 1 failed, 0 skipped ==="
        exit 1
    fi
done
# the same streams under other names (the type is inferred from the extension)
cp video.h264 video.264
cp video.h264 video.avc
cp video.h264 VIDEO2.H264
cp video.h264 video.xyz
cp video.h264 video
cp audio2.aac audio2.adts
cp audio2.aac audio2.m4a
cp audio2.aac audio2.m4v
cp audio2.aac audio2.mov
cp video-h264-001.mp4 movie.MP4
# derived streams
: > empty.h264
: > empty.aac
cat video.h264 video.h264 video.h264 > video-x3.h264
head -c 20000 video.h264 > video-truncated.h264
head -c 3 video.h264 > video-tiny.h264
head -c 5000 audio2.aac > audio-truncated.aac
head -c 5 audio2.aac > audio-tiny.aac
{ head -c 3000 audio2.aac; printf 'this is not an ADTS frame, this is not an ADTS frame'; tail -c +3001 audio2.aac; } > audio-garbage.aac
{ printf 'ID3 some leading bytes'; cat audio2.aac; } > audio-leading.aac
# an ADTS stream whose frames claim to be 8191 bytes long
{ printf '\xff\xf1\x50\x83\xff\xff\xfc'; head -c 20000 /dev/zero; } > audio-huge-frame.aac
# AC-3: 48 kHz, 64 kbps (frames of 256 bytes), bsid 8, 2/0 channels; only the header is meaningful
ac3_frame() { printf '\x0b\x77\x00\x00\x08\x40\x40'; head -c 249 /dev/zero; }
for i in 1 2 3 4 5 6 7 8; do ac3_frame; done > synthetic.ac3
# the same frames, 16-bit words byte-swapped (little-endian AC-3)
ac3_frame_le() { printf '\x77\x0b\x00\x00\x40\x08\x00\x40'; head -c 248 /dev/zero; }
for i in 1 2 3 4 5 6 7 8; do ac3_frame_le; done > synthetic-le.ac3
# E-AC-3: independent substream 0, frames of 256 bytes (frmsiz = 127), 48 kHz, 6 blocks, 2/0 channels, bsid 16
eac3_frame() { printf '\x0b\x77\x00\x7f\x34\x80\x00'; head -c 249 /dev/zero; }
for i in 1 2 3 4 5 6 7 8; do eac3_frame; done > synthetic.ec3
# an MP4 file whose audio is declared as MPEG-2 AAC LC (object type 0x67 instead of 0x40):
# 'esds' <version, flags> 03 <length> <ES id, flags> 04 <length> <object type>
make_mpeg2_aac() {
    local in="$1" out="$2" esds pos b
    cp "$in" "$out"
    esds=$(LC_ALL=C grep -obUa esds "$in" | head -1 | cut -d: -f1)
    [ -n "$esds" ] || return 1
    pos=$((esds+8))
    byte() { od -An -tu1 -j "$1" -N 1 "$in" | tr -d ' \n'; }
    [ "$(byte $pos)" = 3 ] || return 1
    pos=$((pos+1)); while [ "$(byte $pos)" -ge 128 ]; do pos=$((pos+1)); done; pos=$((pos+1))
    pos=$((pos+3))
    [ "$(byte $pos)" = 4 ] || return 1
    pos=$((pos+1)); while [ "$(byte $pos)" -ge 128 ]; do pos=$((pos+1)); done; pos=$((pos+1))
    [ "$(byte $pos)" = 64 ] || return 1
    printf '\x67' | dd of="$out" bs=1 seek=$pos conv=notrunc 2> /dev/null
}
make_mpeg2_aac audio-aac-001.mp4 mpeg2-aac.mp4   || echo "generation failed: mpeg2-aac.mp4" >> "$WORK/t3-gen.log"
make_mpeg2_aac video-h264-001.mp4 mpeg2-aac-av.mp4 || echo "generation failed: mpeg2-aac-av.mp4" >> "$WORK/t3-gen.log"
make_mpeg2_aac audio-aac-002.mp4 mpeg2-aac-frag.mp4 || echo "generation failed: mpeg2-aac-frag.mp4" >> "$WORK/t3-gen.log"
mkdir -p a-directory

#----------------------------------------------------------------------
# mp4mux
#----------------------------------------------------------------------
section mp4mux
# H.264
mux h264                       --track h264:video.h264
mux h264.verbose               --verbose --track h264:video.h264
mux h264.verbose-last          --track h264:video.h264 --verbose
mux h264.by-extension          --track video.h264
mux h264.by-extension-264      --track video.264
mux h264.by-extension-avc      --track video.avc
mux h264.by-extension-upper    --track VIDEO2.H264
mux h264.frame-rate-30         --track h264:video.h264#frame_rate=30
mux h264.frame-rate-29.97      --track h264:video.h264#frame_rate=29.97
mux h264.frame-rate-23.976     --track h264:video.h264#frame_rate=23.976
mux h264.frame-rate-0.5        --track h264:video.h264#frame_rate=0.5
mux h264.frame-rate-exp        --track h264:video.h264#frame_rate=2.5e1
mux h264.frame-rate-hex        --track h264:video.h264#frame_rate=0x1e
mux h264.frame-rate-hex-frac   --track h264:video.h264#frame_rate=0x1.9p4
mux h264.frame-rate-space      --track "h264:video.h264#frame_rate= 25"
mux h264.frame-rate-suffix     --track h264:video.h264#frame_rate=25fps
mux h264.frame-rate-plus       --track h264:video.h264#frame_rate=+50.
mux h264.frame-rate-dot        --track h264:video.h264#frame_rate=.5
mux h264.frame-rate-small      --track h264:video.h264#frame_rate=0.0001
mux h264.frame-rate-twice      --track h264:video.h264#frame_rate=30,frame_rate=60
mux h264.frame-rate-zero       --track h264:video.h264#frame_rate=0
mux h264.frame-rate-hex-zero   --track h264:video.h264#frame_rate=0x
mux h264.frame-rate-max        --track h264:video.h264#frame_rate=4294967.295
# (frame rates that do not fit in 32 bits once multiplied by 1000: undefined in C++, 0 in the original tools as built for the reference)
mux h264.frame-rate-too-large  --track h264:video.h264#frame_rate=4294967.296
mux h264.frame-rate-1e10       --track h264:video.h264#frame_rate=1e10
mux h264.frame-rate-negative   --track h264:video.h264#frame_rate=-1
mux h264.frame-rate-inf        --track h264:video.h264#frame_rate=inf
mux h264.frame-rate-nan        --track h264:video.h264#frame_rate=NaN
mux h264.frame-rate-invalid    --track h264:video.h264#frame_rate=abc
mux h264.frame-rate-empty      --track h264:video.h264#frame_rate=
mux h264.frame-rate-no-value   --track h264:video.h264#frame_rate
mux h264.format-avc1           --track h264:video.h264#format=avc1
mux h264.format-avc3           --track h264:video.h264#format=avc3
mux h264.format-unknown        --track h264:video.h264#format=xyz
mux h264.language              --track h264:video.h264#language=fra
mux h264.language-invalid      --track h264:video.h264#language=fr
mux h264.language-long         --track h264:video.h264#language=french
mux h264.language-empty        --track h264:video.h264#language=
mux h264.params                --track h264:video.h264#frame_rate=25,language=deu,format=avc3
mux h264.params-unknown        --track h264:video.h264#foo=bar,,=,x=y=z,frame_rate=50
mux h264.params-empty          --track h264:video.h264#
mux h264.x3                    --track h264:video-x3.h264
mux h264.x3.verbose            --verbose --track h264:video-x3.h264#frame_rate=60
mux h264.truncated             --verbose --track h264:video-truncated.h264
mux h264.tiny                  --track h264:video-tiny.h264
mux h264.empty                 --track h264:empty.h264
mux h264.not-h264              --track h264:audio2.aac
mux h264.mp4-as-h264           --verbose --track h264:video-h264-001.mp4
mux h264.missing               --track h264:missing.h264
mux h264.twice                 --track h264:video.h264 --track h264:video.h264#frame_rate=30,language=spa
# H.264 with Dolby Vision parameters
mux h264.dovi-9                --track h264:video.h264#dv_profile=9,dv_bc=2
mux h264.dovi-9.verbose        --verbose --track h264:video.h264#dv_profile=9,dv_bc=2
mux h264.dovi-9-avc1           --track h264:video.h264#dv_profile=9,dv_bc=2,format=avc1
mux h264.dovi-9-avc3           --track h264:video.h264#dv_profile=9,dv_bc=2,format=avc3
mux h264.dovi-9-avc3-x3        --track h264:video-x3.h264#dv_profile=9,dv_bc=2,format=avc3,frame_rate=60
mux h264.dovi-9-dvav           --track h264:video.h264#dv_profile=9,dv_bc=2,format=dvav
mux h264.dovi-9-dva1           --track h264:video.h264#dv_profile=9,dv_bc=2,format=dva1
mux h264.dovi-9-hvc1           --track h264:video.h264#dv_profile=9,dv_bc=2,format=hvc1
mux h264.dovi-9-format-bad     --track h264:video.h264#dv_profile=9,dv_bc=2,format=xyz
mux h264.dovi-9-frame-rate     --track h264:video.h264#format=avc3,frame_rate=59.94,dv_bc=2,dv_profile=9,language=jpn
mux h264.dovi-9-frame-rate-bad --track h264:video.h264#dv_profile=9,dv_bc=2,frame_rate=x
mux h264.dovi-9-language-bad   --track h264:video.h264#dv_profile=9,dv_bc=2,language=x
mux h264.dovi-9-no-bc          --track h264:video.h264#dv_profile=9
mux h264.dovi-9-bc-1           --track h264:video.h264#dv_profile=9,dv_bc=1
mux h264.dovi-9-bc-8           --track h264:video.h264#dv_profile=9,dv_bc=8
mux h264.dovi-9-bc-negative    --track h264:video.h264#dv_profile=9,dv_bc=-1
mux h264.dovi-5                --track h264:video.h264#dv_profile=5
mux h264.dovi-5-bc-0           --track h264:video.h264#dv_profile=5,dv_bc=0
mux h264.dovi-5-bc-1           --track h264:video.h264#dv_profile=5,dv_bc=1
mux h264.dovi-5-avc1           --track h264:video.h264#dv_profile=5,format=avc1
mux h264.dovi-5-dvav           --track h264:video.h264#dv_profile=5,format=dvav
mux h264.dovi-8-bc-1           --track h264:video.h264#dv_profile=8,dv_bc=1
mux h264.dovi-8-bc-2           --track h264:video.h264#dv_profile=8,dv_bc=2
mux h264.dovi-8-bc-4           --track h264:video.h264#dv_profile=8,dv_bc=4
mux h264.dovi-8-bc-3           --track h264:video.h264#dv_profile=8,dv_bc=3
mux h264.dovi-8-avc1           --track h264:video.h264#dv_profile=8,dv_bc=1,format=avc1
mux h264.dovi-7                --track h264:video.h264#dv_profile=7
mux h264.dovi-profile-empty    --track h264:video.h264#dv_profile
mux h264.dovi-profile-text     --track "h264:video.h264#dv_profile= 9x,dv_bc=2"
mux h264.dovi-bc-only          --track h264:video.h264#dv_bc=2
mux h264.dovi-bc-only-1-4      --track h264:video.h264#dv_bc=1 --track h264:video.h264#dv_bc=4
mux h264.dovi-empty            --track h264:empty.h264#dv_profile=9,dv_bc=2
mux h264.dovi-missing          --track h264:missing.h264#dv_profile=9,dv_bc=2
# AAC
mux aac                        --track aac:audio.aac
mux aac.verbose                --verbose --track aac:audio2.aac
mux aac.by-extension           --track audio.aac
mux aac.by-extension-adts      --track audio2.adts
mux aac.by-extension-m4a       --track audio2.m4a
mux aac.by-extension-m4v       --track audio2.m4v
mux aac.by-extension-mov       --track audio2.mov
mux aac.language               --track aac:audio2.aac#language=ita
mux aac.language-invalid       --track aac:audio2.aac#language=it
mux aac.params-ignored         --track aac:audio2.aac#frame_rate=30,format=avc3,track=1
mux aac.truncated              --verbose --track aac:audio-truncated.aac
mux aac.tiny                   --track aac:audio-tiny.aac
mux aac.empty                  --track aac:empty.aac
mux aac.garbage                --verbose --track aac:audio-garbage.aac
mux aac.leading                --verbose --track aac:audio-leading.aac
mux aac.huge-frame             --verbose --track aac:audio-huge-frame.aac
mux aac.not-aac                --verbose --track aac:video.h264
mux aac.missing                --track aac:missing.aac
mux aac.twice                  --track aac:audio.aac --track aac:audio2.aac#language=por
# several elementary streams
mux h264+aac                   --track h264:video.h264 --track aac:audio2.aac
mux aac+h264                   --track aac:audio2.aac --track h264:video.h264
mux h264+aac.verbose           --verbose --track h264:video.h264#frame_rate=15 --track aac:audio2.aac#language=eng
mux h264+aac.by-extension      --track video.264 --track audio2.adts
mux h264+aac.x4                --track video.h264 --track audio2.aac --track h264:video-x3.h264#frame_rate=30 --track aac:audio.aac
mux h264+missing+aac           --track h264:video.h264 --track aac:missing.aac --track aac:audio2.aac
mux h264-dovi+aac              --track h264:video.h264#dv_profile=9,dv_bc=2 --track aac:audio2.aac
# tracks of MP4 files
mux mp4                        --track mp4:video-h264-001.mp4
mux mp4.verbose                --verbose --track mp4:video-h264-001.mp4
mux mp4.by-extension           --track video-h264-001.mp4
mux mp4.by-extension-upper     --track movie.MP4
mux mp4.track-1                --track mp4:video-h264-001.mp4#track=1
mux mp4.track-2                --track mp4:video-h264-001.mp4#track=2
mux mp4.track-2.verbose        --verbose --track mp4:video-h264-001.mp4#track=2
mux mp4.track-3                --verbose --track mp4:video-h264-001.mp4#track=3
mux mp4.track-audio            --verbose --track mp4:video-h264-001.mp4#track=audio
mux mp4.track-video            --verbose --track mp4:video-h264-001.mp4#track=video
mux mp4.track-video-none       --verbose --track mp4:audio-aac-001.mp4#track=video
mux mp4.track-zero             --verbose --track mp4:video-h264-001.mp4#track=0
mux mp4.track-text             --verbose --track mp4:video-h264-001.mp4#track=abc
mux mp4.track-number-text      --verbose --track "mp4:video-h264-001.mp4#track= +2abc"
mux mp4.track-negative         --verbose --track mp4:video-h264-001.mp4#track=-1
mux mp4.track-2-pow-32-plus-1  --verbose --track mp4:video-h264-001.mp4#track=4294967297
mux mp4.track-huge             --verbose --track mp4:video-h264-001.mp4#track=99999999999999999999999
mux mp4.track-twice            --verbose --track mp4:video-h264-001.mp4#track=1,track=2
mux mp4.language               --track mp4:video-h264-001.mp4#language=nld
mux mp4.language-track         --track mp4:video-h264-001.mp4#track=audio,language=nld
mux mp4.language-invalid       --track mp4:video-h264-001.mp4#language=nl
mux mp4.audio                  --track mp4:audio-aac-001.mp4
mux mp4.audio.by-extension     --track audio-aac-001.mp4
mux mp4.fragmented             --verbose --track mp4:video-h264-002.mp4
mux mp4.fragmented-track-1     --track mp4:video-h264-002.mp4#track=1
mux mp4.fragmented-audio       --track mp4:audio-aac-002.mp4
mux mp4.fragmented-audio-3     --track mp4:audio-aac-003.mp4#track=audio
mux mp4.mpeg2-aac              --track mp4:mpeg2-aac.mp4
mux mp4.not-mp4                --verbose --track mp4:video.h264
mux mp4.empty                  --verbose --track mp4:empty.aac
mux mp4.missing                --track mp4:missing.mp4
mux mp4.twice                  --track mp4:video-h264-001.mp4 --track mp4:video-h264-001.mp4
mux mp4.video+audio            --track mp4:video-h264-001.mp4#track=video --track mp4:audio-aac-001.mp4#track=audio,language=swe
mux mp4.audio+video            --track mp4:audio-aac-001.mp4 --track mp4:video-h264-001.mp4#track=1
mux mp4+h264+aac               --verbose --track mp4:video-h264-001.mp4 --track h264:video.h264#frame_rate=30 --track aac:audio.aac
mux h264+mp4                   --track h264:video.h264 --track mp4:audio-aac-001.mp4
mux aac+mp4                    --track aac:audio2.aac#language=fin --track mp4:video-h264-001.mp4#track=video
# other types, fed with what is available (no H.265, AC-3, E-AC-3 or AC-4 test media)
mux h265.h264-data             --verbose --track h265:video.h264
mux h265.by-extension-265      --track video.264 --track h265:empty.h264
mux h265.empty                 --track h265:empty.h264#frame_rate=30,format=hev1
mux h265.frame-rate-invalid    --track h265:empty.h264#frame_rate=0
mux h265.missing               --track h265:missing.h265
mux h265.language-invalid      --track h265:video.h264#language=
mux h265.dovi-5-empty          --track h265:empty.h264#dv_profile=5
mux h265.dovi-8-empty          --track h265:empty.h264#dv_profile=8,dv_bc=1,format=hev1
mux h265.dovi-8-bc-4-empty     --track h265:empty.h264#dv_profile=8,dv_bc=4
mux h265.dovi-8-no-bc          --track h265:empty.h264#dv_profile=8
mux h265.dovi-8-bc-0           --track h265:empty.h264#dv_profile=8,dv_bc=0
mux h265.dovi-8-dvh1           --track h265:empty.h264#dv_profile=8,dv_bc=2,format=dvh1
mux h265.dovi-5-hvc1           --track h265:empty.h264#dv_profile=5,format=hvc1
mux h265.dovi-5-dvhe           --track h265:empty.h264#dv_profile=5,format=dvhe,frame_rate=60
mux h265.dovi-9                --track h265:empty.h264#dv_profile=9,dv_bc=2
mux h265.dovi-frame-rate-bad   --track h265:empty.h264#dv_profile=5,frame_rate=zero
mux h265.dovi-missing          --track h265:missing.h265#dv_profile=5
mux ac3.synthetic              --verbose --track ac3:synthetic.ac3
mux ac3.synthetic.by-extension --track synthetic.ac3#language=kor
mux ac3.synthetic-le           --verbose --track ac3:synthetic-le.ac3
mux ac3.synthetic+h264         --track h264:video.h264 --track ac3:synthetic.ac3
mux ac3.aac-data               --verbose --track ac3:audio2.aac
mux ac3.empty                  --track ac3:empty.aac
mux ac3.missing                --track ac3:missing.ac3
mux ac3.language-invalid       --track ac3:synthetic.ac3#language=k
mux ec3.synthetic              --verbose --track ec3:synthetic.ec3
mux ec3.synthetic.by-extension --track synthetic.ec3#language=zho
mux ec3.synthetic+h264         --track h264:video.h264 --track ec3:synthetic.ec3
mux ec3.ac3-data               --verbose --track ec3:synthetic.ac3
mux ec3.aac-data               --verbose --track ec3:audio2.aac
mux ec3.empty                  --track ec3:empty.aac
mux ec3.missing                --track ec3:missing.ec3
mux ac4.aac-data               --verbose --track ac4:audio2.aac
mux ac4.h264-data              --verbose --track ac4:video.h264
mux ac4.empty                  --track ac4:empty.aac#language=und
mux ac4.missing                --track ac4:missing.ac4
# errors
compare_case mp4mux.no-arguments      mp4mux no ""
compare_case mp4mux.no-input          mp4mux no out.mp4 out.mp4
compare_case mp4mux.verbose-only      mp4mux no "" --verbose
compare_case mp4mux.no-output         mp4mux no "" --track h264:video.h264
compare_case mp4mux.unexpected        mp4mux no out.mp4 --track h264:video.h264 out.mp4 extra
compare_case mp4mux.unknown-option    mp4mux no out.mp4 --frobnicate --track h264:video.h264 out.mp4
compare_case mp4mux.output-first      mp4mux yes out.mp4 out.mp4 --verbose --track h264:video.h264
mux unknown-type               --track xyz:video.h264
mux unknown-type-empty         --track :video.h264
mux unknown-extension          --track video.xyz
mux no-extension               --track video
mux no-extension-dot-path      --track ./video
mux empty-name                 --track ""
mux empty-name-typed           --track h264:
mux type-case                  --track H264:video.h264
mux unknown-type-second        --track h264:video.h264 --track xyz:audio.aac
mux dovi-error-second          --track aac:audio2.aac --track h264:video.h264#dv_profile=3
compare_case mp4mux.output-in-missing-directory mp4mux no missing-directory/out.mp4 --track h264:video.h264 missing-directory/out.mp4
compare_case mp4mux.output-is-a-directory       mp4mux no a-directory --track h264:video.h264 a-directory
compare_case mp4mux.track-without-value-no-output mp4mux no "" --track h264:video.h264 --track
skip "mp4mux.track-without-value" "'out.mp4 --track': the original tool uses a NULL input name"
skip "mp4mux.input-is-a-directory" "the original tool opens the directory, then fails or loops forever depending on the type"

#----------------------------------------------------------------------
# aac2mp4
#----------------------------------------------------------------------
section aac2mp4
for f in audio audio2 audio-truncated audio-tiny audio-garbage audio-leading audio-huge-frame empty; do
    compare_case "aac2mp4.$f" aac2mp4 no out.mp4 $f.aac out.mp4
done
compare_case aac2mp4.not-aac            aac2mp4 no out.mp4 video.h264 out.mp4
skip "aac2mp4.mp4-input" "the original tool loops forever on this input (so does the port)"
compare_case aac2mp4.extra-argument     aac2mp4 no out.mp4 audio2.aac out.mp4 extra
compare_case aac2mp4.no-arguments       aac2mp4 no ""
compare_case aac2mp4.one-argument       aac2mp4 no "" audio2.aac
compare_case aac2mp4.missing-input      aac2mp4 no out.mp4 missing.aac out.mp4
compare_case aac2mp4.missing-directory  aac2mp4 no missing-directory/out.mp4 audio2.aac missing-directory/out.mp4

#----------------------------------------------------------------------
# fixaacsampledescription
#----------------------------------------------------------------------
section fixaacsampledescription
for f in audio-aac-001 audio-aac-002 audio-aac-003 video-h264-001 video-h264-002 mpeg2-aac mpeg2-aac-av mpeg2-aac-frag; do
    compare_case "fixaacsampledescription.$f" fixaacsampledescription no out.mp4 $f.mp4 out.mp4
done
compare_case fixaacsampledescription.not-mp4            fixaacsampledescription no out.mp4 audio2.aac out.mp4
compare_case fixaacsampledescription.empty              fixaacsampledescription no out.mp4 empty.aac out.mp4
compare_case fixaacsampledescription.no-arguments       fixaacsampledescription no ""
compare_case fixaacsampledescription.one-argument       fixaacsampledescription no "" audio-aac-001.mp4
compare_case fixaacsampledescription.extra-argument     fixaacsampledescription no out.mp4 audio-aac-001.mp4 out.mp4 extra
compare_case fixaacsampledescription.missing-input      fixaacsampledescription no out.mp4 missing.mp4 out.mp4
compare_case fixaacsampledescription.missing-directory  fixaacsampledescription no missing-directory/out.mp4 mpeg2-aac.mp4 missing-directory/out.mp4

cd "$ROOT" || exit 1
report_cases
if [ -s "$WORK/t3-gen.log" ] && grep -q "generation failed" "$WORK/t3-gen.log"; then
    grep "generation failed" "$WORK/t3-gen.log"
    FAILED=$((FAILED+1))
    FAILURES+=("input generation")
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
