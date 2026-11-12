#!/bin/bash
# Compares the ported tools of group T1 (mp4fragment, mp4split, mp4extract,
# mp4compact, mp4edit) with the original Bento4 tools: same arguments, then
# the standard output, the standard error, the exit code and every output
# file are compared byte for byte.
#
# usage: tests/compare-tools-T1.sh [<reference-dir> [<test-data-dir>]]
#
#   <reference-dir>  (or $BENTO4_REFERENCE_DIR) directory with
#                      bin/<tool>.js  the original tools built with emscripten, run with node
#                                     (mp4fragment mp4split mp4extract mp4compact mp4edit mp4encrypt)
#   <test-data-dir>  (or $BENTO4_TEST_DATA_DIR) the Test/Data directory of the Bento4 sources (*.mp4)
#
# other environment variables:
#   BENTO4SHARP_ARTIFACTS   where to build (default: a temporary directory, removed on exit)
#   BENTO4SHARP_WORK_DIR    where to put generated inputs and outputs (default: a temporary directory)
#   BENTO4SHARP_SKIP_BUILD  set to 1 to reuse the binaries already in $BENTO4SHARP_ARTIFACTS
#   BENTO4SHARP_FRAMEWORK   target framework of the port to build and run (default: net8.0; net20 runs
#                           the .NET Framework 2.0 executables)
#   BENTO4SHARP_JOBS        number of cases to run at the same time (default: 4)
#   BENTO4SHARP_ONLY        only run the cases whose name starts with this prefix (e.g. "fragment.")
#
# The inputs are generated in $WORK/run. Each case runs twice (original and
# port), each time in a fresh current directory $WORK/run/<case>.{ref,port},
# so the arguments (and so the messages that quote them) are identical for
# both. For a failed case these two directories are kept: they hold the
# output files, plus .stdout, .stderr and .rc (the exit code).
#
# A case where the original tool crashes (a failed assertion of its debug
# build, or an invalid memory access) is reported as SKIP: there is nothing
# to compare with.
#
# exit code: 0 if every case passes, 1 otherwise.

REF="${1:-$BENTO4_REFERENCE_DIR}"
DATA="${2:-$BENTO4_TEST_DATA_DIR}"
if [ -z "$REF" ] || [ -z "$DATA" ]; then
    sed -n "2,33p" "$0" >&2
    exit 2
fi
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
if ! command -v node > /dev/null || [ ! -f "$REF/bin/mp4fragment.js" ]; then
    echo "the original tools ($REF/bin/*.js) and node are required" >&2
    exit 2
fi

CLEANUP=()
cleanup() { cd /; for d in "${CLEANUP[@]}"; do rm -rf "$d"; done; }
trap cleanup EXIT

ART="$BENTO4SHARP_ARTIFACTS"
if [ -z "$ART" ]; then ART="$(mktemp -d)"; CLEANUP+=("$ART"); fi
WORK="$BENTO4SHARP_WORK_DIR"
if [ -z "$WORK" ]; then WORK="$(mktemp -d)"; CLEANUP+=("$WORK"); fi
GEN="$WORK/run"
rm -rf "$GEN" 2> /dev/null
mkdir -p "$GEN"

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
    for tool in Mp4Fragment Mp4Split Mp4Extract Mp4Compact Mp4Edit; do
        if ! dotnet build "$ROOT/src/Tools/$tool/$tool.csproj" -c Release -f "$FW" -nologo -v q -nodeReuse:false --artifacts-path "$ART" > "$WORK/build.log" 2>&1; then
            cat "$WORK/build.log"
            echo "BUILD FAILED: $tool"
            exit 1
        fi
    done
fi
# port_dll <tool>: sets $DLL
port_dll() {
    case "$1" in
        mp4fragment) DLL="$ART/bin/Mp4Fragment/release_$FW/mp4fragment.dll" ;;
        mp4split)    DLL="$ART/bin/Mp4Split/release_$FW/mp4split.dll" ;;
        mp4extract)  DLL="$ART/bin/Mp4Extract/release_$FW/mp4extract.dll" ;;
        mp4compact)  DLL="$ART/bin/Mp4Compact/release_$FW/mp4compact.dll" ;;
        mp4edit)     DLL="$ART/bin/Mp4Edit/release_$FW/mp4edit.dll" ;;
    esac
}

#----------------------------------------------------------------------
# helpers
# (written to start as few processes as possible: that is what takes the
# time on Windows)
#----------------------------------------------------------------------
PASSED=0; FAILED=0; SKIPPED=0
JOBS="${BENTO4SHARP_JOBS:-4}"
FAILURES=()

# ref <tool> <args>...: runs an original tool in $GEN to generate an input
ref() {
    local tool="$1"; shift
    (cd "$GEN" && node "$REF/bin/$tool.js" "$@") >> "$WORK/gen.log" 2>&1 || echo "generation failed: $tool $*" >> "$WORK/gen.log"
}

# same_but_mvhd_times <file a> <file b>
# mp4fragment stamps the current time in the creation and modification time
# of the 'mvhd' atom: the two files must be identical except for those 8 bytes.
same_but_mvhd_times() {
    local off
    [ -f "$1" ] && [ -f "$2" ] || return 1
    [ "$(wc -c < "$1")" = "$(wc -c < "$2")" ] || return 1
    off="$(LC_ALL=C grep -obUa mvhd "$1" | head -1 | cut -d: -f1)"
    [ -n "$off" ] || return 1
    cmp -l "$1" "$2" 2> /dev/null | awk -v s="$off" '$1 < s+9 || $1 > s+16 { bad=1 } END { exit bad }'
}

