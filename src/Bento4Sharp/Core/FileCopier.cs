// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4FileCopier.h, Ap4FileCopier.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System.Collections.Generic;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_FileCopier
    +---------------------------------------------------------------------*/
    public class FileCopier
    {
        /*----------------------------------------------------------------------
        |   AP4_FileCopier::Write
        +---------------------------------------------------------------------*/
        public static Ap4Result Write(File file, ByteStream stream)
        {
            // write the top-level atoms
            List<Atom> atoms = file.GetTopLevelAtoms();
            for (int i = 0; i < atoms.Count; i++) {
                Atom atom = atoms[i];
                atom.Write(stream);
            }

            return Ap4Result.SUCCESS;
        }

        // don't instantiate this class
        private FileCopier() {}
    }
}
