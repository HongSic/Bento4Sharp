// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4VmhdAtom.h, Ap4VmhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_VmhdAtom
    +---------------------------------------------------------------------*/
    public class VmhdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_VmhdAtom::Create
        +---------------------------------------------------------------------*/
        public static VmhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new VmhdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_VmhdAtom::AP4_VmhdAtom
        +---------------------------------------------------------------------*/
        public VmhdAtom(ushort graphics_mode, ushort r, ushort g, ushort b) :
            base(AtomType.VMHD, Ap4.FULL_ATOM_HEADER_SIZE+8, 0, 1)
        {
            _graphicsMode = graphics_mode;
            _opColor[0] = r;
            _opColor[1] = g;
            _opColor[2] = b;
        }

        /*----------------------------------------------------------------------
        |   AP4_VmhdAtom::AP4_VmhdAtom
        +---------------------------------------------------------------------*/
        private VmhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.VMHD, size, version, flags)
        {
            stream.ReadUI16(out _graphicsMode);
            // PORT NOTE: upstream reads the 6 bytes straight into AP4_UI16 m_OpColor[3]
            // (stream.Read(m_OpColor, sizeof(m_OpColor))), i.e. in the byte order of the
            // platform, and writes them back the same way. The same is done here so that
            // the "op_color" text and the bytes written match upstream on this platform.
            byte[] op_color = new byte[6];
            stream.Read(op_color, 6);
            for (int i=0; i<3; i++) {
                if (Ap4.PLATFORM_BYTE_ORDER == Ap4.PLATFORM_BYTE_ORDER_BIG_ENDIAN) {
                    _opColor[i] = (ushort)((op_color[2*i]<<8) | op_color[2*i+1]);
                } else {
                    _opColor[i] = (ushort)((op_color[2*i+1]<<8) | op_color[2*i]);
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_VmhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // graphics mode
            result = stream.WriteUI16(_graphicsMode);
            if (result.Failed()) return result;

            // op color
            // PORT NOTE: platform byte order, see the constructor.
            byte[] op_color = new byte[6];
            for (int i=0; i<3; i++) {
                if (Ap4.PLATFORM_BYTE_ORDER == Ap4.PLATFORM_BYTE_ORDER_BIG_ENDIAN) {
                    op_color[2*i]   = (byte)(_opColor[i]>>8);
                    op_color[2*i+1] = (byte)(_opColor[i]&0xFF);
                } else {
                    op_color[2*i+1] = (byte)(_opColor[i]>>8);
                    op_color[2*i]   = (byte)(_opColor[i]&0xFF);
                }
            }
            result = stream.Write(op_color, 6);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_VmhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("graphics_mode", _graphicsMode);
            // "%04x,%04x,%04x"
            string formatted = _opColor[0].ToString("x4", CultureInfo.InvariantCulture) + "," +
                               _opColor[1].ToString("x4", CultureInfo.InvariantCulture) + "," +
                               _opColor[2].ToString("x4", CultureInfo.InvariantCulture);
            inspector.AddField("op_color", formatted);

            return Ap4Result.SUCCESS;
        }

        // members
        private ushort   _graphicsMode;
        private ushort[] _opColor = new ushort[3];
    }
}
