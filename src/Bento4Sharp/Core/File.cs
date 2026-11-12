// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4File.h, Ap4File.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   file type/brands
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint FILE_BRAND_QT__ = 0x71742020; // 'qt  '
        public const uint FILE_BRAND_ISOM = 0x69736f6d; // 'isom'
        public const uint FILE_BRAND_ISO1 = 0x69736f31; // 'iso1'
        public const uint FILE_BRAND_ISO2 = 0x69736f32; // 'iso2'
        public const uint FILE_BRAND_ISO3 = 0x69736f33; // 'iso3'
        public const uint FILE_BRAND_ISO4 = 0x69736f34; // 'iso4'
        public const uint FILE_BRAND_ISO5 = 0x69736f35; // 'iso5'
        public const uint FILE_BRAND_ISO6 = 0x69736f36; // 'iso6'
        public const uint FILE_BRAND_MP41 = 0x6d703431; // 'mp41'
        public const uint FILE_BRAND_MP42 = 0x6d703432; // 'mp42'
        public const uint FILE_BRAND_3GP1 = 0x33677031; // '3gp1'
        public const uint FILE_BRAND_3GP2 = 0x33677032; // '3gp2'
        public const uint FILE_BRAND_3GP3 = 0x33677033; // '3gp3'
        public const uint FILE_BRAND_3GP4 = 0x33677034; // '3gp4'
        public const uint FILE_BRAND_3GP5 = 0x33677035; // '3gp5'
        public const uint FILE_BRAND_3G2A = 0x33673261; // '3g2a'
        public const uint FILE_BRAND_MMP4 = 0x6d6d7034; // 'mmp4'
        public const uint FILE_BRAND_M4A_ = 0x4d344120; // 'M4A '
        public const uint FILE_BRAND_M4P_ = 0x4d345020; // 'M4P '
        public const uint FILE_BRAND_MJP2 = 0x6d6a7032; // 'mjp2'
        public const uint FILE_BRAND_ODCF = 0x6f646366; // 'odcf'
        public const uint FILE_BRAND_OPF2 = 0x6f706632; // 'opf2'
        public const uint FILE_BRAND_AVC1 = 0x61766331; // 'avc1'
        public const uint FILE_BRAND_HVC1 = 0x68766331; // 'hvc1'
        public const uint FILE_BRAND_DBY1 = 0x64627931; // 'dby1'
        public const uint FILE_BRAND_DB1P = 0x64623170; // 'db1p'
        public const uint FILE_BRAND_DB2G = 0x64623267; // 'db2g'
        public const uint FILE_BRAND_DB4H = 0x64623468; // 'db4h'
        public const uint FILE_BRAND_DB4G = 0x64623467; // 'db4g'
    }

    /*----------------------------------------------------------------------
    |   AP4_File
    +---------------------------------------------------------------------*/

    /// <summary>
    /// The <see cref="File"/> object is the top level object for MP4 Files.
    /// </summary>
    public class File : AtomParent
    {
        // constructors and destructor

        /// <summary>
        /// Constructs an <see cref="File"/> from an <see cref="Movie"/> (used for writing)
        /// </summary>
        /// <param name="movie">the movie</param>
        public File(Movie movie = null)
        {
            _movie = movie;
            _fileType = null;
            _metaData = null;
            _moovIsBeforeMdat = true;
        }

        /// <summary>
        /// Constructs an <see cref="File"/> from a stream
        /// </summary>
        /// <param name="stream">the stream containing the data of the file</param>
        /// <param name="atom_factory">the atom factory that will be used to parse the stream</param>
        /// <param name="moov_only">indicates whether parsing of the atoms should stop
        /// when the moov atom is found or if all atoms should be parsed until the
        /// end of the file.</param>
        public File(ByteStream  stream,
                    AtomFactory atom_factory,
                    bool        moov_only)
        {
            _movie = null;
            _fileType = null;
            _metaData = null;
            _moovIsBeforeMdat = true;
            ParseStream(stream, atom_factory, moov_only);
        }

        /// <summary>
        /// Constructs an <see cref="File"/> from a stream using the default atom factory
        /// </summary>
        /// <param name="stream">the stream containing the data of the file</param>
        /// <param name="moov_only">indicates whether parsing of the atoms should stop
        /// when the moov atom is found or if all atoms should be parsed until the
        /// end of the file.</param>
        public File(ByteStream stream,
                    bool       moov_only = false)
        {
            _movie = null;
            _fileType = null;
            _metaData = null;
            _moovIsBeforeMdat = true;
            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
            ParseStream(stream, atom_factory, moov_only);
        }

        /// <summary>
        /// Get the top level atoms of the file
        /// </summary>
        public List<Atom> GetTopLevelAtoms() { return _children; }

        /// <summary>
        /// Get the <see cref="Movie"/> object of this file
        /// </summary>
        public Movie GetMovie() { return _movie; }

        /// <summary>
        /// Get the file type atom of this file
        /// </summary>
        public FtypAtom GetFileType() { return _fileType; }

        /// <summary>
        /// Ask whether the moov atom appears before the first mdat atom
        /// </summary>
        public bool IsMoovBeforeMdat() { return _moovIsBeforeMdat; }

        /*----------------------------------------------------------------------
        |   AP4_File::ParseStream
        +---------------------------------------------------------------------*/
        private void ParseStream(ByteStream  stream,
                                 AtomFactory atom_factory,
                                 bool        moov_only)
        {
            // parse top-level atoms
            Atom    atom = null;
            ulong   stream_position;
            bool    keep_parsing = true;
            while (keep_parsing &&
                   stream.Tell(out stream_position).Succeeded() &&
                   atom_factory.CreateAtomFromStream(stream, out atom).Succeeded()) {
                AddChild(atom);
                switch (atom.GetType()) {
                    case AtomType.MOOV:
                        _movie = new Movie(atom as MoovAtom, stream, false);
                        if (moov_only) keep_parsing = false;
                        break;

                    case AtomType.FTYP:
                        _fileType = atom as FtypAtom;
                        break;

                    case AtomType.MDAT:
                        // see if we are before the moov atom
                        if (_movie == null) _moovIsBeforeMdat = false;
                        break;
                }
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_File::Inspect
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Inspect the content of the file with an <see cref="AtomInspector"/>
        /// </summary>
        public virtual Ap4Result Inspect(AtomInspector inspector)
        {
            // dump the moov atom first
            if (_movie != null) _movie.Inspect(inspector);

            // dump the other atoms
            _children.Apply(new AtomListInspector(inspector));

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_File::SetFileType
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Set the file type. Will internally create an <c>AP4_Ftyp</c> atom
        /// and attach it to the file
        /// </summary>
        public Ap4Result SetFileType(uint    major_brand,
                                     uint    minor_version,
                                     uint[]  compatible_brands = null,
                                     uint    compatible_brand_count = 0)
        {
            if (_fileType != null) {
                RemoveChild(_fileType);
            }
            _fileType = new FtypAtom(major_brand,
                                     minor_version,
                                     compatible_brands,
                                     compatible_brand_count);
            AddChild(_fileType, 0);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_File::GetMetaData
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Get the file's metadata description
        /// </summary>
        public MetaData GetMetaData()
        {
            if (_metaData == null) {
                _metaData = new MetaData(this);
            }
            return _metaData;
        }

        // members
        private Movie     _movie;
        private FtypAtom  _fileType;
        private MetaData  _metaData;
        private bool      _moovIsBeforeMdat;
    }
}
