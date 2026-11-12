// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4DmlpAtom.h, Ap4DmlpAtom.cpp
// Original work Copyright 2002-2018 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_DmlpAtom
    +---------------------------------------------------------------------*/
    public class DmlpAtom : Atom
    {
        /*----------------------------------------------------------------------
        |   AP4_DmlpAtom::Create
        +---------------------------------------------------------------------*/
        public static DmlpAtom Create(uint size, ByteStream stream)
        {
            if (size < Ap4.ATOM_HEADER_SIZE + 10) {
                return null;
            }

            uint format_info;
            stream.ReadUI32(out format_info);
            ushort peak_data_rate;
            stream.ReadUI16(out peak_data_rate);
            uint reserved;
            stream.ReadUI32(out reserved);

            return new DmlpAtom(format_info, peak_data_rate);
        }

        /*----------------------------------------------------------------------
        |   AP4_DmlpAtom::AP4_DmlpAtom
        +---------------------------------------------------------------------*/
        public DmlpAtom(uint format_info, ushort peak_data_rate) :
            base(AtomType.DMLP, Ap4.ATOM_HEADER_SIZE)
        {
            _formatInfo = format_info;
            _peakDataRate = peak_data_rate;
            _size32 += 10;
        }

        /*----------------------------------------------------------------------
        |   AP4_DmlpAtom::AP4_DmlpAtom
        +---------------------------------------------------------------------*/
        public DmlpAtom(DmlpAtom other) :
            base(AtomType.DMLP, other._size32)
        {
            _formatInfo = other._formatInfo;
            _peakDataRate = other._peakDataRate;
        }

        /*----------------------------------------------------------------------
        |   AP4_DmlpAtom::GetCodecString
        +---------------------------------------------------------------------*/
        public void GetCodecString(out string codec)
        {
            codec = "mlpa";
        }

        /*----------------------------------------------------------------------
        |   AP4_DmlpAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.WriteUI32(_formatInfo);
            if (result.Failed()) return result;
            result = stream.WriteUI16((ushort)(_peakDataRate << 1));
            if (result.Failed()) return result;
            result = stream.WriteUI32(0);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_DmlpAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("format_info", _formatInfo);
            inspector.AddField("peak_data_rate", _peakDataRate);
            return Ap4Result.SUCCESS;
        }

        // members
        public uint   _formatInfo;
        public ushort _peakDataRate;
    }
}
