// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4FtypAtom.h, Ap4FtypAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint FTYP_BRAND_ISOM = 0x69736f6d; // 'isom'
        public const uint FTYP_BRAND_MP41 = 0x6d703431; // 'mp41'
        public const uint FTYP_BRAND_MP42 = 0x6d703432; // 'mp42'
        public const uint FTYP_BRAND_CCFF = 0x63636666; // 'ccff'
        public const uint FTYP_BRAND_ISML = 0x69736d6c; // 'isml'
        public const uint FTYP_BRAND_PIFF = 0x70696666; // 'piff'
        public const uint FTYP_BRAND_ISO2 = 0x69736f32; // 'iso2'
        public const uint FTYP_BRAND_MSDH = 0x6d736468; // 'msdh'
    }

    /*----------------------------------------------------------------------
    |   AP4_FtypAtom
    +---------------------------------------------------------------------*/
    public class FtypAtom : Atom
    {
        // class methods
        public static FtypAtom Create(uint size, ByteStream stream) {
            return new FtypAtom(size, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_FtypAtom::AP4_FtypAtom
        +---------------------------------------------------------------------*/
        private FtypAtom(uint size, ByteStream stream) :
            base(AtomType.FTYP, size)
        {
            _majorBrand = 0;
            _minorVersion = 0;
            if (size < Ap4.ATOM_HEADER_SIZE + 8) return;
            stream.ReadUI32(out _majorBrand);
            stream.ReadUI32(out _minorVersion);
            size -= 16;
            while (size >= 4) {
                uint compatible_brand;
                Ap4Result result = stream.ReadUI32(out compatible_brand);
                if (result.Failed()) return;
                _compatibleBrands.Add(compatible_brand);
                size -= 4;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_FtypAtom::AP4_FtypAtom
        +---------------------------------------------------------------------*/
        public FtypAtom(uint    major_brand,
                        uint    minor_version,
                        uint[]  compatible_brands = null,
                        uint    compatible_brand_count = 0) :
            base(AtomType.FTYP, Ap4.ATOM_HEADER_SIZE+8+4*compatible_brand_count)
        {
            _majorBrand = major_brand;
            _minorVersion = minor_version;
            // m_CompatibleBrands(compatible_brands, compatible_brand_count)
            for (uint i=0; i<compatible_brand_count; i++) {
                _compatibleBrands.Add(compatible_brands[i]);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_FtypAtom::HasCompatibleBrand
        +---------------------------------------------------------------------*/
        public bool HasCompatibleBrand(uint brand)
        {
            for (int i=0; i<_compatibleBrands.Count; i++) {
                if (_compatibleBrands[i] == brand) return true;
            }

            return false;
        }

        /*----------------------------------------------------------------------
        |   AP4_FtypAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // major brand
            result = stream.WriteUI32(_majorBrand);
            if (result.Failed()) return result;

            // minor version
            result = stream.WriteUI32(_minorVersion);
            if (result.Failed()) return result;

            // compatible brands
            uint compat_brand_count = (uint)_compatibleBrands.Count;
            for (uint i=0; i<compat_brand_count; i++) {
                result = stream.WriteUI32(_compatibleBrands[(int)i]);
                if (result.Failed()) return result;
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_FtypAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            string name = Ap4Utils.FormatFourChars(_majorBrand);
            inspector.AddField("major_brand", name);
            inspector.AddField("minor_version", _minorVersion, AtomInspector.FormatHint.HINT_HEX);

            // compatible brands
            for (int i=0; i<_compatibleBrands.Count; i++) {
                uint cb = _compatibleBrands[i];
                name = Ap4Utils.FormatFourChars(cb);
                inspector.AddField("compatible_brand", name);
            }

            return Ap4Result.SUCCESS;
        }

        // accessors
        public uint GetMajorBrand()         { return _majorBrand;       }
        public uint GetMinorVersion()       { return _minorVersion;     }
        public List<uint> GetCompatibleBrands() {
            return _compatibleBrands;
        }
        public void SetMajorBrandAndVersion(uint major_brand, uint minor_version) {
            _majorBrand   = major_brand;
            _minorVersion = minor_version;
        }

        // members
        private uint       _majorBrand;
        private uint       _minorVersion;
        private List<uint> _compatibleBrands = new List<uint>();
    }
}
