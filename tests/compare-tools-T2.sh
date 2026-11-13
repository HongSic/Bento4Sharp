#!/bin/bash
# Compares the ported tools of group T2 (mp4encrypt, mp4decrypt, mp4pssh,
# mp4dcfpackager, mp4tag) with the original Bento4 tools: for each case the
# original tool and the port are run with the same arguments, and the standard
# output, the standard error, the exit code and every output file are compared
# byte for byte.
#
# usage: tests/compare-tools-T2.sh [<reference-dir> [<test-data-dir>]]
#
#   <reference-dir>  (or $BENTO4_REFERENCE_DIR) directory with
#                      bin/<tool>.js  the original tools built with emscripten, run with node:
#                          mp4encrypt mp4decrypt mp4pssh mp4dcfpackager mp4tag
#                          and, to generate the inputs, mp4fragment mp4split
#   <test-data-dir>  (or $BENTO4_TEST_DATA_DIR) the Test/Data directory of the Bento4 sources (*.mp4)
#
# other environment variables:
#   BENTO4SHARP_ARTIFACTS   where to build (default: a temporary directory, removed on exit)
#   BENTO4SHARP_WORK_DIR    where to put generated inputs and outputs (default: a temporary directory)
#   BENTO4SHARP_SKIP_BUILD  set to 1 to reuse the binaries already in $BENTO4SHARP_ARTIFACTS
#   BENTO4SHARP_FRAMEWORK   target framework of the port to build and run (default: net6.0; net20 runs
#                           the .NET Framework 2.0 executables)
#   BENTO4SHARP_ONLY        only run the cases whose name matches this regular expression (to
#                           investigate a failure; a case that needs the output of a case that
#                           was not run fails)
#
# exit code: 0 if every case passes, 1 otherwise.
#
# Notes about the original tools built with emscripten:
# - no random bytes: 'random' keys/IVs fail, so every case uses explicit IVs;
# - mp4pssh --insert/--replace writes a correct output file and then crashes
#   (double delete of the 'pssh' atom in the original code): for those cases
#   only the output file is compared, and the port must exit with 0 and print
#   nothing (what a native build does on the success path);
# - mp4tag keeps the metadata it has listed for --show-tags/--extract and uses
#   it again after --set/--remove have deleted the atoms it points to (use
#   after free in the original code): no case lists tags, changes an existing
#   tag, and lists again;
# - `long` is 32 bits wide in that build (as in native Windows builds).
# With Git Bash on Windows, negative exit codes (mp4tag returns AP4 error codes)
# are all seen as 127: they are only compared exactly on other systems.

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
GEN="$WORK/t2-ref"      # where the original tools run
GENP="$WORK/t2-port"    # where the ported tools run (same inputs)
rm -rf "$GEN" "$GENP" "$WORK/t2-failed"
mkdir -p "$GEN" "$GENP"

if ! command -v node > /dev/null; then
    echo "node not found"
    exit 1
fi
for tool in mp4encrypt mp4decrypt mp4pssh mp4dcfpackager mp4tag mp4fragment mp4split; do
    if [ ! -f "$REF/bin/$tool.js" ]; then
        echo "original tool not found: $REF/bin/$tool.js"
        exit 1
    fi
done

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
    for name in Mp4Encrypt Mp4Decrypt Mp4Pssh Mp4DcfPackager Mp4Tag; do
        if ! dotnet build "$ROOT/src/Tools/$name/$name.csproj" -c Release -f "$FW" -nologo -v q -nodeReuse:false --artifacts-path "$ART" > "$WORK/t2-build.log" 2>&1; then
            cat "$WORK/t2-build.log"
            echo "BUILD FAILED: $name"
            exit 1
        fi
    done
fi

# port_dll <tool>: sets $DLL, the path of the ported tool
port_dll() {
    case "$1" in
        mp4encrypt)     DLL="$ART/bin/Mp4Encrypt/release_$FW/mp4encrypt.dll" ;;
        mp4decrypt)     DLL="$ART/bin/Mp4Decrypt/release_$FW/mp4decrypt.dll" ;;
        mp4pssh)        DLL="$ART/bin/Mp4Pssh/release_$FW/mp4pssh.dll" ;;
        mp4dcfpackager) DLL="$ART/bin/Mp4DcfPackager/release_$FW/mp4dcfpackager.dll" ;;
        mp4tag)         DLL="$ART/bin/Mp4Tag/release_$FW/mp4tag.dll" ;;
    esac
}

#----------------------------------------------------------------------
# helpers
#----------------------------------------------------------------------
PASSED=0; FAILED=0; SKIPPED=0
FAILURES=()

