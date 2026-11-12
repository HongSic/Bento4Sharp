// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Marlin.h, Ap4Marlin.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const uint   MARLIN_BRAND_MGSV      = 0x4d475356; // 'MGSV'
        public const ushort MARLIN_IPMPS_TYPE_MGSV = 0xA551;
        public const uint   PROTECTION_SCHEME_TYPE_MARLIN_ACBC = 0x41434243; // 'ACBC'
        public const uint   PROTECTION_SCHEME_TYPE_MARLIN_ACGK = 0x4143474b; // 'ACGK'

        public const string MARLIN_IPMP_STYP_VIDEO = "urn:marlin:organization:sne:content-type:video";
        public const string MARLIN_IPMP_STYP_AUDIO = "urn:marlin:organization:sne:content-type:audio";

        public static readonly byte[] MARLIN_PSSH_SYSTEM_ID = {
            0x69, 0xF9, 0x08, 0xAF, 0x48, 0x16, 0x46, 0xEA, 0x91, 0x0C, 0xCD, 0x5D, 0xCC, 0xCB, 0x0A, 0x3A
        };
    }

    public static partial class AtomType
    {
        public const uint SATR = 0x73617472; // 'satr'
        public const uint STYP = 0x73747970; // 'styp'
        public const uint HMAC = 0x686d6163; // 'hmac'
        public const uint GKEY = 0x676b6579; // 'gkey'
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpAtomTypeHandler
    +---------------------------------------------------------------------*/
    // PORT NOTE: declared in Ap4Marlin.cpp only (not in the header), hence internal
    internal class MarlinIpmpAtomTypeHandler : AtomFactory.TypeHandler
    {
        // constructor
        public MarlinIpmpAtomTypeHandler(AtomFactory atom_factory) {
            _atomFactory = atom_factory;
        }

        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpAtomTypeHandler::CreateAtom
        +---------------------------------------------------------------------*/
        public override Ap4Result CreateAtom(uint       type,
                                             uint       size,
                                             ByteStream stream,
                                             uint       context,
                                             out Atom   atom)
        {
            switch (type) {
                case AtomType.SATR:
                    atom = ContainerAtom.Create(type, size, false, false, stream, _atomFactory);
                    break;

                case AtomType.STYP:
                    atom = new NullTerminatedStringAtom(type, size, stream);
                    break;

                default:
                    atom = null;
                    break;
            }

            return atom != null ? Ap4Result.SUCCESS : Ap4Result.FAILURE;
        }

        // members
        private AtomFactory _atomFactory;
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpAtomFactory
    +---------------------------------------------------------------------*/
    // PORT NOTE: declared in Ap4Marlin.cpp only (not in the header), hence internal
    internal class MarlinIpmpAtomFactory : DefaultAtomFactory
    {
        // class members
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpAtomFactory::Instance
        +---------------------------------------------------------------------*/
        public static new readonly MarlinIpmpAtomFactory Instance = new MarlinIpmpAtomFactory();

        // constructor
        public MarlinIpmpAtomFactory() {
            AddTypeHandler(new MarlinIpmpAtomTypeHandler(this));
        }
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpParser
    +---------------------------------------------------------------------*/
    public class MarlinIpmpParser
    {
        // types
        public class SinfEntry {
            public SinfEntry(uint track_id, ContainerAtom sinf) {
                _trackId = track_id;
                _sinf = sinf;
            }
            public uint           _trackId;
            public ContainerAtom  _sinf;
        }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpParser:Parse
        +---------------------------------------------------------------------*/
        public static Ap4Result Parse(AtomParent      top_level,
                                      ByteStream      stream,
                                      List<SinfEntry> sinf_entries,
                                      bool            remove_od_data=false)
        {
            // check the file type
            FtypAtom ftyp = top_level.GetChild(AtomType.FTYP) as FtypAtom;
            if (ftyp == null ||
                (ftyp.GetMajorBrand() != Ap4.MARLIN_BRAND_MGSV && !ftyp.HasCompatibleBrand(Ap4.MARLIN_BRAND_MGSV))) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // check the initial object descriptor and get the OD Track ID
            IodsAtom  iods = top_level.FindChild("moov/iods") as IodsAtom;
            uint      od_track_id = 0;
            if (iods == null) return Ap4Result.ERROR_INVALID_FORMAT;
            ObjectDescriptor  od = iods.GetObjectDescriptor();
            if (od == null) return Ap4Result.ERROR_INVALID_FORMAT;
            EsIdIncDescriptor es_id_inc = od.FindSubDescriptor(Ap4.DESCRIPTOR_TAG_ES_ID_INC) as EsIdIncDescriptor;
            if (es_id_inc == null) return Ap4Result.ERROR_INVALID_FORMAT;
            od_track_id = es_id_inc.GetTrackId();

            // find the track pointed to by the descriptor
            MoovAtom moov = top_level.GetChild(AtomType.MOOV) as MoovAtom;
            if (moov == null) return Ap4Result.ERROR_INVALID_FORMAT;
            TrakAtom od_trak = null;
            List<TrakAtom> trak_atoms = moov.GetTrakAtoms();
            for (int trak_item = 0; trak_item < trak_atoms.Count; trak_item++) {
                TrakAtom trak = trak_atoms[trak_item];
                if (trak != null) {
                    if (trak.GetId() == od_track_id) {
                        od_trak = trak;
                    } else {
                        sinf_entries.Add(new SinfEntry(trak.GetId(), null));
                    }
                }
            }

            // check that we have found the OD track
            if (od_trak == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // look for the 'mpod' trak references
            TrefTypeAtom track_references;
            track_references = od_trak.FindChild("tref/mpod") as TrefTypeAtom;
            if (track_references == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // create an AP4_Track object from the trak atom and check that it has samples
            Track od_track = new Track(od_trak, stream, 0);
            if (od_track.GetSampleCount() < 1) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // get the first sample (in this version, we only look at a single OD command)
            Sample od_sample = new Sample();
            Ap4Result result = od_track.GetSample(0, od_sample);
            if (result.Failed()) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // adapt the sample data into a byte stream for parsing
            DataBuffer sample_data = new DataBuffer();
            od_sample.ReadData(sample_data);
            MemoryByteStream sample_stream = new MemoryByteStream(sample_data);

            // look for one ObjectDescriptorUpdate command and
            // one IPMP_DescriptorUpdate command
            DescriptorUpdateCommand od_update = null;
            DescriptorUpdateCommand ipmp_update = null;
            do {
                Command  command = null;
                result = CommandFactory.CreateCommandFromStream(sample_stream, out command);
                if (result.Succeeded()) {
                    // found a command in the sample, check the type
                    switch (command.GetTag()) {
                      case Ap4.COMMAND_TAG_OBJECT_DESCRIPTOR_UPDATE:
                        if (od_update == null) {
                            od_update = command as DescriptorUpdateCommand;
                        }
                        break;

                      case Ap4.COMMAND_TAG_IPMP_DESCRIPTOR_UPDATE:
                        if (ipmp_update == null) {
                            ipmp_update = command as DescriptorUpdateCommand;
                        }
                        break;

                      default:
                        break;
                    }
                }
            } while (result.Succeeded());

            // check that we have what we need
            if (od_update == null || ipmp_update == null) {
                return Ap4Result.ERROR_INVALID_FORMAT;
            }

            // process all the object descriptors in the od update
            List<Descriptor> od_descriptors = od_update.GetDescriptors();
            for (int od_item = 0; od_item < od_descriptors.Count; od_item++) {
                od = od_descriptors[od_item] as ObjectDescriptor;
                if (od == null) continue;

                // find which track this od references
                EsIdRefDescriptor es_id_ref;
                es_id_ref = od.FindSubDescriptor(Ap4.DESCRIPTOR_TAG_ES_ID_REF) as EsIdRefDescriptor;
                if (es_id_ref == null ||
                    es_id_ref.GetRefIndex() > track_references.GetTrackIds().Count ||
                    es_id_ref.GetRefIndex() == 0) {
                    continue;
                }
                uint track_id = track_references.GetTrackIds()[es_id_ref.GetRefIndex()-1];
                SinfEntry sinf_entry = null;
                for (int sinf_entry_item = 0; sinf_entry_item < sinf_entries.Count; sinf_entry_item++) {
                    sinf_entry = sinf_entries[sinf_entry_item];
                    if (sinf_entry._trackId == track_id) {
                        break; // match
                    } else {
                        sinf_entry = null; // no match
                    }
                }
                if (sinf_entry == null) continue; // no matching entry
                if (sinf_entry._sinf != null) continue; // entry already populated

                // see what ipmp descriptor this od points to
                IpmpDescriptorPointer ipmpd_pointer;
                ipmpd_pointer = od.FindSubDescriptor(Ap4.DESCRIPTOR_TAG_IPMP_DESCRIPTOR_POINTER) as IpmpDescriptorPointer;
                if (ipmpd_pointer == null) continue; // no pointer

                // find the ipmp descriptor referenced by the pointer
                IpmpDescriptor ipmpd = null;
                List<Descriptor> ipmp_descriptors = ipmp_update.GetDescriptors();
                for (int ipmpd_item = 0; ipmpd_item < ipmp_descriptors.Count; ipmpd_item++) {
                    // check that this descriptor is of the right type
                    ipmpd = ipmp_descriptors[ipmpd_item] as IpmpDescriptor;
                    if (ipmpd == null || ipmpd.GetIpmpsType() != Ap4.MARLIN_IPMPS_TYPE_MGSV) continue;

                    // check the descriptor id
                    if (ipmpd.GetDescriptorId() == ipmpd_pointer.GetDescriptorId()) {
                        break; // match
                    } else {
                        ipmpd = null; // no match
                    }
                }
                if (ipmpd == null) continue; // no matching entry

                // parse the ipmp data into one or more 'sinf' atoms, and keep the one with the
                // right type
                MemoryByteStream data = new MemoryByteStream(ipmpd.GetData().GetData(),
                                                             ipmpd.GetData().GetDataSize());
                ulong bytes_available = ipmpd.GetData().GetDataSize();
                do {
                    Atom atom = null;

                    // setup the factory with a context so we can instantiate a 'schm'
                    // atom with a slightly different format than the standard 'schm'
                    AtomFactory factory = MarlinIpmpAtomFactory.Instance;
                    factory.PushContext(0x6d726c6e /* 'mrln' */);

                    // parse the next atom in the stream
                    result = factory.CreateAtomFromStream(data, ref bytes_available, out atom);
                    factory.PopContext();
                    if (result.Failed() || atom == null) break;

                    // check that what we have parsed is indeed an 'sinf' of the right type
                    if (atom.GetType() == AtomType.SINF) {
                        ContainerAtom sinf = atom as ContainerAtom;
                        SchmAtom schm = sinf.FindChild("schm") as SchmAtom;
                        // PORT NOTE: upstream dereferences schm without a NULL check (a
                        // crash when the 'sinf' has no 'schm'); such a 'sinf' is skipped here.
                        if (schm != null &&
                            ((schm.GetSchemeType()    == Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACBC &&
                              schm.GetSchemeVersion() == 0x0100) ||
                             (schm.GetSchemeType()    == Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACGK &&
                              schm.GetSchemeVersion() == 0x0100))) {
                            // store the sinf in the entry for that track
                            sinf_entry._sinf = sinf;
                            break;
                        }
                    }
                } while (result.Succeeded());
            }

            // get rid of entries that have no SINF
            {
                int sinf_entry_item = 0;
                while (sinf_entry_item < sinf_entries.Count) {
                    SinfEntry sinf_entry = sinf_entries[sinf_entry_item];
                    if (sinf_entry._sinf == null) {
                        sinf_entries.RemoveAt(sinf_entry_item);
                    } else {
                        sinf_entry_item++;
                    }
                }
            }

            // remove the iods atom and the OD track if required
            if (remove_od_data) {
                od_trak.Detach();
                iods.Detach();
            }

            return Ap4Result.SUCCESS;
        }

        private MarlinIpmpParser() {} // this class can't be instantiated
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpSampleDecrypter
    +---------------------------------------------------------------------*/
    public class MarlinIpmpSampleDecrypter : SampleDecrypter
    {
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpSampleDecrypter::Create
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a sample decrypter given the top-level atoms, the track ID, and the key
        /// </summary>
        public static Ap4Result Create(AtomParent                     top_level,
                                       byte[]                         key,
                                       uint                           key_size,
                                       BlockCipherFactory             block_cipher_factory,
                                       out MarlinIpmpSampleDecrypter  sample_decrypter)
        {
            // TODO: need to parse group key info
            return Create(key, key_size, block_cipher_factory, out sample_decrypter);
        }

        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpSampleDecrypter::Create
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a sample decrypter given the key (can't be used with group keys, since the group
        /// key info needs to be parsed from the top level atoms)
        /// </summary>
        public static Ap4Result Create(byte[]                         key,
                                       uint                           key_size,
                                       BlockCipherFactory             block_cipher_factory,
                                       out MarlinIpmpSampleDecrypter  sample_decrypter)
        {
            // default value
            sample_decrypter = null;
            if (block_cipher_factory == null) block_cipher_factory = DefaultBlockCipherFactory.Instance;

            // create a block cipher for the decrypter
            BlockCipher block_cipher = null;
            Ap4Result result = block_cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                                 BlockCipher.CipherDirection.DECRYPT,
                                                                 BlockCipher.CipherMode.CBC,
                                                                 null,
                                                                 key,
                                                                 key_size,
                                                                 out block_cipher);
            if (result.Failed()) return result;

            // create a CBC cipher
            CbcStreamCipher cbc_cipher = new CbcStreamCipher(block_cipher);
            sample_decrypter = new MarlinIpmpSampleDecrypter(cbc_cipher);

            return Ap4Result.SUCCESS;
        }

        // AP4_SampleDecrypter methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpSampleDecrypter::GetDecryptedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetDecryptedSampleSize(Sample sample)
        {
            // with CBC, we need to decrypt the last block to know what the padding was
            uint       encrypted_size = sample.GetSize()-Ap4.AES_BLOCK_SIZE;
            DataBuffer encrypted = new DataBuffer();
            DataBuffer decrypted = new DataBuffer();
            uint       decrypted_size = Ap4.CIPHER_BLOCK_SIZE;
            if (sample.GetSize() < 2*Ap4.CIPHER_BLOCK_SIZE) {
                return 0;
            }
            uint offset = sample.GetSize()-2*Ap4.CIPHER_BLOCK_SIZE;
            if (sample.ReadData(encrypted, 2*Ap4.CIPHER_BLOCK_SIZE, offset).Failed()) {
                return 0;
            }
            decrypted.Reserve(decrypted_size);
            _cipher.SetIV(encrypted.GetData());
            if (_cipher.ProcessBuffer(encrypted.GetData(), (int)Ap4.CIPHER_BLOCK_SIZE,
                                      Ap4.CIPHER_BLOCK_SIZE,
                                      decrypted.UseData(), 0,
                                      ref decrypted_size,
                                      true).Failed()) {
                return 0;
            }
            uint padding_size = Ap4.CIPHER_BLOCK_SIZE-decrypted_size;
            return encrypted_size-padding_size;
        }

        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpSampleDecrypter::DecryptSampleData
        +---------------------------------------------------------------------*/
        public override Ap4Result DecryptSampleData(DataBuffer data_in,
                                                    DataBuffer data_out,
                                                    byte[]     iv = null)
        {
            Ap4Result result;

            byte[] @in = data_in.GetData();
            uint   in_size = data_in.GetDataSize();

            // default to 0 output
            data_out.SetDataSize(0);

            // check that we have at least the minimum size
            if (in_size < 2*Ap4.AES_BLOCK_SIZE) return Ap4Result.ERROR_INVALID_FORMAT;

            // process the sample data
            uint out_size = in_size-Ap4.AES_BLOCK_SIZE; // worst case
            data_out.SetDataSize(out_size);
            byte[] @out = data_out.UseData();

            // decrypt the data
            _cipher.SetIV(@in);
            result = _cipher.ProcessBuffer(@in, (int)Ap4.AES_BLOCK_SIZE,
                                           in_size-Ap4.AES_BLOCK_SIZE,
                                           @out, 0,
                                           ref out_size,
                                           true);
            if (result.Failed()) return result;

            // update the payload size
            data_out.SetDataSize(out_size);

            return Ap4Result.SUCCESS;
        }

        private MarlinIpmpSampleDecrypter(StreamCipher cipher) { _cipher = cipher; }

        private StreamCipher _cipher;
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpDecryptingProcessor
    +---------------------------------------------------------------------*/
    public class MarlinIpmpDecryptingProcessor : Processor
    {
        // constructor and destructor
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpDecryptingProcessor:AP4_MarlinIpmpDecryptingProcessor
        +---------------------------------------------------------------------*/
        public MarlinIpmpDecryptingProcessor(
            ProtectionKeyMap    key_map              = null,
            BlockCipherFactory  block_cipher_factory = null) {
            if (key_map != null) {
                // copy the keys
                _keyMap.SetKeys(key_map);
            }

            if (block_cipher_factory == null) {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            } else {
                _blockCipherFactory = block_cipher_factory;
            }
        }

        // accessors
        public ProtectionKeyMap GetKeyMap() { return _keyMap; }

        // methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpDecryptingProcessor:Initialize
        +---------------------------------------------------------------------*/
        public override Ap4Result Initialize(AtomParent                  top_level,
                                             ByteStream                  stream,
                                             Processor.ProgressListener  listener = null)
        {
            Ap4Result result = MarlinIpmpParser.Parse(top_level, stream, _sinfEntries, true);
            if (result.Failed()) return result;

            // update the file type
            FtypAtom  ftyp = top_level.GetChild(AtomType.FTYP) as FtypAtom;
            if (ftyp != null) {
                ftyp.SetMajorBrandAndVersion(Ap4.FTYP_BRAND_MP42, 1);
                for (int i=0; i<ftyp.GetCompatibleBrands().Count; i++) {
                    if (ftyp.GetCompatibleBrands()[i] == Ap4.MARLIN_BRAND_MGSV) {
                        ftyp.GetCompatibleBrands()[i] = Ap4.FTYP_BRAND_MP42;
                    }
                }
            }

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpDecryptingProcessor:CreateTrackHandler
        +---------------------------------------------------------------------*/
        public override Processor.TrackHandler CreateTrackHandler(TrakAtom trak_)
        {
            TrakAtom trak = trak_; // PORT NOTE: upstream dereferences trak unconditionally

            // look for this track in the list of entries
            MarlinIpmpParser.SinfEntry sinf_entry = null;
            for (int sinf_entry_item = 0; sinf_entry_item < _sinfEntries.Count; sinf_entry_item++) {
                sinf_entry = _sinfEntries[sinf_entry_item];
                if (sinf_entry._trackId == trak.GetId()) {
                    break; // match
                } else {
                    sinf_entry = null; // no match
                }
            }
            if (sinf_entry == null) return null; // no matching entry
            ContainerAtom sinf = sinf_entry._sinf;

            // check the scheme
            bool use_group_key;
            SchmAtom schm = sinf.GetChild(AtomType.SCHM) as SchmAtom;
            if (schm == null) return null; // no schm
            if (schm.GetSchemeType()    == Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACBC &&
                schm.GetSchemeVersion() == 0x0100) {
                use_group_key = false;
            } else if (schm.GetSchemeType()    == Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACGK &&
                       schm.GetSchemeVersion() == 0x0100) {
                use_group_key = true;
            } else {
                // unsupported scheme
                return null;
            }

            // find the key
            DataBuffer  key = null;
            DataBuffer  unwrapped_key = new DataBuffer();
            if (use_group_key) {
                DataBuffer group_key = _keyMap.GetKey(0);
                if (group_key == null) return null; // no group key
                ContainerAtom schi = sinf.GetChild(AtomType.SCHI) as ContainerAtom;
                if (schi == null) return null; // no schi
                Atom gkey = schi.GetChild(AtomType.GKEY);
                if (gkey == null) return null; // no gkey
                MemoryByteStream gkey_data = new MemoryByteStream();
                gkey.WriteFields(gkey_data);
                Ap4.AesKeyUnwrap(group_key.GetData(), gkey_data.GetData(), gkey_data.GetDataSize(), unwrapped_key);
                key = unwrapped_key;
            } else {
                key = _keyMap.GetKey(sinf_entry._trackId);
            }
            if (key == null) return null;

            // create the decrypter
            MarlinIpmpTrackDecrypter decrypter = null;
            Ap4Result result = MarlinIpmpTrackDecrypter.Create(_blockCipherFactory,
                                                               key.GetData(),
                                                               key.GetDataSize(),
                                                               out decrypter);
            if (result.Failed()) return null;

            return decrypter;
        }

        // members
        private BlockCipherFactory               _blockCipherFactory;
        private ProtectionKeyMap                 _keyMap = new ProtectionKeyMap();
        private List<MarlinIpmpParser.SinfEntry> _sinfEntries = new List<MarlinIpmpParser.SinfEntry>();
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpTrackDecrypter
    +---------------------------------------------------------------------*/
    public class MarlinIpmpTrackDecrypter : Processor.TrackHandler
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpTrackDecrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(BlockCipherFactory            cipher_factory,
                                       byte[]                        key,
                                       uint                          key_size,
                                       out MarlinIpmpTrackDecrypter  decrypter)
        {
            decrypter = null;

            // create a sample decrypter
            MarlinIpmpSampleDecrypter sample_decrypter = null;
            Ap4Result result = MarlinIpmpSampleDecrypter.Create(key, key_size, cipher_factory, out sample_decrypter);
            if (result.Failed()) return result;

            // create the track decrypter
            decrypter = new MarlinIpmpTrackDecrypter(sample_decrypter);

            return Ap4Result.SUCCESS;
        }

        // constructor and destructor
        public MarlinIpmpTrackDecrypter() { _sampleDecrypter = null; }

        // AP4_Processor::TrackHandler methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpTrackDecrypter:GetProcessedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetProcessedSampleSize(Sample sample)
        {
            return _sampleDecrypter.GetDecryptedSampleSize(sample);
        }

        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpTrackDecrypter::ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            return _sampleDecrypter.DecryptSampleData(data_in, data_out);
        }

        // constructor
        private MarlinIpmpTrackDecrypter(SampleDecrypter sample_decrypter) {
            _sampleDecrypter = sample_decrypter;
        }

        // members
        private SampleDecrypter _sampleDecrypter;
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpEncryptingProcessor
    +---------------------------------------------------------------------*/
    public class MarlinIpmpEncryptingProcessor : Processor
    {
        // constructor
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpEncryptingProcessor::AP4_MarlinIpmpEncryptingProcessor
        +---------------------------------------------------------------------*/
        public MarlinIpmpEncryptingProcessor(
            bool                use_group_key        = false,
            ProtectionKeyMap    key_map              = null,
            BlockCipherFactory  block_cipher_factory = null)
        {
            _useGroupKey = use_group_key;

            if (key_map != null) {
                // copy the keys
                _keyMap.SetKeys(key_map);
            }

            if (block_cipher_factory == null) {
                _blockCipherFactory = DefaultBlockCipherFactory.Instance;
            } else {
                _blockCipherFactory = block_cipher_factory;
            }
        }

        // accessors
        public ProtectionKeyMap GetKeyMap()      { return _keyMap;      }
        public TrackPropertyMap GetPropertyMap() { return _propertyMap; }

        // AP4_Processor methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpEncryptingProcessor::Initialize
        +---------------------------------------------------------------------*/
        public override Ap4Result Initialize(
            AtomParent                  top_level,
            ByteStream                  stream,
            Processor.ProgressListener  listener = null)
        {
            // get the moov atom
            MoovAtom moov = top_level.GetChild(AtomType.MOOV) as MoovAtom;
            if (moov == null) return Ap4Result.ERROR_INVALID_FORMAT;

            // deal with the file type
            FtypAtom ftyp = top_level.GetChild(AtomType.FTYP) as FtypAtom;
            if (ftyp != null) {
                // remove the atom, it will be replaced with a new one
                top_level.RemoveChild(ftyp);

                // keep the existing brand and compatible brands
                List<uint> compatible_brands = new List<uint>();
                compatible_brands.EnsureCapacity(ftyp.GetCompatibleBrands().Count+1);
                for (int i=0; i<ftyp.GetCompatibleBrands().Count; i++) {
                    compatible_brands.Add(ftyp.GetCompatibleBrands()[i]);
                }

                // add the MGSV compatible brand if it is not already there
                if (!ftyp.HasCompatibleBrand(Ap4.MARLIN_BRAND_MGSV)) {
                    compatible_brands.Add(Ap4.MARLIN_BRAND_MGSV);
                }

                // create a replacement for the major brand
                FtypAtom new_ftyp = new FtypAtom(Ap4.MARLIN_BRAND_MGSV,
                                                 0x13c078c, //AP4_MARLIN_BRAND_MGSV_MAJOR_VERSION,
                                                 compatible_brands.ToArray(),
                                                 (uint)compatible_brands.Count);
                ftyp = new_ftyp;
            } else {
                uint isom = Ap4.FTYP_BRAND_ISOM;
                ftyp = new FtypAtom(Ap4.MARLIN_BRAND_MGSV, 0, new uint[] { isom }, 1);
            }

            // insert the ftyp atom as the first child
            top_level.AddChild(ftyp, 0);

            // create and 'mpod' track reference atom
            TrefTypeAtom mpod = new TrefTypeAtom(AtomType.MPOD);

            // look for an available track ID, starting at 1
            uint od_track_id       = 0;
            uint od_track_position = 0;
            List<TrakAtom> trak_atoms = moov.GetTrakAtoms();
            for (int trak_item = 0; trak_item < trak_atoms.Count; trak_item++) {
                TrakAtom trak = trak_atoms[trak_item];
                if (trak != null) {
                    od_track_position++;
                    if (trak.GetId() >= od_track_id) {
                        od_track_id = trak.GetId()+1;
                    }

                    // if the track is encrypted, reference it in the mpod
                    if (_keyMap.GetKey(trak.GetId()) != null) {
                        mpod.AddTrackId(trak.GetId());
                    }

                    //m_SinfEntries.Add(new SinfEntry(trak->GetId(), NULL));
                }
            }

            // check that there was at least one track in the file
            if (od_track_id == 0) return Ap4Result.ERROR_INVALID_FORMAT;

            // create an initial object descriptor
            InitialObjectDescriptor iod =
                // TODO: get real values from the property map
                new InitialObjectDescriptor(Ap4.DESCRIPTOR_TAG_MP4_IOD,
                                            1022, // object descriptor id
                                            false,
                                            0xFE,    // OD profile level (0xFE = No OD profile specified)
                                            0xFF,    // scene profile level
                                            0xFE,    // audio profile level
                                            0xFE,    // visual profile level
                                            0xFF);   // graphics profile

            // create an ES_ID_Inc subdescriptor and add it to the initial object descriptor
            EsIdIncDescriptor es_id_inc = new EsIdIncDescriptor(od_track_id);
            iod.AddSubDescriptor(es_id_inc);

            // create an iods atom to hold the initial object descriptor
            IodsAtom iods = new IodsAtom(iod);

            // add the iods atom to the moov atom (try to put it just after mvhd)
            int iods_position = 0;
            int item_position = 0;
            List<Atom> moov_children = moov.GetChildren();
            for (int moov_item = 0; moov_item < moov_children.Count; moov_item++) {
                 ++item_position;
                 if (moov_children[moov_item].GetType() == AtomType.MVHD) {
                    iods_position = item_position;
                    break;
                 }
            }
            Ap4Result result = moov.AddChild(iods, iods_position);
            if (result.Failed()) {
                return result;
            }

            // create a sample table for the OD track
            SyntheticSampleTable od_sample_table = new SyntheticSampleTable();

            // create the sample description for the OD track
            MpegSystemSampleDescription od_sample_description;
            od_sample_description = new MpegSystemSampleDescription(Ap4.STREAM_TYPE_OD,
                                                                    Ap4.OTI_MPEG4_SYSTEM,
                                                                    null,
                                                                    32768, // buffer size
                                                                    1024,  // max bitrate
                                                                    512);  // avg bitrate
            od_sample_table.AddSampleDescription(od_sample_description, true);

            // create the OD descriptor update
            DescriptorUpdateCommand od_update = new DescriptorUpdateCommand(Ap4.COMMAND_TAG_OBJECT_DESCRIPTOR_UPDATE);
            for (uint i=0; i<mpod.GetTrackIds().Count; i++) {
                ObjectDescriptor od = new ObjectDescriptor(Ap4.DESCRIPTOR_TAG_MP4_OD, (ushort)(256+i)); // descriptor id = 256+i
                od.AddSubDescriptor(new EsIdRefDescriptor((ushort)(i+1)));     // index into mpod (1-based)
                od.AddSubDescriptor(new IpmpDescriptorPointer((byte)(i+1))); // descriptor id = i+1
                od_update.AddDescriptor(od);
            }

            // create the IPMP descriptor update
            DescriptorUpdateCommand ipmp_update = new DescriptorUpdateCommand(Ap4.COMMAND_TAG_IPMP_DESCRIPTOR_UPDATE);
            for (uint i=0; i<mpod.GetTrackIds().Count; i++) {
                // create the ipmp descriptor
                IpmpDescriptor ipmp_descriptor = new IpmpDescriptor((byte)(i+1), Ap4.MARLIN_IPMPS_TYPE_MGSV);

                // create the sinf container
                ContainerAtom sinf = new ContainerAtom(AtomType.SINF);

                // add the scheme type atom
                sinf.AddChild(new SchmAtom(_useGroupKey?
                                           Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACGK:
                                           Ap4.PROTECTION_SCHEME_TYPE_MARLIN_ACBC,
                                           0x0100, null, true));

                // create the 'schi' container
                ContainerAtom schi = new ContainerAtom(AtomType.SCHI);

                // add the content ID
                string content_id = _propertyMap.GetProperty(mpod.GetTrackIds()[(int)i], "ContentId");
                if (content_id != null) {
                    // add the content ID (8id_)
                    schi.AddChild(new NullTerminatedStringAtom(AtomType._8ID_, content_id));
                }

                // find what the track type is (necessary for the next step) and the key
                DataBuffer key = null;
                Track.Type track_type = Track.Type.TYPE_UNKNOWN;
                for (int trak_item = 0; trak_item < trak_atoms.Count; trak_item++) {
                    TrakAtom trak = trak_atoms[trak_item];
                    if (trak.GetId() == mpod.GetTrackIds()[(int)i]) {
                        // find the handler type
                        Atom sub = trak.FindChild("mdia/hdlr");
                        if (sub != null) {
                            HdlrAtom hdlr = sub as HdlrAtom;
                            if (hdlr != null) {
                                uint type = hdlr.GetHandlerType();
                                if (type == Ap4.HANDLER_TYPE_SOUN) {
                                    track_type = Track.Type.TYPE_AUDIO;
                                } else if (type == Ap4.HANDLER_TYPE_VIDE) {
                                    track_type = Track.Type.TYPE_VIDEO;
                                }
                            }
                        }

                        // find the key
                        key = _keyMap.GetKey(trak.GetId());
                        break;
                    }
                }

                // group key
                if (_useGroupKey && key != null) {
                    // find the group key
                    DataBuffer  group_key = _keyMap.GetKey(0);
                    if (group_key != null) {
                        DataBuffer wrapped_key = new DataBuffer();
                        result = Ap4.AesKeyWrap(group_key.GetData(), key.GetData(), key.GetDataSize(), wrapped_key);
                        if (result.Succeeded()) {
                            UnknownAtom gkey = new UnknownAtom(AtomType.GKEY,
                                                               wrapped_key.GetData(),
                                                               wrapped_key.GetDataSize());
                            schi.AddChild(gkey);
                        }
                    }
                }

                // create and add the security attributes (satr)
                if (track_type != Track.Type.TYPE_UNKNOWN && key != null && key != null) {
                    ContainerAtom satr = new ContainerAtom(AtomType.SATR);
                    switch (track_type) {
                        case Track.Type.TYPE_AUDIO:
                            satr.AddChild(new NullTerminatedStringAtom(AtomType.STYP, Ap4.MARLIN_IPMP_STYP_AUDIO));
                            break;
                        case Track.Type.TYPE_VIDEO:
                            satr.AddChild(new NullTerminatedStringAtom(AtomType.STYP, Ap4.MARLIN_IPMP_STYP_VIDEO));
                            break;
                        default:
                            break;
                    }

                    // add the signed attributes, if any
                    string signed_attributes = _propertyMap.GetProperty(mpod.GetTrackIds()[(int)i], "SignedAttributes");
                    if (signed_attributes != null) {
                        // decode the hex-encoded data
                        uint size = (uint)Ap4Utils.StringLength(signed_attributes)/2;
                        DataBuffer attributes_atoms = new DataBuffer();
                        attributes_atoms.SetDataSize(size);
                        if (Ap4Utils.ParseHex(signed_attributes, attributes_atoms.UseData(), size).Succeeded()) {
                            // parse all the atoms encoded in the data and add them to the 'schi' container
                            MemoryByteStream mbs_ = new MemoryByteStream(attributes_atoms.GetData(),
                                                                         attributes_atoms.GetDataSize());
                            DefaultAtomFactory atom_factory = new DefaultAtomFactory();
                            do {
                                Atom atom = null;
                                result = atom_factory.CreateAtomFromStream(mbs_, out atom);
                                if (result.Succeeded() && atom != null) {
                                    satr.AddChild(atom);
                                }
                            } while (result.Succeeded());
                        }
                    }

                    // compute the hmac
                    MemoryByteStream mbs = new MemoryByteStream();
                    satr.Write(mbs);
                    Hmac digester = null;
                    Hmac.Create(Hmac.Algorithm.SHA256, key.GetData(), key.GetDataSize(), out digester);
                    digester.Update(mbs.GetData(), mbs.GetDataSize());
                    DataBuffer hmac_value = new DataBuffer();
                    digester.Final(hmac_value);
                    Atom hmac = new UnknownAtom(AtomType.HMAC, hmac_value.GetData(), hmac_value.GetDataSize());

                    schi.AddChild(satr);
                    schi.AddChild(hmac);
                }

                sinf.AddChild(schi);

                // serialize the sinf atom to a buffer and set it as the ipmp data
                MemoryByteStream sinf_data = new MemoryByteStream((uint)sinf.GetSize());
                sinf.Write(sinf_data);
                ipmp_descriptor.SetData(sinf_data.GetData(), sinf_data.GetDataSize());

                ipmp_update.AddDescriptor(ipmp_descriptor);
            }

            // add the sample with the descriptors and updates
            MemoryByteStream sample_data = new MemoryByteStream();
            od_update.Write(sample_data);
            ipmp_update.Write(sample_data);
            od_sample_table.AddSample(sample_data, 0, sample_data.GetDataSize(), 0, 0, 0, 0, true);

            // create the OD track
            TrakAtom od_track = new TrakAtom(od_sample_table,
                                             Ap4.HANDLER_TYPE_ODSM,
                                             "Bento4 Marlin OD Handler",
                                             od_track_id,
                                             0, 0,
                                             1, 1000, 1, 0, "und",
                                             0, 0);

            // add an entry in the processor's stream table to indicate that the
            // media data for the OD track is not in the file stream, but in our
            // memory stream.
            _externalTrackData.Add(new ExternalTrackData(od_track_id, sample_data));

            // add a tref track reference atom
            ContainerAtom tref = new ContainerAtom(AtomType.TREF);
            tref.AddChild(mpod);
            od_track.AddChild(tref, 1); // add after 'tkhd'

            // add the track to the moov atoms (just after the last track)
            moov.AddChild(od_track, (int)od_track_position);

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpEncryptingProcessor::CreateTrackHandler
        +---------------------------------------------------------------------*/
        public override Processor.TrackHandler CreateTrackHandler(TrakAtom trak_)
        {
            TrakAtom trak = trak_; // PORT NOTE: upstream dereferences trak unconditionally

            // create a handler for this track if we have a key for it
            DataBuffer  key;
            DataBuffer iv;
            if (_keyMap.GetKeyAndIv(trak.GetId(), out key, out iv).Succeeded()) {
                // create the track handler
                MarlinIpmpTrackEncrypter handler = null;
                Ap4Result result = MarlinIpmpTrackEncrypter.Create(_blockCipherFactory,
                                                                   key.GetData(),
                                                                   key.GetDataSize(),
                                                                   iv.GetData(),
                                                                   iv.GetDataSize(),
                                                                   out handler);
                if (result.Failed()) return null;
                return handler;
            }

            // not encrypted
            return null;
        }

        // members
        private BlockCipherFactory _blockCipherFactory;
        private bool               _useGroupKey;
        private ProtectionKeyMap   _keyMap = new ProtectionKeyMap();
        private TrackPropertyMap   _propertyMap = new TrackPropertyMap();
    }

    /*----------------------------------------------------------------------
    |   AP4_MarlinIpmpTrackEncrypter
    +---------------------------------------------------------------------*/
    public class MarlinIpmpTrackEncrypter : Processor.TrackHandler
    {
        // class methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpTrackEncrypter::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(BlockCipherFactory            cipher_factory,
                                       byte[]                        key,
                                       uint                          key_size,
                                       byte[]                        iv,
                                       uint                          iv_size,
                                       out MarlinIpmpTrackEncrypter  encrypter)
        {
            // default value
            encrypter = null;

            // check args
            if (iv != null && iv_size != 16) {
                return Ap4Result.ERROR_INVALID_PARAMETERS;
            }

            // create a block cipher
            BlockCipher block_cipher = null;
            Ap4Result result = cipher_factory.CreateCipher(BlockCipher.CipherType.AES_128,
                                                           BlockCipher.CipherDirection.ENCRYPT,
                                                           BlockCipher.CipherMode.CBC,
                                                           null,
                                                           key,
                                                           key_size,
                                                           out block_cipher);
            if (result.Failed()) return result;

            // create a CBC cipher
            CbcStreamCipher cbc_cipher = new CbcStreamCipher(block_cipher);

            // create the track encrypter
            encrypter = new MarlinIpmpTrackEncrypter(cbc_cipher, iv);

            return Ap4Result.SUCCESS;
        }

        // AP4_Processor::TrackHandler methods
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpTrackEncrypter:GetProcessedSampleSize
        +---------------------------------------------------------------------*/
        public override uint GetProcessedSampleSize(Sample sample)
        {
            return Ap4.CIPHER_BLOCK_SIZE*(2+(sample.GetSize()/Ap4.CIPHER_BLOCK_SIZE));
        }

        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpTrackEncrypter:ProcessSample
        +---------------------------------------------------------------------*/
        public override Ap4Result ProcessSample(DataBuffer data_in,
                                                DataBuffer data_out)
        {
            Ap4Result result;

            byte[] @in = data_in.GetData();
            uint   in_size = data_in.GetDataSize();

            // default to 0 output
            data_out.SetDataSize(0);

            // process the sample data
            uint out_size = Ap4.CIPHER_BLOCK_SIZE*(2+(in_size/Ap4.CIPHER_BLOCK_SIZE));
            data_out.SetDataSize(out_size);
            byte[] @out = data_out.UseData();

            // write the IV
            Array.Copy(_iv, 0, @out, 0, (int)Ap4.CIPHER_BLOCK_SIZE);
            out_size -= Ap4.CIPHER_BLOCK_SIZE;

            // encrypt the data
            _cipher.SetIV(_iv);
            result = _cipher.ProcessBuffer(@in, 0,
                                           in_size,
                                           @out, (int)Ap4.AES_BLOCK_SIZE,
                                           ref out_size,
                                           true);
            if (result.Failed()) return result;

            // update the payload size
            data_out.SetDataSize(out_size+Ap4.AES_BLOCK_SIZE);

            return Ap4Result.SUCCESS;
        }

        // constructor
        /*----------------------------------------------------------------------
        |   AP4_MarlinIpmpTrackEncrypter::AP4_MarlinIpmpTrackEncrypter
        +---------------------------------------------------------------------*/
        private MarlinIpmpTrackEncrypter(StreamCipher cipher,
                                         byte[]       iv)
        {
            _cipher = cipher;

            // copy the IV
            // PORT NOTE: upstream copies from `iv` even when it is NULL (which
            // Create allows): undefined behaviour there, an all-zero IV here.
            if (iv != null) {
                Array.Copy(iv, 0, _iv, 0, (int)Ap4.AES_BLOCK_SIZE);
            }
        }

        // members
        private byte[]       _iv = new byte[16];
        private StreamCipher _cipher;
    }

    /*----------------------------------------------------------------------
    |   AP4_MkidAtom
    +---------------------------------------------------------------------*/
    public class MkidAtom : Atom
    {
        // types
        public class Entry {
            public byte[] _kid = new byte[16];
            public string _contentId = "";
        }

        // virtual constructor
        /*----------------------------------------------------------------------
        |   AP4_MkidAtom::Create
        +---------------------------------------------------------------------*/
        public static MkidAtom Create(uint size, ByteStream stream)
        {
            byte version;
            uint flags;
            if (size < Ap4.FULL_ATOM_HEADER_SIZE) return null;
            if (Atom.ReadFullHeader(stream, out version, out flags).Failed()) return null;
            if (version > 0) return null;
            return new MkidAtom(size, version, flags, stream);
        }

        // constructors
        /*----------------------------------------------------------------------
        |   AP4_MkidAtom::AP4_MkidAtom
        +---------------------------------------------------------------------*/
        public MkidAtom() :
            base(AtomType.MKID, Ap4.FULL_ATOM_HEADER_SIZE+4, 0, 0)
        {
        }

        /*----------------------------------------------------------------------
        |   AP4_MkidAtom::AP4_MkidAtom
        +---------------------------------------------------------------------*/
        private MkidAtom(uint       size,
                         byte       version,
                         uint       flags,
                         ByteStream stream) :
            base(AtomType.MKID, size, version, flags)
        {
            if (size < Ap4.FULL_ATOM_HEADER_SIZE+4) return;
            uint available = size-(Ap4.FULL_ATOM_HEADER_SIZE+4);
            uint entry_count = 0;
            stream.ReadUI32(out entry_count);
            if (available < (ulong)entry_count*(16+4)) return;
            // m_Entries.SetItemCount(entry_count)
            for (uint i=0; i<entry_count; i++) {
                _entries.Add(new Entry());
            }
            for (uint i=0; i<entry_count && available >= 16+4; i++) {
                uint entry_size;
                stream.ReadUI32(out entry_size);
                if (available < 4+entry_size) break;
                if (entry_size < 16) continue;
                available -= (4+entry_size);
                stream.Read(_entries[(int)i]._kid, 16);
                uint content_id_size = entry_size-16;
                byte[] content_id = new byte[content_id_size];
                stream.Read(content_id, content_id_size);
                _entries[(int)i]._contentId = Ap4Utils.StringFromBytes(content_id, 0, content_id_size);
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_MkidAtom::AddEntry
        +---------------------------------------------------------------------*/
        public Ap4Result AddEntry(byte[] kid, string content_id)
        {
            // PORT NOTE: content_id is a C string upstream (stops at the first 0)
            byte[] content_id_bytes = Ap4Utils.StringToCStringBytes(content_id);
            uint content_id_size = (uint)content_id_bytes.Length;
            int  entry_count = _entries.Count;

            // add the entry
            _entries.Add(new Entry());
            Array.Copy(kid, 0, _entries[entry_count]._kid, 0, 16);
            _entries[entry_count]._contentId = Ap4Utils.StringFromBytes(content_id_bytes, 0, content_id_size);

            // update the size
            _size32 += 4+16+content_id_size;

            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_MkidAtom::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            Ap4Result result;

            // encoding
            result = stream.WriteUI32((uint)_entries.Count);
            if (result.Failed()) return result;

            // entries
            for (int i=0; i<_entries.Count; i++) {
                // PORT NOTE: AP4_String::GetLength() counts embedded 0 bytes (the string
                // is read with Assign(chars, size)), so all the bytes are used here and
                // not the C string length.
                byte[] content_id = Ap4Utils.StringToBytes(_entries[i]._contentId);
                stream.WriteUI32(16+(uint)content_id.Length);
                stream.Write(_entries[i]._kid, 16);
                stream.Write(content_id, (uint)content_id.Length);
            }

            return result;
        }

        /*----------------------------------------------------------------------
        |   AP4_MkidAtom::InspectFields
        +---------------------------------------------------------------------*/
        public override Ap4Result InspectFields(AtomInspector inspector)
        {
            inspector.AddField("entry_count", (uint)_entries.Count);
            for (int i=0; i<_entries.Count; i++) {
                inspector.AddField("KID", _entries[i]._kid, 16);
                inspector.AddField("content_id", _entries[i]._contentId);
            }
            return Ap4Result.SUCCESS;
        }

        // accessors
        public List<Entry> GetEntries() { return _entries; }

        // members
        private List<Entry> _entries = new List<Entry>();
    }
}
