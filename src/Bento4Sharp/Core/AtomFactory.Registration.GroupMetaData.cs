// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4AtomFactory.cpp (AP4_DefaultAtomFactory::Initialize), Source/C++/MetaData/Ap4MetaData.h
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    public partial class AtomFactory
    {
        /*----------------------------------------------------------------------
        |   group MetaData registrations
        +---------------------------------------------------------------------*/
        // PORT NOTE: the metadata atoms are not created by `case`s of
        // AP4_AtomFactory::CreateAtomFromStream but by a type handler that
        // AP4_DefaultAtomFactory::Initialize installs:
        //     AddTypeHandler(new AP4_MetaDataAtomTypeHandler(this));
        // The port provides the handler through this hook; nothing is added to
        // the registry tables.
        static partial void RegisterGroupMetaData(AtomRegistry r)
        {
            DefaultAtomFactory.MetaDataTypeHandlerFactory = (factory) => new MetaDataAtomTypeHandler(factory);
        }
    }
}
