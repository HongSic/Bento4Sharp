// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Command.h, Ap4Command.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Globalization;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   constants
    +---------------------------------------------------------------------*/
    public static partial class Ap4
    {
        public const byte COMMAND_TAG_OBJECT_DESCRIPTOR_UPDATE  = 0x01;
        public const byte COMMAND_TAG_OBJECT_DESCRIPTOR_REMOVE  = 0x02;
        public const byte COMMAND_TAG_ES_DESCRIPTOR_UPDATE      = 0x03;
        public const byte COMMAND_TAG_ES_DESCRIPTOR_REMOVE      = 0x04;
        public const byte COMMAND_TAG_IPMP_DESCRIPTOR_UPDATE    = 0x05;
        public const byte COMMAND_TAG_IPMP_DESCRIPTOR_REMOVE    = 0x06;
        public const byte COMMAND_TAG_ES_DESCRIPTOR_REMOVE_REF  = 0x07;
        public const byte COMMAND_TAG_OBJECT_DESCRIPTOR_EXECUTE = 0x08;
    }

    /*----------------------------------------------------------------------
    |   AP4_Command
    +---------------------------------------------------------------------*/
    // PORT NOTE: upstream AP4_Command is implicitly abstract (it inherits the
    // pure virtual WriteFields from AP4_Expandable); declared abstract here.
    public abstract class Command : Expandable
    {
        // constructor
        public Command(byte tag, uint header_size, uint payload_size) :
            base(tag, ClassIdSize.CLASS_ID_SIZE_08, header_size, payload_size) {}

        /*----------------------------------------------------------------------
        |   AP4_Command::Inspect
        +---------------------------------------------------------------------*/
        public override Ap4Result Inspect(AtomInspector inspector)
        {
            string name = "Command:" + _classId.ToString("x2", CultureInfo.InvariantCulture);
            inspector.StartDescriptor(name, GetHeaderSize(), GetSize());
            inspector.EndDescriptor();

            return Ap4Result.SUCCESS;
        }

        // methods
        public byte GetTag() { return (byte)_classId; }
    }

    /*----------------------------------------------------------------------
    |   AP4_UnknownCommand
    +---------------------------------------------------------------------*/
    public class UnknownCommand : Command
    {
        /*----------------------------------------------------------------------
        |   AP4_UnknownCommand::AP4_UnknownCommand
        +---------------------------------------------------------------------*/
        public UnknownCommand(ByteStream stream,
                              byte       tag,
                              uint       header_size,
                              uint       payload_size) :
            base(tag, header_size, payload_size)
        {
            _data.SetDataSize(payload_size);
            stream.Read(_data.UseData(), payload_size);
        }

        /*----------------------------------------------------------------------
        |   AP4_UnknownCommand::WriteFields
        +---------------------------------------------------------------------*/
        public override Ap4Result WriteFields(ByteStream stream)
        {
            // write the payload
            stream.Write(_data.GetData(), _data.GetDataSize());

            return Ap4Result.SUCCESS;
        }

        // members
        private DataBuffer _data = new DataBuffer();
    }
}
