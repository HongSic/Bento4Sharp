// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Codecs/Ap4Ac4Parser.h, Ap4Ac4Parser.cpp
// Original work Copyright 2002-2020 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint AC4_HEADER_SIZE   = 7;       /* The header size for AC-4 parser, only include (sync word + frame size) */
        public const uint AC4_MAX_TOC_SIZE  = 512;     /* Ths assumption of max toc size */
        public const uint AC4_SYNC_WORD     = 0xAC40;  /* 16 sync bits without CRC */
        public const uint AC4_SYNC_WORD_CRC = 0xAC41;  /* 16 sync bits with    CRC */
    }

    /*----------------------------------------------------------------------
    |   types
    +---------------------------------------------------------------------*/
    public class Ac4Header {
        public static bool _deprecatedV0 = true;

        // "ONDEL TEAM UUID\0"
        private static readonly byte[] ONDEL_TEAM_UUID = {
            (byte)'O', (byte)'N', (byte)'D', (byte)'E', (byte)'L', (byte)' ', (byte)'T', (byte)'E',
            (byte)'A', (byte)'M', (byte)' ', (byte)'U', (byte)'U', (byte)'I', (byte)'D', 0
        };

        // constructor
        // PORT NOTE: members that upstream leaves uninitialized on some paths (m_SyncWord,
        // m_HeaderSize, m_FrameSize, m_CrcSize when header_exists is false; m_BrCode; the program id
        // fields, m_ChannelCount and m_PresentationV1 when bitstream_version <= 1) are 0 / null here.
        public Ac4Header(byte[] bytes, uint size, bool header_exists = true)
        {
            /* Read the entire the frame */
            BitReader bits = new BitReader(bytes, size);

            /* Sync word and frame_size()*/
            if (header_exists) {
                _syncWord = bits.ReadBits(16);
                _headerSize = 2;
                if (_syncWord == Ap4.AC4_SYNC_WORD_CRC){
                    _crcSize = 2;
                }else {
                    _crcSize = 0;
                }
                _frameSize = bits.ReadBits(16);
                _headerSize += 2;
                if (_frameSize == 0xFFFF){
                    _frameSize = bits.ReadBits(24);
                    _headerSize += 3;
                }
            }

            /* Begin to parse TOC */
            uint tocStart = bits.GetBitsPosition() / 8;

            _bitstreamVersion = bits.ReadBits(2);
            if (_bitstreamVersion == 3) {
                _bitstreamVersion = Ac4Utils.Ac4VariableBits(bits, 2);
            }
            _sequenceCounter  = bits.ReadBits(10);
            _bWaitFrames      = (uint)bits.ReadBit();
            if (_bWaitFrames != 0) {
                _waitFrames = bits.ReadBits(3);
                if (_waitFrames > 0){ _brCode = bits.ReadBits(2);}
            }else {
                _waitFrames = unchecked((uint)-1);
            }

            _fsIndex               = (uint)bits.ReadBit();
            _frameRateIndex        = bits.ReadBits(4);
            _bIframeGlobal         = (uint)bits.ReadBit();
            _bSinglePresentation   = (uint)bits.ReadBit();
            if (_bSinglePresentation == 1){
                _nPresentations = 1;
                _bMorePresentations = 0;
            }else{
                _bMorePresentations = (uint)bits.ReadBit();
                if (_bMorePresentations == 1){
                    _nPresentations = Ac4Utils.Ac4VariableBits(bits, 2) + 2;
                }else{
                    _nPresentations = 0;
                }
            }

            _payloadBase = 0;
            _bPayloadBase = (uint)bits.ReadBit();
            if (_bPayloadBase == 1){
                _payloadBase = bits.ReadBits(5)  + 1;
                if (_payloadBase == 0x20){
                    _payloadBase += Ac4Utils.Ac4VariableBits(bits, 3);
                }
            }

            if (_bitstreamVersion <= 1){
                if (_deprecatedV0){
                    _deprecatedV0 = false;
                    Console.Out.Write("Warning: Bitstream version 0 is deprecated\n");
                }
            }else{
                _bProgramId = (uint)bits.ReadBit();
                if (_bProgramId == 1){
                    _shortProgramId = bits.ReadBits(16);
                    _bProgramUuidPresent = (uint)bits.ReadBit();
                    if (_bProgramUuidPresent == 1){
                        for (int cnt = 0; cnt < 16; cnt++){
                            _programUuid[cnt] = (byte)bits.ReadBits(8);
                        }
                    }else {
                        Array.Copy(ONDEL_TEAM_UUID, _programUuid, 16);
                    }
                }else {
                    _shortProgramId = 0;
                    _bProgramUuidPresent = 0;
                    Array.Copy(ONDEL_TEAM_UUID, _programUuid, 16);
                }

                uint maxGroupIndex = 0;
                uint[] firstPresentationSubstreamGroupIndexes = null;
                uint firstPresentationNSubstreamGroups = 0;

                if (_nPresentations > 0){
                    _presentationV1 = new Dac4Atom.Ac4Dsi.PresentationV1[_nPresentations];
                    for (uint i = 0; i < _nPresentations; i++) {
                        _presentationV1[i] = new Dac4Atom.Ac4Dsi.PresentationV1();
                    }
                } else {
                    _presentationV1 = null;
                }
                // ac4_presentation_v1_info()
                for (uint pres_idx = 0; pres_idx < _nPresentations; pres_idx++){
                    Dac4Atom.Ac4Dsi.PresentationV1 presentation = _presentationV1[pres_idx];
                    presentation.ParsePresentationV1Info(bits,
                                                         _bitstreamVersion,
                                                         _frameRateIndex,
                                                         pres_idx,
                                                         ref maxGroupIndex,
                                                         ref firstPresentationSubstreamGroupIndexes,
                                                         ref firstPresentationNSubstreamGroups);
                }

                uint bObjorAjoc = 0;
                uint channelCount = 0;
                uint speakerGroupIndexMask = 0;
                Dac4Atom.Ac4Dsi.SubStreamGroupV1[] substream_groups = new Dac4Atom.Ac4Dsi.SubStreamGroupV1[maxGroupIndex + 1];
                for (uint i = 0; i < maxGroupIndex + 1; i++) {
                    substream_groups[i] = new Dac4Atom.Ac4Dsi.SubStreamGroupV1();
                }
                for (uint sg = 0 ; (sg < (maxGroupIndex + 1)) && (_nPresentations > 0); sg ++) {
                    Dac4Atom.Ac4Dsi.SubStreamGroupV1 substream_group = substream_groups[sg];
                    int pres_index = GetPresentationIndexBySGIndex(sg);
                    if (pres_index == (int)Ap4Result.FAILURE) {
                        break;
                    }
                    uint localChannelCount = 0;
                    uint frame_rate_factor = (_presentationV1[pres_index].d.v1.dsi_frame_rate_multiply_info == 0)? 1: (uint)(_presentationV1[pres_index].d.v1.dsi_frame_rate_multiply_info * 2);
                    substream_group.ParseSubstreamGroupInfo(bits,
                                                            _bitstreamVersion,
                                                            unchecked((uint)GetPresentationVersionBySGIndex(sg)),
                                                            (byte)Ac4Utils.Ac4SubstreamGroupPartOfDefaultPresentation(sg, firstPresentationSubstreamGroupIndexes, firstPresentationNSubstreamGroups),
                                                            frame_rate_factor,
                                                            _fsIndex,
                                                            ref localChannelCount,
                                                            ref speakerGroupIndexMask,
                                                            ref bObjorAjoc);
                    if (channelCount < localChannelCount) { channelCount = localChannelCount;}
                }

                for (uint pres_idx = 0; pres_idx < _nPresentations; pres_idx++){
                    Dac4Atom.Ac4Dsi.PresentationV1.V1 v1 = _presentationV1[pres_idx].d.v1;
                    v1.substream_groups = new Dac4Atom.Ac4Dsi.SubStreamGroupV1[v1.n_substream_groups];
                    for (uint sg = 0; sg < v1.n_substream_groups; sg++){
                        // TODO: Default Copy Construct Function
                        v1.substream_groups[sg]   = substream_groups[v1.substream_group_indexs[sg]].Clone();
                        v1.dolby_atmos_indicator |= v1.substream_groups[sg].d.v1.dolby_atmos_indicator;
                    }
                }

                if (bObjorAjoc == 0){
                    _channelCount = (uint)Ac4Utils.Ac4ChannelCountFromSpeakerGroupIndexMask(speakerGroupIndexMask);
                }else {
                    _channelCount = 2;
                }
            }

            // substream_index_table
            uint n_substreams = bits.ReadBits(2);
            if (n_substreams == 0) {
                n_substreams = Ac4Utils.Ac4VariableBits(bits, 2) + 4;
            }
            byte b_size_present;
            if (n_substreams == 1) {
                b_size_present = (byte)bits.ReadBit();
            } else {
                b_size_present = 1;
            }
            if (b_size_present != 0) {
                for (uint s = 0; s < n_substreams; s++) {
                    byte b_more_bits = (byte)bits.ReadBit();
                    uint substream_size = bits.ReadBits(10);
                    if (b_more_bits != 0) {
                        substream_size += (Ac4Utils.Ac4VariableBits(bits, 2) << 10);
                    }
                }
            }
            if ((bits.GetBitsPosition() % 8) != 0) {
                bits.SkipBits(8 - (bits.GetBitsPosition() % 8));
            }
            _tocSize = (uint)(bits.GetBitsPosition() / 8 - tocStart);
        }

        // methods
        public Ap4Result Check()
        {
            // Bitstream version
            if (_bitstreamVersion != 2) {
                return Ap4Result.FAILURE;
            }
            if ((_fsIndex == 0  && _frameRateIndex != 13) || (_fsIndex == 1 &&  _frameRateIndex >13)){
                return Ap4Result.FAILURE;
            }

            return Ap4Result.SUCCESS;
        }

        // AC-4 Sync Frame Inforamtion
        public uint _syncWord;
        public uint _headerSize;
        public uint _frameSize;
        public uint _crcSize;

        // AC-4 Channel Count
        public uint _channelCount;

        // Ac-4 General/High Level TOC Information
        public uint _bitstreamVersion;
        public uint _sequenceCounter;
        public uint _bWaitFrames;
        public uint _waitFrames;
        public uint _brCode;
        public uint _fsIndex;
        public uint _frameRateIndex;
        public uint _bIframeGlobal;
        public uint _bSinglePresentation;
        public uint _bMorePresentations;
        public uint _nPresentations;
        public uint _bPayloadBase;
        public uint _payloadBase;
        public uint _bProgramId;
        public uint _shortProgramId;
        public uint _bProgramUuidPresent;
        public byte[] _programUuid = new byte[16];
        public uint _tocSize;

        // AC-4 Presentation Information
        public Dac4Atom.Ac4Dsi.PresentationV1[] _presentationV1;

        // class methods
        /*
         * Check that two fixed headers are the same
         */
        public static bool MatchFixed(Ac4Header frame, Ac4Header next_frame)
        {
            // Some parameter shall be const which defined in AC-4 in ISO-BMFF specs
            // TODO: More constraints will be added
            if ((frame._fsIndex == next_frame._fsIndex) &&
                (frame._frameRateIndex == next_frame._frameRateIndex)){
                return true;
            }else {
                return false;
            }
        }

        // PORT NOTE: declared as returning AP4_Result (an int) upstream; the values are a
        // presentation version / index, or AP4_FAILURE (-1).
        private int GetPresentationVersionBySGIndex(uint substream_group_index)
        {
            for (uint idx = 0; idx < _nPresentations; idx++){
                for (uint sg = 0; sg < _presentationV1[idx].d.v1.n_substream_groups; sg++){
                    if (substream_group_index ==  _presentationV1[idx].d.v1.substream_group_indexs[sg]) {
                        return _presentationV1[idx].presentation_version;
                    }
                }
            }
            return (int)Ap4Result.FAILURE;
        }

        private int GetPresentationIndexBySGIndex(uint substream_group_index)
        {
            for (uint idx = 0; idx < _nPresentations; idx++){
                for (uint sg = 0; sg < _presentationV1[idx].d.v1.n_substream_groups; sg++){
                    if (substream_group_index ==  _presentationV1[idx].d.v1.substream_group_indexs[sg]) {
                        return (int)idx;
                    }
                }
            }
            return (int)Ap4Result.FAILURE;
        }
    }

    // PORT NOTE: upstream C structs; classes here (filled in place by Ac4Parser.FindFrame)
    public class Ac4FrameInfo {
        public uint _headerSize;
        public uint _frameSize;
        public uint _CRCSize;
        public uint _channelCount;
        public uint _sampleDuration;
        public uint _mediaTimeScale;
        public uint _iframe;
        public Dac4Atom.Ac4Dsi _ac4Dsi = new Dac4Atom.Ac4Dsi();
    }

    public class Ac4Frame {
        public BitStream    _source;
        public Ac4FrameInfo _info = new Ac4FrameInfo();
    }

    public class Ac4Parser {
        // constructor
        public Ac4Parser()
        {
            _bits = new BitStream();
            _frameCount = 0;
        }

        // methods
        public Ap4Result Reset()
        {
            _frameCount = 0;

            return Ap4Result.SUCCESS;
        }

        public Ap4Result Feed(byte[]   buffer,
                              ref uint buffer_size,
                              uint     flags = 0)
        {
            return Feed(buffer, 0, ref buffer_size, flags);
        }

        // PORT NOTE: twin with an explicit offset for call sites that pass a pointer into a buffer.
        // Upstream takes `AP4_Size* buffer_size` (in: bytes offered, out: bytes accepted), which may
        // be NULL; a `ref uint` cannot be null, so that case is covered by buffer == null.
        public Ap4Result Feed(byte[]   buffer,
                              int      offset,
                              ref uint buffer_size,
                              uint     flags = 0)
        {
            uint free_space;

            /* update flags */
            _bits._flags = flags;

            /* possible shortcut */
            if (buffer == null ||
                buffer_size == 0) {
                return Ap4Result.SUCCESS;
            }

            /* see how much data we can write */
            free_space = _bits.GetBytesFree();
            if (buffer_size > free_space) buffer_size = free_space;
            if (buffer_size == 0) return Ap4Result.SUCCESS;

            /* write the data */
            return _bits.WriteBytes(buffer, offset, buffer_size);
        }

        public Ap4Result FindFrame(Ac4Frame frame)
        {
            uint       available;
            byte[]     raw_header = new byte[Ap4.AC4_HEADER_SIZE];
            Ap4Result  result;

            /* align to the start of the next byte */
            _bits.ByteAlign();

            /* find a frame header */
            result = FindHeader(raw_header);
            if (result.Failed()) return result;

            // duplicated work, just to get the frame size
            BitReader tmp_bits = new BitReader(raw_header, Ap4.AC4_HEADER_SIZE);
            uint sync_frame_size = GetSyncFrameSize(tmp_bits);
            if (sync_frame_size > (Ap4.BITSTREAM_BUFFER_SIZE - 1)) {
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }

            raw_header = new byte[sync_frame_size];
            /*
             * Error handling to skip the 'fake' sync word.
             * - the maximum sync frame size is about (AP4_BITSTREAM_BUFFER_SIZE - 1) bytes.
             */
            if (_bits.GetBytesAvailable() < sync_frame_size ) {
                if (_bits.GetBytesAvailable() == (Ap4.BITSTREAM_BUFFER_SIZE - 1)) {
                    // skip the sync word, assume it's 'fake' sync word
                    _bits.SkipBytes(2);
                }
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }
            // copy the whole frame becasue toc size is unknown
            _bits.PeekBytes(raw_header, sync_frame_size);
            /* parse the header */
            Ac4Header ac4_header = new Ac4Header(raw_header, sync_frame_size);

            // Place before goto statement to resolve Xcode compiler issue
            uint bit_rate_mode = 0;

            /* check the header */
            result = ac4_header.Check();
            if (result.Failed()) {
                _bits.SkipBytes(sync_frame_size);
                goto fail;
            }

            /* check if we have enough data to peek at the next header */
            available = _bits.GetBytesAvailable();
            // TODO: find the proper AP4_AC4_MAX_TOC_SIZE or just parse what this step need ?
            if (available >= ac4_header._frameSize + ac4_header._headerSize + ac4_header._crcSize + Ap4.AC4_HEADER_SIZE + Ap4.AC4_MAX_TOC_SIZE) {
                // enough to peek at the header of the next frame
                byte[] peek_raw_header = new byte[Ap4.AC4_HEADER_SIZE];

                _bits.SkipBytes(ac4_header._frameSize + ac4_header._headerSize + ac4_header._crcSize);
                _bits.PeekBytes(peek_raw_header, Ap4.AC4_HEADER_SIZE);

                // duplicated work, just to get the frame size
                BitReader peak_tmp_bits = new BitReader(peek_raw_header, Ap4.AC4_HEADER_SIZE);
                uint peak_sync_frame_size = GetSyncFrameSize(peak_tmp_bits);

                peek_raw_header = new byte[peak_sync_frame_size];
                // copy the whole frame becasue toc size is unknown
                if (_bits.GetBytesAvailable() < (peak_sync_frame_size)) {
                    peak_sync_frame_size = _bits.GetBytesAvailable();
                }
                _bits.PeekBytes(peek_raw_header, peak_sync_frame_size);

                _bits.SkipBytes(unchecked((uint)(-((int)(ac4_header._frameSize + ac4_header._headerSize + ac4_header._crcSize)))));

                /* check the header */
                Ac4Header peek_ac4_header = new Ac4Header(peek_raw_header, peak_sync_frame_size, true);
                result = peek_ac4_header.Check();
                if (result.Failed()) {
                    // TODO: need to reserve current sync frame ?
                    _bits.SkipBytes(sync_frame_size + peak_sync_frame_size);
                    goto fail;
                }

                /* check that the fixed part of this header is the same as the */
                /* fixed part of the previous header                           */
                if (!Ac4Header.MatchFixed(ac4_header, peek_ac4_header)) {
                    // TODO: need to reserve current sync frame ?
                    _bits.SkipBytes(sync_frame_size + peak_sync_frame_size);
                    goto fail;
                }
            } else if (available < (ac4_header._frameSize + ac4_header._headerSize + ac4_header._crcSize) || (_bits._flags & Ap4.BITSTREAM_FLAG_EOS) == 0) {
                // not enough for a frame, or not at the end (in which case we'll want to peek at the next header)
                return Ap4Result.ERROR_NOT_ENOUGH_DATA;
            }

            _bits.SkipBytes(ac4_header._headerSize);

            /* fill in the frame info */
            frame._info._headerSize     = ac4_header._headerSize;
            frame._info._frameSize      = ac4_header._frameSize;
            frame._info._CRCSize        = ac4_header._crcSize;
            frame._info._channelCount   = ac4_header._channelCount;
            frame._info._sampleDuration = (ac4_header._fsIndex == 0)? 2048 : Ac4Utils.Ac4SampleDeltaTable   [ac4_header._frameRateIndex];
            frame._info._mediaTimeScale = (ac4_header._fsIndex == 0)? 44100: Ac4Utils.Ac4MediaTimeScaleTable[ac4_header._frameRateIndex];
            frame._info._iframe         = ac4_header._bIframeGlobal;

            /* fill the AC4 DSI info */
            frame._info._ac4Dsi.ac4_dsi_version        = 1;
            frame._info._ac4Dsi.d.v1.bitstream_version = (byte)ac4_header._bitstreamVersion;
            frame._info._ac4Dsi.d.v1.fs_index          = (byte)ac4_header._fsIndex;
            frame._info._ac4Dsi.d.v1.fs                = Ac4Utils.Ac4SamplingFrequencyTable[frame._info._ac4Dsi.d.v1.fs_index];
            frame._info._ac4Dsi.d.v1.frame_rate_index  = (byte)ac4_header._frameRateIndex;
            frame._info._ac4Dsi.d.v1.b_program_id      = (byte)ac4_header._bProgramId;
            frame._info._ac4Dsi.d.v1.short_program_id  = (ushort)ac4_header._shortProgramId;
            frame._info._ac4Dsi.d.v1.b_uuid            = (byte)ac4_header._bProgramUuidPresent;
            Array.Copy(ac4_header._programUuid, frame._info._ac4Dsi.d.v1.program_uuid, 16);

            // Calcuate the bit rate mode according to ETSI TS 103 190-2 V1.2.1 Annex B
            if (ac4_header._waitFrames      == 0)                              { bit_rate_mode = 1;}
            else if (ac4_header._waitFrames >= 1 && ac4_header._waitFrames <= 6) { bit_rate_mode = 2;}
            else if (ac4_header._waitFrames >  6)                              { bit_rate_mode = 3;}

            frame._info._ac4Dsi.d.v1.ac4_bitrate_dsi.bit_rate_mode = (byte)bit_rate_mode;
            frame._info._ac4Dsi.d.v1.ac4_bitrate_dsi.bit_rate = 0;                    // unknown, fixed value now
            frame._info._ac4Dsi.d.v1.ac4_bitrate_dsi.bit_rate_precision = 0xffffffff; // unknown, fixed value now
            frame._info._ac4Dsi.d.v1.n_presentations = (ushort)ac4_header._nPresentations;

            if (ac4_header._presentationV1 != null) {
                Ap4.ASSERT(ac4_header._presentationV1 != null);
                // PORT NOTE: upstream copies the structs with AP4_CopyMemory and then replaces the
                // pointer members with freshly allocated copies; Clone() + new arrays do the same.
                Dac4Atom.Ac4Dsi.PresentationV1[] presentations = new Dac4Atom.Ac4Dsi.PresentationV1[ac4_header._nPresentations];
                frame._info._ac4Dsi.d.v1.presentations = presentations;
                for (uint pres_idx = 0; pres_idx < ac4_header._nPresentations; pres_idx++) {
                    presentations[pres_idx] = ac4_header._presentationV1[pres_idx].Clone();
                }
                for (uint pres_idx = 0; pres_idx < ac4_header._nPresentations; pres_idx++) {
                    Dac4Atom.Ac4Dsi.PresentationV1.V1 src = ac4_header._presentationV1[pres_idx].d.v1;
                    Dac4Atom.Ac4Dsi.PresentationV1.V1 dst = presentations[pres_idx].d.v1;
                    Ap4.ASSERT(src.substream_groups != null);
                    uint n_substream_group = src.n_substream_groups;
                    dst.substream_groups = new Dac4Atom.Ac4Dsi.SubStreamGroupV1[n_substream_group];
                    dst.substream_group_indexs = new uint[n_substream_group];
                    if (n_substream_group != 0) {
                        Array.Copy(src.substream_group_indexs, dst.substream_group_indexs, (int)n_substream_group);
                    }
                    for (uint sg_idx = 0; sg_idx < n_substream_group; sg_idx++) {
                        dst.substream_groups[sg_idx] = src.substream_groups[sg_idx].Clone();
                    }
                    for (uint sg_idx = 0; sg_idx < n_substream_group; sg_idx++) {
                        Ap4.ASSERT(src.substream_groups[sg_idx].d.v1.substreams != null);
                        int n_substreams = src.substream_groups[sg_idx].d.v1.n_substreams;
                        Dac4Atom.Ac4Dsi.SubStream[] substreams = new Dac4Atom.Ac4Dsi.SubStream[n_substreams];
                        dst.substream_groups[sg_idx].d.v1.substreams = substreams;
                        for (int i = 0; i < n_substreams; i++) {
                            substreams[i] = src.substream_groups[sg_idx].d.v1.substreams[i].Clone();
                        }
                    }
                }
            }

            /* set the frame source */
            frame._source = _bits;

            return Ap4Result.SUCCESS;

        fail:
            /* skip the header and return (only skip the first byte in  */
            /* case this was a false header that hides one just after)  */
            return Ap4.ERROR_CORRUPTED_BITSTREAM;
        }

        // PORT NOTE: AP4_Ac4Parser::Skip is declared upstream but never defined; not ported.

        public uint GetBytesFree()
        {
            return (_bits.GetBytesFree());
        }

        public uint GetBytesAvailable()
        {
            return (_bits.GetBytesAvailable());
        }

        // methods
        private Ap4Result FindHeader(byte[] header)
        {
            uint available = _bits.GetBytesAvailable();

            /* look for the sync pattern */
            while (available-- >= Ap4.AC4_HEADER_SIZE) {
                _bits.PeekBytes(header, 2);

                if (((uint)((header[0] << 8) | header[1]) == Ap4.AC4_SYNC_WORD_CRC) || ((uint)((header[0] << 8) | header[1]) == Ap4.AC4_SYNC_WORD)) {
                    /* found a sync pattern, read the entire the header */
                    _bits.PeekBytes(header, Ap4.AC4_HEADER_SIZE);

                    return Ap4Result.SUCCESS;
                } else {
                    _bits.SkipBytes(1);
                }
            }

            return Ap4Result.ERROR_NOT_ENOUGH_DATA;
        }

        private uint GetSyncFrameSize(BitReader bits)
        {
            uint sync_word  = bits.ReadBits(16);
            uint frame_size = bits.ReadBits(16);
            uint head_size  = 4;
            if (frame_size == 0xFFFF){
                frame_size = bits.ReadBits(24);
                head_size += 3;
            }
            if (sync_word == Ap4.AC4_SYNC_WORD_CRC) {
                head_size += 2;
            }
            return (head_size + frame_size);
        }

        // members
        private BitStream _bits;
        private uint      _frameCount;
    }
}