# compare_outputs <dir a> <dir b> <tool>: sets $WHY to what differs (empty: nothing)
compare_outputs() {
    local a="$1" b="$2" f
    WHY=""
    cmp -s "$a/.stdout" "$b/.stdout" || WHY="$WHY stdout"
    cmp -s "$a/.stderr" "$b/.stderr" || WHY="$WHY stderr"
    cmp -s "$a/.rc" "$b/.rc" || WHY="$WHY exit-code"
    if [ "$(ls "$a")" != "$(ls "$b")" ]; then WHY="$WHY files"; return; fi
    for f in "$a"/*; do
        [ -e "$f" ] || continue
        if ! cmp -s "$f" "$b/${f##*/}"; then
            if [ "$3" != mp4fragment ] || ! same_but_mvhd_times "$f" "$b/${f##*/}"; then WHY="$WHY files"; return; fi
        fi
    done
}

# run_case <case name> <tool> <args>...
# Runs the original and the port, writes the PASS/FAIL/SKIP line to $GEN/<case>.result
run_case() {
    local name="$1" tool="$2" err rc files
    local a="$GEN/$1.ref" b="$GEN/$1.port"
    shift 2
    port_dll $tool
    rm -rf "$a" "$b"
    mkdir "$a" "$b"
    (cd "$a" && { node "$REF/bin/$tool.js" "$@" > .stdout 2> .stderr; echo $? > .rc; }) &
    (cd "$b" && { port "$DLL" "$@" > .stdout 2> .stderr; echo $? > .rc; }) &
    wait
    err="$(< "$a/.stderr")"
    case "$err" in *"Assertion failed"*|*"RuntimeError:"*)
        # the original tool crashed (failed assertion of its debug build, or
        # invalid memory access): undefined behaviour, nothing to compare with
        err="${err%%$'\n'*}"; err="${err%%, at:*}"; err="${err%%,*}"
        echo "SKIP  $name   (the original tool crashed: $err)   $tool $*" > "$GEN/$name.result"
        rm -rf "$a" "$b"
        return ;;
    esac
    WHY=""
    if ! diff -rq "$a" "$b" > /dev/null 2>&1; then compare_outputs "$a" "$b" $tool; fi
    if [ -z "$WHY" ]; then
        read -r rc < "$a/.rc"
        files=("$a"/*); [ -e "${files[0]}" ] || files=()
        echo "PASS  $name   [rc $rc, ${#files[@]} file(s)]" > "$GEN/$name.result"
        rm -rf "$a" "$b"
    else
        echo "FAIL  $name   (differs:$WHY)   $tool $*" > "$GEN/$name.result"
    fi
}

# tcase <case name> <tool> <args>...
# Inputs are named ../<file> (they are in $GEN), outputs are plain names
# (they land in the current directory of the tool). $JOBS cases run at the
# same time; the results are printed in order by flush_cases.
PENDING=()
tcase() {
    case "$1" in "$BENTO4SHARP_ONLY"*) ;; *) return ;; esac
    run_case "$@" &
    PENDING+=("$1")
    if [ ${#PENDING[@]} -ge $JOBS ]; then flush_cases; fi
}
flush_cases() {
    local name line results=()
    wait
    for name in "${PENDING[@]}"; do
        line=""
        read -r line < "$GEN/$name.result"
        results+=("$GEN/$name.result")
        case "$line" in
            PASS*) PASSED=$((PASSED+1)) ;;
            SKIP*) SKIPPED=$((SKIPPED+1)) ;;
            *)     FAILED=$((FAILED+1)); FAILURES+=("$name"); [ -n "$line" ] || line="FAIL  $name   (no result)" ;;
        esac
        echo "$line"
    done
    [ ${#results[@]} -eq 0 ] || rm -f "${results[@]}"
    PENDING=()
}

#----------------------------------------------------------------------
# inputs
#----------------------------------------------------------------------
cp "$DATA"/*.mp4 "$GEN/"
: > "$WORK/gen.log"
K1=000102030405060708090a0b0c0d0e0f
K2=101112131415161718191a1b1c1d1e1f
KEY1=1:$K1:0001020304050607
KEY2=2:$K2:1011121314151617
STOCK="audio-aac-001 audio-aac-002 audio-aac-003 video-h264-001 video-h264-002"

# fragmented files
ref mp4fragment video-h264-001.mp4 frag-v1.mp4
ref mp4fragment audio-aac-001.mp4 frag-a1.mp4
ref mp4fragment --fragment-duration 500 video-h264-001.mp4 frag-v1-500.mp4
ref mp4fragment --fragment-duration 2000 --index audio-aac-001.mp4 frag-a1-index.mp4
ref mp4fragment --index video-h264-001.mp4 frag-v1-index.mp4
ref mp4fragment --track video video-h264-001.mp4 frag-v1-video-only.mp4
# encrypted files
ref mp4encrypt --method MPEG-CENC --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-cenc-v1.mp4
ref mp4encrypt --method MPEG-CENC --key $KEY1 audio-aac-001.mp4 enc-cenc-a1.mp4
ref mp4encrypt --method MPEG-CENC --key $KEY1 --key $KEY2 frag-v1.mp4 enc-cenc-frag-v1.mp4
ref mp4encrypt --method MPEG-CBCS --key $KEY1 --key $KEY2 frag-v1-500.mp4 enc-cbcs-frag-v1.mp4
ref mp4encrypt --method MPEG-CENC --key $KEY1 frag-a1-index.mp4 enc-cenc-frag-a1.mp4
ref mp4encrypt --method PIFF-CTR --key $KEY1 --key $KEY2 frag-v1.mp4 enc-piff-frag-v1.mp4
ref mp4encrypt --method OMA-PDCF-CBC --key $KEY1 --key $KEY2 video-h264-001.mp4 enc-oma-v1.mp4
ref mp4encrypt --method ISMA-IAEC --key $KEY1 --kms-uri urn:example:kms audio-aac-001.mp4 enc-isma-a1.mp4
# atoms
ref mp4extract moov/udta video-h264-001.mp4 udta.atom
ref mp4extract moov/trak/mdia/minf/stbl/stsd audio-aac-001.mp4 stsd.atom
ref mp4extract --payload-only moov/mvhd audio-aac-001.mp4 mvhd.payload
ref mp4extract ftyp audio-aac-001.mp4 ftyp.atom
: > "$GEN/empty.bin"
printf 'abc' > "$GEN/short.bin"
printf 'this is not an mp4 file, just some text that is long enough to look like atoms\n' > "$GEN/not-mp4.mp4"
head -c 1000 "$GEN/video-h264-001.mp4" > "$GEN/truncated.mp4"

DERIVED="frag-v1 frag-a1 frag-v1-500 frag-a1-index frag-v1-index frag-v1-video-only enc-cenc-v1 enc-cenc-a1 enc-cenc-frag-v1 enc-cbcs-frag-v1 enc-cenc-frag-a1 enc-piff-frag-v1 enc-oma-v1 enc-isma-a1"
for n in $STOCK $DERIVED; do
    if [ ! -s "$GEN/$n.mp4" ]; then
        echo "FAIL  $n.mp4 (input was not generated, see $WORK/gen.log)"
        FAILED=$((FAILED+1))
        FAILURES+=("$n.mp4 generation")
    fi
done

#----------------------------------------------------------------------
# mp4extract
#----------------------------------------------------------------------
echo "=== mp4extract ==="
for n in $STOCK frag-v1-500 frag-a1-index enc-cenc-v1 enc-cenc-frag-v1 enc-oma-v1; do
    f="../$n.mp4"
    tcase extract.$n.ftyp          mp4extract ftyp $f out.atom
    tcase extract.$n.moov          mp4extract moov $f out.atom
    tcase extract.$n.mvhd-payload  mp4extract --payload-only moov/mvhd $f out.bin
    tcase extract.$n.moov-payload  mp4extract moov $f out.bin --payload-only
    tcase extract.$n.stsd          mp4extract moov/trak/mdia/minf/stbl/stsd $f out.atom
    tcase extract.$n.stsd-payload  mp4extract --payload-only moov/trak/mdia/minf/stbl/stsd $f out.bin
    tcase extract.$n.trak1         mp4extract "moov/trak[1]" $f out.atom
    tcase extract.$n.trak1-stbl    mp4extract "moov/trak[1]/mdia/minf/stbl" $f out.atom
    tcase extract.$n.trak5         mp4extract "moov/trak[5]" $f out.atom
    tcase extract.$n.udta          mp4extract moov/udta $f out.atom
    tcase extract.$n.mdat          mp4extract mdat $f out.atom
    tcase extract.$n.mdat-payload  mp4extract --payload-only mdat $f out.bin
    tcase extract.$n.moof          mp4extract moof $f out.atom
    tcase extract.$n.moof2-trun    mp4extract "moof[2]/traf/trun" $f out.atom
    tcase extract.$n.mvex          mp4extract moov/mvex $f out.atom
    tcase extract.$n.missing       mp4extract moov/zzzz $f out.atom
    tcase extract.$n.sinf          mp4extract moov/trak/mdia/minf/stbl/stsd/encv/sinf $f out.atom
done
tcase extract.err.no-args        mp4extract
tcase extract.err.one-arg        mp4extract moov
tcase extract.err.two-args       mp4extract moov ../audio-aac-001.mp4
tcase extract.err.payload-only-3 mp4extract --payload-only moov ../audio-aac-001.mp4
tcase extract.err.extra-arg      mp4extract moov ../audio-aac-001.mp4 out.atom extra
tcase extract.err.no-input       mp4extract moov ../no-such-file.mp4 out.atom
tcase extract.err.no-output-dir  mp4extract moov ../audio-aac-001.mp4 no-such-dir/out.atom
tcase extract.err.unknown-option mp4extract --verbose moov ../audio-aac-001.mp4
tcase extract.err.empty-path     mp4extract "" ../audio-aac-001.mp4 out.atom
tcase extract.err.bad-path       mp4extract "moov/trak[" ../audio-aac-001.mp4 out.atom
tcase extract.err.not-mp4        mp4extract moov ../not-mp4.mp4 out.atom
tcase extract.err.empty-file     mp4extract moov ../empty.bin out.atom
tcase extract.err.truncated      mp4extract ftyp ../truncated.mp4 out.atom

#----------------------------------------------------------------------
# mp4compact
#----------------------------------------------------------------------
echo "=== mp4compact ==="
for n in $STOCK frag-v1-500 frag-a1-index enc-cenc-v1 enc-cenc-a1 enc-cenc-frag-v1 enc-oma-v1 enc-isma-a1; do
    f="../$n.mp4"
    tcase compact.$n.default  mp4compact $f out.mp4
    tcase compact.$n.verbose  mp4compact --verbose $f out.mp4
done
tcase compact.verbose-last       mp4compact ../audio-aac-001.mp4 out.mp4 --verbose
tcase compact.err.no-args        mp4compact
tcase compact.err.options-only   mp4compact --verbose
tcase compact.err.extra-arg      mp4compact ../audio-aac-001.mp4 out.mp4 extra
tcase compact.err.no-input       mp4compact ../no-such-file.mp4 out.mp4
tcase compact.err.no-output-dir  mp4compact ../audio-aac-001.mp4 no-such-dir/out.mp4
tcase compact.err.not-mp4        mp4compact --verbose ../not-mp4.mp4 out.mp4
tcase compact.err.empty-file     mp4compact --verbose ../empty.bin out.mp4
tcase compact.err.truncated      mp4compact --verbose ../truncated.mp4 out.mp4

#----------------------------------------------------------------------
# mp4edit
#----------------------------------------------------------------------
echo "=== mp4edit ==="
UUID=a5d40b30e81411ddba2f0800200c9a66
for n in $STOCK frag-v1-500 frag-a1-index enc-cenc-v1 enc-cenc-frag-v1 enc-oma-v1; do
    f="../$n.mp4"
    tcase edit.$n.no-command        mp4edit $f out.mp4
    tcase edit.$n.remove-udta       mp4edit --remove moov/udta $f out.mp4
    tcase edit.$n.remove-trak       mp4edit --remove moov/trak $f out.mp4
    tcase edit.$n.remove-mvex       mp4edit --remove moov/mvex $f out.mp4
    tcase edit.$n.remove-stss       mp4edit --remove moov/trak/mdia/minf/stbl/stss $f out.mp4
    tcase edit.$n.remove-two        mp4edit --remove moov/trak/mdia/minf/stbl/stsd --remove "moov/trak[0]/tkhd" $f out.mp4
    tcase edit.$n.remove-missing    mp4edit --remove moov/zzzz $f out.mp4
    tcase edit.$n.insert-moov       mp4edit --insert moov:../udta.atom $f out.mp4
    tcase edit.$n.insert-moov-0     mp4edit --insert moov:../udta.atom:0 $f out.mp4
    tcase edit.$n.insert-moov-1     mp4edit --insert moov:../udta.atom:1 $f out.mp4
    tcase edit.$n.insert-moov-99    mp4edit --insert moov:../udta.atom:99 $f out.mp4
    tcase edit.$n.insert-top        mp4edit --insert :../ftyp.atom $f out.mp4
    tcase edit.$n.insert-top-0      mp4edit --insert :../udta.atom:0 $f out.mp4
    tcase edit.$n.insert-top-2      mp4edit --insert :../stsd.atom:2 $f out.mp4
    tcase edit.$n.insert-trak       mp4edit --insert moov/trak/mdia:../stsd.atom:1 $f out.mp4
    tcase edit.$n.insert-auto       mp4edit --insert moov/udta/meta:../stsd.atom $f out.mp4
    tcase edit.$n.insert-leaf       mp4edit --insert moov/mvhd:../udta.atom $f out.mp4
    tcase edit.$n.insert-uuid       mp4edit --insert "moov:$UUID#../mvhd.payload" $f out.mp4
    tcase edit.$n.insert-uuid-0     mp4edit --insert "moov/trak:$UUID#../mvhd.payload:0" $f out.mp4
    tcase edit.$n.insert-uuid-empty mp4edit --insert ":$UUID#../empty.bin" $f out.mp4
    tcase edit.$n.replace-udta      mp4edit --replace moov/udta:../stsd.atom $f out.mp4
    tcase edit.$n.replace-mvhd      mp4edit --replace moov/mvhd:../udta.atom $f out.mp4
    tcase edit.$n.replace-ftyp      mp4edit --replace ftyp:../ftyp.atom $f out.mp4
    tcase edit.$n.replace-uuid      mp4edit --replace "moov/trak/tkhd:$UUID#../mvhd.payload" $f out.mp4
    tcase edit.$n.replace-missing   mp4edit --replace moov/zzzz:../stsd.atom $f out.mp4
    tcase edit.$n.combined          mp4edit --insert moov/trak:../udta.atom:0 --remove moov/trak/edts --replace moov/mvhd:../stsd.atom --insert ":$UUID#../short.bin" $f out.mp4
    tcase edit.$n.remove-moof       mp4edit --remove moof --remove "moof[1]/traf/tfdt" $f out.mp4
    tcase edit.$n.remove-mdat       mp4edit --remove mdat $f out.mp4
    # (no "--remove moov" case: the original keeps using the 'moov' atom after
    # it has been deleted, so its output is undefined)
done
if command -v cygpath > /dev/null; then
    # <atom_source> given as a Windows path (drive letter, colon, backslash)
    tcase edit.windows-path        mp4edit --insert "moov:$(cygpath -w "$GEN/udta.atom"):0" ../audio-aac-001.mp4 out.mp4
    tcase edit.windows-path-uuid   mp4edit --insert "moov/trak:$UUID#$(cygpath -w "$GEN/mvhd.payload")" ../video-h264-001.mp4 out.mp4
fi
tcase edit.err.no-args             mp4edit
tcase edit.err.one-arg             mp4edit ../audio-aac-001.mp4
tcase edit.err.no-output           mp4edit --remove moov/udta ../audio-aac-001.mp4
tcase edit.err.extra-arg           mp4edit ../audio-aac-001.mp4 out.mp4 extra
tcase edit.err.no-input            mp4edit ../no-such-file.mp4 out.mp4
tcase edit.err.no-output-dir       mp4edit ../audio-aac-001.mp4 no-such-dir/out.mp4
tcase edit.err.insert-no-arg       mp4edit ../audio-aac-001.mp4 out.mp4 --insert
tcase edit.err.remove-no-arg       mp4edit ../audio-aac-001.mp4 out.mp4 --remove
tcase edit.err.replace-no-arg      mp4edit ../audio-aac-001.mp4 out.mp4 --replace
tcase edit.err.insert-no-colon     mp4edit --insert moov ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-empty-file   mp4edit --insert moov: ../audio-aac-001.mp4 out.mp4
tcase edit.err.replace-no-colon    mp4edit --replace moov ../audio-aac-001.mp4 out.mp4
tcase edit.err.replace-empty-file  mp4edit --replace moov/mvhd: ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-no-file      mp4edit --insert moov:../no-such.atom ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-uuid-no-file mp4edit --insert "moov:$UUID#../no-such.bin" ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-empty-atom   mp4edit --insert moov:../empty.bin ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-short-atom   mp4edit --insert moov:../short.bin ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-short-uuid   mp4edit --insert "moov:a5d40b30#../mvhd.payload" ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-bad-position mp4edit --insert moov:../udta.atom:abc ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-neg-position mp4edit --insert moov:../udta.atom:-1 ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-empty-pos    mp4edit --insert moov:../udta.atom: ../audio-aac-001.mp4 out.mp4
tcase edit.err.insert-missing-path mp4edit --insert moov/zzzz/yyyy:../udta.atom ../audio-aac-001.mp4 out.mp4
tcase edit.err.second-fails        mp4edit --remove moov/udta --remove moov/udta ../video-h264-001.mp4 out.mp4
tcase edit.err.not-mp4             mp4edit --remove moov ../not-mp4.mp4 out.mp4
tcase edit.err.empty-file          mp4edit ../empty.bin out.mp4
tcase edit.err.truncated           mp4edit --remove moov/udta ../truncated.mp4 out.mp4

#----------------------------------------------------------------------
# mp4split
#----------------------------------------------------------------------
echo "=== mp4split ==="
for n in $STOCK frag-v1 frag-a1 frag-v1-500 frag-a1-index frag-v1-index frag-v1-video-only enc-cenc-v1 enc-cenc-frag-v1 enc-cbcs-frag-v1 enc-cenc-frag-a1 enc-piff-frag-v1; do
    f="../$n.mp4"
    tcase split.$n.default        mp4split $f
    tcase split.$n.verbose        mp4split --verbose $f
    tcase split.$n.video          mp4split --video $f
    tcase split.$n.audio          mp4split --audio $f
    tcase split.$n.init-only      mp4split --init-only $f
    tcase split.$n.video-init     mp4split $f --video --init-only --init-segment video-init.mp4
    tcase split.$n.track-1        mp4split --track-id 1 $f
    tcase split.$n.track-2        mp4split --track-id 2 $f
    tcase split.$n.track-1-2      mp4split --track-id 1,2 $f
    tcase split.$n.track-2-1      mp4split --track-id 2,1 $f
    tcase split.$n.track-2x2      mp4split --track-id 2 --track-id 1 $f
    tcase split.$n.track-3        mp4split --track-id 3 $f
    tcase split.$n.params-n       mp4split --pattern-parameters N --media-segment "seg-%llu.m4s" $f
    tcase split.$n.params-i       mp4split --pattern-parameters I --media-segment "trk-%llu.m4s" $f
    tcase split.$n.params-ni      mp4split --pattern-parameters NI --media-segment "s-%05llu-t%llu.m4s" $f
    tcase split.$n.params-nn      mp4split --pattern-parameters NN --media-segment "s-%llu-%03llu.m4s" $f
    tcase split.$n.params-ii      mp4split --pattern-parameters II $f
    tcase split.$n.start-0        mp4split --start-number 0 $f
    tcase split.$n.start-100      mp4split --start-number 100 --pattern-parameters N --media-segment "seg-%llu.m4s" $f
    tcase split.$n.names          mp4split --init-segment my-init.mp4 --media-segment "media-%llu-%llu.m4s" $f
    tcase split.$n.hex-names      mp4split --media-segment "m-%llx-%08llX.m4s" --start-number 250 $f
    tcase split.$n.signed-names   mp4split --media-segment "m-%lld-%-6lld-.m4s" $f
    tcase split.$n.percent-name   mp4split --media-segment "m-%%-%llu-%llu.m4s" $f
    tcase split.$n.fixed-name     mp4split --media-segment "all.m4s" $f
    tcase split.$n.audio-n        mp4split --audio --start-number 5 --pattern-parameters N --media-segment "a-%llu.m4s" $f
    tcase split.$n.video-names    mp4split --video --init-segment v-init.mp4 --media-segment "v-%llu-%llu.m4s" $f
done
tcase split.err.no-args             mp4split
tcase split.err.options-only        mp4split --verbose --audio
tcase split.err.no-input            mp4split ../no-such-file.mp4
tcase split.err.extra-arg           mp4split ../frag-v1.mp4 extra
tcase split.err.init-no-arg         mp4split ../frag-v1.mp4 --init-segment
tcase split.err.media-no-arg        mp4split ../frag-v1.mp4 --media-segment
tcase split.err.params-no-arg       mp4split ../frag-v1.mp4 --pattern-parameters
tcase split.err.audio-video         mp4split --audio --video ../frag-v1.mp4
tcase split.err.audio-track         mp4split --audio --track-id 1 ../frag-v1.mp4
tcase split.err.video-track         mp4split --track-id 1 --video ../frag-v1.mp4
tcase split.err.params-long         mp4split --pattern-parameters INN ../frag-v1.mp4
tcase split.err.params-short        mp4split --pattern-parameters "" ../frag-v1.mp4
tcase split.err.params-invalid      mp4split --pattern-parameters IX ../frag-v1.mp4
tcase split.err.params-lowercase    mp4split --pattern-parameters n ../frag-v1.mp4
tcase split.err.track-empty         mp4split --track-id "" ../frag-v1.mp4
tcase split.err.track-empty-item    mp4split --track-id "1,,2" ../frag-v1.mp4
tcase split.err.track-not-number    mp4split --track-id abc ../frag-v1.mp4
tcase split.err.track-too-many      mp4split --track-id 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1 ../frag-v1.mp4
tcase split.err.track-32            mp4split --track-id 1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,2 ../frag-v1.mp4
tcase split.err.start-not-number    mp4split --start-number abc ../frag-v1.mp4
tcase split.err.start-negative      mp4split --start-number -2 ../frag-v1.mp4
tcase split.err.no-audio            mp4split --audio ../frag-v1-video-only.mp4
tcase split.err.no-video            mp4split --video ../frag-a1.mp4
tcase split.err.init-no-dir         mp4split --init-segment no-such-dir/init.mp4 ../frag-v1.mp4
tcase split.err.media-no-dir        mp4split --media-segment "no-such-dir/seg-%llu-%llu.m4s" ../frag-v1.mp4
tcase split.err.not-mp4             mp4split ../not-mp4.mp4
tcase split.err.empty-file          mp4split ../empty.bin
tcase split.err.truncated           mp4split ../truncated.mp4

#----------------------------------------------------------------------
# mp4fragment
#----------------------------------------------------------------------
echo "=== mp4fragment ==="
for n in $STOCK enc-cenc-v1 enc-cenc-a1 enc-cenc-frag-v1 enc-cbcs-frag-v1 enc-oma-v1 enc-isma-a1 frag-v1-video-only frag-a1-index frag-v1-500; do
    f="../$n.mp4"
    tcase fragment.$n.default         mp4fragment $f out.mp4
    tcase fragment.$n.quiet           mp4fragment --quiet $f out.mp4
    tcase fragment.$n.duration-500    mp4fragment --fragment-duration 500 $f out.mp4
    tcase fragment.$n.duration-1000   mp4fragment --fragment-duration 1000 $f out.mp4
    tcase fragment.$n.duration-5000   mp4fragment $f out.mp4 --fragment-duration 5000
    tcase fragment.$n.duration-33     mp4fragment --fragment-duration 33 $f out.mp4
    tcase fragment.$n.duration-1      mp4fragment --fragment-duration 1 $f out.mp4
    tcase fragment.$n.duration-huge   mp4fragment --fragment-duration 4000000000 $f out.mp4
    tcase fragment.$n.index           mp4fragment --index $f out.mp4
    tcase fragment.$n.index-1000      mp4fragment --index --fragment-duration 1000 --tfdt-start 3.5 $f out.mp4
    tcase fragment.$n.timescale-smooth mp4fragment --timescale 10000000 $f out.mp4
    tcase fragment.$n.timescale-1000  mp4fragment --timescale 1000 $f out.mp4
    tcase fragment.$n.timescale-90000 mp4fragment --timescale 90000 --index $f out.mp4
    tcase fragment.$n.timescale-7     mp4fragment --timescale 7 $f out.mp4
    tcase fragment.$n.track-video     mp4fragment --track video $f out.mp4
    tcase fragment.$n.track-audio     mp4fragment --track audio $f out.mp4
    tcase fragment.$n.track-audio-idx mp4fragment --track audio --index --fragment-duration 700 $f out.mp4
    tcase fragment.$n.track-subtitles mp4fragment --track subtitles $f out.mp4
    tcase fragment.$n.track-1         mp4fragment --track 1 $f out.mp4
    tcase fragment.$n.track-2         mp4fragment --track 2 --index $f out.mp4
    tcase fragment.$n.track-3         mp4fragment --track 3 $f out.mp4
    tcase fragment.$n.track-prefix    mp4fragment --track videoXYZ $f out.mp4
    tcase fragment.$n.track-not-num   mp4fragment --track xyz $f out.mp4
    tcase fragment.$n.tfdt-start      mp4fragment --tfdt-start 10.5 $f out.mp4
    tcase fragment.$n.tfdt-start-exp  mp4fragment --tfdt-start 1e3 --index $f out.mp4
    tcase fragment.$n.tfdt-start-neg  mp4fragment --tfdt-start -1 $f out.mp4
    tcase fragment.$n.tfdt-start-bad  mp4fragment --tfdt-start abc $f out.mp4
    tcase fragment.$n.sequence-7      mp4fragment --sequence-number-start 7 $f out.mp4
    tcase fragment.$n.sequence-0      mp4fragment --sequence-number-start 0 $f out.mp4
    tcase fragment.$n.force-auto      mp4fragment --force-i-frame-sync auto $f out.mp4
    tcase fragment.$n.force-auto-dbg  mp4fragment --force-i-frame-sync auto --debug $f out.mp4
    tcase fragment.$n.force-all       mp4fragment --force-i-frame-sync all $f out.mp4
    tcase fragment.$n.force-all-500   mp4fragment --force-i-frame-sync all --fragment-duration 500 --verbosity 3 $f out.mp4
    tcase fragment.$n.copy-udta       mp4fragment --copy-udta $f out.mp4
    tcase fragment.$n.no-tfdt         mp4fragment --no-tfdt $f out.mp4
    tcase fragment.$n.trim            mp4fragment --trim $f out.mp4
    tcase fragment.$n.trim-500-debug  mp4fragment --trim --fragment-duration 500 --debug $f out.mp4
    tcase fragment.$n.trun-v0         mp4fragment --trun-version-zero $f out.mp4
    tcase fragment.$n.trun-v0-elst    mp4fragment --trun-version-zero --no-zero-elst $f out.mp4
    tcase fragment.$n.trun-v0-ts      mp4fragment --trun-version-zero --timescale 90000 $f out.mp4
    tcase fragment.$n.trun-v0-ts-elst mp4fragment --trun-version-zero --timescale 10000000 --no-zero-elst $f out.mp4
    tcase fragment.$n.no-zero-elst    mp4fragment --no-zero-elst $f out.mp4
    tcase fragment.$n.verbosity-0     mp4fragment --verbosity 0 $f out.mp4
    tcase fragment.$n.verbosity-1     mp4fragment --verbosity 1 $f out.mp4
    tcase fragment.$n.verbosity-2     mp4fragment --verbosity 2 $f out.mp4
    tcase fragment.$n.verbosity-3     mp4fragment --verbosity 3 $f out.mp4
    tcase fragment.$n.debug           mp4fragment --debug $f out.mp4
    tcase fragment.$n.debug-v0        mp4fragment --debug --verbosity 0 --fragment-duration 800 $f out.mp4
    tcase fragment.$n.debug-v3        mp4fragment --debug --verbosity 3 --index --timescale 48000 $f out.mp4
    tcase fragment.$n.smooth          mp4fragment --track video --trun-version-zero --no-tfdt --timescale 10000000 $f out.mp4
    tcase fragment.$n.all-options     mp4fragment --index --copy-udta --sequence-number-start 42 --tfdt-start 0.25 --trim --no-zero-elst --trun-version-zero --fragment-duration 1500 --timescale 44100 --verbosity 3 --debug $f out.mp4
done
# number parsing (strtod, strtoul) and out of range values
n=0
for v in 0x1.8p1 0X10 0x.8p+3 0x 0x1p-2 .5 5. 1e 1e+ +2.5e1xyz " 3" inf -inf nan 1e30 1e400 -0.5 4e14; do
    n=$((n+1))
    tcase fragment.num.tfdt-start-$n   mp4fragment --tfdt-start "$v" --index ../audio-aac-001.mp4 out.mp4
done
n=0
for v in -1 " 12" +7 99999999999999999999 4294967295 0012 12abc ""; do
    n=$((n+1))
    tcase fragment.num.sequence-$n     mp4fragment --sequence-number-start "$v" ../audio-aac-001.mp4 out.mp4
    tcase fragment.num.timescale-$n    mp4fragment --timescale "$v" ../video-h264-001.mp4 out.mp4
    tcase fragment.num.verbosity-$n    mp4fragment --verbosity "$v" ../video-h264-001.mp4 out.mp4
done
tcase fragment.err.no-args            mp4fragment
tcase fragment.err.options-only       mp4fragment --index
tcase fragment.err.no-output          mp4fragment ../audio-aac-001.mp4
tcase fragment.err.no-input-file      mp4fragment ../no-such-file.mp4 out.mp4
tcase fragment.err.no-input-no-output mp4fragment ../no-such-file.mp4
tcase fragment.err.extra-arg          mp4fragment ../audio-aac-001.mp4 out.mp4 extra
tcase fragment.err.unknown-option     mp4fragment --bogus ../audio-aac-001.mp4 out.mp4
tcase fragment.err.no-output-dir      mp4fragment ../audio-aac-001.mp4 no-such-dir/out.mp4
tcase fragment.err.verbosity-no-arg   mp4fragment ../audio-aac-001.mp4 out.mp4 --verbosity
tcase fragment.err.tfdt-no-arg        mp4fragment ../audio-aac-001.mp4 out.mp4 --tfdt-start
tcase fragment.err.sequence-no-arg    mp4fragment ../audio-aac-001.mp4 out.mp4 --sequence-number-start
tcase fragment.err.force-no-arg       mp4fragment ../audio-aac-001.mp4 out.mp4 --force-i-frame-sync
tcase fragment.err.force-bad-mode     mp4fragment --force-i-frame-sync none ../video-h264-001.mp4 out.mp4
tcase fragment.err.duration-no-arg    mp4fragment ../audio-aac-001.mp4 out.mp4 --fragment-duration
tcase fragment.err.timescale-no-arg   mp4fragment ../audio-aac-001.mp4 out.mp4 --timescale
tcase fragment.err.track-no-arg       mp4fragment ../audio-aac-001.mp4 out.mp4 --track
tcase fragment.err.verbosity-bad      mp4fragment --verbosity abc ../video-h264-001.mp4 out.mp4
tcase fragment.err.duration-bad       mp4fragment --fragment-duration 12abc ../video-h264-001.mp4 out.mp4
tcase fragment.err.not-mp4            mp4fragment ../not-mp4.mp4 out.mp4
tcase fragment.err.empty-file         mp4fragment ../empty.bin out.mp4
tcase fragment.err.truncated          mp4fragment ../truncated.mp4 out.mp4
tcase fragment.err.ftyp-only          mp4fragment ../ftyp.atom out.mp4
#----------------------------------------------------------------------
# synthetic inputs (other codecs, many tracks, fragments with every optional
# field, odd structures, truncated files): all five tools
#----------------------------------------------------------------------
PYTHON="$(command -v python3 || command -v python)"
if [ -n "$PYTHON" ] && "$PYTHON" -c "import sys; sys.exit(0 if sys.version_info[0] >= 3 else 1)" 2> /dev/null; then
    echo "=== synthetic inputs ==="
    "$PYTHON" "$ROOT/tests/make-synthetic-inputs.py" "$GEN" >> "$WORK/gen.log" 2>&1 || echo "generation failed: synthetic inputs" >> "$WORK/gen.log"
    # derived inputs: two video tracks, a track without samples
    ref mp4extract "moov/trak[0]" video-h264-001.mp4 trak0.atom
    ref mp4edit --insert moov:trak0.atom:1 video-h264-001.mp4 two-video.mp4
    ref mp4edit --remove "moov/trak[1]/mdia/minf/stbl/stsz" video-h264-001.mp4 no-samples.mp4
    ref mp4edit --remove "moov/trak[0]/mdia/minf/stbl/stss" video-h264-001.mp4 no-stss.mp4
    ref mp4edit --remove "moov/trak[0]/mdia/minf/stbl/stsz" --remove "moov/trak[1]/mdia/minf/stbl/stsz" video-h264-001.mp4 no-samples-at-all.mp4
    ref mp4fragment syn-codecs.mp4 syn-codecs-frag.mp4
    for f in "$GEN"/syn-*.mp4 "$GEN"/enc-syn-*.mp4 "$GEN"/two-video.mp4 "$GEN"/no-samples.mp4 "$GEN"/no-stss.mp4 "$GEN"/no-samples-at-all.mp4; do
        [ -e "$f" ] || continue
        n="$(basename "$f" .mp4)"
        f="../$n.mp4"
        tcase syn.$n.extract-moov        mp4extract moov $f out.atom
        tcase syn.$n.extract-stsd        mp4extract --payload-only "moov/trak[1]/mdia/minf/stbl/stsd" $f out.bin
        tcase syn.$n.extract-traf        mp4extract "moof/traf[1]" $f out.atom
        tcase syn.$n.extract-meta        mp4extract moov/udta/meta/ilst $f out.atom
        tcase syn.$n.compact             mp4compact --verbose $f out.mp4
        tcase syn.$n.edit-none           mp4edit $f out.mp4
        tcase syn.$n.edit-remove         mp4edit --remove "moov/trak[1]" --remove moov/udta $f out.mp4
        tcase syn.$n.edit-insert         mp4edit --insert moov/trak:../udta.atom:1 --insert ":$UUID#../mvhd.payload:0" $f out.mp4
        tcase syn.$n.edit-replace        mp4edit --replace moov/trak/mdia/minf/stbl/stsd:../stsd.atom $f out.mp4
        tcase syn.$n.split               mp4split $f
        tcase syn.$n.split-video         mp4split --video $f
        tcase syn.$n.split-audio         mp4split --audio --pattern-parameters N --media-segment "a-%llu.m4s" $f
        tcase syn.$n.split-track-2       mp4split --track-id 2 $f
        tcase syn.$n.split-track-1-3     mp4split --track-id 1,3 --start-number 10 $f
        tcase syn.$n.split-init-only     mp4split --init-only $f
        tcase syn.$n.fragment            mp4fragment $f out.mp4
        tcase syn.$n.fragment-debug      mp4fragment --debug --verbosity 3 --index $f out.mp4
        tcase syn.$n.fragment-500        mp4fragment --fragment-duration 500 --timescale 90000 $f out.mp4
        tcase syn.$n.fragment-video      mp4fragment --track video --trun-version-zero $f out.mp4
        tcase syn.$n.fragment-audio      mp4fragment --track audio --index --tfdt-start 2.5 $f out.mp4
        tcase syn.$n.fragment-subtitles  mp4fragment --track subtitles $f out.mp4
        tcase syn.$n.fragment-track-3    mp4fragment --track 3 --no-tfdt --copy-udta $f out.mp4
        tcase syn.$n.fragment-force      mp4fragment --force-i-frame-sync all --debug $f out.mp4
        tcase syn.$n.fragment-force-auto mp4fragment --force-i-frame-sync auto --debug $f out.mp4
        tcase syn.$n.fragment-trim       mp4fragment --trim --trun-version-zero --no-zero-elst --verbosity 2 $f out.mp4
    done
else
    echo "(python 3 not found: no synthetic inputs)"
fi
flush_cases

#----------------------------------------------------------------------
# summary
#----------------------------------------------------------------------
echo "=== summary: $PASSED passed, $FAILED failed, $SKIPPED skipped ==="
if [ $FAILED -ne 0 ]; then
    for f in "${FAILURES[@]}"; do echo "  failed: $f"; done
    exit 1
fi
exit 0
