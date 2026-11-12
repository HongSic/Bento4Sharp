// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4ColrAtom.h, Ap4ColrAtom.cpp
// Original work Copyright 2002-2016 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_ColrAtom
    +---------------------------------------------------------------------*/
    public class ColrAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_ColrAtom::Create
        +---------------------------------------------------------------------*/
        public static ColrAtom Create(uint size, ByteStream stream)
        {
            // read the raw bytes in a buffer
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;
            DataBuffer payload_data = new DataBuffer(payload_size);
            Ap4Result result = stream.Read(payload_data.UseData(), payload_size);
            if (result.Failed()) return null;

            return new ColrAtom(size, payload_data.GetData());
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_ColrAtom::AP4_ColrAtom
        +---------------------------------------------------------------------*/
        public ColrAtom() :
            base(AtomType.COLR, Ap4.ATOM_HEADER_SIZE + 11)
        {
            _colourParameterType = 0;
            _primariesIndex = 0;
            _transferFunctionIndex = 0;
            _matrixIndex = 0;
            _fullRangeFlag = 0;
            _payloadSize = 11;
        }

        /*----------------------------------------------------------------------
        |   AP4_ColrAtom::AP4_ColrAtom
        +---------------------------------------------------------------------*/
        public ColrAtom(uint   colour_parameter_type,
                        ushort primaries_index,
                        ushort transfer_function_index,
                        ushort matrix_index,
                        byte   video_full_range_flag,
                        uint   payload_size = 11) : // default payload size is 11, unless specified
            base(AtomType.COLR, Ap4.ATOM_HEADER_SIZE + payload_size)
        {
            _colourParameterType = colour_parameter_type;
            _primariesIndex = primaries_index;
            _transferFunctionIndex = transfer_function_index;
            _matrixIndex = matrix_index;
            _fullRangeFlag = video_full_range_flag;
            _payloadSize = payload_size;
        }

        /*----------------------------------------------------------------------
        |   AP4_ColrAtom::AP4_ColrAtom
        +---------------------------------------------------------------------*/
        // PORT NOTE: when the payload size is neither 10 nor 11 upstream leaves the
        // members uninitialized; here they are zero.
        public ColrAtom(uint size, byte[] payload) :
            base(AtomType.COLR, size)
        {
            // make a copy of our configuration bytes
            uint payload_size = size-Ap4.ATOM_HEADER_SIZE;

            // payload size of QuickTime colr is 10,
            // payload size of ISOBMFF colr is 11.
            if (payload_size != 10 && payload_size != 11) return;

            // parse the payload
            _payloadSize = payload_size;
            _colourParameterType = Ap4Utils.BytesToUInt32BE(payload, 0);
            _primariesIndex = Ap4Utils.BytesToUInt16BE(payload, 4);
            _transferFunctionIndex = Ap4Utils.BytesToUInt16BE(payload, 6);
            _matrixIndex = Ap4Utils.BytesToUInt16BE(payload, 8);
            if (payload_size == 11) {
                _fullRangeFlag = (byte)((payload[10] >> 7) & 0x01);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_ColrAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            byte[] payload = new byte[11];
            payload[0] = (byte)(_colourParameterType >> 24);
            payload[1] = (byte)((_colourParameterType >> 16) & 0xFF);
            payload[2] = (byte)((_colourParameterType >> 8) & 0xFF);
            payload[3] = (byte)(_colourParameterType & 0xFF);
            payload[4] = (byte)(_primariesIndex >> 8);
            payload[5] = (byte)(_primariesIndex & 0xFF);
            payload[6] = (byte)(_transferFunctionIndex >> 8);
            payload[7] = (byte)(_transferFunctionIndex & 0xFF);
            payload[8] = (byte)(_matrixIndex >> 8);
            payload[9] = (byte)(_matrixIndex & 0xFF);
            payload[10] = (byte)((_fullRangeFlag << 7) & 0x80);
            if (GetPayloadSize() == 10) {
                return stream.Write(payload, 10);
            }
            return stream.Write(payload, 11);
        }

        /*----------------------------------------------------------------------
        |   AP4_ColrAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("Colour Parameter Type:", _colourParameterType);
            inspector.AddField("Primaries Index:", _primariesIndex);
            inspector.AddField("Transfer Funcion Index:", _transferFunctionIndex);
            inspector.AddField("Matrix Index:", _matrixIndex);
            if (GetPayloadSize() == 11) {
                inspector.AddField("Video Full Range Flag:", _fullRangeFlag);
            }
            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint   GetColourParameterType()            { return _colourParameterType; }
        public ushort GetPrimariesIndex()                 { return _primariesIndex; }
        public ushort GetTransferFunctionIndex()          { return _transferFunctionIndex; }
        public ushort GetMatrixIndex()                    { return _matrixIndex; }
        public byte   GetFullRangeFlag()                  { return _fullRangeFlag; }
        public uint   GetPayloadSize()                    { return _payloadSize; }

        // members
        private uint   _colourParameterType;
        private ushort _primariesIndex;
        private ushort _transferFunctionIndex;
        private ushort _matrixIndex;
        private byte   _fullRangeFlag;
        private uint   _payloadSize;
    }
}
