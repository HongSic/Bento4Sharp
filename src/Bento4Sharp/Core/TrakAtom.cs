// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4TrakAtom.h, Ap4TrakAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_TrakAtom
    +---------------------------------------------------------------------*/
    public class TrakAtom : ContainerAtom
    {
        // class methods
        public static TrakAtom Create(uint        size,
                                       ByteStream  stream,
                                       AtomFactory atom_factory) {
            return new TrakAtom(size, stream, atom_factory);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::AP4_TrakAtom
        +---------------------------------------------------------------------*/
        public TrakAtom(SampleTable sample_table,
                        uint        hdlr_type,
                        string      hdlr_name,
                        uint        track_id,
                        ulong       creation_time,
                        ulong       modification_time,
                        ulong       track_duration,
                        uint        media_time_scale,
                        ulong       media_duration,
                        ushort      volume,
                        string      language,
                        uint        width,
                        uint        height,
                        ushort      layer = 0,
                        ushort      alternate_group = 0,
                        int[]       matrix = null) :
            base(AtomType.TRAK)
        {
            Ap4Result result;

            // create a tkhd atom
            _tkhdAtom = new TkhdAtom(creation_time,
                                     modification_time,
                                     track_id,
                                     track_duration,
                                     volume,
                                     width,
                                     height,
                                     layer,
                                     alternate_group,
                                     matrix);

            // create an edts

            // create a mdia atom
            ContainerAtom mdia = new ContainerAtom(AtomType.MDIA);

            // create a hdlr atom for the mdia atom
            HdlrAtom hdlr = new HdlrAtom(hdlr_type, hdlr_name);

            // create a minf atom
            ContainerAtom minf = new ContainerAtom(AtomType.MINF);

            // create a media header atom for minf (vmhd, smhd, hmhd or nmhd)
            Atom minf_header;
            switch (hdlr_type) {
                case Ap4.HANDLER_TYPE_VIDE:
                    minf_header = new VmhdAtom(0, 0, 0, 0);
                    break;

                case Ap4.HANDLER_TYPE_SOUN:
                    minf_header = new SmhdAtom(0);
                    break;

                case Ap4.HANDLER_TYPE_SUBT:
                    minf_header = new SthdAtom();
                    break;

                default:
                    minf_header = new NmhdAtom();
                    break;
            }

            // create a dinf atom for minf
            ContainerAtom dinf = new ContainerAtom(AtomType.DINF);

            // create a url atom as a ref for dref
            Atom url = new UrlAtom(); // local ref

            // create a dref atom for dinf
            DrefAtom dref = new DrefAtom(new Atom[] { url }, 1);

            // create a stbl atom for minf
            ContainerAtom stbl;
            result = sample_table.GenerateStblAtom(out stbl);
            if (result.Failed()) stbl = null;

            // populate the dinf atom
            dinf.AddChild(dref);

            // populate the minf atom
            minf.AddChild(minf_header);
            minf.AddChild(dinf);
            if (stbl != null) minf.AddChild(stbl);

            // create a mdhd atom for the mdia atom
            _mdhdAtom = new MdhdAtom(creation_time,
                                     modification_time,
                                     media_time_scale,
                                     media_duration,
                                     language);

            // populate the mdia atom
            mdia.AddChild(_mdhdAtom);
            mdia.AddChild(hdlr);
            mdia.AddChild(minf);

            // attach the children
            AddChild(_tkhdAtom);
            AddChild(mdia);
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::AP4_TrakAtom
        +---------------------------------------------------------------------*/
        private TrakAtom(uint        size,
                         ByteStream  stream,
                         AtomFactory atom_factory) :
            base(AtomType.TRAK, size, false, stream, atom_factory)
        {
            _tkhdAtom = FindChild("tkhd") as TkhdAtom;
            _mdhdAtom = FindChild("mdia/mdhd") as MdhdAtom;
        }

        public TkhdAtom GetTkhdAtom() { return _tkhdAtom; }
        public TkhdAtom UseTkhdAtom() { return _tkhdAtom; }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::GetId
        +---------------------------------------------------------------------*/
        public uint GetId()
        {
            return _tkhdAtom != null?_tkhdAtom.GetTrackId():0;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::SetId
        +---------------------------------------------------------------------*/
        public Ap4Result SetId(uint id)
        {
            if (_tkhdAtom != null) {
                _tkhdAtom.SetTrackId(id);
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::GetMediaTimeScale
        +---------------------------------------------------------------------*/
        public uint GetMediaTimeScale()
        {
            return _mdhdAtom != null?_mdhdAtom.GetTimeScale():0;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::SetMediaTimeScale
        +---------------------------------------------------------------------*/
        public Ap4Result SetMediaTimeScale(uint timescale)
        {
            if (_mdhdAtom != null) {
                _mdhdAtom.SetTimeScale(timescale);
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::GetDuration
        +---------------------------------------------------------------------*/
        public ulong GetDuration()
        {
            return _tkhdAtom != null?_tkhdAtom.GetDuration():0;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::SetDuration
        +---------------------------------------------------------------------*/
        public Ap4Result SetDuration(ulong duration)
        {
            if (_tkhdAtom != null) {
                _tkhdAtom.SetDuration(duration);
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::GetMediaDuration
        +---------------------------------------------------------------------*/
        public ulong GetMediaDuration()
        {
            return _mdhdAtom != null?_mdhdAtom.GetDuration():0;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::SetMediaDuration
        +---------------------------------------------------------------------*/
        public Ap4Result SetMediaDuration(uint duration)
        {
            if (_mdhdAtom != null) {
                _mdhdAtom.SetDuration(duration);
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::GetWidth
        +---------------------------------------------------------------------*/
        public uint GetWidth()
        {
            return _tkhdAtom != null?_tkhdAtom.GetWidth():0;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::SetWidth
        +---------------------------------------------------------------------*/
        public Ap4Result SetWidth(uint width)
        {
            if (_tkhdAtom != null) {
                _tkhdAtom.SetWidth(width);
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::GetHeight
        +---------------------------------------------------------------------*/
        public uint GetHeight()
        {
            return _tkhdAtom != null?_tkhdAtom.GetHeight():0;
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::SetHeight
        +---------------------------------------------------------------------*/
        public Ap4Result SetHeight(uint height)
        {
            if (_tkhdAtom != null) {
                _tkhdAtom.SetHeight(height);
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::AdjustChunkOffsets
        +---------------------------------------------------------------------*/
        public Ap4Result AdjustChunkOffsets(long delta)
        {
            Atom atom;
            if ((atom = FindChild("mdia/minf/stbl/stco")) != null) {
                StcoAtom stco = atom as StcoAtom;
                if (stco == null) return Ap4Result.ERROR_INVALID_FORMAT;
                return stco.AdjustChunkOffsets((int)delta);
            } else if ((atom = FindChild("mdia/minf/stbl/co64")) != null) {
                Co64Atom co64 = atom as Co64Atom;
                if (co64 == null) return Ap4Result.ERROR_INVALID_FORMAT;
                return co64.AdjustChunkOffsets(delta);
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::GetChunkOffsets
        +---------------------------------------------------------------------*/
        public Ap4Result GetChunkOffsets(List<ulong> chunk_offsets)
        {
            Atom atom;
            if ((atom = FindChild("mdia/minf/stbl/stco")) != null) {
                StcoAtom stco = atom as StcoAtom;
                if (stco == null) return Ap4Result.ERROR_INTERNAL;
                uint   stco_chunk_count   = stco.GetChunkCount();
                uint[] stco_chunk_offsets = stco.GetChunkOffsets();
                SetItemCount(chunk_offsets, stco_chunk_count);
                for (uint i=0; i<stco_chunk_count; i++) {
                    chunk_offsets[(int)i] = stco_chunk_offsets[i];
                }
                return Ap4Result.SUCCESS;
            } else if ((atom = FindChild("mdia/minf/stbl/co64")) != null) {
                Co64Atom co64 = atom as Co64Atom;
                if (co64 == null) return Ap4Result.ERROR_INTERNAL;
                uint    co64_chunk_count   = co64.GetChunkCount();
                ulong[] co64_chunk_offsets = co64.GetChunkOffsets();
                SetItemCount(chunk_offsets, co64_chunk_count);
                for (uint i=0; i<co64_chunk_count; i++) {
                    chunk_offsets[(int)i] = co64_chunk_offsets[i];
                }
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_TrakAtom::SetChunkOffsets
        +---------------------------------------------------------------------*/
        public Ap4Result SetChunkOffsets(List<ulong> chunk_offsets)
        {
            Atom atom;
            if ((atom = FindChild("mdia/minf/stbl/stco")) != null) {
                StcoAtom stco = atom as StcoAtom;
                if (stco == null) return Ap4Result.ERROR_INTERNAL;
                uint   stco_chunk_count   = stco.GetChunkCount();
                uint[] stco_chunk_offsets = stco.GetChunkOffsets();
                if (stco_chunk_count > (uint)chunk_offsets.Count) {
                    return Ap4Result.ERROR_OUT_OF_RANGE;
                }
                for (uint i=0; i<stco_chunk_count; i++) {
                    stco_chunk_offsets[i] = (uint)chunk_offsets[(int)i];
                }
                return Ap4Result.SUCCESS;
            } else if ((atom = FindChild("mdia/minf/stbl/co64")) != null) {
                Co64Atom co64 = atom as Co64Atom;
                if (co64 == null) return Ap4Result.ERROR_INTERNAL;
                uint    co64_chunk_count   = co64.GetChunkCount();
                ulong[] co64_chunk_offsets = co64.GetChunkOffsets();
                if (co64_chunk_count > (uint)chunk_offsets.Count) {
                    return Ap4Result.ERROR_OUT_OF_RANGE;
                }
                for (uint i=0; i<co64_chunk_count; i++) {
                    co64_chunk_offsets[i] = chunk_offsets[(int)i];
                }
                return Ap4Result.SUCCESS;
            } else {
                return Ap4Result.ERROR_INVALID_STATE;
            }
        }

        // PORT NOTE: AP4_Array<AP4_UI64>::SetItemCount (keeps the existing items,
        // truncates or appends zeros).
        private static void SetItemCount(List<ulong> list, uint item_count)
        {
            if ((uint)list.Count > item_count) {
                list.RemoveRange((int)item_count, list.Count-(int)item_count);
            } else {
                while ((uint)list.Count < item_count) list.Add(0);
            }
        }

        // members
        private TkhdAtom _tkhdAtom;
        private MdhdAtom _mdhdAtom;
    }
}
