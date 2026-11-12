// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4MdhdAtom.h, Ap4MdhdAtom.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint MDHD_DEFAULT_GENERIC_TIMESCALE = 1000;
        public const uint MDHD_DEFAULT_VIDEO_TIMESCALE   = 90000;
    }

    /*----------------------------------------------------------------------
    |   AP4_MdhdAtom
    +---------------------------------------------------------------------*/
    public class MdhdAtom : Atom
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_MdhdAtom::Create
        +---------------------------------------------------------------------*/
        public static MdhdAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 1) return null;
            return new MdhdAtom(size, version, flags, stream);
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MdhdAtom::AP4_MdhdAtom
        +---------------------------------------------------------------------*/
        public MdhdAtom(ulong   creation_time,
                        ulong   modification_time,
                        uint    time_scale,
                        ulong   duration,
                        string  language) :
            base(AtomType.MDHD, Ap4.FULL_ATOM_HEADER_SIZE+20, 0, 0)
        {
            _creationTime = creation_time;
            _modificationTime = modification_time;
            _timeScale = time_scale;
            _duration = duration;

            if (language != null && Ap4Utils.StringLength(language) == 3) {
                // m_Language.Assign(language, 3)
                byte[] language_bytes = Ap4Utils.StringToCStringBytes(language);
                _language = Ap4Utils.StringFromBytes(language_bytes, 0, 3);
            } else {
                _language = "und";
            }
            if (duration          > 0xFFFFFFFFUL ||
                creation_time     > 0xFFFFFFFFUL ||
                modification_time > 0xFFFFFFFFUL) {
                _version = 1;
                _size32 += 12;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MdhdAtom::AP4_MdhdAtom
        +---------------------------------------------------------------------*/
        private MdhdAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.MDHD, size, version, flags)
        {
            if (_version == 0) {
                uint creation_time;
                stream.ReadUI32(out creation_time);
                _creationTime = creation_time;
                uint modification_time;
                stream.ReadUI32(out modification_time);
                _modificationTime = modification_time;
                stream.ReadUI32(out _timeScale);
                uint duration;
                stream.ReadUI32(out duration);
                _duration = duration;
            } else {
                stream.ReadUI64(out _creationTime);
                stream.ReadUI64(out _modificationTime);
                stream.ReadUI32(out _timeScale);
                stream.ReadUI64(out _duration);
            }

            byte[] lang = new byte[2];
            stream.Read(lang, 2);
            byte l0 = (byte)((lang[0]>>2)&0x1F);
            byte l1 = (byte)(((lang[0]&0x3)<<3) | ((lang[1]>>5)&0x7));
            byte l2 = (byte)((lang[1]&0x1F));
            if (lang[0] == 0x7F && lang[1] == 0xFF) {
                // undefined (Quicktime)
                _language = "und";
            } else if (l0 != 0 && l1 != 0 && l2 != 0) {
                byte[] lang_str = new byte[3] {(byte)(l0+0x60), (byte)(l1+0x60), (byte)(l2+0x60)};
                _language = Ap4Utils.StringFromBytes(lang_str, 0, 3);
            } else {
                _language = "```";
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MdhdAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            if (_version == 0) {
                result = stream.WriteUI32((uint)_creationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32((uint)_modificationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_timeScale);
                if (result.Failed()) return result;
                result = stream.WriteUI32((uint)_duration);
                if (result.Failed()) return result;
            } else {
                result = stream.WriteUI64(_creationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI64(_modificationTime);
                if (result.Failed()) return result;
                result = stream.WriteUI32(_timeScale);
                if (result.Failed()) return result;
                result = stream.WriteUI64(_duration);
                if (result.Failed()) return result;
            }

            // write the language
            if (Ap4Utils.StringLength(_language) == 3) {
                byte[] language = Ap4Utils.StringToCStringBytes(_language);
                byte l0 = (byte)(language[0]-0x60);
                byte l1 = (byte)(language[1]-0x60);
                byte l2 = (byte)(language[2]-0x60);
                result = stream.WriteUI08((byte)((l0<<2 | l1>>3)&0xFF));
                if (result.Failed()) return result;
                result = stream.WriteUI08((byte)((l1<<5 | l2)&0xFF));
                if (result.Failed()) return result;
            } else {
                result = stream.WriteUI16(0);
                if (result.Failed()) return result;
            }

            // pre-defined
            return stream.WriteUI16(0);
        }

        /*----------------------------------------------------------------------
        |   AP4_MdhdAtom::GetDurationMs
        +---------------------------------------------------------------------*/
        public uint GetDurationMs()
        {
            return Ap4Utils.DurationMsFromUnits(_duration, _timeScale);
        }

        public ulong  GetDuration()  { return _duration;  }
        public void   SetDuration(ulong duration) { _duration = duration; }
        public uint   GetTimeScale() { return _timeScale; }
        public void   SetTimeScale(uint timescale) { _timeScale = timescale; }
        public string GetLanguage()  { return _language;  }

        /*----------------------------------------------------------------------
        |   AP4_MdhdAtom::SetLanguage
        +---------------------------------------------------------------------*/
        public Ap4Result SetLanguage(string language)
        {
            if (Ap4Utils.StringLength(language) != 3) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }
            _language = language;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MdhdAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("timescale", _timeScale);
            inspector.AddField("duration", _duration);
            inspector.AddField("duration(ms)", GetDurationMs());
            inspector.AddField("language", _language);

            return Ap4Result.SUCCESS;
        }

        // members
        private ulong  _creationTime;
        private ulong  _modificationTime;
        private uint   _timeScale;
        private ulong  _duration;
        private string _language;
    }
}