# t2 <case name> <tool> <output files (space separated, relative, in one argument)> <arguments>...
# Runs the original tool in $GEN and the port in $GENP (two directories with
# the same content) with the same arguments, then compares stdout, stderr, the
# exit code and the output files. An output file that only one of the tools
# wrote is a difference. The output files stay where they are, so that later
# cases can use them as inputs (when they differ, the port's directory gets
# the file of the original tool, so that each case stands on its own).
# When UPSTREAM_CRASH=1, stdout/stderr/exit code of the original tool are
# ignored: the port must exit with 0 and print nothing.
# (the function starts as few processes as it can: that is what takes the
# time on Windows)
t2() {
    local name="$1" tool="$2" outs="$3" o bad="" erc rc pid list=() sums=() line i n
    shift 3
    if [ -n "$BENTO4SHARP_ONLY" ] && [[ ! "$name" =~ $BENTO4SHARP_ONLY ]]; then return; fi
    port_dll "$tool"
    for o in $outs; do list+=("$GEN/$o" "$GENP/$o"); done
    [ ${#list[@]} -eq 0 ] || rm -f "${list[@]}"
    cd "$GENP" || exit 1
    port "$DLL" "$@" > "$GENP/.stdout" 2> "$GENP/.stderr" &
    pid=$!
    cd "$GEN" || exit 1
    node "$REF/bin/$tool.js" "$@" > "$GEN/.stdout" 2> "$GEN/.stderr"
    erc=$?
    wait $pid
    rc=$?

    # what to compare (pairs of files)
    list=()
    if [ "$UPSTREAM_CRASH" = "1" ]; then
        [ $rc -eq 0 ] || bad="$bad exit-code($rc,expected:0)"
        [ ! -s "$GENP/.stdout" ] || bad="$bad stdout"
        [ ! -s "$GENP/.stderr" ] || bad="$bad stderr"
    else
        [ $rc -eq $erc ] || bad="$bad exit-code($rc,expected:$erc)"
        list+=("$GEN/.stdout" "$GENP/.stdout" "$GEN/.stderr" "$GENP/.stderr")
    fi
    for o in $outs; do
        if [ -f "$GEN/$o" ] && [ -f "$GENP/$o" ]; then
            list+=("$GEN/$o" "$GENP/$o")
        elif [ -f "$GEN/$o" ] || [ -f "$GENP/$o" ]; then
            bad="$bad $o(written-by-one-tool-only)"
        fi
    done
    if [ ${#list[@]} -ne 0 ]; then
        sha1sum -b "${list[@]}" > "$WORK/t2-sums" 2> /dev/null
        while read -r line; do sums+=("${line%% *}"); done < "$WORK/t2-sums"
        n=${#list[@]}
        [ ${#sums[@]} -eq $n ] || bad="$bad (checksums-failed)"
        for ((i=0; i<n; i+=2)); do
            [ "${sums[$i]}" = "${sums[$((i+1))]}" ] || bad="$bad ${list[$i]##*/}"
        done
    fi

    if [ -z "$bad" ]; then
        echo "PASS  $name"
        PASSED=$((PASSED+1))
    else
        echo "FAIL  $name  [$tool $*] differs in:$bad"
        FAILED=$((FAILED+1))
        FAILURES+=("$name:$bad")
        # keep what differs, and give the port's directory the original outputs
        mkdir -p "$WORK/t2-failed/$name.expected" "$WORK/t2-failed/$name.actual"
        cp "$GEN/.stdout" "$GEN/.stderr" "$WORK/t2-failed/$name.expected/"
        cp "$GENP/.stdout" "$GENP/.stderr" "$WORK/t2-failed/$name.actual/"
        for o in $outs; do
            [ -f "$GEN/$o" ] && cp "$GEN/$o" "$WORK/t2-failed/$name.expected/"
            [ -f "$GENP/$o" ] && cp "$GENP/$o" "$WORK/t2-failed/$name.actual/"
            rm -f "$GENP/$o"
            [ -f "$GEN/$o" ] && cp "$GEN/$o" "$GENP/$o"
        done
    fi
}

# ref <tool> <arguments>...: runs an original tool (in $GEN) to generate an input
ref() {
    local tool="$1"; shift
    node "$REF/bin/$tool.js" "$@" >> "$WORK/t2-gen.log" 2>&1 || echo "generation failed: $tool $*" >> "$WORK/t2-gen.log"
}

# mirror <file>...: copies generated inputs from $GEN to the port's directory
mirror() {
    local f
    for f in "$@"; do
        [ -f "$GEN/$f" ] && cp "$GEN/$f" "$GENP/$f"
    done
}

# need <file>...: fails when a generated input is missing
need() {
    local f
    for f in "$@"; do
        if [ ! -s "$f" ]; then
            echo "FAIL  $f (input was not generated, see $WORK/t2-gen.log)"
            FAILED=$((FAILED+1))
            FAILURES+=("$f generation")
        fi
    done
}

#----------------------------------------------------------------------
# inputs
#----------------------------------------------------------------------
cp "$DATA/audio-aac-001.mp4" "$GEN/audio.mp4"
cp "$DATA/video-h264-001.mp4" "$GEN/video.mp4"
# the emscripten tools only see the current directory: use relative names
cd "$GEN" || exit 1
: > "$WORK/t2-gen.log"

ref mp4fragment video.mp4 fvideo.mp4
ref mp4fragment audio.mp4 faudio.mp4
ref mp4fragment --fragment-duration 500 video.mp4 f500.mp4
ref mp4split --init-segment init.mp4 --media-segment "seg-%llu-%04llu.m4s" f500.mp4
need fvideo.mp4 faudio.mp4 f500.mp4 init.mp4 seg-1-0001.m4s seg-2-0001.m4s

printf 'pssh-payload-\000\001\002\377 data' > pssh1.bin
printf '\010\001\022\020\000\001\002\003\004\005\006\007\010\011\012\013\014\015\016\017' > pssh2.bin
: > empty.bin
printf '\377\330\377\340\000\020JFIF\000\001\001\000\000\001\000\001\000\000\377\331' > cover.jpg
printf 'GIF89a\001\000\001\000\200\000\000\377\377\377\000\000\000;' > cover.gif
printf 'Hello DCF packager\nline 2\n' > hello.txt
printf 'not an mp4 file at all' > garbage.mp4
cp video.mp4 "vidéo-✓.mp4"
cp pssh1.bin "päyload-✓.bin"
cp cover.jpg "cövér-✓.jpg"
cp seg-1-0001.m4s "ségment-✓.m4s"
cp "$GEN"/* "$GENP"/

K1=000102030405060708090a0b0c0d0e0f
K2=101112131415161718191a1b1c1d1e1f
K3=202122232425262728292a2b2c2d2e2f
IV8A=0001020304050607
IV8B=1011121314151617
IV16A=a0a1a2a3a4a5a6a7a8a9aaabacadaeaf
IV16B=b0b1b2b3b4b5b6b7b8b9babbbcbdbebf
KID1=00112233445566778899aabbccddeeff
KID2=ffeeddccbbaa99887766554433221100
SYS1=edef8ba979d64acea3c827dcd51d21ed
SYS2=9a04f07998404286ab92e65be0885f95
SYS3=1077efecc0b24d02ace33c1e52e2fb4b

METHODS="OMA-PDCF-CBC OMA-PDCF-CTR MARLIN-IPMP-ACBC MARLIN-IPMP-ACGK ISMA-IAEC PIFF-CBC PIFF-CTR MPEG-CENC MPEG-CBC1 MPEG-CENS MPEG-CBCS"

# method_options <method>: sets $MOPT, the options a method needs besides the track keys
method_options() {
    case "$1" in
        ISMA-IAEC)        MOPT=(--kms-uri urn:example:kms) ;;
        MARLIN-IPMP-ACGK) MOPT=(--key 0:$K3:0000000000000000) ;;
        *)                MOPT=() ;;
    esac
}

#----------------------------------------------------------------------
# mp4encrypt
#----------------------------------------------------------------------
echo "=== mp4encrypt ==="
ENCRYPTED=()
for m in $METHODS; do
    method_options $m
    for i in audio video faudio fvideo; do
        # 64-bit IVs
        o="enc-$m-$i.mp4"
        t2 "encrypt.$m.$i" mp4encrypt "$o" --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B $i.mp4 "$o"
        ENCRYPTED+=("$o")
    done
    for i in video fvideo; do
        # 128-bit IVs
        o="enc16-$m-$i.mp4"
        t2 "encrypt.$m.$i.iv16" mp4encrypt "$o" --method $m "${MOPT[@]}" --key 1:$K1:$IV16A --key 2:$K2:$IV16B $i.mp4 "$o"
        ENCRYPTED+=("$o")
    done
    # one track left in the clear, progress
    o="enc1-$m-fvideo.mp4"
    t2 "encrypt.$m.fvideo.track2-only.progress" mp4encrypt "$o" --show-progress --method $m "${MOPT[@]}" --key 2:$K2:$IV8B fvideo.mp4 "$o"
    ENCRYPTED+=("$o")
    t2 "encrypt.$m.video.strict-fail" mp4encrypt "o.mp4" --strict --method $m "${MOPT[@]}" --key 1:$K1:$IV8A video.mp4 o.mp4
    t2 "encrypt.$m.fvideo.strict-ok" mp4encrypt "o.mp4" --strict --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B fvideo.mp4 o.mp4
    t2 "encrypt.$m.video.progress" mp4encrypt "o.mp4" --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B --show-progress video.mp4 o.mp4
    t2 "encrypt.$m.no-key" mp4encrypt "o.mp4" --method $m fvideo.mp4 o.mp4
    t2 "encrypt.$m.garbage-input" mp4encrypt "o.mp4" --method $m "${MOPT[@]}" --key 1:$K1:$IV8A garbage.mp4 o.mp4
    t2 "encrypt.$m.empty-input" mp4encrypt "o.mp4" --method $m "${MOPT[@]}" --key 1:$K1:$IV8A empty.bin o.mp4
    t2 "encrypt.$m.segment-input" mp4encrypt "o.mp4" --method $m "${MOPT[@]}" --key 1:$K1:$IV8A seg-1-0001.m4s o.mp4
    t2 "encrypt.$m.short-iv" mp4encrypt "o.mp4" --method $m "${MOPT[@]}" --key 1:$K1:0a0b0c --key 2:$K2: fvideo.mp4 o.mp4
    t2 "encrypt.$m.fragments-info" mp4encrypt "o.m4s" --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B --fragments-info init.mp4 seg-1-0001.m4s o.m4s
done

# properties
for m in MPEG-CENC MPEG-CBC1 MPEG-CENS MPEG-CBCS PIFF-CTR PIFF-CBC; do
    for i in fvideo faudio video; do
        o="enckid-$m-$i.mp4"
        t2 "encrypt.$m.$i.kid" mp4encrypt "$o" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B --property 1:KID:$KID1 --property 2:KID:$KID2 $i.mp4 "$o"
        ENCRYPTED+=("$o")
    done
    t2 "encrypt.$m.content-id" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B --property 1:KID:$KID1 \
        --property 1:ContentId:urn:marlin:kid:1 --property 2:ContentId:urn:marlin:kid:2 fvideo.mp4 o.mp4
    t2 "encrypt.$m.content-id.padding" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B \
        --property 1:ContentId:urn:marlin:kid:1 --property 0:PsshPadding:1024 fvideo.mp4 o.mp4
    t2 "encrypt.$m.content-id.small-padding" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B \
        --property 2:ContentId:x --property 0:PsshPadding:10 fvideo.mp4 o.mp4
    t2 "encrypt.$m.unknown-property" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --property 1:Foo:bar --property 7:KID:$KID1 fvideo.mp4 o.mp4
    # global options
    for g in eme-pssh piff-compatible piff-iv-size-16 iv-size-8 no-senc; do
        t2 "encrypt.$m.global.$g" mp4encrypt "o.mp4" --method $m --global-option mpeg-cenc.$g:true --key 1:$K1:$IV8A --key 2:$K2:$IV8B \
            --property 1:KID:$KID1 --property 2:KID:$KID2 fvideo.mp4 o.mp4
        t2 "encrypt.$m.global.$g.iv16" mp4encrypt "o.mp4" --method $m --global-option mpeg-cenc.$g:true --key 1:$K1:$IV16A --key 2:$K2:$IV16B fvideo.mp4 o.mp4
        t2 "encrypt.$m.global.$g.false" mp4encrypt "o.mp4" --method $m --global-option mpeg-cenc.$g:false --key 1:$K1:$IV8A faudio.mp4 o.mp4
    done
    o="encpiffcompat-$m-fvideo.mp4"
    t2 "encrypt.$m.global.piff-all" mp4encrypt "$o" --method $m --global-option mpeg-cenc.piff-compatible:true --global-option mpeg-cenc.piff-iv-size-16:true \
        --key 1:$K1:$IV16A --key 2:$K2:$IV16B fvideo.mp4 "$o"
    ENCRYPTED+=("$o")
    o="encnosenc-$m-fvideo.mp4"
    t2 "encrypt.$m.global.all" mp4encrypt "$o" --method $m --global-option mpeg-cenc.eme-pssh:true --global-option mpeg-cenc.iv-size-8:true \
        --global-option mpeg-cenc.no-senc:true --global-option other.option:value --key 1:$K1:$IV8A --key 2:$K2:$IV8B --property 2:KID:$KID2 fvideo.mp4 "$o"
    ENCRYPTED+=("$o")
    # pssh
    o="encpssh-$m-fvideo.mp4"
    t2 "encrypt.$m.pssh" mp4encrypt "$o" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B --pssh $SYS1:pssh1.bin --pssh $SYS2: --pssh $SYS3:empty.bin fvideo.mp4 "$o"
    ENCRYPTED+=("$o")
    t2 "encrypt.$m.pssh-v1" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B --property 1:KID:$KID1 --property 2:KID:$KID2 \
        --pssh-v1 $SYS1:pssh2.bin --pssh-v1 $SYS2: --pssh $SYS3:pssh1.bin fvideo.mp4 o.mp4
    t2 "encrypt.$m.pssh-v1.no-kid" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --pssh-v1 $SYS1:pssh2.bin --property 1:KID:$KID1 faudio.mp4 o.mp4
    t2 "encrypt.$m.pssh-v1.same-kid" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B --property 1:KID:$KID1 --property 2:KID:$KID1 \
        --pssh-v1 $SYS1:pssh2.bin fvideo.mp4 o.mp4
    t2 "encrypt.$m.pssh.plain-file" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --pssh $SYS1:pssh1.bin audio.mp4 o.mp4
    # separate fragments
    t2 "encrypt.$m.multi" mp4encrypt "encm-seg-1-0001.m4s encm-seg-2-0001.m4s" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B \
        --fragments-info init.mp4 --multi seg-1-0001.m4s seg-2-0001.m4s encm-%s.m4s
    t2 "encrypt.$m.multi.progress.strict" mp4encrypt "encm-seg-1-0001.m4s encm-seg-2-0001.m4s" --method $m --key 1:$K1:$IV8A \
        --fragments-info init.mp4 --multi --show-progress --strict seg-1-0001.m4s seg-2-0001.m4s encm-%s.m4s
done
for m in OMA-PDCF-CBC OMA-PDCF-CTR; do
    o="encprop-$m-video.mp4"
    t2 "encrypt.$m.properties" mp4encrypt "$o" --method $m --key 1:$K1:$IV8A --key 2:$K2:$IV8B --property 1:ContentId:cid-1234 \
        --property 1:RightsIssuerUrl:http://example.com/ri --property 1:Custom:value --property 1:Other:value:with:colons --property 2:ContentId:cid-2 \
        --property 2:Header: video.mp4 "$o"
    ENCRYPTED+=("$o")
    t2 "encrypt.$m.properties.fragmented" mp4encrypt "o.mp4" --method $m --key 1:$K1:$IV8A --property 1:ContentId:cid-1234 --property 1:X:y faudio.mp4 o.mp4
done
for m in MARLIN-IPMP-ACBC MARLIN-IPMP-ACGK; do
    method_options $m
    o="encprop-$m-video.mp4"
    t2 "encrypt.$m.properties" mp4encrypt "$o" --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B \
        --property 1:ContentId:urn:marlin:organization:example:cid-1 --property 2:ContentId:urn:marlin:organization:example:cid-2 --property 2:Other:x video.mp4 "$o"
    ENCRYPTED+=("$o")
done
t2 "encrypt.isma.kms-uri" mp4encrypt "o.mp4" --method ISMA-IAEC --kms-uri https://kms.example.com/key?id=1 --key 1:$K1:$IV8A --key 2:$K2:$IV8B video.mp4 o.mp4
t2 "encrypt.isma.kms-uri.empty" mp4encrypt "o.mp4" --method ISMA-IAEC --kms-uri "" --key 1:$K1:$IV8A audio.mp4 o.mp4
t2 "encrypt.kid-style-track" mp4encrypt "o.mp4" --method MPEG-CENC --key $KID1:$K1:$IV8A fvideo.mp4 o.mp4
t2 "encrypt.track-ids" mp4encrypt "o.mp4" --method MPEG-CENC --key " +1:$K1:$IV8A" --key 2x:$K2:$IV8B --key -1:$K2:$IV8B --key 99999999999:$K2:$IV8B fvideo.mp4 o.mp4
t2 "encrypt.track-ids.overflow" mp4encrypt "o.mp4" --method MPEG-CENC --key 4294967297:$K1:$IV8A --key 4294967298:$K2:$IV8B --property 4294967297:KID:$KID1 fvideo.mp4 o.mp4
t2 "encrypt.track-ids.wrap" mp4encrypt "o.mp4" --method MPEG-CENC --key -4294967295:$K1:$IV8A --key -4294967294:$K2:$IV8B --property -4294967295:KID:$KID1 fvideo.mp4 o.mp4
t2 "encrypt.err.track-ids.duplicate-property" mp4encrypt "o.mp4" --method MPEG-CENC --property 4294967297:KID:$KID1 --property 5555555555:KID:$KID2 fvideo.mp4 o.mp4
t2 "encrypt.non-ascii" mp4encrypt "chiffré-✓.mp4" --method OMA-PDCF-CBC --key 1:$K1:$IV8A --key 2:$K2:$IV8B --property "1:ContentId:cid-é ✓" \
    --property "1:Tïtle ✓:välue 日本語" --property 2:RightsIssuerUrl:http://example.com/é "vidéo-✓.mp4" "chiffré-✓.mp4"
t2 "encrypt.non-ascii.isma" mp4encrypt "o.mp4" --method ISMA-IAEC --kms-uri "urn:kms:é ✓" --key 1:$K1:$IV8A --key 2:$K2:$IV8B "vidéo-✓.mp4" o.mp4
t2 "encrypt.non-ascii.pssh" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV8A --pssh "$SYS1:päyload-✓.bin" faudio.mp4 o.mp4
t2 "encrypt.non-ascii.multi" mp4encrypt "chiffré-ségment-✓.m4s" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi "ségment-✓.m4s" chiffré-%s.m4s
t2 "encrypt.err.non-ascii.input-not-found" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV8A "n'existe pas ✓ é.mp4" o.mp4
t2 "encrypt.err.non-ascii.output-dir-not-found" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A video.mp4 "répertoire ✓/o.mp4"
t2 "encrypt.err.non-ascii.unexpected-argument" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV8A video.mp4 o.mp4 "éxtra ✓"
t2 "encrypt.err.non-ascii.property-duplicate" mp4encrypt "o.mp4" --method MPEG-CENC --property "1:Clé ✓:a" --property "1:Clé ✓:b" video.mp4 o.mp4
t2 "encrypt.err.non-ascii.pssh" mp4encrypt "o.mp4" --method MPEG-CENC --pssh "édef8ba979d64acea3c827dcd51d21ed:pssh1.bin" fvideo.mp4 o.mp4
t2 "encrypt.err.non-ascii.multi-input-not-found" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi "pas là-✓.m4s" chiffré-%s.m4s
t2 "encrypt.upper-hex" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:000102030405060708090A0B0C0D0E0F:A0A1A2A3A4A5A6A7 faudio.mp4 o.mp4
t2 "encrypt.method-twice" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV16A --method MPEG-CBCS --key 2:$K2:$IV16B fvideo.mp4 o.mp4
t2 "encrypt.multi.no-suffix" mp4encrypt "encm-init" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi init encm-%s
t2 "encrypt.multi.pattern" mp4encrypt "encm-seg-1-0001-100%.m4s" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi seg-1-0001.m4s encm-%s-100%%.m4s
t2 "encrypt.fragments-info.self" mp4encrypt "o.mp4" --method MPEG-CBCS --key 1:$K1:$IV16A --key 2:$K2:$IV16B --fragments-info fvideo.mp4 fvideo.mp4 o.mp4

# errors
t2 "encrypt.err.usage" mp4encrypt ""
t2 "encrypt.err.no-method" mp4encrypt "o.mp4" video.mp4 o.mp4
t2 "encrypt.err.method-missing-arg" mp4encrypt "" --method
t2 "encrypt.err.method-invalid" mp4encrypt "o.mp4" --method FOO video.mp4 o.mp4
t2 "encrypt.err.method-case" mp4encrypt "o.mp4" --method mpeg-cenc video.mp4 o.mp4
t2 "encrypt.err.key-before-method" mp4encrypt "o.mp4" --key 1:$K1:$IV8A --method MPEG-CENC video.mp4 o.mp4
t2 "encrypt.err.key-missing-arg" mp4encrypt "" --method MPEG-CENC --key
t2 "encrypt.err.key-two-parts" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1 video.mp4 o.mp4
t2 "encrypt.err.key-one-part" mp4encrypt "o.mp4" --method MPEG-CENC --key $K1 video.mp4 o.mp4
t2 "encrypt.err.key-bad-hex" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:0001020304050607080g0a0b0c0d0e0f:$IV8A video.mp4 o.mp4
t2 "encrypt.err.key-short" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:00010203:$IV8A video.mp4 o.mp4
t2 "encrypt.err.key-long" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:${K1}aabb:$IV8A fvideo.mp4 o.mp4
t2 "encrypt.err.iv-bad-hex" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:00010203040506zz video.mp4 o.mp4
t2 "encrypt.err.iv-odd" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:000102030405060 fvideo.mp4 o.mp4
t2 "encrypt.err.key-duplicate" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV8A --key 1:$K2:$IV8B video.mp4 o.mp4
t2 "encrypt.err.property-missing-arg" mp4encrypt "" --method MPEG-CENC --property
t2 "encrypt.err.property-invalid" mp4encrypt "o.mp4" --method MPEG-CENC --property 1:KID video.mp4 o.mp4
t2 "encrypt.err.property-duplicate" mp4encrypt "o.mp4" --method MPEG-CENC --property 1:ContentId:a --property 1:ContentId:b video.mp4 o.mp4
t2 "encrypt.err.property-isma" mp4encrypt "o.mp4" --method ISMA-IAEC --property 1:ContentId:a video.mp4 o.mp4
t2 "encrypt.err.property-no-method" mp4encrypt "o.mp4" --property 1:ContentId:a video.mp4 o.mp4
t2 "encrypt.err.kid-size" mp4encrypt "o.mp4" --method MPEG-CENC --property 1:KID:0011 video.mp4 o.mp4
t2 "encrypt.err.kid-hex" mp4encrypt "o.mp4" --method MPEG-CENC --property 1:KID:0011223344556677889zaabbccddeeff video.mp4 o.mp4
t2 "encrypt.err.global-option-missing-arg" mp4encrypt "" --method MPEG-CENC --global-option
t2 "encrypt.err.global-option-invalid" mp4encrypt "o.mp4" --method MPEG-CENC --global-option foo video.mp4 o.mp4
t2 "encrypt.err.kms-uri-missing-arg" mp4encrypt "" --method ISMA-IAEC --kms-uri
t2 "encrypt.err.kms-uri-other-method" mp4encrypt "o.mp4" --method MPEG-CENC --kms-uri urn:x video.mp4 o.mp4
t2 "encrypt.err.isma-no-kms-uri" mp4encrypt "o.mp4" --method ISMA-IAEC --key 1:$K1:$IV8A video.mp4 o.mp4
t2 "encrypt.err.acgk-no-group-key" mp4encrypt "o.mp4" --method MARLIN-IPMP-ACGK --key 1:$K1:$IV8A video.mp4 o.mp4
t2 "encrypt.err.pssh-missing-arg" mp4encrypt "" --method MPEG-CENC --pssh
t2 "encrypt.err.pssh-v1-missing-arg" mp4encrypt "" --method MPEG-CENC --pssh-v1
t2 "encrypt.err.pssh-short" mp4encrypt "o.mp4" --method MPEG-CENC --pssh abcd:pssh1.bin fvideo.mp4 o.mp4
t2 "encrypt.err.pssh-no-colon" mp4encrypt "o.mp4" --method MPEG-CENC --pssh ${SYS1}xpssh1.bin fvideo.mp4 o.mp4
t2 "encrypt.err.pssh-bad-hex" mp4encrypt "o.mp4" --method MPEG-CENC --pssh zdef8ba979d64acea3c827dcd51d21ed:pssh1.bin fvideo.mp4 o.mp4
t2 "encrypt.err.pssh-no-file" mp4encrypt "o.mp4" --method MPEG-CENC --pssh $SYS1:does-not-exist.bin fvideo.mp4 o.mp4
t2 "encrypt.err.fragments-info-missing-arg" mp4encrypt "" --method MPEG-CENC --fragments-info
t2 "encrypt.err.fragments-info-no-file" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info does-not-exist.mp4 seg-1-0001.m4s o.mp4
t2 "encrypt.err.no-input" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A
t2 "encrypt.err.no-output" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A video.mp4
t2 "encrypt.err.input-not-found" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV8A does-not-exist.mp4 o.mp4
t2 "encrypt.err.output-dir-not-found" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A video.mp4 no-such-dir/o.mp4
t2 "encrypt.err.unexpected-argument" mp4encrypt "o.mp4" --method MPEG-CENC --key 1:$K1:$IV8A video.mp4 o.mp4 extra
t2 "encrypt.err.unknown-option" mp4encrypt "o.mp4" --method MPEG-CENC --frobnicate video.mp4 o.mp4
t2 "encrypt.err.multi-no-fragments-info" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A --multi seg-1-0001.m4s encm-%s.m4s
t2 "encrypt.err.multi-no-input" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi encm-%s.m4s
t2 "encrypt.err.multi-no-output" mp4encrypt "" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi
t2 "encrypt.err.multi-no-placeholder" mp4encrypt "encm.m4s" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi seg-1-0001.m4s encm.m4s
t2 "encrypt.err.multi-input-not-found" mp4encrypt "encm-seg-1-0001.m4s encm-nope.m4s" --method MPEG-CENC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi seg-1-0001.m4s nope.m4s encm-%s.m4s
t2 "encrypt.err.multi-isma-no-kms-uri" mp4encrypt "encm-seg-1-0001.m4s" --method ISMA-IAEC --key 1:$K1:$IV8A --fragments-info init.mp4 --multi seg-1-0001.m4s encm-%s.m4s

#----------------------------------------------------------------------
# mp4decrypt
#----------------------------------------------------------------------
echo "=== mp4decrypt ==="
for f in "${ENCRYPTED[@]}"; do
    n="${f%.mp4}"
    if [ ! -s "$GEN/$f" ]; then
        [ -z "$BENTO4SHARP_ONLY" ] || continue
        echo "SKIP  decrypt.$n   (the original mp4encrypt did not produce this file)"
        SKIPPED=$((SKIPPED+1))
        continue
    fi
    t2 "decrypt.$n" mp4decrypt "o.mp4" --key 1:$K1 --key 2:$K2 $f o.mp4
    t2 "decrypt.$n.wrong-key" mp4decrypt "o.mp4" --key 1:$K2 --key 2:$K3 $f o.mp4
    t2 "decrypt.$n.no-key" mp4decrypt "o.mp4" $f o.mp4
    case "$n" in *-fvideo|enckid-*)
        t2 "decrypt.$n.track1-only.progress" mp4decrypt "o.mp4" --show-progress --key 1:$K1 $f o.mp4
        t2 "decrypt.$n.kid" mp4decrypt "o.mp4" --key $KID1:$K1 --key $KID2:$K2 $f o.mp4
        t2 "decrypt.$n.default-kid" mp4decrypt "o.mp4" --key 00000000000000000000000000000001:$K1 --key 00000000000000000000000000000002:$K2 --show-progress $f o.mp4
        t2 "decrypt.$n.kid-and-track" mp4decrypt "o.mp4" --key 2:$K2 --key $KID1:$K1 $f o.mp4
        ;;
    esac
done
for m in $METHODS; do
    # separate fragments
    method_options $m
    ref mp4encrypt --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B --fragments-info init.mp4 seg-1-0001.m4s encseg-$m-1.m4s
    ref mp4encrypt --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B --fragments-info init.mp4 seg-2-0001.m4s encseg-$m-2.m4s
    ref mp4encrypt --method $m "${MOPT[@]}" --key 1:$K1:$IV8A --key 2:$K2:$IV8B init.mp4 encinit-$m.mp4
    mirror encseg-$m-1.m4s encseg-$m-2.m4s encinit-$m.mp4
    for s in 1 2; do
        t2 "decrypt.fragments-info.$m.$s" mp4decrypt "o.m4s" --key 1:$K1 --key 2:$K2 --fragments-info encinit-$m.mp4 encseg-$m-$s.m4s o.m4s
        t2 "decrypt.fragments-info.$m.$s.progress" mp4decrypt "o.m4s" --show-progress --key $s:$K1 --fragments-info encinit-$m.mp4 encseg-$m-$s.m4s o.m4s
        t2 "decrypt.fragments-info.$m.$s.clear-init" mp4decrypt "o.m4s" --key 1:$K1 --key 2:$K2 --fragments-info init.mp4 encseg-$m-$s.m4s o.m4s
        t2 "decrypt.segment-alone.$m.$s" mp4decrypt "o.m4s" --key 1:$K1 --key 2:$K2 encseg-$m-$s.m4s o.m4s
    done
    t2 "decrypt.init.$m" mp4decrypt "o.mp4" --key 1:$K1 --key 2:$K2 encinit-$m.mp4 o.mp4
done
for f in audio.mp4 video.mp4 fvideo.mp4 faudio.mp4 init.mp4 seg-1-0001.m4s garbage.mp4 empty.bin; do
    t2 "decrypt.clear.$f" mp4decrypt "o.mp4" --key 1:$K1 --key 2:$K2 $f o.mp4
    t2 "decrypt.clear.$f.progress" mp4decrypt "o.mp4" --show-progress $f o.mp4
done
t2 "decrypt.track-ids" mp4decrypt "o.mp4" --key " +1:$K1" --key 2x:$K2 enc-MPEG-CENC-fvideo.mp4 o.mp4
t2 "decrypt.track-ids.overflow" mp4decrypt "o.mp4" --key 4294967297:$K1 --key 4294967298:$K2 enc-MPEG-CENC-fvideo.mp4 o.mp4
t2 "decrypt.track-ids.wrap" mp4decrypt "o.mp4" --key -4294967295:$K1 --key -4294967294:$K2 enc-MPEG-CENC-fvideo.mp4 o.mp4
t2 "decrypt.err.track-ids.wrap-to-zero" mp4decrypt "o.mp4" --key -0:$K1 enc-MPEG-CENC-fvideo.mp4 o.mp4
if [ -s "chiffré-✓.mp4" ]; then
    t2 "decrypt.non-ascii" mp4decrypt "déchiffré-✓.mp4" --key 1:$K1 --key 2:$K2 "chiffré-✓.mp4" "déchiffré-✓.mp4"
    t2 "decrypt.non-ascii.show" mp4tag "" "chiffré-✓.mp4"
fi
t2 "decrypt.err.non-ascii.input-not-found" mp4decrypt "o.mp4" --key 1:$K1 "n'existe pas ✓ é.mp4" o.mp4
t2 "decrypt.err.non-ascii.output-dir-not-found" mp4decrypt "" --key 1:$K1 video.mp4 "répertoire ✓/o.mp4"
t2 "decrypt.err.non-ascii.fragments-info" mp4decrypt "o.mp4" --key 1:$K1 --fragments-info "pas là-✓.mp4" video.mp4 o.mp4
t2 "decrypt.err.non-ascii.unexpected-argument" mp4decrypt "o.mp4" video.mp4 o.mp4 "éxtra ✓"
t2 "decrypt.upper-hex" mp4decrypt "o.mp4" --key 1:000102030405060708090A0B0C0D0E0F --key 2:101112131415161718191A1B1C1D1E1F enc-MPEG-CBCS-fvideo.mp4 o.mp4
t2 "decrypt.key-twice" mp4decrypt "o.mp4" --key 1:$K3 --key 1:$K1 --key 2:$K2 enc-MPEG-CENC-fvideo.mp4 o.mp4
t2 "decrypt.long-key" mp4decrypt "o.mp4" --key 1:${K1}ffff --key 2:$K2 enc-MPEG-CENC-fvideo.mp4 o.mp4
t2 "decrypt.fragments-info.self" mp4decrypt "o.mp4" --key 1:$K1 --key 2:$K2 --fragments-info enc-MPEG-CENC-fvideo.mp4 enc-MPEG-CENC-fvideo.mp4 o.mp4

# errors
t2 "decrypt.err.usage" mp4decrypt ""
t2 "decrypt.err.key-missing-arg" mp4decrypt "" --key
t2 "decrypt.err.key-invalid" mp4decrypt "o.mp4" --key $K1 video.mp4 o.mp4
t2 "decrypt.err.key-id-zero" mp4decrypt "o.mp4" --key 0:$K1 video.mp4 o.mp4
t2 "decrypt.err.key-id-text" mp4decrypt "o.mp4" --key abc:$K1 video.mp4 o.mp4
t2 "decrypt.err.kid-bad-hex" mp4decrypt "o.mp4" --key 0011223344556677889zaabbccddeeff:$K1 video.mp4 o.mp4
t2 "decrypt.err.key-bad-hex" mp4decrypt "o.mp4" --key 1:0001020304050607080g0a0b0c0d0e0f video.mp4 o.mp4
t2 "decrypt.err.key-short" mp4decrypt "o.mp4" --key 1:0001 video.mp4 o.mp4
t2 "decrypt.err.key-three-parts" mp4decrypt "o.mp4" --key 1:$K1:$IV8A enc-MPEG-CENC-fvideo.mp4 o.mp4
t2 "decrypt.err.fragments-info-missing-arg" mp4decrypt "" --fragments-info
t2 "decrypt.err.fragments-info-no-file" mp4decrypt "o.mp4" --key 1:$K1 --fragments-info does-not-exist.mp4 video.mp4 o.mp4
t2 "decrypt.err.no-input" mp4decrypt "" --key 1:$K1
t2 "decrypt.err.no-output" mp4decrypt "" --key 1:$K1 video.mp4
t2 "decrypt.err.input-not-found" mp4decrypt "o.mp4" --key 1:$K1 does-not-exist.mp4 o.mp4
t2 "decrypt.err.output-dir-not-found" mp4decrypt "" --key 1:$K1 video.mp4 no-such-dir/o.mp4
t2 "decrypt.err.unexpected-argument" mp4decrypt "o.mp4" video.mp4 o.mp4 extra
t2 "decrypt.err.unknown-option" mp4decrypt "o.mp4" --frobnicate video.mp4 o.mp4

# 'random' keys and IVs: no random bytes in the original tools built with
# emscripten (they report an error), so the port is checked on its own: a file
# encrypted with random values and decrypted with the key that was printed must
# be the same as what the original tools give with fixed keys
cd "$GENP" || exit 1
for m in MPEG-CENC MPEG-CBCS PIFF-CBC; do
    name="encrypt.$m.random (port only)"
    port_dll mp4encrypt
    rm -f random.mp4 random-o.mp4
    port "$DLL" --method $m --key 1:random:random --key 2:$K2:random fvideo.mp4 random.mp4 > .stdout 2> .stderr
    rc=$?
    key=""
    read -r key < .stdout
    port_dll mp4decrypt
    case "$key" in
        KEY.1=????????????????????????????????) port "$DLL" --key 1:${key#KEY.1=} --key 2:$K2 random.mp4 random-o.mp4 > /dev/null 2>&1 ;;
    esac
    node "$REF/bin/mp4decrypt.js" --key 1:$K1 --key 2:$K2 enc-$m-fvideo.mp4 random-expected.mp4 > /dev/null 2>&1
    if [ $rc -eq 0 ] && [ ! -s .stderr ] && [ -s random-expected.mp4 ] && cmp -s random-expected.mp4 random-o.mp4 && ! cmp -s random.mp4 enc-$m-fvideo.mp4; then
        echo "PASS  $name"
        PASSED=$((PASSED+1))
    else
        echo "FAIL  $name"
        FAILED=$((FAILED+1))
        FAILURES+=("$name")
    fi
done
cd "$GEN" || exit 1

#----------------------------------------------------------------------
# mp4pssh
#----------------------------------------------------------------------
echo "=== mp4pssh ==="
ref mp4encrypt --method MPEG-CENC --key 1:$K1:$IV8A --key 2:$K2:$IV8B --pssh $SYS1:pssh1.bin --pssh $SYS2:pssh2.bin fvideo.mp4 withpssh.mp4
need withpssh.mp4
mirror withpssh.mp4
for f in video.mp4 audio.mp4 fvideo.mp4 withpssh.mp4; do
    n="${f%.mp4}"
    UPSTREAM_CRASH=1 t2 "pssh.insert.$n" mp4pssh "o.mp4" --insert $SYS3:pssh1.bin $f o.mp4
    UPSTREAM_CRASH=1 t2 "pssh.insert.$n.empty-payload" mp4pssh "o.mp4" --insert $SYS3:empty.bin $f o.mp4
    UPSTREAM_CRASH=1 t2 "pssh.insert.$n.existing-id" mp4pssh "o.mp4" --insert $SYS1:pssh2.bin $f o.mp4
    UPSTREAM_CRASH=1 t2 "pssh.insert.$n.twice" mp4pssh "o.mp4" --insert $SYS3:pssh1.bin --insert $SYS2:pssh2.bin $f o.mp4
    UPSTREAM_CRASH=1 t2 "pssh.replace.$n" mp4pssh "o.mp4" --replace $SYS1:pssh2.bin $f o.mp4
    UPSTREAM_CRASH=1 t2 "pssh.replace.$n.other-id" mp4pssh "o.mp4" --replace $SYS3:pssh2.bin $f o.mp4
    UPSTREAM_CRASH=1 t2 "pssh.replace.$n.options-last" mp4pssh "o.mp4" $f o.mp4 --replace $SYS2:pssh1.bin
    t2 "pssh.remove.$n" mp4pssh "o.mp4" --remove $SYS1 $f o.mp4
    t2 "pssh.remove.$n.other-id" mp4pssh "o.mp4" --remove $SYS3 $f o.mp4
    t2 "pssh.no-command.$n" mp4pssh "o.mp4" $f o.mp4
done
t2 "pssh.remove.both" mp4pssh "o.mp4" --remove $SYS2 --remove $SYS1 withpssh.mp4 o.mp4
t2 "pssh.remove.twice" mp4pssh "o.mp4" --remove $SYS2 --remove $SYS2 withpssh.mp4 o.mp4
t2 "pssh.remove.upper-hex" mp4pssh "o.mp4" --remove EDEF8BA979D64ACEA3C827DCD51D21ED withpssh.mp4 o.mp4
UPSTREAM_CRASH=1 t2 "pssh.remove-insert" mp4pssh "o.mp4" --remove $SYS1 --insert $SYS3:pssh1.bin withpssh.mp4 o.mp4
UPSTREAM_CRASH=1 t2 "pssh.insert-remove" mp4pssh "o.mp4" --insert $SYS3:pssh1.bin --remove $SYS2 withpssh.mp4 o.mp4
UPSTREAM_CRASH=1 t2 "pssh.replace-both" mp4pssh "o.mp4" --replace $SYS1:pssh2.bin --replace $SYS2:pssh1.bin withpssh.mp4 o.mp4
# no 'moov'
t2 "pssh.insert.segment" mp4pssh "o.mp4" --insert $SYS3:pssh1.bin seg-1-0001.m4s o.mp4
t2 "pssh.replace.segment" mp4pssh "o.mp4" --replace $SYS3:pssh1.bin seg-1-0001.m4s o.mp4
t2 "pssh.remove.segment" mp4pssh "o.mp4" --remove $SYS3 seg-1-0001.m4s o.mp4
t2 "pssh.insert.garbage" mp4pssh "o.mp4" --insert $SYS3:pssh1.bin garbage.mp4 o.mp4
t2 "pssh.remove.empty" mp4pssh "o.mp4" --remove $SYS3 empty.bin o.mp4
# errors
t2 "pssh.err.usage" mp4pssh ""
t2 "pssh.err.usage.one-arg" mp4pssh "" video.mp4
t2 "pssh.err.insert-missing-arg" mp4pssh "" video.mp4 --insert
t2 "pssh.err.insert-no-colon" mp4pssh "o.mp4" --insert $SYS1 video.mp4 o.mp4
t2 "pssh.err.insert-empty-path" mp4pssh "o.mp4" --insert $SYS1: video.mp4 o.mp4
t2 "pssh.err.insert-no-file" mp4pssh "o.mp4" --insert $SYS1:does-not-exist.bin video.mp4 o.mp4
# (invalid hex digits are not an error for the original tool: they count as 0)
UPSTREAM_CRASH=1 t2 "pssh.insert.bad-hex-id" mp4pssh "o.mp4" --insert zzef8ba979d64acea3c827dcd51d21ed:pssh1.bin video.mp4 o.mp4
UPSTREAM_CRASH=1 t2 "pssh.insert.long-id" mp4pssh "o.mp4" --insert ${SYS1}0011:pssh1.bin video.mp4 o.mp4
t2 "pssh.err.insert-short-id" mp4pssh "o.mp4" --insert edef:pssh1.bin video.mp4 o.mp4
t2 "pssh.err.remove-missing-arg" mp4pssh "" video.mp4 --remove
t2 "pssh.err.remove-bad-id" mp4pssh "o.mp4" --remove xyz video.mp4 o.mp4
t2 "pssh.err.replace-missing-arg" mp4pssh "" video.mp4 --replace
t2 "pssh.err.replace-no-colon" mp4pssh "o.mp4" --replace $SYS1 video.mp4 o.mp4
t2 "pssh.err.replace-empty-path" mp4pssh "o.mp4" --replace $SYS1: video.mp4 o.mp4
t2 "pssh.err.replace-no-file" mp4pssh "o.mp4" --replace $SYS1:does-not-exist.bin video.mp4 o.mp4
UPSTREAM_CRASH=1 t2 "pssh.replace.bad-hex-id" mp4pssh "o.mp4" --replace zzef8ba979d64acea3c827dcd51d21ed:pssh1.bin withpssh.mp4 o.mp4
t2 "pssh.err.replace-short-id" mp4pssh "o.mp4" --replace edef:pssh1.bin video.mp4 o.mp4
t2 "pssh.remove.bad-hex-id" mp4pssh "o.mp4" --remove zzef8ba979d64acea3c827dcd51d21ed withpssh.mp4 o.mp4
UPSTREAM_CRASH=1 t2 "pssh.insert.non-ascii" mp4pssh "ävec-pssh-✓.mp4" --insert "$SYS3:päyload-✓.bin" "vidéo-✓.mp4" "ävec-pssh-✓.mp4"
t2 "pssh.err.non-ascii.insert-no-file" mp4pssh "o.mp4" --insert "$SYS1:pas là-✓.bin" video.mp4 o.mp4
t2 "pssh.err.non-ascii.insert-short-id" mp4pssh "o.mp4" --insert "édef ✓:pssh1.bin" video.mp4 o.mp4
t2 "pssh.err.non-ascii.remove-short-id" mp4pssh "o.mp4" --remove "édef ✓" video.mp4 o.mp4
t2 "pssh.err.non-ascii.input-not-found" mp4pssh "o.mp4" --remove $SYS1 "pas là-✓.mp4" o.mp4
t2 "pssh.err.non-ascii.output-dir-not-found" mp4pssh "" --remove $SYS1 video.mp4 "répertoire ✓/o.mp4"
t2 "pssh.err.non-ascii.extra-argument" mp4pssh "o.mp4" --remove $SYS1 video.mp4 o.mp4 "éxtra ✓"
t2 "pssh.err.no-input" mp4pssh "" --remove $SYS1
t2 "pssh.err.no-output" mp4pssh "" --remove $SYS1 video.mp4
t2 "pssh.err.input-not-found" mp4pssh "o.mp4" --remove $SYS1 does-not-exist.mp4 o.mp4
t2 "pssh.err.output-dir-not-found" mp4pssh "" --remove $SYS1 video.mp4 no-such-dir/o.mp4
t2 "pssh.err.extra-argument" mp4pssh "o.mp4" --remove $SYS1 video.mp4 o.mp4 extra

#----------------------------------------------------------------------
# mp4dcfpackager
#----------------------------------------------------------------------
echo "=== mp4dcfpackager ==="
for m in CBC CTR; do
    for f in hello.txt audio.mp4 video.mp4 empty.bin pssh2.bin; do
        t2 "dcf.$m.$f" mp4dcfpackager "dcf-$m-$f.odf" --method $m --key $K1:$IV16A $f dcf-$m-$f.odf
        t2 "dcf.$m.$f.options" mp4dcfpackager "o.odf" --method $m --show-progress --content-type audio/mp4 --content-id cid:1234@example.com \
            --rights-issuer http://example.com/ri?x=1 --key $K2:$IV16B --textual-header Name:value --textual-header Other:value:with:colons \
            --textual-header Empty: $f o.odf
        if [ -f "dcf-$m-$f.odf" ]; then
            t2 "dcf.$m.$f.decrypt" mp4decrypt "o.bin" --key 1:$K1 dcf-$m-$f.odf o.bin
            t2 "dcf.$m.$f.decrypt.wrong-key.progress" mp4decrypt "o.bin" --show-progress --key 1:$K2 dcf-$m-$f.odf o.bin
            t2 "dcf.$m.$f.decrypt.no-key" mp4decrypt "o.bin" dcf-$m-$f.odf o.bin
            t2 "dcf.$m.$f.tags" mp4tag "" --show-tags dcf-$m-$f.odf
        fi
    done
    t2 "dcf.$m.content-type" mp4dcfpackager "o.odf" --method $m --key $K1:$IV16A --content-type text/plain hello.txt o.odf
    t2 "dcf.$m.content-id" mp4dcfpackager "o.odf" --method $m --key $K1:$IV16A --content-id my-content hello.txt o.odf
    t2 "dcf.$m.rights-issuer" mp4dcfpackager "o.odf" --method $m --key $K1:$IV16A --rights-issuer http://ri.example.com/ hello.txt o.odf
    t2 "dcf.$m.textual-header" mp4dcfpackager "o.odf" --method $m --key $K1:$IV16A --textual-header a:b hello.txt o.odf
    t2 "dcf.$m.empty-strings" mp4dcfpackager "o.odf" --method $m --key $K1:$IV16A --content-type "" --content-id "" --rights-issuer "" hello.txt o.odf
    t2 "dcf.$m.options-last" mp4dcfpackager "o.odf" hello.txt o.odf --method $m --key $K1:$IV16A --content-id x
    t2 "dcf.$m.method-twice" mp4dcfpackager "o.odf" --method CBC --method CTR --method $m --key $K1:$IV16A hello.txt o.odf
    t2 "dcf.$m.short-key" mp4dcfpackager "o.odf" --method $m --key 0011:$IV16A hello.txt o.odf
    t2 "dcf.$m.bad-key-hex" mp4dcfpackager "o.odf" --method $m --key 000102030405060708090a0b0c0d0exx:$IV16A hello.txt o.odf
    t2 "dcf.$m.then-null" mp4dcfpackager "dcf-NULL-$m.odf" --method $m --key $K1:$IV16A --method NULL --content-type text/plain --content-id null-content hello.txt dcf-NULL-$m.odf
    if [ -f "dcf-NULL-$m.odf" ]; then
        t2 "dcf.$m.then-null.decrypt" mp4decrypt "o.bin" --key 1:$K1 dcf-NULL-$m.odf o.bin
    fi
    t2 "dcf.$m.null-then-method" mp4dcfpackager "o.odf" --method NULL --method $m --key $K1:$IV16A audio.mp4 o.odf
done
# errors
t2 "dcf.err.usage" mp4dcfpackager ""
t2 "dcf.err.no-method" mp4dcfpackager "o.odf" hello.txt o.odf
t2 "dcf.err.method-invalid" mp4dcfpackager "o.odf" --method cbc hello.txt o.odf
t2 "dcf.err.null-no-key" mp4dcfpackager "o.odf" --method NULL hello.txt o.odf
t2 "dcf.err.null-key" mp4dcfpackager "o.odf" --method NULL --key $K1:$IV16A hello.txt o.odf
t2 "dcf.err.key-before-method" mp4dcfpackager "o.odf" --key $K1:$IV16A --method CBC hello.txt o.odf
t2 "dcf.err.key-missing-arg" mp4dcfpackager "" --method CBC --key
t2 "dcf.err.key-invalid" mp4dcfpackager "o.odf" --method CBC --key $K1 hello.txt o.odf
t2 "dcf.err.key-bad-iv" mp4dcfpackager "o.odf" --method CBC --key $K1:zz hello.txt o.odf
t2 "dcf.err.key-short-iv" mp4dcfpackager "o.odf" --method CTR --key $K1:$IV8A hello.txt o.odf
t2 "dcf.err.key-duplicate" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A --key $K2:$IV16B hello.txt o.odf
t2 "dcf.err.no-key" mp4dcfpackager "o.odf" --method CBC hello.txt o.odf
t2 "dcf.err.content-type-missing-arg" mp4dcfpackager "" --method CBC --key $K1:$IV16A --content-type
t2 "dcf.err.content-id-missing-arg" mp4dcfpackager "" --method CBC --key $K1:$IV16A --content-id
t2 "dcf.err.rights-issuer-missing-arg" mp4dcfpackager "" --method CBC --key $K1:$IV16A --rights-issuer
t2 "dcf.err.textual-header-missing-arg" mp4dcfpackager "" --method CBC --key $K1:$IV16A --textual-header
t2 "dcf.err.textual-header-invalid" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A --textual-header name hello.txt o.odf
t2 "dcf.err.textual-header-duplicate" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A --textual-header a:b --textual-header a:c hello.txt o.odf
t2 "dcf.non-ascii" mp4dcfpackager "paquet-✓.odf" --method CBC --key $K1:$IV16A --content-type "téxte/plain ✓" --content-id "cid:é ✓" --rights-issuer "http://example.com/émetteur ✓" \
    --textual-header "Nöm ✓:väleur 日本語" "päyload-✓.bin" "paquet-✓.odf"
if [ -s "paquet-✓.odf" ]; then
    t2 "dcf.non-ascii.decrypt" mp4decrypt "o.bin" --show-progress --key 1:$K1 "paquet-✓.odf" o.bin
    t2 "dcf.non-ascii.tags" mp4tag "" "paquet-✓.odf"
fi
t2 "dcf.err.non-ascii.textual-header-duplicate" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A --textual-header "Nöm ✓:b" --textual-header "Nöm ✓:c" hello.txt o.odf
t2 "dcf.err.non-ascii.input-not-found" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A "pas là-✓.txt" o.odf
t2 "dcf.err.non-ascii.output-dir-not-found" mp4dcfpackager "" --method CBC --key $K1:$IV16A hello.txt "répertoire ✓/o.odf"
t2 "dcf.err.non-ascii.unexpected-argument" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A hello.txt o.odf "éxtra ✓"
t2 "dcf.err.no-input" mp4dcfpackager "" --method CBC --key $K1:$IV16A
t2 "dcf.err.no-output" mp4dcfpackager "" --method CBC --key $K1:$IV16A hello.txt
t2 "dcf.err.input-not-found" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A does-not-exist.txt o.odf
t2 "dcf.err.output-dir-not-found" mp4dcfpackager "" --method CBC --key $K1:$IV16A hello.txt no-such-dir/o.odf
t2 "dcf.err.unexpected-argument" mp4dcfpackager "o.odf" --method CBC --key $K1:$IV16A hello.txt o.odf extra

#----------------------------------------------------------------------
# mp4tag
#----------------------------------------------------------------------
echo "=== mp4tag ==="
t2 "tag.list-keys" mp4tag "" --list-keys
t2 "tag.list-symbols" mp4tag "" --list-symbols
t2 "tag.list-both" mp4tag "" --list-symbols --list-keys --list-keys
for f in audio.mp4 video.mp4 fvideo.mp4 faudio.mp4 init.mp4; do
    n="${f%.mp4}"
    t2 "tag.show.$n" mp4tag "" --show-tags $f
    t2 "tag.show.$n.default" mp4tag "" $f
    t2 "tag.show.$n.with-keys" mp4tag "" --list-keys --show-tags --show-tags $f
    # one value of each type
    t2 "tag.add.$n.S" mp4tag "o.mp4" --add "Name:S:The Title" $f o.mp4
    t2 "tag.add.$n.S.empty" mp4tag "o.mp4" --add "Artist:S:" $f o.mp4
    t2 "tag.add.$n.S.colons" mp4tag "o.mp4" --add "Comment:S:a:b:c" $f o.mp4
    t2 "tag.add.$n.LS" mp4tag "o.mp4" --add "Name:LS:fra:Le Titre" $f o.mp4
    t2 "tag.add.$n.I8" mp4tag "o.mp4" --add "Compilation:I8:1" $f o.mp4
    t2 "tag.add.$n.I16" mp4tag "o.mp4" --add "Tempo:I16:120" $f o.mp4
    t2 "tag.add.$n.I32" mp4tag "o.mp4" --add "cnID:I32:123456789" $f o.mp4
    t2 "tag.add.$n.JPEG" mp4tag "o.mp4" --add "Cover:JPEG:cover.jpg" $f o.mp4
    t2 "tag.add.$n.GIF" mp4tag "o.mp4" --add "Cover:GIF:cover.gif" $f o.mp4
    t2 "tag.add.$n.B.ascii" mp4tag "o.mp4" --add "Rating:B:+hello" $f o.mp4
    t2 "tag.add.$n.B.hex" mp4tag "o.mp4" --add "Track:B:#0000000100020000" $f o.mp4
    t2 "tag.add.$n.Z" mp4tag "o.mp4" --add "Genre:Z:Blues" $f o.mp4
    t2 "tag.set.$n.S" mp4tag "o.mp4" --set "Name:S:The Title" $f o.mp4
    t2 "tag.set.$n.Tool" mp4tag "o.mp4" --set "Tool:S:Bento4Sharp" $f o.mp4
    t2 "tag.remove.$n.Tool" mp4tag "o.mp4" --remove Tool $f o.mp4
    t2 "tag.remove.$n.Name" mp4tag "o.mp4" --remove Name $f o.mp4
    t2 "tag.extract.$n.Tool" mp4tag "x.bin" --extract Tool:x.bin $f
    # many tags
    t2 "tag.add.$n.many" mp4tag "tagged-$n.mp4" \
        --add "Name:S:The Title" --add "Artist:S:The Artist" --add "Album:LS:eng:The Album" --add "Composer:S:Çômpösér ✓" \
        --add "Compilation:I8:1" --add "Tempo:I16:-2" --add "cnID:I32:-123456" --add "Cover:JPEG:cover.jpg" --add "Cover:GIF:cover.gif" \
        --add "Cover:JPEG:pssh1.bin" --add "Rating:B:+rated" --add "Track:B:#00000003000a0000" --add "Disc:B:#000000010002" \
        --add "GenreName:S:Rock" --add "Comment:S:first" --add "Comment:S:second" --add "Comment:LS:deu:dritte" \
        --add "meta/abcd:S:four cc" --add "meta/abcd:I32:77" --add "com.mycompany.foo/bar:S:long form" --add "com.mycompany.foo/bar:B:#cafe" \
        --add "com.mycompany.foo/Name:I16:300" --add "org.example/Name:S:other namespace" \
        $f tagged-$n.mp4
    if [ -s "tagged-$n.mp4" ]; then
        t="tagged-$n.mp4"
        t2 "tag.tagged.$n.show" mp4tag "" --show-tags $t
        t2 "tag.tagged.$n.show.default" mp4tag "" $t
        for k in Name Album Composer Compilation Tempo cnID Cover Cover#0 Cover#1 Cover#2 Cover#3 Rating Track Disc Comment Comment#1 Comment#2 \
                 meta/abcd meta/abcd#1 abcd com.mycompany.foo/bar com.mycompany.foo/bar#1 com.mycompany.foo/Name org.example/Name org.example/Nope Lyrics; do
            kn="${k//\//_}"
            t2 "tag.tagged.$n.extract.$kn" mp4tag "x.bin" --extract $k:x.bin $t
            t2 "tag.tagged.$n.remove.$kn" mp4tag "o.mp4" --remove $k $t o.mp4
            t2 "tag.tagged.$n.set.$kn" mp4tag "o.mp4" --set "$k:S:replaced" $t o.mp4
        done
        t2 "tag.tagged.$n.extract.several" mp4tag "x1.bin x2.bin x3.bin" --extract Cover#1:x1.bin --show-tags --extract Name:x2.bin --extract Tempo:x3.bin $t
        t2 "tag.tagged.$n.remove.several" mp4tag "o.mp4" --remove Comment --remove Comment --remove Cover#1 --remove Name --show-tags $t o.mp4
        t2 "tag.tagged.$n.remove.all-comments" mp4tag "o.mp4" --remove Comment --remove Comment --remove Comment --show-tags --remove Comment $t o.mp4
        t2 "tag.tagged.$n.mixed" mp4tag "o.mp4 x.bin" --set "Name:S:New Title" --remove Artist --add "Lyrics:S:la la la" --set "Cover#1:JPEG:cover.jpg" \
            --set "Tempo:I16:99" --set "Comment#2:LS:ita:terzo" --extract Cover#1:x.bin --show-tags --add "Artist:S:added after the listing" $t o.mp4
        t2 "tag.tagged.$n.show-then-add" mp4tag "o.mp4" --show-tags --add "Lyrics:S:la la la" --show-tags --extract Name:x.bin $t o.mp4
        t2 "tag.tagged.$n.set.types" mp4tag "o.mp4" --set "Name:I8:5" --set "Tempo:S:fast" --set "Cover:B:+notanimage" --set "cnID:I32:1" --set "Compilation:I8:0" $t o.mp4
        t2 "tag.tagged.$n.add.more" mp4tag "o.mp4" --add "Name:S:Second Title" --add "Cover:GIF:cover.gif" --add "com.mycompany.foo/bar:S:third" --add "meta/abcd:S:third" $t o.mp4
        if [ -s o.mp4 ]; then
            t2 "tag.tagged.$n.add.more.show" mp4tag "" o.mp4
        fi
    fi
    # values
    t2 "tag.add.$n.int-values" mp4tag "o.mp4" --add "Compilation:I8:255" --add "Compilation:I8:-1" --add "Compilation:I8:300" --add "Tempo:I16:65535" \
        --add "Tempo:I16:-32768" --add "Tempo:I16:70000" --add "cnID:I32:2147483647" --add "cnID:I32:-2147483648" --add "cnID:I32: 42abc" --add "cnID:I32:abc" \
        --add "cnID:I32:" --add "cnID:I32:+7" --show-tags $f o.mp4
    t2 "tag.add.$n.int-overflow" mp4tag "o.mp4" --add "cnID:I32:2147483648" --add "cnID:I32:4294967295" --add "cnID:I32:5000000000" --add "cnID:I32:-2147483649" \
        --add "cnID:I32:-5000000000" --add "cnID:I32:99999999999999999999999" --add "Tempo:I16:4294967297" --add "Compilation:I8:-4294967295" --show-tags $f o.mp4
    t2 "tag.add.$n.key-index-overflow" mp4tag "o.mp4" --add "Name:S:zero" --add "Name#4294967296:S:a" --add "Name#-1:S:b" --add "Name#99999999999999999999:S:c" \
        --add "Name# 1:S:d" --show-tags $f o.mp4
    t2 "tag.add.$n.binary-values" mp4tag "o.mp4" --add "Rating:B:+" --add "Rating:B:#" --add "Rating:B:#0g1H" --add "Rating:B:#AbCdEf0123456789" --add "Rating:B:+#notHex" $f o.mp4
    t2 "tag.add.$n.ls-values" mp4tag "o.mp4" --add "Name:LS:eng:" --add "Name:LS:x y:z" --add "Name:LS:abc:d:e" $f o.mp4
    t2 "tag.add.$n.dcf" mp4tag "o.mp4" --add "dcf/Name:S:dcf name" --add "dcf/dcfD:S:dcf description" --add "dcf/cprt:S:copyright" $f o.mp4
    t2 "tag.add.$n.dcf.ls" mp4tag "o.mp4" --add "dcf/titl:LS:eng:3gpp title" --add "dcf/dscp:LS:fra:description" $f o.mp4
    t2 "tag.add.$n.dcf.binary" mp4tag "o.mp4" --add "dcf/abcd:B:#00010203" $f o.mp4
    t2 "tag.add.$n.dcf.int" mp4tag "o.mp4" --add "dcf/yrrc:I16:2024" $f o.mp4
    t2 "tag.add.$n.key-index" mp4tag "o.mp4" --add "Name#0:S:zero" --add "Name#0:S:zero again" --add "Name#5:S:five" --add "Name#1:S:one" --show-tags $f o.mp4
    t2 "tag.add.$n.key-forms" mp4tag "o.mp4" --add "meta/Name:S:a" --add "©nam:S:b" --add "meta/©ART:S:c" --add "xyz:S:three chars" --add "toolong:S:d" \
        --add "a.b/c:S:e" --add "/Name:S:empty namespace" --add "ns/:S:empty name" --add "Name#x#1:S:two indexes" --show-tags $f o.mp4
    t2 "tag.set.$n.new-then-set" mp4tag "o.mp4" --set "Name:S:one" --set "Name:S:two" --add "Name:S:three" --set "Name#1:S:four" --set "Name#7:S:five" --show-tags $f o.mp4
    t2 "tag.remove.$n.missing-index" mp4tag "o.mp4" --remove Tool#3 $f o.mp4
    t2 "tag.remove.$n.namespace" mp4tag "o.mp4" --remove com.mycompany.foo/bar $f o.mp4
    t2 "tag.remove.$n.dcf" mp4tag "o.mp4" --remove dcf/Name $f o.mp4
    t2 "tag.extract.$n.missing" mp4tag "x.bin" --extract Name:x.bin $f
done
t2 "tag.show.dcf" mp4tag "" dcf-CBC-hello.txt.odf
t2 "tag.add.dcf-file" mp4tag "o.odf" --add "dcf/Name:S:title" --add "Name:S:meta title" --show-tags dcf-CBC-hello.txt.odf o.odf
t2 "tag.add.dcf-file.2" mp4tag "tagged.odf" --add "dcf/dcfD:S:description" --add "dcf/cprt:S:copyright" --add "dcf/titl:LS:eng:title" dcf-CTR-hello.txt.odf tagged.odf
if [ -s tagged.odf ]; then
    t2 "tag.tagged-dcf.show" mp4tag "" tagged.odf
    t2 "tag.tagged-dcf.remove" mp4tag "o.odf" --remove dcf/dcfD --show-tags tagged.odf o.odf
    t2 "tag.tagged-dcf.extract" mp4tag "x.bin" --extract dcf/cprt:x.bin tagged.odf
    t2 "tag.tagged-dcf.set" mp4tag "o.odf" --set "dcf/titl:LS:fra:titre" --show-tags tagged.odf o.odf
fi
t2 "tag.add.non-ascii-files" mp4tag "tägged-✓.mp4" --add "Cover:JPEG:cövér-✓.jpg" --add "Name:S:nön-äscii ✓ 日本語" --add "Artist:LS:fra:é" "vidéo-✓.mp4" "tägged-✓.mp4"
t2 "tag.extract.non-ascii-files" mp4tag "éxtract-✓.bin x.bin" --extract "Cover:éxtract-✓.bin" --extract Name:x.bin --show-tags "tägged-✓.mp4"
t2 "tag.err.non-ascii" mp4tag "o.mp4" --add "Näme ✓:S:x" --add "Name:Ä:x" video.mp4 o.mp4
t2 "tag.err.non-ascii.type" mp4tag "o.mp4" --add "Name:Ä✓:x" video.mp4 o.mp4
t2 "tag.err.non-ascii.jpeg" mp4tag "o.mp4" --add "Cover:JPEG:nöt-there-✓.jpg" video.mp4 o.mp4
t2 "tag.err.non-ascii.third-file" mp4tag "" video.mp4 o.mp4 "éxtra ✓"
t2 "tag.err.non-ascii.remove" mp4tag "o.mp4" --remove "Näme#x" video.mp4 o.mp4
t2 "tag.show.garbage" mp4tag "" garbage.mp4
t2 "tag.show.empty" mp4tag "" empty.bin
t2 "tag.show.segment" mp4tag "" seg-1-0001.m4s
t2 "tag.add.garbage" mp4tag "o.mp4" --add "Name:S:x" garbage.mp4 o.mp4
t2 "tag.add.segment" mp4tag "o.mp4" --add "Name:S:x" seg-1-0001.m4s o.mp4
t2 "tag.remove.garbage" mp4tag "o.mp4" --remove Name garbage.mp4 o.mp4
t2 "tag.show.encrypted" mp4tag "" enc-OMA-PDCF-CBC-video.mp4
# errors
t2 "tag.err.usage" mp4tag ""
t2 "tag.err.help" mp4tag "" --help
t2 "tag.err.help-last" mp4tag "" --show-tags video.mp4 --help
t2 "tag.err.add-missing-arg" mp4tag "" video.mp4 --add
t2 "tag.err.set-missing-arg" mp4tag "" video.mp4 --set
t2 "tag.err.remove-missing-arg" mp4tag "" video.mp4 --remove
t2 "tag.err.extract-missing-arg" mp4tag "" video.mp4 --extract
t2 "tag.err.third-file" mp4tag "o.mp4" --show-tags video.mp4 o.mp4 extra
t2 "tag.err.show-no-input" mp4tag "" --show-tags
t2 "tag.err.list-with-input" mp4tag "" --list-keys video.mp4
t2 "tag.err.add-no-output" mp4tag "" --add "Name:S:x" video.mp4
t2 "tag.err.add-no-input" mp4tag "" --add "Name:S:x"
t2 "tag.err.show-with-output" mp4tag "o.mp4" --show-tags video.mp4 o.mp4
t2 "tag.err.extract-with-output" mp4tag "o.mp4 x.bin" --extract Tool:x.bin video.mp4 o.mp4
t2 "tag.err.input-not-found" mp4tag "" does-not-exist.mp4
t2 "tag.err.add-input-not-found" mp4tag "o.mp4" --add "Name:S:x" does-not-exist.mp4 o.mp4
t2 "tag.err.output-dir-not-found" mp4tag "" --add "Name:S:x" video.mp4 no-such-dir/o.mp4
t2 "tag.err.add-syntax" mp4tag "o.mp4" --add "Name:S" video.mp4 o.mp4
t2 "tag.err.add-syntax-2" mp4tag "o.mp4" --add "Name" video.mp4 o.mp4
t2 "tag.err.add-type" mp4tag "o.mp4" --add "Name:X:value" video.mp4 o.mp4
t2 "tag.err.add-type-lowercase" mp4tag "o.mp4" --add "Name:s:value" video.mp4 o.mp4
t2 "tag.err.add-ls-syntax" mp4tag "o.mp4" --add "Name:LS:english" video.mp4 o.mp4
t2 "tag.err.add-ls-short" mp4tag "o.mp4" --add "Name:LS:en" video.mp4 o.mp4
t2 "tag.err.add-jpeg-not-found" mp4tag "o.mp4" --add "Cover:JPEG:does-not-exist.jpg" video.mp4 o.mp4
t2 "tag.err.add-gif-not-found" mp4tag "o.mp4" --add "Cover:GIF:does-not-exist.gif" video.mp4 o.mp4
t2 "tag.err.add-binary-empty" mp4tag "o.mp4" --add "Rating:B:" video.mp4 o.mp4
t2 "tag.err.add-binary-prefix" mp4tag "o.mp4" --add "Rating:B:hello" video.mp4 o.mp4
t2 "tag.err.add-binary-odd-hex" mp4tag "o.mp4" --add "Rating:B:#abc" video.mp4 o.mp4
t2 "tag.err.add-after-ok" mp4tag "o.mp4" --add "Name:S:ok" --add "Name:X:bad" --add "Artist:S:not reached" video.mp4 o.mp4
t2 "tag.err.remove-missing" mp4tag "o.mp4" --remove Name video.mp4 o.mp4
t2 "tag.err.remove-then-add" mp4tag "o.mp4" --remove Name --add "Name:S:not reached" video.mp4 o.mp4
t2 "tag.err.extract-syntax" mp4tag "" --extract Tool video.mp4
t2 "tag.err.extract-missing" mp4tag "x.bin" --extract Name:x.bin video.mp4
t2 "tag.err.extract-output-dir-not-found" mp4tag "" --extract Tool:no-such-dir/x.bin video.mp4
t2 "tag.err.extract-then-show" mp4tag "x.bin" --extract Name:x.bin --show-tags video.mp4

cd "$ROOT" || exit 1

#----------------------------------------------------------------------
# summary
#----------------------------------------------------------------------
echo "=== summary: $PASSED passed, $FAILED failed, $SKIPPED skipped ==="
if [ $FAILED -ne 0 ]; then
    for f in "${FAILURES[@]}"; do echo "  failed: $f"; done
    exit 1
fi
exit 0
