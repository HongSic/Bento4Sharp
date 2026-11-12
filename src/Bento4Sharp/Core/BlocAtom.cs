// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4BlocAtom.h, Ap4BlocAtom.cpp
// Original work Copyright 2002-2012 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_BlocAtom
    +---------------------------------------------------------------------*/
    public class BlocAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_BlocAtom::Create
        +---------------------------------------------------------------------*/
        public static BlocAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new BlocAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_BlocAtom::AP4_BlocAtom
        +---------------------------------------------------------------------*/
        public BlocAtom() :
            base(AtomType.BLOC, Ap4.FULL_ATOM_HEADER_SIZE+256+256+512, 0, 0)
        {
            Array.Clear(_baseLocation, 0, 256+1);
            Array.Clear(_purchaseLocation, 0, 256+1);
            Array.Clear(_reserved, 0, 512);
        }

        /*----------------------------------------------------------------------
        |   AP4_BlocAtom::AP4_BlocAtom
        +---------------------------------------------------------------------*/
        private BlocAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.BLOC, size, version, flags)
        {
            _baseLocation[256] = 0;
            _purchaseLocation[256] = 0;
            stream.Read(_baseLocation, 256);
            stream.Read(_purchaseLocation, 256);
            stream.Read(_reserved, 512);
        }

        /*----------------------------------------------------------------------
        |   AP4_BlocAtom::SetBaseLocation
        +---------------------------------------------------------------------*/
        public void SetBaseLocation(string base_location)
        {
            byte[] base_location_bytes = Ap4Utils.StringToCStringBytes(base_location);
            uint len = (uint)base_location_bytes.Length;
            if (len > 256) len = 256;
            Array.Copy(base_location_bytes, 0, _baseLocation, 0, (int)len);
            Array.Clear(_baseLocation, (int)len, (int)(256-len+1));
        }

        /*----------------------------------------------------------------------
        |   AP4_BlocAtom::SetPurchaseLocation
        +---------------------------------------------------------------------*/
        public void SetPurchaseLocation(string purchase_location)
        {
            byte[] purchase_location_bytes = Ap4Utils.StringToCStringBytes(purchase_location);
            uint len = (uint)purchase_location_bytes.Length;
            if (len > 256) len = 256;
            Array.Copy(purchase_location_bytes, 0, _purchaseLocation, 0, (int)len);
            Array.Clear(_purchaseLocation, (int)len, (int)(256-len+1));
        }

        /*----------------------------------------------------------------------
        |   AP4_BlocAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            result = stream.Write(_baseLocation, 256);
            if (result.Failed()) return result;
            result = stream.Write(_purchaseLocation, 256);
            if (result.Failed()) return result;
            result = stream.Write(_reserved, 512);
            if (result.Failed()) return result;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_BlocAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("base_location", GetBaseLocation());
            inspector.AddField("purchase_location", GetPurchaseLocation());
            return Ap4Result.SUCCESS;
        }

        // accessors
        public string GetBaseLocation() { return Ap4Utils.StringFromCString(_baseLocation, 0, 256+1); }
        public string GetPurchaseLocation() { return Ap4Utils.StringFromCString(_purchaseLocation, 0, 256+1); }

        // members
        private byte[] _baseLocation = new byte[256+1];
        private byte[] _purchaseLocation = new byte[256+1];
        private byte[] _reserved = new byte[512];
    }
}
