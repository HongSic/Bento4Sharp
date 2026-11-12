// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap48bdlAtom.h, Ap48bdlAtom.cpp
// Original work Copyright 2002-2009 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint _8BDL_XML_DATA_ENCODING = 0x786d6c20; // 'xml '
    }

    /*----------------------------------------------------------------------
    |   AP4_8bdlAtom
    +---------------------------------------------------------------------*/
    public class _8bdlAtom : Atom
    {
        // virtual constructor
        /*----------------------------------------------------------------------
        |   AP4_8bdlAtom::Create
        +---------------------------------------------------------------------*/
        public static _8bdlAtom Create(uint size, ByteStream stream)
        {
            // make sure we have enough data
            if (size < Ap4.ATOM_HEADER_SIZE+8) {
                return null;
            } else {
                return new _8bdlAtom(size, stream);
            }
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_8bdlAtom::AP4_8bdlAtom
        +---------------------------------------------------------------------*/
        public _8bdlAtom(uint    encoding,
                         uint    encoding_version,
                         byte[]  data,
                         uint    data_size) :
            base(AtomType._8BDL, (uint)(Ap4.ATOM_HEADER_SIZE+8+data_size))
        {
            _encoding = encoding;
            _encodingVersion = encoding_version;
            _bundleData = new DataBuffer(data, data_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_8bdlAtom::AP4_8bdlAtom
        +---------------------------------------------------------------------*/
        private _8bdlAtom(uint       size,
                          ByteStream stream) :
            base(AtomType._8BDL, (uint)(size))
        {
            _encoding = Ap4._8BDL_XML_DATA_ENCODING;
            _encodingVersion = 0;
            _bundleData = new DataBuffer(size-Ap4.ATOM_HEADER_SIZE-8);

            stream.ReadUI32(out _encoding);
            stream.ReadUI32(out _encodingVersion);
            _bundleData.SetDataSize(_bundleData.GetBufferSize());
            stream.Read(_bundleData.UseData(), _bundleData.GetDataSize());
        }

        /*----------------------------------------------------------------------
        |   AP4_8bdlAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // encoding
            result = stream.WriteUI32(_encoding);
            if (result.Failed()) return result;

            // encoding version
            result = stream.WriteUI32(_encodingVersion);
            if (result.Failed()) return result;

            // bundle_data
            result = stream.Write(_bundleData.GetData(), _bundleData.GetDataSize());
            if (result.Failed()) return result;

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_8bdlAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string enc = Ap4Utils.FormatFourChars(_encoding);
            inspector.AddField("encoding", enc);
            inspector.AddField("encoding_version", _encodingVersion);
            if (_encoding == Ap4._8BDL_XML_DATA_ENCODING) {
                // we, in fact have an xml string
                // PORT NOTE: upstream builds an AP4_String of the whole data and passes
                // xml.GetChars(), which the inspector reads as a C string (up to the
                // first 0 byte).
                string xml = Ap4Utils.StringFromCString(_bundleData.GetData(), 0, _bundleData.GetDataSize());
                inspector.AddField("bundle_data", xml);
            } else {
                inspector.AddField("bundle_data", _bundleData.GetData(), _bundleData.GetDataSize());
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint       GetEncoding()        { return _encoding; }
        public uint       GetEncodingVersion() { return _encodingVersion; }
        public DataBuffer GetBundleData()      { return _bundleData; }

        // members
        private uint       _encoding;
        private uint       _encodingVersion;
        private DataBuffer _bundleData;
    }
}
