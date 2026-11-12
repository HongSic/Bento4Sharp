// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Interfaces.h
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    // PORT NOTE: AP4_Referenceable, AP4_RELEASE and AP4_ADD_REFERENCE are dropped
    // (no reference counting in the port, see docs/PORTING.md).

    /*----------------------------------------------------------------------
    |   AP4_Exception
    +---------------------------------------------------------------------*/
    // PORT NOTE: named Ap4Exception rather than Exception: a Bento4.Exception type
    // would hide System.Exception in every file of the port.
    public class Ap4Exception : System.Exception
    {
        // methods
        public Ap4Exception(Ap4Result error) : base(Ap4.ResultText(error))
        {
            _error = error;
        }

        // members
        public Ap4Result _error;
    }
}
