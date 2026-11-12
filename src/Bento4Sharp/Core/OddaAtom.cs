// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4OddaAtom.h, Ap4OddaAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_OddaAtom
    +---------------------------------------------------------------------*/
    public class OddaAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_OddaAtom::Create
        +---------------------------------------------------------------------*/
        public static OddaAtom Create(ulong      size,
                                       ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version != 0) return null;
            return new OddaAtom(size, version, flags, stream);
        }

        /*----------------------------------------------------------------------
        |   AP4_OddaAtom::AP4_OddaAtom
        +---------------------------------------------------------------------*/
        private OddaAtom(ulong      size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.ODDA, size, true, version, flags)
        {
            // data length
            stream.ReadUI64(out _encryptedDataLength);

            // get the source stream position
            ulong position;
            stream.Tell(out position);

            // create a substream to represent the payload
            _encryptedPayload = new SubStream(stream, position, _encryptedDataLength);

            // seek to the end
            stream.Seek(position+_encryptedDataLength);
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_OddaAtom::AP4_OddaAtom
        +---------------------------------------------------------------------*/
        public OddaAtom(ByteStream encrypted_payload) :
            base(AtomType.ODDA, (ulong)0, true, 0, 0)
        {
            // encrypted data length
            encrypted_payload.GetSize(out _encryptedDataLength);

            // update our size
            SetSize(Ap4.FULL_ATOM_HEADER_SIZE_64+8+_encryptedDataLength, true);

            // keep a reference to the encrypted payload
            _encryptedPayload = encrypted_payload;
        }

        /*----------------------------------------------------------------------
        |   AP4_OddaAtom::SetEncryptedPayload
        +---------------------------------------------------------------------*/
        public Ap4Result SetEncryptedPayload(ByteStream stream, ulong length)
        {
            // keep a reference to the stream
            _encryptedPayload = stream;

            // update the size
            _encryptedDataLength = length;
            SetSize(Ap4.FULL_ATOM_HEADER_SIZE_64 + 8 + length, true);
            if (_parent != null) _parent.OnChildChanged(this);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OddaAtom::SetEncryptedPayload
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Sets the encrypted payload stream (and releases any existing stream references)
        /// </summary>
        public Ap4Result SetEncryptedPayload(ByteStream stream)
        {
            // the new encrypted data length is the size of the stream
            ulong length;
            Ap4Result result = stream.GetSize(out length);
            if (result.Failed()) return result;

            return SetEncryptedPayload(stream, length);
        }

        /*----------------------------------------------------------------------
        |   AP4_OddaAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the content type
            { Ap4Result _result = stream.WriteUI64(_encryptedDataLength); if (_result.Failed()) return _result; }

            // check that we have a source stream
            // and a normal size
            if (_encryptedPayload == null || GetSize() < 8) {
                return Ap4Result.FAILURE;
            }

            // rewind the encrypted stream
            { Ap4Result _result = _encryptedPayload.Seek(0); if (_result.Failed()) return _result; }

            // copy the encrypted stream to the output
            { Ap4Result _result = _encryptedPayload.CopyTo(stream, _encryptedDataLength); if (_result.Failed()) return _result; }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_OddaAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("encrypted_data_length", (uint)_encryptedDataLength);
            return Ap4Result.SUCCESS;
        }

        // accessors
        public ulong GetEncryptedDataLength() { return _encryptedDataLength; }

        /// <summary>
        /// Returns a reference to the encrypted payload stream (does not increment the reference counter)
        /// </summary>
        public ByteStream GetEncryptedPayload() { return _encryptedPayload; }

        // members
        private ulong       _encryptedDataLength;
        private ByteStream  _encryptedPayload;
    }
}
